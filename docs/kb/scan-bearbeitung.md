# Scan-Bearbeitung: Seiten, Fassungen, Papierkorb, Stapel-Trennung

Entscheidungen: [ADR-0013](../adr/0013-scan-bearbeitung-versionen-papierkorb.md) (was) und
[ADR-0025](../adr/0025-scan-bearbeitung-umsetzung.md) (Bedienung, Rechte, Werkzeuge). Plan:
[M4b](../plan/m4b-scan-bearbeitung.md), Details: [Detailplan](../plan/features/m4b-scan-bearbeitung-detail.md).

## Ablauf „Seiten bearbeiten“

1. Die Seite `/dokumente/{id}/seiten` zeigt die Seitenliste mit Ansage je Seite (Textanfang, „vermutlich leer“).
   Änderungen (drehen, löschen, umsortieren, zuschneiden, Trennstellen setzen) werden gesammelt, nichts wird
   sofort gespeichert.
2. „Speichern“ schickt den **Seitenplan** (Reihenfolge, Drehung, Löschen, ersetzte Seiten) an
   `RequestPageEdit`. Die Anfrage nimmt eine Zeilensperre auf das Dokument; solange das Dokument nicht fertig
   verarbeitet ist, antwortet der Server mit 409.
3. Der Auftrag bekommt die Stufe `Edit` (`processing_jobs`). Der Worker führt sie in `EditStageRunner` aus:
   - Quelle: die bearbeitete Fassung, sonst das Original.
   - qpdf setzt die Seiten nach Plan zusammen (`QpdfTool`, `--rotate=+90:n`, Text und Bilder bleiben
     unverändert). Ergebnis: `edited.pdf`.
   - OCRmyPDF läuft mit `--skip-text`, PDF/A, `deu+eng` und **ohne** `--rotate-pages` und `--deskew`
     (`OcrMyPdfEngine`), damit die Drehung des Nutzers nicht „korrigiert“ wird.
   - Neues Archiv, Seitentexte in `document_pages`, Versionsregel (siehe unten), danach die Erkennung wie
     bei jedem Dokument.
4. Bis zum Ende zeigt der Viewer die bisherige Version und „wird bearbeitet“.

Zuschnitt und Geraderichten laufen im Browser (OpenCV.js, Blatt- und Schräglageerkennung; Seite als JPEG
an den Server, `CropDetection`, `CropInterop`). Eine so ersetzte Seite verliert eine vorhandene Textebene
und wird neu erkannt.

## Zusammenfügen und Teilen

- **Zusammenfügen** (`AppendDocument`): Seiten des angehängten Dokuments kommen ans Ende des geöffneten.
  Das angehängte Dokument geht in den Papierkorb. Ist das angehängte privat, wird das Ergebnis privat.
  Beide Dokumente werden in fester Reihenfolge (kleinere Id zuerst) gesperrt, damit sich zwei Vorgänge
  nicht verklemmen.
- **Teilen** (`SplitDocument`): je Teil ein neues Dokument (Original = Seitenauszug, Besitzer, Privat und
  Bearbeiter übernommen). Die Teile werden normal per OCR und Erkennung verarbeitet. Das Ausgangsdokument
  geht in den Papierkorb.
- Rechte: Bearbeiten, Zusammenfügen und Teilen brauchen das Bearbeitungsrecht aus M3 (Besitzer, Bearbeiter).

## Fassungen und Versionsregel

- Es bleiben das **Original** und die **neueste** bearbeitete Fassung (Dateiart `Edited`). Ältere Fassungen
  gibt es nicht mehr.
- Das Archiv (PDF/A, Suche) entsteht immer aus der aktuellen Quelle: bearbeitet, sonst Original.
- Die Richtlinie („Nach dem Bearbeiten behalten“) ist je Konto gesetzt (Werkswert: beides, nur bearbeitet,
  nur Original) und je Dokument änderbar (`VersionPolicy`, `ChooseVersions`).
- Was verschwindet, geht in den Papierkorb: die ältere Fassung bzw. das Original.
- Ein Dokument belegt bis zu dreimal Platz (Original, bearbeitet, Archiv); im Papierkorb zusätzlich 30 Tage.

## Papierkorb

- Löschen eines Dokuments, Originals oder einer Fassung geht in den Papierkorb (`MoveDocumentToTrash`,
  `ITrashStore`). Das Dokument bleibt als Zeile mit `deleted_at`; Dateien liegen in `trash/<id>/`.
- Aufbewahrung **30 Tage** (`TrashRules.Retention`, Feld `purge_after`).
- Auflisten zeigt nur die eigenen Einträge. Wiederherstellen: ist der Platz belegt (z. B. neue Fassung),
  wird getauscht (`RestoreTrashFile`): die Fassung am Platz geht dafür in den Papierkorb.
- Endgültig entfernen und Papierkorb leeren: nur der Besitzer (ADR-0025).
- **Filter:** Dokumente im Papierkorb erscheinen in keiner Abfrage. EF-Abfragefilter `NotInTrash` für alle
  Zugriffe, Filter `TrashSql` in den rohen SQL-Abfragen (Suche, Duplikate, SHA-256, „Zu prüfen“). Ein
  Architekturtest (`TrashFilterTests`) prüft jede Datei mit `FROM documents`. Papierkorb-Fälle nutzen nur
  `ITrashStore` mit `IgnoreQueryFilters`.
- **Aufräumdienst:** `TrashPurger` im Worker läuft beim Start und danach alle 24 Stunden und entfernt über
  `PurgeDueTrash` alle Einträge mit `purge_after` < jetzt (Dateien und Zeilen). Ein fehlgeschlagener Lauf
  wird beim nächsten Termin wiederholt.

## Stapel-Trennung (Stufe `Separation`)

- Läuft nach der OCR, nur bei mehrseitigen Scans ohne eigene Textebene. Teile aus einem Split
  (`Document.SplitFrom`) überspringen sie: Ihre Seiten hat der Nutzer schon festgelegt.
- Heuristik (`SeparationHeuristics`): leere Seiten, Seitenzähler „Seite x von y“, Anrede, Absenderwechsel.
- Ein LLM-Aufruf (`SeparationPrompt`, `SeparationAssessor`) mit den Seitenanfängen: höchstens **15 Seiten**,
  je Seite höchstens **160 Zeichen**. Vom Modell genannte Grenzen außerhalb des Stapels oder doppelt
  werden verworfen (Warnung 3991).
- Der LLM-Zugang läuft über `LlmGate`; die Erkennung hat Vorrang. Ist das LLM beschäftigt, gibt der Job den
  Anspruch zurück, ohne einen Versuch zu zählen.
- Ergebnis ist **nur ein Vorschlag** (`SplitSuggestion`) in „Zu prüfen“. Nichts wird getrennt, bevor der
  Nutzer annimmt (dann wie „Teilen“) oder anpasst (dann „Seiten bearbeiten“ mit Trennstellen).
- Fällt das LLM aus, zählen nur die Seitenzeichen. Scheitert die letzte Stufe, läuft die Erkennung ohne
  Vorschlag weiter.

## Werkzeuge

| Werkzeug | Wo | Zweck | Lizenz |
|---|---|---|---|
| qpdf | Worker (Image) | Seiten wählen, drehen, zusammensetzen, teilen; verlustfrei | Apache-2.0 |
| OCRmyPDF | Worker | Neue Textebene, PDF/A (wie beim Eingang) | MPL-2.0 |
| Poppler (`PopplerPageAnalyzer`) | Worker | Leere Seiten, Seiten mit Textebene | GPL-2.0; seit M1 im Worker-Image (`poppler-utils`), nur als eigener Prozess aufgerufen, nicht gelinkt |
| OpenCV.js | Browser | Blatt- und Schräglage beim Zuschnitt | Apache-2.0 |
| PDF.js | Browser | Seite für den Zuschnitt rendern (200 dpi) | Apache-2.0 |

## Messwerte

- Trennung: Ziel ≥ 80 % der Grenzen richtig an 12 Teststapeln, keine Trennung ohne Bestätigung.
  **Noch offen:** Der Testsatz (Plan, Aufgabe D) ist nicht erstellt, die Messung mit echtem Ollama fehlt.
- Dauer qpdf, Seitenanalyse und Bearbeitung je Seite: **noch offen** (keine Messung dokumentiert).
- Die Abnahme (Zoom 200 %/400 %, Tastatur, NVDA) folgt in P6 durch den Nutzer.
