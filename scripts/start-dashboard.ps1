$repoRoot = Split-Path -Parent $PSScriptRoot

Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
    Where-Object {
        $_.CommandLine -and ($_.CommandLine -like '*DashboardEnergie.Api*' -or $_.CommandLine -like '*DashboardEnergie.WinForms*')
    } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }

$api = Start-Process dotnet -ArgumentList 'run --project DashboardEnergie.Api' -WorkingDirectory $repoRoot -PassThru

$ready = $false
for ($index = 0; $index -lt 30; $index++) {
    Start-Sleep -Milliseconds 500
    try {
        Invoke-RestMethod 'http://localhost:5188/api/dashboard/snapshot' -TimeoutSec 2 | Out-Null
        $ready = $true
        break
    }
    catch {
    }
}

if (-not $ready) {
    if ($api -and -not $api.HasExited) {
        Stop-Process -Id $api.Id -Force
    }

    throw 'L''API n''a pas demarre correctement.'
}

$ui = Start-Process dotnet -ArgumentList 'run --project DashboardEnergie.WinForms' -WorkingDirectory $repoRoot -PassThru

Write-Host "API PID: $($api.Id)"
Write-Host "WinForms PID: $($ui.Id)"
Write-Host "URL: http://localhost:5188/api/dashboard/snapshot"
