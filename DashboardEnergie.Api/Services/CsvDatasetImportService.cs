using System.Globalization;
using DashboardEnergie.Api.Data;
using DashboardEnergie.Api.Models;
using DashboardEnergie.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DashboardEnergie.Api.Services;

public sealed class CsvDatasetImportService(
    EnergyDbContext dbContext,
    IWebHostEnvironment environment,
    IOptions<DatasetOptions> options,
    ILogger<CsvDatasetImportService> logger)
{
    private readonly DatasetOptions _options = options.Value;

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.TechnicianReadings.ExecuteDeleteAsync(cancellationToken);
        await dbContext.RseMonthlyBreakdowns.ExecuteDeleteAsync(cancellationToken);

        var technicianRows = await LoadTechnicianReadingsAsync(cancellationToken);
        var rseRows = await LoadRseBreakdownsAsync(cancellationToken);

        dbContext.TechnicianReadings.AddRange(technicianRows);
        dbContext.RseMonthlyBreakdowns.AddRange(rseRows);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Imported {TechnicianCount} technician rows and {RseCount} RSE rows from CSV.",
            technicianRows.Count,
            rseRows.Count);
    }

    private async Task<List<TechnicianReading>> LoadTechnicianReadingsAsync(CancellationToken cancellationToken)
    {
        var path = ResolvePath(_options.TechnicianCsvPath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Technician dataset not found.", path);
        }

        var rows = new List<TechnicianReading>();

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        await reader.ReadLineAsync(cancellationToken);

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(';');
            if (parts.Length != 3)
            {
                throw new InvalidOperationException($"Unexpected technician CSV line: '{line}'.");
            }

            var timestamp = DateTime.ParseExact(
                $"{parts[0]} {parts[1]}",
                "dd/MM/yyyy HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None);
            var energyKwh = double.Parse(parts[2], CultureInfo.InvariantCulture);

            rows.Add(new TechnicianReading
            {
                Timestamp = timestamp,
                Source = _options.TechnicianSourceName,
                EnergyKwh = Math.Round(energyKwh, 5),
                PowerWatts = Math.Round(energyKwh * 1000d, 0),
                IsAnomaly = (energyKwh * 1000d) >= _options.AlertThresholdWatts
            });
        }

        return rows;
    }

    private async Task<List<RseMonthlyBreakdown>> LoadRseBreakdownsAsync(CancellationToken cancellationToken)
    {
        var path = ResolvePath(_options.RseCsvPath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("RSE dataset not found.", path);
        }

        var rows = new List<RseMonthlyBreakdown>();

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        await reader.ReadLineAsync(cancellationToken);

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length != 7)
            {
                throw new InvalidOperationException($"Unexpected RSE CSV line: '{line}'.");
            }

            var monthStart = DateTime.ParseExact(
                $"{parts[0]}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None);

            rows.Add(new RseMonthlyBreakdown
            {
                MonthStart = monthStart,
                MonthLabel = parts[0],
                TotalKwh = ParseDouble(parts[1]),
                HeatingKwh = ParseDouble(parts[2]),
                WaterHeatingKwh = ParseDouble(parts[3]),
                AppliancesKwh = ParseDouble(parts[4]),
                LightingKwh = ParseDouble(parts[5]),
                OtherKwh = ParseDouble(parts[6])
            });
        }

        return rows;
    }

    private string ResolvePath(string configuredPath)
    {
        var directPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));
        if (File.Exists(directPath))
        {
            return directPath;
        }

        var dataFileName = Path.GetFileName(configuredPath);
        var currentDirectory = new DirectoryInfo(environment.ContentRootPath);

        while (currentDirectory is not null)
        {
            var candidate = Path.Combine(currentDirectory.FullName, "Data", dataFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return directPath;
    }

    private static double ParseDouble(string value)
    {
        return double.Parse(value, CultureInfo.InvariantCulture);
    }
}
