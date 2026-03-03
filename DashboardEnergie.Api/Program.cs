using DashboardEnergie.Api.Data;
using DashboardEnergie.Api.Options;
using DashboardEnergie.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5188");

builder.Services.AddControllers();
builder.Services.Configure<DatasetOptions>(
    builder.Configuration.GetSection(DatasetOptions.SectionName));
var connectionString = builder.Configuration.GetConnectionString("EnergyDb")
    ?? throw new InvalidOperationException("Missing connection string 'EnergyDb'.");
builder.Services.AddDbContext<EnergyDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddScoped<CsvDatasetImportService>();
builder.Services.AddScoped<EnergyQueryService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EnergyDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    var importService = scope.ServiceProvider.GetRequiredService<CsvDatasetImportService>();
    await importService.ReloadAsync();
}

app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
