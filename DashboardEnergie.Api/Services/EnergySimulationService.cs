using DashboardEnergie.Api.Data;
using DashboardEnergie.Api.Models;
using DashboardEnergie.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DashboardEnergie.Api.Services;

public sealed class EnergySimulationService(
    EnergyDbContext dbContext,
    IOptions<SimulationOptions> options,
    ILogger<EnergySimulationService> logger)
{
    private readonly Random _random = new();
    private readonly SimulationOptions _options = options.Value;

    public async Task SeedIfEmptyAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.Readings.AnyAsync(cancellationToken))
        {
            return;
        }

        var intervalSeconds = Math.Max(60, _options.SeedIntervalSeconds);
        var endUtc = DateTime.UtcNow;
        var cursorUtc = endUtc.AddDays(-Math.Max(1, _options.SeedDays));
        var readings = new List<EnergyReading>();
        double? previousWatts = null;

        while (cursorUtc <= endUtc)
        {
            var reading = CreateReading(cursorUtc, intervalSeconds, previousWatts);
            previousWatts = reading.PowerWatts;
            readings.Add(reading);
            cursorUtc = cursorUtc.AddSeconds(intervalSeconds);
        }

        dbContext.Readings.AddRange(readings);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} synthetic energy readings.", readings.Count);
    }

    public async Task AppendLiveReadingAsync(CancellationToken cancellationToken = default)
    {
        var previousWatts = await dbContext.Readings
            .OrderByDescending(reading => reading.TimestampUtc)
            .Select(reading => (double?)reading.PowerWatts)
            .FirstOrDefaultAsync(cancellationToken);

        var reading = CreateReading(
            DateTime.UtcNow,
            Math.Max(5, _options.LiveIntervalSeconds),
            previousWatts);

        dbContext.Readings.Add(reading);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private EnergyReading CreateReading(DateTime timestampUtc, int intervalSeconds, double? previousWatts)
    {
        var hour = timestampUtc.Hour;
        var officeLoad = hour switch
        {
            >= 6 and < 9 => 520d,
            >= 9 and < 18 => 820d,
            >= 18 and < 22 => 610d,
            _ => 340d
        };

        var cycle = Math.Sin(timestampUtc.TimeOfDay.TotalHours / 24d * Math.PI * 2d);
        var randomDrift = (_random.NextDouble() - 0.5d) * 150d;
        var targetWatts = officeLoad + ((cycle + 1.15d) * 95d) + randomDrift;

        if (_random.NextDouble() < 0.05d)
        {
            targetWatts += _random.Next(650, 1200);
        }

        var smoothedWatts = previousWatts.HasValue
            ? (previousWatts.Value * 0.55d) + (targetWatts * 0.45d)
            : targetWatts;

        var watts = Math.Round(Math.Clamp(smoothedWatts, 180d, 2600d), 2);

        return new EnergyReading
        {
            TimestampUtc = timestampUtc,
            Source = _options.DataSourceName,
            PowerWatts = watts,
            EnergyKwh = Math.Round((watts / 1000d) * (intervalSeconds / 3600d), 5),
            IsAnomaly = watts >= _options.AlertThresholdWatts
        };
    }
}
