namespace DashboardEnergie.Shared;

public sealed class DashboardHealthDto
{
    public string Status { get; init; } = "Unknown";

    public bool IsSnapshotReady { get; init; }

    public int TechnicianRowCount { get; init; }

    public int RseRowCount { get; init; }

    public DateTime? LatestTechnicianTimestamp { get; init; }

    public DateTime TimestampUtc { get; init; }
}
