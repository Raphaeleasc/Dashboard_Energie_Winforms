using DashboardEnergie.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DashboardEnergie.Api.Data;

public sealed class EnergyDbContext(DbContextOptions<EnergyDbContext> options) : DbContext(options)
{
    public DbSet<TechnicianReading> TechnicianReadings => Set<TechnicianReading>();

    public DbSet<RseMonthlyBreakdown> RseMonthlyBreakdowns => Set<RseMonthlyBreakdown>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TechnicianReading>(entity =>
        {
            entity.HasKey(reading => reading.Id);
            entity.HasIndex(reading => reading.Timestamp);
            entity.Property(reading => reading.Source).HasMaxLength(64);
        });

        modelBuilder.Entity<RseMonthlyBreakdown>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => row.MonthStart).IsUnique();
            entity.Property(row => row.MonthLabel).HasMaxLength(7);
        });
    }
}
