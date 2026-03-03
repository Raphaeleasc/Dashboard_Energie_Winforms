namespace DashboardEnergie.Shared;

public sealed class RseMonthlyBreakdownDto
{
    public string Month { get; init; } = string.Empty;

    public double TotalKwh { get; init; }

    public double HeatingKwh { get; init; }

    public double WaterHeatingKwh { get; init; }

    public double AppliancesKwh { get; init; }

    public double LightingKwh { get; init; }

    public double OtherKwh { get; init; }
}
