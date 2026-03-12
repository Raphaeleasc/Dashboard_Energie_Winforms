# Correspondance avec le PDF

## Architecture

- WinForms : oui
- API ASP.NET Core locale : oui
- SQLite locale : oui
- code 100% C# : oui

## Fonctionnalites cles

- centralisation de donnees : via import des CSV dans SQLite
- affichage graphique clair : courbe vue d'ensemble, courbe technicien, graphique mensuel RSE, vue previsions
- analyse basique : aggregations minute/heure/jour et synthese mensuelle
- bascule de lecture technicien : mesures brutes, aggregation horaire, aggregation journaliere
- detection d'anomalies : seuil fixe sur la vue technicien
- tableau des dernieres valeurs : present dans la vue technicien
- alertes visuelles : liste d'alertes avec niveaux `Surveillance` et `Critique`
- KPI derives : cout estime, CO2 estime, progression objectif RSE
- prevision court terme : tendance et projection J+1/J+7 avec bornes

## Utilisateurs cibles

- technicien d'exploitation : onglet dedie avec mesures horaires, alertes et agregations
- responsable energie / RSE : onglet dedie avec totaux annuels, repartition par poste et evolution mensuelle
- pilotage transverse : vue d'ensemble et vue previsions pour suivi global

## Livrables minimaux couverts

- application fonctionnelle : oui
- code structure et documente : oui, structure en trois projets
- diagramme d'architecture simplifie : `docs/architecture.md`
- mini-documentation : `README.md` et ce document
- tests automatises : oui (`DashboardEnergie.Tests`)

## Limites actuelles

- les donnees proviennent de CSV et non de capteurs temps reel
- la vue minute reutilise la granularite disponible dans le fichier technicien
- le design est WinForms custom (pas moteur web reactif)
