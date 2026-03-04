# Dashboard Energie WinForms

Solution Visual Studio pour un dashboard energetique compose de :

- `DashboardEnergie.Api` : API ASP.NET Core locale qui importe les CSV vers SQLite
- `DashboardEnergie.Shared` : DTO partages entre l'API et le client
- `DashboardEnergie.WinForms` : interface Windows Forms
- `Data/` : jeux de donnees fournis pour les vues technicien et RSE

## Jeux de donnees utilises

- `Data/technician_dataset.csv` : historique horaire technicien
- `Data/rse_dataset_detailed.csv` : synthese mensuelle detaillee RSE

Au demarrage, l'API recree la base SQLite locale puis recharge ces deux fichiers.

## Vues utilisateur

- `Technicien d'exploitation` : dernieres mesures, alertes, aggregation journaliere et graphique basculable entre mesures brutes, vue horaire et vue journaliere
- `Responsable energie / RSE` : total annuel, evolution mensuelle, detail par poste, repartition annuelle

## Documentation

- Architecture simplifiee : `docs/architecture.md`
- Correspondance avec le PDF : `docs/conformite-pdf.md`

## Demarrage

1. Lancer le client WinForms : `dotnet run --project DashboardEnergie.WinForms`
2. Si l'API n'est pas deja active, WinForms la demarre automatiquement et attend sa disponibilite.

L'API ecoute sur `http://localhost:5188`.

Depuis WinForms, le bouton `Recharger les CSV` permet de recharger les jeux de donnees dans SQLite sans quitter l'application.

Option: vous pouvez forcer une autre URL API via la variable d'environnement `DASHBOARD_API_URL`.

## Scripts de demo

- `scripts/start-dashboard.ps1` : lance l'API puis WinForms
- `scripts/stop-dashboard.ps1` : arrete les deux processus

## Endpoint de sante API

- `GET /api/dashboard/health` : etat de disponibilite de l'API et des donnees chargees

## Tests

Projet de tests : `DashboardEnergie.Tests`

- verification de l'import CSV vers SQLite
- verification du snapshot API et des principaux indicateurs
