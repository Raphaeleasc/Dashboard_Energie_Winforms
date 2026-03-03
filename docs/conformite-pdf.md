# Correspondance avec le PDF

## Architecture

- WinForms : oui
- API ASP.NET Core locale : oui
- SQLite locale : oui
- code 100% C# : oui

## Fonctionnalites cles

- centralisation de donnees : via import des CSV dans SQLite
- affichage graphique clair : courbe technicien + graphique mensuel RSE
- analyse basique : aggregations minute/heure/jour et synthese mensuelle
- bascule de lecture technicien : mesures brutes, aggregation horaire, aggregation journaliere
- detection d'anomalies : seuil fixe sur la vue technicien
- tableau des dernieres valeurs : present dans la vue technicien
- alertes visuelles : liste d'alertes avec niveaux `Surveillance` et `Critique`

## Utilisateurs cibles

- technicien d'exploitation : onglet dedie avec mesures horaires, alertes et agregations
- responsable energie / RSE : onglet dedie avec totaux annuels, repartition par poste et evolution mensuelle

## Livrables minimaux couverts

- application fonctionnelle : oui
- code structure et documente : oui, structure en trois projets
- diagramme d'architecture simplifie : `docs/architecture.md`
- mini-documentation : `README.md` et ce document

## Limites actuelles

- les donnees proviennent de CSV et non de capteurs temps reel
- la vue minute reutilise la granularite disponible dans le fichier technicien
- il n'y a pas encore de suite de tests automatisee
