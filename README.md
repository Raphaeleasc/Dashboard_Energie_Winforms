# Dashboard Energie WinForms

Solution de demarrage pour un dashboard energetique compose de :

- `DashboardEnergie.Api` : API ASP.NET Core locale avec simulation et stockage SQLite
- `DashboardEnergie.Shared` : DTO partages entre l'API et le client
- `DashboardEnergie.WinForms` : interface Windows Forms pour la visualisation

## Demarrage

1. Lancer l'API : `dotnet run --project DashboardEnergie.Api`
2. Lancer le client WinForms : `dotnet run --project DashboardEnergie.WinForms`

L'API ecoute sur `http://localhost:5188` et cree automatiquement une base SQLite locale.
