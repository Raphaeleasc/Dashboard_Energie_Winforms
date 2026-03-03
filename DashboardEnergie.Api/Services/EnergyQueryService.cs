using DashboardEnergie.Api.Data;
using DashboardEnergie.Api.Options;
using DashboardEnergie.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DashboardEnergie.Api.Services;

public sealed class EnergyQueryService(
    EnergyDbContext dbContext,
    IOptions<DatasetOptions> options)
{
    private readonly DatasetOptions _options = options.Value;

    public async Task<DashboardSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var summary = await GetSummaryAsync(cancellationToken);
        var latestReadings = await GetLatestReadingsAsync(24, cancellationToken);
        var hourlyConsumption = await GetAggregationsAsync(AggregationPeriod.Hour, 24, cancellationToken);
        var dailyConsumption = await GetAggregationsAsync(AggregationPeriod.Day, 14, cancellationToken);
        var alerts = await GetAlertsAsync(12, cancellationToken);
        var rseCategoryTotals = await GetRseCategoryTotalsAsync(cancellationToken);
        var rseMonthlyBreakdowns = await GetRseMonthlyBreakdownsAsync(cancellationToken);

        return new DashboardSnapshotDto
        {
            Summary = summary,
            LatestReadings = latestReadings,
            HourlyConsumption = hourlyConsumption,
            DailyConsumption = dailyConsumption,
            Alerts = alerts,
            RseCategoryTotals = rseCategoryTotals,
            RseMonthlyBreakdowns = rseMonthlyBreakdowns
        };
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var technicianRows = await dbContext.TechnicianReadings
            .AsNoTracking()
            .OrderBy(reading => reading.Timestamp)
            .ToListAsync(cancellationToken);

        var rseRows = await dbContext.RseMonthlyBreakdowns
            .AsNoTracking()
            .OrderBy(row => row.MonthStart)
            .ToListAsync(cancellationToken);

        if (technicianRows.Count == 0)
        {
            return new DashboardSummaryDto
            {
                SourceName = $"{_options.TechnicianSourceName} / {_options.RseSourceName}",
                AlertThresholdWatts = _options.AlertThresholdWatts
            };
        }

        var latest = technicianRows[^1];
        var latestDayStart = latest.Timestamp.Date;
        var latestDayRows = technicianRows
            .Where(reading => reading.Timestamp >= latestDayStart && reading.Timestamp < latestDayStart.AddDays(1))
            .ToList();
        var anomalyWindowStart = latest.Timestamp.AddHours(-23);
        var annualRse = rseRows.Sum(row => row.TotalKwh);
        var latestRseMonth = rseRows.LastOrDefault();

        return new DashboardSummaryDto
        {
            SourceName = $"{_options.TechnicianSourceName} / {_options.RseSourceName}",
            CurrentPowerWatts = Math.Round(latest.PowerWatts, 0),
            PeakTodayWatts = Math.Round(latestDayRows.Max(reading => reading.PowerWatts), 0),
            LastHourKwh = Math.Round(latest.EnergyKwh, 3),
            TodayKwh = Math.Round(latestDayRows.Sum(reading => reading.EnergyKwh), 3),
            AnomalyCount24h = technicianRows.Count(reading => reading.Timestamp >= anomalyWindowStart && reading.IsAnomaly),
            AlertThresholdWatts = _options.AlertThresholdWatts,
            LastUpdate = latest.Timestamp,
            AnnualRseKwh = Math.Round(annualRse, 1),
            LatestMonthLabel = latestRseMonth?.MonthLabel ?? string.Empty,
            CoverageStart = technicianRows[0].Timestamp,
            CoverageEnd = latest.Timestamp
        };
    }

    public async Task<IReadOnlyList<EnergyReadingDto>> GetLatestReadingsAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        var items = await dbContext.TechnicianReadings
            .AsNoTracking()
            .OrderByDescending(reading => reading.Timestamp)
            .Take(Math.Clamp(count, 1, 240))
            .ToListAsync(cancellationToken);

        return items
            .OrderBy(reading => reading.Timestamp)
            .Select(reading => new EnergyReadingDto
            {
                Timestamp = reading.Timestamp,
                Source = reading.Source,
                PowerWatts = Math.Round(reading.PowerWatts, 0),
                EnergyKwh = Math.Round(reading.EnergyKwh, 5),
                IsAnomaly = reading.IsAnomaly
            })
            .ToList();
    }

    public async Task<IReadOnlyList<EnergyAlertDto>> GetAlertsAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        var items = await dbContext.TechnicianReadings
            .AsNoTracking()
            .Where(reading => reading.IsAnomaly)
            .OrderByDescending(reading => reading.Timestamp)
            .Take(Math.Clamp(count, 1, 50))
            .ToListAsync(cancellationToken);

        return items
            .Select(reading => new EnergyAlertDto
            {
                Timestamp = reading.Timestamp,
                Severity = reading.PowerWatts >= _options.AlertThresholdWatts + 400 ? "Critique" : "Surveillance",
                Message = $"{reading.Source} a depasse le seuil ({reading.PowerWatts:F0} W).",
                PowerWatts = Math.Round(reading.PowerWatts, 0)
            })
            .ToList();
    }

    public async Task<IReadOnlyList<EnergyAggregationPointDto>> GetAggregationsAsync(
        AggregationPeriod period,
        int points,
        CancellationToken cancellationToken = default)
    {
        return period switch
        {
            AggregationPeriod.Minute => await BuildMinuteAggregationAsync(points, cancellationToken),
            AggregationPeriod.Day => await BuildDailyAggregationAsync(points, cancellationToken),
            _ => await BuildHourlyAggregationAsync(points, cancellationToken)
        };
    }

    public async Task<IReadOnlyList<RseMonthlyBreakdownDto>> GetRseMonthlyBreakdownsAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.RseMonthlyBreakdowns
            .AsNoTracking()
            .OrderBy(row => row.MonthStart)
            .Select(row => new RseMonthlyBreakdownDto
            {
                Month = row.MonthLabel,
                TotalKwh = row.TotalKwh,
                HeatingKwh = row.HeatingKwh,
                WaterHeatingKwh = row.WaterHeatingKwh,
                AppliancesKwh = row.AppliancesKwh,
                LightingKwh = row.LightingKwh,
                OtherKwh = row.OtherKwh
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RseCategoryTotalDto>> GetRseCategoryTotalsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.RseMonthlyBreakdowns
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<RseCategoryTotalDto>();
        }

        var annualTotal = rows.Sum(row => row.TotalKwh);
        var categories = new Dictionary<string, double>
        {
            ["Chauffage"] = rows.Sum(row => row.HeatingKwh),
            ["Eau chaude"] = rows.Sum(row => row.WaterHeatingKwh),
            ["Appareils"] = rows.Sum(row => row.AppliancesKwh),
            ["Eclairage"] = rows.Sum(row => row.LightingKwh),
            ["Autres"] = rows.Sum(row => row.OtherKwh)
        };

        return categories
            .Select(item => new RseCategoryTotalDto
            {
                Category = item.Key,
                TotalKwh = Math.Round(item.Value, 1),
                SharePercent = annualTotal == 0d ? 0d : Math.Round((item.Value / annualTotal) * 100d, 1)
            })
            .OrderByDescending(item => item.TotalKwh)
            .ToList();
    }

    private async Task<IReadOnlyList<EnergyAggregationPointDto>> BuildHourlyAggregationAsync(
        int points,
        CancellationToken cancellationToken)
    {
        var bucketCount = Math.Clamp(points, 1, 96);
        var latest = await dbContext.TechnicianReadings
            .AsNoTracking()
            .OrderByDescending(reading => reading.Timestamp)
            .Select(reading => (DateTime?)reading.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (!latest.HasValue)
        {
            return Array.Empty<EnergyAggregationPointDto>();
        }

        var endBucket = TruncateToHour(latest.Value);
        var firstBucket = endBucket.AddHours(-(bucketCount - 1));

        var readings = await dbContext.TechnicianReadings
            .AsNoTracking()
            .Where(reading => reading.Timestamp >= firstBucket && reading.Timestamp <= endBucket.AddHours(1))
            .ToListAsync(cancellationToken);

        var grouped = readings
            .GroupBy(reading => TruncateToHour(reading.Timestamp))
            .ToDictionary(group => group.Key, group => group.Sum(item => item.EnergyKwh));

        return Enumerable.Range(0, bucketCount)
            .Select(index => firstBucket.AddHours(index))
            .Select(bucket => new EnergyAggregationPointDto
            {
                Period = AggregationPeriod.Hour,
                BucketStart = bucket,
                Label = bucket.ToString("dd/MM HH:mm"),
                ValueKwh = Math.Round(grouped.GetValueOrDefault(bucket), 3)
            })
            .ToList();
    }

    private async Task<IReadOnlyList<EnergyAggregationPointDto>> BuildMinuteAggregationAsync(
        int points,
        CancellationToken cancellationToken)
    {
        var bucketCount = Math.Clamp(points, 1, 120);
        var items = await dbContext.TechnicianReadings
            .AsNoTracking()
            .OrderByDescending(reading => reading.Timestamp)
            .Take(bucketCount)
            .OrderBy(reading => reading.Timestamp)
            .ToListAsync(cancellationToken);

        return items
            .Select(reading => new EnergyAggregationPointDto
            {
                Period = AggregationPeriod.Minute,
                BucketStart = reading.Timestamp,
                Label = reading.Timestamp.ToString("dd/MM HH:mm"),
                ValueKwh = Math.Round(reading.EnergyKwh, 3)
            })
            .ToList();
    }

    private async Task<IReadOnlyList<EnergyAggregationPointDto>> BuildDailyAggregationAsync(
        int points,
        CancellationToken cancellationToken)
    {
        var bucketCount = Math.Clamp(points, 1, 31);
        var latest = await dbContext.TechnicianReadings
            .AsNoTracking()
            .OrderByDescending(reading => reading.Timestamp)
            .Select(reading => (DateTime?)reading.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (!latest.HasValue)
        {
            return Array.Empty<EnergyAggregationPointDto>();
        }

        var endBucket = latest.Value.Date;
        var firstBucket = endBucket.AddDays(-(bucketCount - 1));

        var readings = await dbContext.TechnicianReadings
            .AsNoTracking()
            .Where(reading => reading.Timestamp >= firstBucket && reading.Timestamp < endBucket.AddDays(1))
            .ToListAsync(cancellationToken);

        var grouped = readings
            .GroupBy(reading => reading.Timestamp.Date)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.EnergyKwh));

        return Enumerable.Range(0, bucketCount)
            .Select(index => firstBucket.AddDays(index))
            .Select(bucket => new EnergyAggregationPointDto
            {
                Period = AggregationPeriod.Day,
                BucketStart = bucket,
                Label = bucket.ToString("dd/MM"),
                ValueKwh = Math.Round(grouped.GetValueOrDefault(bucket), 3)
            })
            .ToList();
    }

    private static DateTime TruncateToHour(DateTime value)
    {
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Unspecified);
    }
}
