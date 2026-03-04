using System.Net.Http.Json;
using DashboardEnergie.Shared;

namespace DashboardEnergie.WinForms;

internal sealed class DashboardApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(700);

    public DashboardApiClient(string baseAddress)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    public Uri BaseAddress => _httpClient.BaseAddress!;

    public async Task<DashboardSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _httpClient.GetFromJsonAsync<DashboardSnapshotDto>(
            "api/dashboard/snapshot",
            cancellationToken);

        return snapshot ?? throw new InvalidOperationException("The API returned an empty dashboard payload.");
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync("api/dashboard/reload", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> IsSnapshotReadyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await _httpClient.GetFromJsonAsync<DashboardHealthDto>(
                "api/dashboard/health",
                cancellationToken);

            return health?.IsSnapshotReady == true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> WaitForSnapshotReadyAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await IsSnapshotReadyAsync(cancellationToken))
            {
                return true;
            }

            await Task.Delay(DefaultPollInterval, cancellationToken);
        }

        return false;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
