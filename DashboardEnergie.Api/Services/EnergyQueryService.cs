using DashboardEnergie.Api.Data;
using DashboardEnergie.Api.Options;
using DashboardEnergie.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DashboardEnergie.Api.Services;

public sealed class EnergyQueryService(
    EnergyDbContext dbContext,
    IOptions<SimulationOptions> options)
{
    private readonly SimulationOptions _options = options.Value;

    public async Task<DashboardSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var summary = await GetSummaryAsync(cancellationToken);
        var latestReadings = await GetLatestReadingsAsync(18, cancellationToken);
        var hourlyConsumption = await GetAggregationsAsync(AggregationPeriod.Hour, 12, cancellationToken);
        var dailyConsumption = await GetAggregationsAsync(AggregationPeriod.Day, 7, cancellationToken);
        var alerts = await GetAlertsAsync(8, cancellationToken);

        return new DashboardSnapshotDto
        {
            Summary = summary,
            LatestReadings = latestReadings,
            HourlyConsumption = hourlyConsumption,
            DailyConsumption = dailyConsumption,
            Alerts = alerts
        };
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var todayUtc = nowUtc.Date;
        var dayWindow = await dbContext.Readings
            .AsNoTracking()
            .Where(reading => reading.TimestampUtc >= todayUtc)
            .ToListAsync(cancellationToken);

        var latest = await dbContext.Readings
            .AsNoTracking()
            .OrderByDescending(reading => reading.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var lastHourKwh = await dbContext.Readings
            .AsNoTracking()
            .Where(reading => reading.TimestampUtc >= nowUtc.AddHours(-1))
            .Select(reading => (double?)reading.EnergyKwh)
            .SumAsync(cancellationToken) ?? 0d;

        var anomalyCount24h = await dbContext.Readings
            .AsNoTracking()
            .CountAsync(
                reading => reading.TimestampUtc >= nowUtc.AddHours(-24) && reading.IsAnomaly,
                cancellationToken);

        return new DashboardSummaryDto
        {
            SourceName = latest?.Source ?? _options.DataSourceName,
            CurrentPowerWatts = Math.Round(latest?.PowerWatts ?? 0d, 0),
            PeakTodayWatts = Math.Round(dayWindow.Count == 0 ? 0d : dayWindow.Max(reading => reading.PowerWatts), 0),
            LastHourKwh = Math.Round(lastHourKwh, 3),
            TodayKwh = Math.Round(dayWindow.Sum(reading => reading.EnergyKwh), 3),
            AnomalyCount24h = anomalyCount24h,
            AlertThresholdWatts = _options.AlertThresholdWatts,
            LastUpdateUtc = latest?.TimestampUtc ?? DateTime.UtcNow
        };
    }

    public async Task<IReadOnlyList<EnergyReadingDto>> GetLatestReadingsAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        var items = await dbContext.Readings
            .AsNoTracking()
            .OrderByDescending(reading => reading.TimestampUtc)
            .Take(Math.Clamp(count, 1, 100))
            .ToListAsync(cancellationToken);

        return items
            .OrderBy(reading => reading.TimestampUtc)
            .Select(reading => new EnergyReadingDto
            {
                TimestampUtc = reading.TimestampUtc,
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
        var items = await dbContext.Readings
            .AsNoTracking()
            .Where(reading => reading.IsAnomaly)
            .OrderByDescending(reading => reading.TimestampUtc)
            .Take(Math.Clamp(count, 1, 50))
            .ToListAsync(cancellationToken);

        return items
            .Select(reading => new EnergyAlertDto
            {
                TimestampUtc = reading.TimestampUtc,
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
            AggregationPeriod.Day => await BuildDailyAggregationAsync(points, cancellationToken),
            _ => await BuildHourlyAggregationAsync(points, cancellationToken)
        };
    }

    private async Task<IReadOnlyList<EnergyAggregationPointDto>> BuildHourlyAggregationAsync(
        int points,
        CancellationToken cancellationToken)
    {
        var bucketCount = Math.Clamp(points, 1, 48);
        var nowBucket = TruncateToHour(DateTime.UtcNow);
        var firstBucket = nowBucket.AddHours(-(bucketCount - 1));

        var readings = await dbContext.Readings
            .AsNoTracking()
            .Where(reading => reading.TimestampUtc >= firstBucket)
            .ToListAsync(cancellationToken);

        var grouped = readings
            .GroupBy(reading => TruncateToHour(reading.TimestampUtc))
            .ToDictionary(group => group.Key, group => group.Sum(item => item.EnergyKwh));

        return Enumerable.Range(0, bucketCount)
            .Select(index => firstBucket.AddHours(index))
            .Select(bucket => new EnergyAggregationPointDto
            {
                Period = AggregationPeriod.Hour,
                BucketStartUtc = bucket,
                Label = bucket.ToLocalTime().ToString("HH:mm"),
                ValueKwh = Math.Round(grouped.GetValueOrDefault(bucket), 3)
            })
            .ToList();
    }

    private async Task<IReadOnlyList<EnergyAggregationPointDto>> BuildDailyAggregationAsync(
        int points,
        CancellationToken cancellationToken)
    {
        var bucketCount = Math.Clamp(points, 1, 31);
        var nowBucket = DateTime.UtcNow.Date;
        var firstBucket = nowBucket.AddDays(-(bucketCount - 1));

        var readings = await dbContext.Readings
            .AsNoTracking()
            .Where(reading => reading.TimestampUtc >= firstBucket)
            .ToListAsync(cancellationToken);

        var grouped = readings
            .GroupBy(reading => reading.TimestampUtc.Date)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.EnergyKwh));

        return Enumerable.Range(0, bucketCount)
            .Select(index => firstBucket.AddDays(index))
            .Select(bucket => new EnergyAggregationPointDto
            {
                Period = AggregationPeriod.Day,
                BucketStartUtc = bucket,
                Label = bucket.ToLocalTime().ToString("dd/MM"),
                ValueKwh = Math.Round(grouped.GetValueOrDefault(bucket), 3)
            })
            .ToList();
    }

    private static DateTime TruncateToHour(DateTime value)
    {
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc);
    }
}
