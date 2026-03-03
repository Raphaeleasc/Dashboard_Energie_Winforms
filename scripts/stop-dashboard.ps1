Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
    Where-Object {
        $_.CommandLine -and ($_.CommandLine -like '*DashboardEnergie.Api*' -or $_.CommandLine -like '*DashboardEnergie.WinForms*')
    } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
