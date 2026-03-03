namespace DashboardEnergie.Api.Options;

public sealed class SimulationOptions
{
    public const string SectionName = "Simulation";

    public string DataSourceName { get; set; } = "Compteur principal";

    public int AlertThresholdWatts { get; set; } = 1500;

    public int LiveIntervalSeconds { get; set; } = 15;

    public int SeedDays { get; set; } = 3;

    public int SeedIntervalSeconds { get; set; } = 300;
}
