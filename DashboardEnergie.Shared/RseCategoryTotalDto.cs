namespace DashboardEnergie.Shared;

public sealed class RseCategoryTotalDto
{
    public string Category { get; init; } = string.Empty;

    public double TotalKwh { get; init; }

    public double SharePercent { get; init; }
}
