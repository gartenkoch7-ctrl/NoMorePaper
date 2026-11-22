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
| `screen.png` / `.webp` | Bildschirmfoto des Entwurfs |
| `notizen.md` | Stitch-IDs, Zielseite im Code, Abgleich mit der App, was nicht übernommen wird |

## Screens

Ablauf und Status-Stufen: [KB ui-prozess.md](../../kb/ui-prozess.md). Stitch-Projekt „NoMorePaper“
(`17713397687229704853`) – das einzige Projekt.

| Nr. | Seite | Route | Ordner | Status |
|---|---|---|---|---|
| 01 | Anmelden | `/anmelden` | [01-anmelden](01-anmelden/notizen.md) | umgesetzt (Abnahme offen) |
| 02 | Dokumente (Übersicht, mit Seitenleiste und Kopf) | `/dokumente` | [02-dokumente](02-dokumente/notizen.md) | umgesetzt (Abnahme offen) |
| 03 | Dokument ansehen (Viewer + Angaben) | `/dokumente/{id}` | [03-dokument-ansehen](03-dokument-ansehen/notizen.md) | freigegeben (`screen.png` fehlt) |
| 04 | Eingänge (Scanner-Ordner, Mail, iCloud) | `/eingaenge` | [04-eingaenge](04-eingaenge/notizen.md) | freigegeben |

Noch nicht abgelegt (geprüft 2026-11-22):

| Stitch-Screen | Empfehlung |
|---|---|
| Mobil „Dokumente“ `1c366315bdf84497bd5864b4dcbf73a9` | als **05-mobil-navigation** ablegen – einziges Mobilmuster (untere Leiste, Knopf „Scannen“, Listenzeile); Inhalt aus 02 |
| Desktop „Posteingang & Scanner“ `0974d75a790140c098a7d2eb24279145` | verwerfen – fast nur Firmen-/Cloud-Funktionen; Idee Status-Zähler je Eingang für 04 bzw. Start merken |
| Desktop „E-Mail-Filterregeln“ `fad9119362ce41e8b1c2bb19a85671cc` | verwerfen – Mail-Regeln sind fest, nicht einstellbar; höchstens als Erklärung „So behandelt NoMorePaper Mails“ in 04 |
