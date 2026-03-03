using DashboardEnergie.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DashboardEnergie.Api.Data;

public sealed class EnergyDbContext(DbContextOptions<EnergyDbContext> options) : DbContext(options)
{
    public DbSet<EnergyReading> Readings => Set<EnergyReading>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EnergyReading>(entity =>
        {
            entity.HasKey(reading => reading.Id);
            entity.HasIndex(reading => reading.TimestampUtc);
            entity.Property(reading => reading.Source).HasMaxLength(64);
        });
    }
}
