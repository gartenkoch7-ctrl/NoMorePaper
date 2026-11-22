# M4b – Scan-Bearbeitung

**Ziel:** Gescannte Dokumente lassen sich vollständig per Tastatur und Screenreader bearbeiten: Seiten
drehen, löschen, umsortieren, zuschneiden und gerade richten, Dokumente zusammenfügen und teilen. Ein
mehrseitiger Stapel wird als Vorschlag in einzelne Dokumente getrennt. Jede Bearbeitung erzeugt eine
neue Version; Entferntes liegt 30 Tage im Papierkorb.
**Demo:** Ein Stapel mit drei Briefen kommt über den Scanner-Ordner → „Zu prüfen“ sagt „Trennvorschlag:
3 Dokumente (Seiten 1–2, 3, 4–6), Seite 5 vermutlich leer“ → bestätigen → drei Dokumente, jedes neu
erkannt. In einem davon „Seiten bearbeiten“: Seite 2 ist schief → „Ränder und Schräglage automatisch“
sagt „um 3 Grad gedreht, Rand oben entfernt“ → Speichern → neue Version, Original bleibt. Ein Dokument
löschen → Papierkorb → wiederherstellen.
**Voraussetzungen:** M3 (Erkennung), M4 (Eingänge, LLM-Zugang, PDFsharp). **Größe:** L.
**Grundlage:** [ADR-0013](../adr/0013-scan-bearbeitung-versionen-papierkorb.md), Umsetzung in
[ADR-0025](../adr/0025-scan-bearbeitung-umsetzung.md).

Entschieden (Grilling 2026-11-19):
- **Bedienung:** eigene Seite **„Seiten bearbeiten“** (aus dem Viewer). Seitenliste, jede Seite mit Ansage
  (Seitenzahl, Anfang des erkannten Textes, „vermutlich leer“) und großer Vorschau mit Shader; Knöpfe und
  Tastenkürzel je Seite; erst **„Speichern“** erzeugt die neue Version, vorher lässt sich alles verwerfen
- **Recht:** Seiten bearbeiten, zusammenfügen und teilen darf, **wer die Angaben ändern darf** (Besitzer,
  Bearbeiter). Original endgültig entfernen, Papierkorb leeren: **nur der Besitzer**
- **Papierkorb** für **gelöschte Dokumente**, entfernte Originale und alte Versionen; **30 Tage**, danach
  löscht der Worker endgültig; eigene Seite; nur für den Besitzer sichtbar
- **Versionen:** es bleiben **Original + neueste bearbeitete Version**; eine ältere bearbeitete Version
  kommt bei jeder neuen Bearbeitung in den Papierkorb. Werkswert **je Konto** („beides“ ab Werk, „nur
  bearbeitete Version“, „nur Original“), je Dokument änderbar
- **Zuschnitt/Geraderichten:** Knopf **„Ränder und Schräglage automatisch“** (Blatt erkennen wie die
  Kamera), Ergebnis wird angesagt; Ecken zusätzlich per Pfeiltasten oder Maus verschiebbar
- **Teilen:** die Teile werden **neu erkannt** (stehen danach in „Zu prüfen“); Besitzer, Privat und
  Bearbeiter vom Ausgangsdokument; das Ausgangsdokument kommt in den Papierkorb
- **Zusammenfügen:** in das **geöffnete** Dokument („Dokument anhängen“), Seiten ans Ende; Angaben, Besitzer,
  Privat bleiben; das angehängte kommt in den Papierkorb. Ist das angehängte **privat**, das geöffnete
  geteilt, sagt die App das vorher und **das Ergebnis wird privat**
- **Stapel-Trennung:** Vorschlag für **mehrseitige Scans** aus allen Eingängen (leere Seiten, „Seite 1 von 3“,
  neue Anrede, neuer Absender – das LLM liest nur den Anfang jeder Seite); erscheint in „Zu prüfen“;
  geteilt wird erst nach Bestätigung
- **Technik:** Drehen, Umsortieren, Löschen, Zusammenfügen, Teilen macht der Worker **verlustfrei mit qpdf**
  (Apache-2.0); Zuschnitt und Geraderichten rechnet der **Browser mit OpenCV** (wie die Kamera); danach
  läuft die Texterkennung neu

Daraus abgeleitet:
- Ein Dokument im Papierkorb ist **überall unsichtbar** (Liste, Suche, Duplikate, „Zu prüfen“, Lernbeispiele,
  Sichtbarkeit von Listeneinträgen) – außer auf der Papierkorb-Seite seines Besitzers
- Bearbeiten, Zusammenfügen und Teilen gehen nur bei **fertig verarbeiteten** Dokumenten (sonst 409); während
  der Worker eine Bearbeitung ausführt, zeigt der Viewer weiter die bisherige Version und sagt „wird bearbeitet“
- Die Angaben (Absender, Art, Datum …) bleiben bei Seitenbearbeitung und Zusammenfügen erhalten; nur der Text
  für Suche und Duplikate wird neu erkannt
- Zusammenfügen sperrt beide Dokumente in fester Reihenfolge (kleinere Id zuerst) gegen Verklemmungen
- Wird ein Original entfernt, ist es auch kein Duplikat-Maßstab mehr (SHA-256 nur gegen vorhandene Originale)

## Aufgaben

### 0. Gemeinsame Bausteine (Schritt 0)
- [ ] Papierkorb im Schema: `documents.deleted_at`, `deleted_by`; Tabelle `trash_items`; **ein** Filter
      „nicht im Papierkorb“ für EF (globaler Abfragefilter) und alle rohen SQL-Abfragen (Suche, Duplikate,
      SHA-256, „Zu prüfen“, Listeneinträge, Lernbeispiele)
- [ ] Dateien: Art `Edited` (bearbeitete Fassung) neben `Original` und `Archive`; Ablage `edited.pdf`,
      Papierkorb-Ordner `trash/<id>/`; Speicher-Port erweitert (speichern, in den Papierkorb, zurück, endgültig)
- [ ] Seitentexte: `document_pages` (Seite, Textanfang ≤ 300 Zeichen, vermutlich leer) – die OCR schreibt sie
- [ ] Aufträge: Stufen `Edit` und `Separation` in `processing_jobs`; Tabelle `document_edits` (geplante
      Bearbeitung als JSON, ersetzte Seitenbilder im Dateisystem)
- [ ] Domain: `Document.MoveToTrash/RestoreFromTrash`, `VersionPolicy`, `SplitSuggestion`, `PagePlan`
      (Reihenfolge, Drehung, Löschen, ersetzte Seite), Regeln für Zusammenfügen (Privat)
- [ ] Ports: `IPdfTool` (qpdf: Seiten wählen, drehen, zusammensetzen, teilen), `IPageAnalyzer`, `ITrashStore`
- [ ] Einstellung „Nach dem Bearbeiten behalten“ je Konto (Spalte, API, Contracts)
- [ ] Löschen eines Dokuments geht in den Papierkorb (bisher hartes Löschen bei Duplikaten)
- [ ] `qpdf` im Worker-Image; Contracts für alle Spuren; ADR-0025; Katalogblöcke

### A. Bearbeitung im Worker (Seiten, Zusammenfügen, Teilen)
- [ ] Anwendungsfälle mit Zeilensperre: `RequestPageEdit` (Plan + ersetzte Seiten), `AppendDocument`,
      `SplitDocument`; Endpunkte; 409 solange nicht fertig verarbeitet
- [ ] Stufe `Edit`: Quelle (bearbeitete Fassung, sonst Original) + Plan → qpdf → `edited.pdf` → OCR → neues
      Archiv, Seitentexte, Versionsregel anwenden (alte Fassung bzw. Original in den Papierkorb)
- [ ] Zusammenfügen: Seiten des angehängten Dokuments ans Ende; angehängtes in den Papierkorb; Privat-Regel
- [ ] Teilen: neue Dokumente (Original = Seitenauszug, Besitzer/Privat/Bearbeiter übernommen, normale OCR +
      Erkennung), Ausgangsdokument in den Papierkorb
- [ ] Versionswahl je Dokument („nur bearbeitet“, „nur Original“, „beides“) als Anwendungsfall

### B. Oberfläche „Seiten bearbeiten“ und Versionen
- [ ] Seite `/dokumente/{id}/seiten`: Seitenliste (Liste mit Ansage je Seite), Vorschau mit Shader, Knöpfe und
      Kürzel (drehen, löschen, nach oben/unten, zuschneiden), Drag & Drop nur zusätzlich, „Speichern“/„Verwerfen“
- [ ] Zuschnitt: OpenCV im Browser (Blatt + Schräglage), Ansage des Ergebnisses, Ecken per Pfeiltasten;
      ersetzte Seite als JPEG an den Server
- [ ] „Dokument anhängen“ (Auswahl per Suche, Privat-Hinweis), „Hier teilen“ (Trennstellen in der Seitenliste)
- [ ] Viewer: Knopf „Seiten bearbeiten“, Bereich „Fassungen“ (Original / bearbeitet, Wahl behalten), „wird
      bearbeitet“; Konto-Seite: Werkswert; Vorschaubilder je Seite

### C. Papierkorb
- [ ] Anwendungsfälle: auflisten (nur eigene), wiederherstellen (Dokument, Datei; belegter Platz → Tausch),
      endgültig entfernen, leeren; Endpunkte
- [ ] Täglicher Aufräumdienst im Worker (`purge_after` < jetzt → Dateien und Zeilen weg)
- [ ] Seite `/papierkorb` (Navigation), Löschen-Knopf im Viewer (Besitzer, mit Bestätigung)

### D. Stapel-Trennung
- [ ] Stufe `Separation` nach der OCR bei mehrseitigen Scans (Original ohne eigene Textebene): leere Seiten,
      „Seite x von y“, Anrede, Absenderwechsel (ein LLM-Aufruf mit den Seitenanfängen)
- [ ] Vorschlag am Dokument; „Zu prüfen“-Grund „Trennvorschlag“; annehmen (→ `SplitDocument`), anpassen
      (→ „Seiten bearbeiten“ mit Trennstellen), verwerfen
- [ ] Testsatz: 12 selbst erzeugte Stapel mit bekannten Grenzen, Messung mit echtem Ollama (optional)

### E. Tests und Doku
- [ ] Unit: Seitenplan, Versionsregel, Papierkorb-Regeln, Trennheuristik; Integration: qpdf-Abläufe (Linux),
      Papierkorb-Filter in jeder Abfrage, Aufräumdienst; UI: axe DE/EN, Tastatur in allen neuen Seiten
- [ ] KB `scan-bearbeitung.md`, ADR-0025, Katalog, `verarbeitung.md`

## Akzeptanzkriterien
- [ ] Seiten drehen, löschen, umsortieren, zuschneiden per Tastatur; neue Version, Original bleibt (Werk)
- [ ] Zusammenfügen und Teilen ergeben die erwarteten Seiten; Privat-Regel beim Zusammenfügen greift
- [ ] Ein Dokument im Papierkorb erscheint in keiner Liste, Suche, Duplikat-Meldung oder Listeneintrag-Sicht
- [ ] Wiederherstellen und automatisches Löschen nach 30 Tagen funktionieren (Test mit gestellter Uhr)
- [ ] Trennvorschlag: an 12 Teststapeln ≥ 80 % der Grenzen richtig, keine Trennung ohne Bestätigung (⚠️ messen)
- [ ] axe 0 Verstöße; NVDA-Prüfung „Seiten bearbeiten“ und „Papierkorb“ durch den Nutzer
- [ ] CI grün

## Parallelisierung

Integrationsbranch `feat/m04b-scan-bearbeitung` von `develop`.

| Spur | Inhalt | Branch | Start | wartet auf |
|---|---|---|---|---|
| 0 | Schritt 0 | `feat/m04b-scan-bearbeitung` | sofort | – |
| A | Bearbeitung im Worker | `feat/m04b-a-bearbeitung` | nach 0 | – |
| B | Oberfläche Seiten/Versionen | `feat/m04b-b-oberflaeche` | nach 0 + M4u Spur C in `develop` | [M4u](m4u-oberflaeche.md) (Theme, Layout, Viewer) |
| C | Papierkorb | `feat/m04b-c-papierkorb` | nach 0 (Backend); Seite `/papierkorb` nach M4u Spur C in `develop` | [M4u](m4u-oberflaeche.md) nur für die Seite |
| D | Stapel-Trennung | `feat/m04b-d-trennung` | nach 0 | – |

```
0 ──┬─► A ─┐
    ├─► C ─┤
    ├─► D ─┼─► Merge C, A, D, B ─► Gesamt-Review
    └─► B ─┘
```

- **Exklusiv-Dateien:** siehe Detailplan je Paket
- **Hotspots** (nur ergänzen): `Strings*.resx`, `MainLayout.razor`, `Server/Program.cs`, `Web.Client/Program.cs`,
  `ApplicationServiceCollectionExtensions.cs`, `InfrastructureServiceCollectionExtensions.cs`,
  `WorkerComposition.cs`, `docs/kb/telemetrie.md`, `docs/kb/scan-bearbeitung.md`, `DocumentViewer.razor`
  (je eine Zeile zum Einbinden), `Review.razor` (eine Zeile), `ProcessNextDocument.cs` (Weiche je Stufe)
- **Merge-Reihenfolge:** C → A → D → B

## Detailplan
Eigene Datei: [features/m4b-scan-bearbeitung-detail.md](features/m4b-scan-bearbeitung-detail.md)

## Offene Fragen / Risiken
- **Größe:** realistisch L statt M (Roadmap angepasst)
- **qpdf + PDF/A:** qpdf erhält Textebene und Bilder, aber nicht die PDF/A-Konformität → jede Bearbeitung
  läuft erneut durch OCRmyPDF (`--skip-text`, PDF/A). Gedrehte Seiten mit vorhandener Textebene: ⚠️ prüfen,
  ob `--skip-text` die gedrehte Ebene richtig übernimmt, sonst `--redo-ocr`
- **Zuschnitt im Browser** rendert die Seite neu (PDF.js, 200 dpi) und ersetzt sie durch ein Bild; eine Seite
  mit echter Textebene (digitales PDF) verliert sie und wird neu erkannt – Hinweis in der Ansage
- **Trennung** kostet einen LLM-Aufruf je mehrseitigem Scan; läuft mit Erkennungs-Vorrang über `LlmGate`
- **Speicher:** Original + bearbeitet + Archiv = bis 3× Größe; Papierkorb hält 30 Tage zusätzlich
- **Neue Oberfläche:** Seite `/papierkorb` (Spur C) und Spur B nach dem neuen Design aus M4u bauen;
  Stitch-Entwürfe dafür vorher nach dem [UI-Prozess](../kb/ui-prozess.md) erzeugen und freigeben lassen
  (prompts.md §4.1–4.3)
- Aus M4 offen: iCloud (Spur D von M4), iPhone-/NVDA-Prüfung

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|
