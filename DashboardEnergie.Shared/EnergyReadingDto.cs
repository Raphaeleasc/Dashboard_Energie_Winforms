namespace DashboardEnergie.Shared;

public sealed class EnergyReadingDto
{
    public DateTime Timestamp { get; init; }

    public string Source { get; init; } = string.Empty;

    public double PowerWatts { get; init; }

    public double EnergyKwh { get; init; }

    public bool IsAnomaly { get; init; }
}
