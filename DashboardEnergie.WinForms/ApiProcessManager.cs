using System.Diagnostics;

namespace DashboardEnergie.WinForms;

internal sealed class ApiProcessManager : IDisposable
{
    private Process? _ownedApiProcess;

    public async Task<ApiStartupResult> EnsureApiRunningAsync(
        DashboardApiClient apiClient,
        CancellationToken cancellationToken = default)
    {
        if (await apiClient.WaitForSnapshotReadyAsync(TimeSpan.FromSeconds(2), cancellationToken))
        {
            return new ApiStartupResult(false, null, $"API detectee sur {apiClient.BaseAddress}.");
        }

        StartOwnedApiProcess();

        var deadlineUtc = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadlineUtc)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await apiClient.IsSnapshotReadyAsync(cancellationToken))
            {
                var message = _ownedApiProcess is null
                    ? $"API demarree automatiquement sur {apiClient.BaseAddress}."
                    : $"API demarree automatiquement (PID {_ownedApiProcess.Id}) sur {apiClient.BaseAddress}.";

                return new ApiStartupResult(true, _ownedApiProcess?.Id, message);
            }

            if (_ownedApiProcess is { HasExited: true })
            {
                throw new InvalidOperationException(
                    $"Le processus API s'est termine pendant le demarrage (code {_ownedApiProcess.ExitCode}).");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(700), cancellationToken);
        }

        StopOwnedApiProcess();
        throw new TimeoutException("L'API ne repond pas apres demarrage automatique.");
    }

    public void Dispose()
    {
        StopOwnedApiProcess();
    }

    private void StartOwnedApiProcess()
    {
        if (_ownedApiProcess is { HasExited: false })
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project DashboardEnergie.Api",
            WorkingDirectory = ResolveRepositoryRoot(),
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _ownedApiProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Impossible de lancer le processus de l'API.");
    }

    private void StopOwnedApiProcess()
    {
        if (_ownedApiProcess is null)
        {
            return;
        }

        if (_ownedApiProcess.HasExited)
        {
            _ownedApiProcess.Dispose();
            _ownedApiProcess = null;
            return;
        }

        try
        {
            _ownedApiProcess.Kill(entireProcessTree: true);
            _ownedApiProcess.WaitForExit(5000);
        }
        catch (InvalidOperationException)
        {
        }

        _ownedApiProcess.Dispose();
        _ownedApiProcess = null;
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "DashboardEnergie.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Impossible de localiser la racine du repository DashboardEnergie.");
    }
}

internal readonly record struct ApiStartupResult(
    bool StartedByApp,
    int? ProcessId,
    string Message);
