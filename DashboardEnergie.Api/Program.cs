using DashboardEnergie.Api.Data;
using DashboardEnergie.Api.Options;
using DashboardEnergie.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5188");

builder.Services.AddControllers();
builder.Services.Configure<SimulationOptions>(
    builder.Configuration.GetSection(SimulationOptions.SectionName));
var connectionString = builder.Configuration.GetConnectionString("EnergyDb")
    ?? throw new InvalidOperationException("Missing connection string 'EnergyDb'.");
builder.Services.AddDbContext<EnergyDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddScoped<EnergySimulationService>();
builder.Services.AddScoped<EnergyQueryService>();
builder.Services.AddHostedService<EnergyBackgroundService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EnergyDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    var simulationService = scope.ServiceProvider.GetRequiredService<EnergySimulationService>();
    await simulationService.SeedIfEmptyAsync();
}

app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
