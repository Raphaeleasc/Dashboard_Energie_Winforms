namespace DashboardEnergie.Shared;

public sealed class EnergyAggregationPointDto
{
    public AggregationPeriod Period { get; init; }

    public DateTime BucketStart { get; init; }

    public string Label { get; init; } = string.Empty;

    public double ValueKwh { get; init; }
}
