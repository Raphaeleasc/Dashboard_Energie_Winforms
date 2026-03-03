using System.ComponentModel.DataAnnotations;

namespace DashboardEnergie.Api.Models;

public sealed class EnergyReading
{
    public int Id { get; set; }

    public DateTime TimestampUtc { get; set; }

    [MaxLength(64)]
    public string Source { get; set; } = string.Empty;

    public double PowerWatts { get; set; }

    public double EnergyKwh { get; set; }

    public bool IsAnomaly { get; set; }
}
