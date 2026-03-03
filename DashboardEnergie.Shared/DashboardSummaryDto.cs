namespace DashboardEnergie.Shared;

public sealed class DashboardSummaryDto
{
    public string SourceName { get; init; } = string.Empty;

    public double CurrentPowerWatts { get; init; }

    public double PeakTodayWatts { get; init; }

    public double LastHourKwh { get; init; }

    public double TodayKwh { get; init; }

    public int AnomalyCount24h { get; init; }

    public int AlertThresholdWatts { get; init; }

    public DateTime LastUpdateUtc { get; init; }
}
