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
| 03 | Dokument ansehen (Viewer + Angaben) | `/dokumente/{id}` | [03-dokument-ansehen](03-dokument-ansehen/notizen.md) | umgesetzt (Abnahme offen; `screen.png` fehlt) |
| 04 | Eingänge (Scanner-Ordner, Mail, iCloud) | `/eingaenge` | [04-eingaenge](04-eingaenge/notizen.md) | umgesetzt (Abnahme offen) |
| 05 | Startseite (Übersicht) | `/` | [05-startseite](05-startseite/notizen.md) | umgesetzt (Abnahme offen) |
| 06 | Zu prüfen | `/zu-pruefen` | [06-zu-pruefen](06-zu-pruefen/notizen.md) | umgesetzt (Abnahme offen); M4b-Nachtrag „Trennvorschlag“ (v2): freigegeben 2026-11-23 |
| 07 | Suche | `/suche` | [07-suche](07-suche/notizen.md) | umgesetzt (Abnahme offen) |
| 08 | Listen | `/listen` | [08-listen](08-listen/notizen.md) | umgesetzt (Abnahme offen) |
| 09 | Konto | `/konto` | [09-konto](09-konto/notizen.md) | umgesetzt (Abnahme offen); M4b-Nachtrag „Bearbeitete Dokumente“ (v2): freigegeben 2026-11-23 |
| 10 | Mitglieder | `/mitglieder` | [10-mitglieder](10-mitglieder/notizen.md) | umgesetzt (Abnahme offen) |
| 11 | Hinzufügen | `/hinzufuegen` | [11-hinzufuegen](11-hinzufuegen/notizen.md) | umgesetzt (Abnahme offen) |
| 12 | Hochladen | `/hochladen` | [12-hochladen](12-hochladen/notizen.md) | umgesetzt (Abnahme offen) |
| 13 | Kamera (mobil) | `/kamera` | [13-kamera](13-kamera/notizen.md) | umgesetzt (Abnahme offen) |
| 14 | Seiten bearbeiten (M4b) | `/dokumente/{id}/seiten` | [14-seiten-bearbeiten](14-seiten-bearbeiten/notizen.md) | umgesetzt (Abnahme offen: NVDA); Nachtrag P6 2026-11-24 |
| 15 | Papierkorb (M4b) | `/papierkorb` | [15-papierkorb](15-papierkorb/notizen.md) | umgesetzt (Abnahme offen: NVDA); Nachtrag P6 2026-11-24 |
| 16 | Fassungen im Viewer (M4b) | `/dokumente/{id}` | [16-fassungen](16-fassungen/notizen.md) | umgesetzt (Abnahme offen) |

`code.html` von 05–16 wurde aus der Inline-Antwort von `download_file` übertragen (kein Direkt-Download
möglich, siehe KB [ui-prozess.md](../../kb/ui-prozess.md)); Tippfehler sind nicht ganz ausgeschlossen, Bild
und Stitch bleiben maßgeblich. Start gibt es in Stitch 5×; gewählt `f4ee99bb…`, Gründe in
[05-startseite/notizen.md](05-startseite/notizen.md). 07, 08 und 12–16 wurden als „… v2“, 10 als „… v3“ neu erzeugt (alte Screens
bleiben in Stitch liegen). Entscheidung des Nutzers 2026-11-23: Die Restpunkte der Prüfnotizen (Texte,
Farben, kleine Zusätze) werden **beim Umsetzen** nach App-Texten und Theme behoben, keine weitere Stitch-Runde. In den Stitch-Bildern wirken JetBrains-Mono-Texte serifig – Fehler beim Bildrendern,
nicht im Entwurf.

Noch nicht abgelegt (geprüft 2026-11-22):

| Stitch-Screen | Empfehlung |
|---|---|
| Mobil „Dokumente“ `1c366315bdf84497bd5864b4dcbf73a9` | als **05-mobil-navigation** ablegen – einziges Mobilmuster (untere Leiste, Knopf „Scannen“, Listenzeile); Inhalt aus 02 |
| Desktop „Posteingang & Scanner“ `0974d75a790140c098a7d2eb24279145` | verwerfen – fast nur Firmen-/Cloud-Funktionen; Idee Status-Zähler je Eingang für 04 bzw. Start merken |
| Desktop „E-Mail-Filterregeln“ `fad9119362ce41e8b1c2bb19a85671cc` | verwerfen – Mail-Regeln sind fest, nicht einstellbar; höchstens als Erklärung „So behandelt NoMorePaper Mails“ in 04 |
