# Stitch-Entwürfe

Entwürfe aus Google Stitch für die Überarbeitung der Oberfläche. Sie sind **Vorlagen**, kein
Produktcode: Übernommen werden Aufbau, Farben, Abstände und Typografie ins MudBlazor-Theme und
`app.css`. Die Barrierefreiheitsregeln aus AGENTS.md §2.5 gehen immer vor.

- [prompts.md](prompts.md) – Beschreibung aller Seiten und Prompts für Stitch

## Ablage

Je Screen ein Ordner `NN-seitenname/` mit:

| Datei | Inhalt |
|---|---|
| `code.html` | HTML aus Stitch, unverändert |
| `screen.png` | Bildschirmfoto des Entwurfs |
| `notizen.md` | Stitch-IDs, Zielseite im Code, Abgleich mit der App, was nicht übernommen wird |

## Screens

| Nr. | Seite | Route | Ordner |
|---|---|---|---|
| 01 | Anmelden | `/anmelden` | [01-anmelden](01-anmelden/notizen.md) |
| 02 | Dokumente (Übersicht, mit Seitenleiste und Kopf) | `/dokumente` | [02-dokumente](02-dokumente/notizen.md) |
| 03 | Dokument ansehen (Viewer + Angaben) | `/dokumente/{id}` | [03-dokument-ansehen](03-dokument-ansehen/notizen.md) |
