# 02 – Dokumente (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `2f1c2136e3fd46c88fab3d9985ad20ef`
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/Documents.razor` (`/dokumente`),
  `Classification/DocumentFilterBar.razor`, `Layout/MainLayout.razor` (Seitenleiste, Kopf)

## Übernehmen (passt zur App)

- **Seitenleiste links** statt langer Linkreihe im Kopf; „Einstellungen & Konto“ unten abgesetzt.
- **Kopf:** globale Suche, Hauptknopf „Dokument hochladen / scannen“, Konto-Symbol.
- **Überschrift mit Anzahl** („142 Dokumente“).
- **Schnellzugriff:** zuletzt bearbeitete Dokumente als Karten (Titel, Art, Datum, Betrag).
- **Filter-Chips nach Dokumentart** mit Anzahl („Rechnungen 38“) – schneller Zugriff auf den
  vorhandenen Filter „Art“.
- **Tabellenzeile:** Titel + Textausschnitt aus der OCR, Art/Tags, Datum + Eingangsweg
  (Mail, Upload, Kamera, Scanner-Ordner), Betrag, Aktionen.
- **Blättern** mit „Zeilen pro Seite“ und „Zeige 1–7 von 142“.
- Umschalter **Tabelle / Kacheln**.

## Abgleich mit der App

| Im Entwurf | In NoMorePaper |
|---|---|
| GoBD, „GoBD-Konform“, GoBD-Archiv, Audit-Leiste, Verfahrensdokumentation | gibt es nicht (Haushalt, kein Unternehmen) – weglassen |
| WORM, SHA-256-Hinweise, QES/Signatur, „Rechtskraft attestiert“ | gibt es nicht – weglassen |
| Spalte „GoBD / Signaturprüfpfad“ | ersetzen durch **Status** (Wartet / Wird verarbeitet / Fertig / Fehlgeschlagen) und **Erkennung** (automatisch / bestätigt) |
| „Ordner & Tags“ | **Art** und **Tags**; dazu **Absender** als eigene Spalte |
| Arbeitsbereich „Geschäftlich“ | gibt es nicht; stattdessen Abzeichen **„Privat“** je Dokument |
| Cloud-Speicher S3, „4.2 GB / 25 GB“ | lokale Ablage – höchstens freier Platz auf dem Server |
| Eingangswege ELSTER, API-Webhook, Email-Bot | echte Wege: **Hochladen, Kamera, Scanner-Ordner, Mail, iCloud** |
| „Netto / MwSt / Automatisch bezahlt“ | gibt es nicht |
| Export DATEV / CSV | erst M7 (Steuer-Export) |
| Stapelverarbeitung, Mehrfachauswahl | gibt es nicht (Stapel-Trennung M4b ist etwas anderes) |
| Workflows & Freigaben | gibt es nicht; nächstliegend ist **„Zu prüfen (n)“** – gehört in die Seitenleiste |
| Posteingang & Scanner | entspricht **Eingänge** |
| Ordner & Tags | entspricht **Listen** |
| Papierkorb | kommt mit M4b |
| Benachrichtigungs-Glocke | gibt es nicht |
| Spalten anpassen, Sortierung | gibt es nicht |
| Zweites Suchfeld in der Filterleiste | doppelt zur Suche im Kopf – eins weglassen |
| fehlt | Filter **Datum von/bis, Betrag von/bis, Sichtbarkeit, Absender**; **Miniatur** der ersten Seite |
| fehlt | Seitenleiste: **Zu prüfen, Kamera, Mitglieder** (Admin) |

## Beim Übernehmen nicht mitnehmen (§2.5)

- Schrift 10–12 px in Tabelle, Chips und Leisten – zu klein.
- Status als Punkt (`bg-primary`, `animate-pulse`) ohne Text.
- Aktionsknöpfe nur als Symbol (Auge, drei Punkte) mit `title` statt zugänglichem Namen; dazu
  `opacity-70`.
- Ganze Zeile klickbar (`cursor-pointer` auf `<tr>`) – stattdessen Link im Titel, wie heute.
- Klickbare `div`s als Auswahlfelder (Zeitraum, GoBD, Signatur) – echte `<select>` nehmen.
- Kurzbefehl „⌘K“ – in der App ist es „/“.
- `focus:outline-none` ohne Ersatz; `truncate` schneidet Titel ab.
- Tailwind-CDN und Google Fonts – Werte ins MudBlazor-Theme und `app.css` übertragen.
