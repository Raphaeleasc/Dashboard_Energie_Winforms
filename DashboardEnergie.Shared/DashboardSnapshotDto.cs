namespace DashboardEnergie.Shared;

public sealed class DashboardSnapshotDto
{
    public DashboardSummaryDto Summary { get; init; } = new();

    public IReadOnlyList<EnergyReadingDto> LatestReadings { get; init; } = Array.Empty<EnergyReadingDto>();

    public IReadOnlyList<EnergyAggregationPointDto> HourlyConsumption { get; init; } = Array.Empty<EnergyAggregationPointDto>();

    public IReadOnlyList<EnergyAggregationPointDto> DailyConsumption { get; init; } = Array.Empty<EnergyAggregationPointDto>();

    public IReadOnlyList<EnergyAlertDto> Alerts { get; init; } = Array.Empty<EnergyAlertDto>();
}
