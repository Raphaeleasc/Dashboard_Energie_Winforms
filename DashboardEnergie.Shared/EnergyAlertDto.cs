namespace DashboardEnergie.Shared;

public sealed class EnergyAlertDto
{
    public DateTime TimestampUtc { get; init; }

    public string Severity { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public double PowerWatts { get; init; }
}
