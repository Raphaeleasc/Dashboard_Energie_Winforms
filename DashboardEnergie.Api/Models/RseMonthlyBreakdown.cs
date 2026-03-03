namespace DashboardEnergie.Api.Models;

public sealed class RseMonthlyBreakdown
{
    public int Id { get; set; }

    public DateTime MonthStart { get; set; }

    public string MonthLabel { get; set; } = string.Empty;

    public double TotalKwh { get; set; }

    public double HeatingKwh { get; set; }

    public double WaterHeatingKwh { get; set; }

    public double AppliancesKwh { get; set; }

    public double LightingKwh { get; set; }

    public double OtherKwh { get; set; }
}
