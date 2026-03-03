using System.Net.Http.Json;
using DashboardEnergie.Shared;

namespace DashboardEnergie.WinForms;

internal sealed class DashboardApiClient : IDisposable
{
    private readonly HttpClient _httpClient;

    public DashboardApiClient(string baseAddress)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

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

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
