using DashboardEnergie.Api.Options;
using Microsoft.Extensions.Options;

namespace DashboardEnergie.Api.Services;

public sealed class EnergyBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<SimulationOptions> options,
    ILogger<EnergyBackgroundService> logger) : BackgroundService
{
    private readonly SimulationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.LiveIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                using var scope = scopeFactory.CreateScope();
                var simulationService = scope.ServiceProvider.GetRequiredService<EnergySimulationService>();
                await simulationService.AppendLiveReadingAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unable to generate a live energy reading.");
            }
        }
    }
}
