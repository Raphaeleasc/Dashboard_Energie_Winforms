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
        var lineNumber = 1;
        var skippedLines = 0;

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            lineNumber++;
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
                skippedLines++;
                logger.LogWarning("Skipped technician CSV line {LineNumber}: unexpected format.", lineNumber);
                continue;
            }

            if (!DateTime.TryParseExact(
                    $"{parts[0]} {parts[1]}",
                    "dd/MM/yyyy HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var timestamp))
            {
                skippedLines++;
                logger.LogWarning("Skipped technician CSV line {LineNumber}: invalid timestamp.", lineNumber);
                continue;
            }

            if (!TryParseDouble(parts[2], out var energyKwh))
            {
                skippedLines++;
                logger.LogWarning("Skipped technician CSV line {LineNumber}: invalid energy value '{Value}'.", lineNumber, parts[2]);
                continue;
            }

            rows.Add(new TechnicianReading
            {
                Timestamp = timestamp,
                Source = _options.TechnicianSourceName,
                EnergyKwh = Math.Round(energyKwh, 5),
                PowerWatts = Math.Round(energyKwh * 1000d, 0),
                IsAnomaly = (energyKwh * 1000d) >= _options.AlertThresholdWatts
            });
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException($"No valid technician rows found in '{path}'.");
        }

        if (skippedLines > 0)
        {
            logger.LogWarning("Technician import skipped {SkippedLines} invalid lines.", skippedLines);
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
        var lineNumber = 1;
        var skippedLines = 0;

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            lineNumber++;
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
                skippedLines++;
                logger.LogWarning("Skipped RSE CSV line {LineNumber}: unexpected format.", lineNumber);
                continue;
            }

            if (!DateTime.TryParseExact(
                    $"{parts[0]}-01",
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var monthStart))
            {
                skippedLines++;
                logger.LogWarning("Skipped RSE CSV line {LineNumber}: invalid month label '{Month}'.", lineNumber, parts[0]);
                continue;
            }

            if (!TryParseDouble(parts[1], out var totalKwh) ||
                !TryParseDouble(parts[2], out var heatingKwh) ||
                !TryParseDouble(parts[3], out var waterHeatingKwh) ||
                !TryParseDouble(parts[4], out var appliancesKwh) ||
                !TryParseDouble(parts[5], out var lightingKwh) ||
                !TryParseDouble(parts[6], out var otherKwh))
            {
                skippedLines++;
                logger.LogWarning("Skipped RSE CSV line {LineNumber}: invalid numeric value.", lineNumber);
                continue;
            }

            rows.Add(new RseMonthlyBreakdown
            {
                MonthStart = monthStart,
                MonthLabel = parts[0],
                TotalKwh = totalKwh,
                HeatingKwh = heatingKwh,
                WaterHeatingKwh = waterHeatingKwh,
                AppliancesKwh = appliancesKwh,
                LightingKwh = lightingKwh,
                OtherKwh = otherKwh
            });
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException($"No valid RSE rows found in '{path}'.");
        }

        if (skippedLines > 0)
        {
            logger.LogWarning("RSE import skipped {SkippedLines} invalid lines.", skippedLines);
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

    private static bool TryParseDouble(string value, out double parsedValue)
    {
        return double.TryParse(
                   value,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out parsedValue)
               || double.TryParse(
                   value,
                   NumberStyles.Float,
                   CultureInfo.GetCultureInfo("fr-FR"),
                   out parsedValue);
    }
}
