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
- [x] Papierkorb im Schema: `documents.deleted_at`, `deleted_by`; Tabelle `trash_items`; **ein** Filter
      „nicht im Papierkorb“ für EF (globaler Abfragefilter) und alle rohen SQL-Abfragen (Suche, Duplikate,
      SHA-256, „Zu prüfen“, Listeneinträge, Lernbeispiele)
- [x] Dateien: Art `Edited` (bearbeitete Fassung) neben `Original` und `Archive`; Ablage `edited.pdf`,
      Papierkorb-Ordner `trash/<id>/`; Speicher-Port erweitert (speichern, in den Papierkorb, zurück, endgültig)
- [x] Seitentexte: `document_pages` (Seite, Textanfang ≤ 300 Zeichen, vermutlich leer) – die OCR schreibt sie
- [x] Aufträge: Stufen `Edit` und `Separation` in `processing_jobs`; Tabelle `document_edits` (geplante
      Bearbeitung als JSON, ersetzte Seitenbilder im Dateisystem)
- [x] Domain: `Document.MoveToTrash/RestoreFromTrash`, `VersionPolicy`, `SplitSuggestion`, `PagePlan`
      (Reihenfolge, Drehung, Löschen, ersetzte Seite), Regeln für Zusammenfügen (Privat)
- [x] Ports: `IPdfTool` (qpdf: Seiten wählen, drehen, zusammensetzen, teilen), `IPageAnalyzer`, `ITrashStore`
- [x] Einstellung „Nach dem Bearbeiten behalten“ je Konto (Spalte, API, Contracts)
- [x] Löschen eines Dokuments geht in den Papierkorb (bisher hartes Löschen bei Duplikaten)
- [x] `qpdf` im Worker-Image; Contracts für alle Spuren; ADR-0025; Katalogblöcke

### A. Bearbeitung im Worker (Seiten, Zusammenfügen, Teilen)
- [x] Anwendungsfälle mit Zeilensperre: `RequestPageEdit` (Plan + ersetzte Seiten), `AppendDocument`,
      `SplitDocument`; Endpunkte; 409 solange nicht fertig verarbeitet
- [x] Stufe `Edit`: Quelle (bearbeitete Fassung, sonst Original) + Plan → qpdf → `edited.pdf` → OCR → neues
      Archiv, Seitentexte, Versionsregel anwenden (alte Fassung bzw. Original in den Papierkorb)
- [x] Zusammenfügen: Seiten des angehängten Dokuments ans Ende; angehängtes in den Papierkorb; Privat-Regel
- [x] Teilen: neue Dokumente (Original = Seitenauszug, Besitzer/Privat/Bearbeiter übernommen, normale OCR +
      Erkennung), Ausgangsdokument in den Papierkorb
- [x] Versionswahl je Dokument („nur bearbeitet“, „nur Original“, „beides“) als Anwendungsfall

### B. Oberfläche „Seiten bearbeiten“ und Versionen
- [x] Seite `/dokumente/{id}/seiten`: Seitenliste (Liste mit Ansage je Seite), Vorschau mit Shader, Knöpfe und
      Kürzel (drehen, löschen, nach oben/unten, zuschneiden), Drag & Drop nur zusätzlich, „Speichern“/„Verwerfen“
- [x] Zuschnitt: OpenCV im Browser (Blatt + Schräglage), Ansage des Ergebnisses, Ecken per Pfeiltasten;
      ersetzte Seite als JPEG an den Server
- [x] „Dokument anhängen“ (Auswahl per Suche, Privat-Hinweis), „Hier teilen“ (Trennstellen in der Seitenliste)
- [x] Viewer: Knopf „Seiten bearbeiten“, Bereich „Fassungen“ (Original / bearbeitet, Wahl behalten), „wird
      bearbeitet“; Konto-Seite: Werkswert; Vorschaubilder je Seite

### C. Papierkorb
- [x] Anwendungsfälle: auflisten (nur eigene), wiederherstellen (Dokument, Datei; belegter Platz → Tausch),
      endgültig entfernen, leeren; Endpunkte
- [x] Täglicher Aufräumdienst im Worker (`purge_after` < jetzt → Dateien und Zeilen weg)
- [x] Seite `/papierkorb` (Navigation), Löschen-Knopf im Viewer (Besitzer, mit Bestätigung)

### D. Stapel-Trennung
- [x] Stufe `Separation` nach der OCR bei mehrseitigen Scans (Original ohne eigene Textebene): leere Seiten,
      „Seite x von y“, Anrede, Absenderwechsel (ein LLM-Aufruf mit den Seitenanfängen)
- [x] Vorschlag am Dokument; „Zu prüfen“-Grund „Trennvorschlag“; annehmen (→ `SplitDocument`), anpassen
      (→ „Seiten bearbeiten“ mit Trennstellen), verwerfen
- [ ] Testsatz: 12 selbst erzeugte Stapel mit bekannten Grenzen, Messung mit echtem Ollama (optional)

### E. Tests und Doku
- [ ] Unit: Seitenplan, Versionsregel, Papierkorb-Regeln, Trennheuristik; Integration: qpdf-Abläufe (Linux),
      Papierkorb-Filter in jeder Abfrage, Aufräumdienst; UI: axe DE/EN, Tastatur in allen neuen Seiten
- [x] KB `scan-bearbeitung.md`, ADR-0025, Katalog, `verarbeitung.md`

## Akzeptanzkriterien
- [ ] Seiten drehen, löschen, umsortieren, zuschneiden per Tastatur; neue Version, Original bleibt (Werk)
- [ ] Zusammenfügen und Teilen ergeben die erwarteten Seiten; Privat-Regel beim Zusammenfügen greift
- [ ] Ein Dokument im Papierkorb erscheint in keiner Liste, Suche, Duplikat-Meldung oder Listeneintrag-Sicht
- [ ] Wiederherstellen und automatisches Löschen nach 30 Tagen funktionieren (Test mit gestellter Uhr)
- [ ] Trennvorschlag: an 12 Teststapeln ≥ 80 % der Grenzen richtig, keine Trennung ohne Bestätigung (⚠️ messen)
- [ ] axe 0 Verstöße; NVDA-Prüfung „Seiten bearbeiten“ und „Papierkorb“ durch den Nutzer
- [ ] CI grün

## Parallelisierung

Integrationsbranch `feat/m04b-scan-bearbeitung` von `develop` (`a95ed00`; M4u ist gemergt – keine Spur wartet mehr auf
M4u). Pakete, Modelle und Exklusiv-Dateien: [Detailplan](features/m4b-scan-bearbeitung-detail.md).

| Spur | Inhalt | Pakete (Modell) | Branch | Start | wartet auf |
|---|---|---|---|---|---|
| 0 | Schritt 0 Backend | P0 (Opus) | `feat/m04b-scan-bearbeitung` | sofort | – |
| S | Stitch-Nachtrag 14/15/16 v3, 09, 06 | S0 (Opus, Abholen Haiku) | – (Stitch, `docs/ui/stitch/`) | sofort, parallel zu 0 | – |
| 0u | Schritt 0 Oberfläche (Texte, CSS-Dateien, API-Clients) | P0u (Sonnet) | `feat/m04b-scan-bearbeitung` | nach 0 | Freigabe S durch den Nutzer |
| A | Bearbeitung im Worker | P1 (Opus) | `feat/m04b-a-bearbeitung` | nach 0 | F1 (nur Nachlauf) |
| B | Seiten bearbeiten | P2a, P2b (Sonnet), P2c (Opus) | `feat/m04b-b-seiten` | nach 0u | F2 (nur `NavigationLock`) |
| V | Fassungen, Werkswert | P2d (Sonnet) | `feat/m04b-v-fassungen` | nach 0u | – |
| C | Papierkorb | P3a (Opus), P3b, P3c (Sonnet) | `feat/m04b-c-papierkorb` | P3a/P3b nach 0, P3c nach 0u | – |
| D | Stapel-Trennung | P4a, P4b (Sonnet), P4c (Opus), P4d (Sonnet) | `feat/m04b-d-trennung` | P4a–P4c nach 0, P4d nach 0u | – |

```
0 ──┬─► A ──────────────────────────┐
    ├─► C: P3a, P3b ──► P3c ─┐      │
    ├─► D: P4a–P4c ───► P4d ─┤      │
    │                  ▲     ├──────┼─► Merge C, A, D, V, B ─► P5 Doku ─► P6 Review/Abnahme
S ──┴──────────► 0u ───┴─► B ┤      │
                       └─► V ┘──────┘
```

- **Schritt 0** (nacheinander, steuernde Sitzung): P0 mit Review-Schleife, nach Freigabe von S0 dann P0u. Die
  Backend-Pakete von A, C und D starten schon nach P0; die UI-Pakete (P2*, P3c, P4d) erst nach P0u.
- **Exklusiv-Dateien:** je Spur im Detailplan; wichtig: `DocumentViewer.razor.cs` nur V, `Review.razor(.cs)` nur D,
  `Settings.razor`/`NavSection.cs` nur C, `JobQueue.cs` nach P0 nur D, `JobQueue.Edit.cs` nur A, `Thumbnail*`/`thumbnails.js`
  nur B.
- **Nach Schritt 0 gesperrt** (Lehre M4u): `Strings*.resx`, `index.html`, `_Imports.razor`, `Design/*`, `design*.css`, alle
  Contracts, Migration. Fehlt etwas, meldet die Spur es; die steuernde Sitzung ergänzt im Integrationsbranch.
- **Hotspots** (nur ergänzen, je eine Zeile): `Server/Program.cs` (A, C, D), `ApplicationServiceCollectionExtensions.cs`
  (A, C, D), `RecognitionRegistration.cs` (D), `WorkerComposition.cs` (C), `DocumentViewer.razor` (V: Fassungen, C: Knopf
  „In den Papierkorb“). `docs/kb/telemetrie.md` und `docs/kb/scan-bearbeitung.md` nur in P5.
- **Merge-Reihenfolge:** C → A → D → V → B (B zuletzt: größte Oberfläche, ändert `Thumbnail` aus dem Viewer). Nach jedem
  Merge Build und alle Tests.
- **Last:** höchstens zwei Spuren mit Playwright-Läufen gleichzeitig (M4u: vier Worktrees waren wackelig).

## Detailplan
Eigene Datei: [features/m4b-scan-bearbeitung-detail.md](features/m4b-scan-bearbeitung-detail.md)

## Offene Fragen / Risiken

**Fragen an den Nutzer** (je mit Empfehlung; alles andere entscheiden Plan, ADRs, Entwürfe und Code).
**Beantwortet 2026-11-23: F1 und F2 wie empfohlen** (Seitentexte nachholen; `NavigationLock` mit Rückfrage).

| Nr. | Frage | Empfehlung | Bei der anderen Antwort |
|---|---|---|---|
| F1 | Dokumente, die vor M4b verarbeitet wurden, haben keine gespeicherten Seitentexte. „Seiten bearbeiten“ zeigt bei ihnen keinen Textanfang und kein „vermutlich leer“. Soll der Worker diese Seitentexte einmalig für alle vorhandenen Dokumente nachholen (nur Text auslesen und ein kleines Bild je Seite prüfen, keine neue Texterkennung, mit niedrigster Priorität), oder sollen nur neue Dokumente sie bekommen? | **Nachholen:** Die Ansage je Seite ist die wichtigste Orientierung beim Bearbeiten; die Last ist gering und läuft nur, wenn sonst nichts zu tun ist. | P1 ohne Nachlauf; bei alten Dokumenten sagt die Karte nur „Seite x von y“ und „Textanfang unbekannt“. |
| F2 | Wer in „Seiten bearbeiten“ ungespeicherte Änderungen hat und die Seite über einen Link (zum Beispiel in der Seitenleiste) verlässt, verliert sie. Soll die App dann in der Seite nachfragen („Seite verlassen? Deine Änderungen gehen verloren.“, „Abbrechen“ zuerst), bevor sie wechselt? | **Ja, nachfragen:** Ein versehentlicher Tastendruck kostet sonst viel Arbeit; die Rückfrage nutzt denselben Baustein wie „Alles verwerfen“. Beim Neuladen oder Schließen fragt der Browser selbst. | Kein `NavigationLock`; Änderungen gehen beim Verlassen ohne Rückfrage verloren. |

**Freigabe nötig** (kein Entscheid, Ablauf nach AGENTS §2.5): Stitch-Nachtrag S0 (14/15/16 v3, Konto 09 mit Karte
„Bearbeitete Dokumente“, Zu prüfen 06 mit Trennvorschlag). Die UI-Pakete starten erst danach.

**Risiken:**
- **Größe:** realistisch L statt M (Roadmap angepasst)
- **qpdf + PDF/A:** qpdf erhält Textebene und Bilder, aber nicht die PDF/A-Konformität → jede Bearbeitung
  läuft erneut durch OCRmyPDF (`--skip-text`, PDF/A, **ohne** `--rotate-pages`/`--deskew`, sonst „korrigiert“ es die Wahl
  des Nutzers). Gedrehte Seiten mit vorhandener Textebene: ⚠️ prüfen, ob `--skip-text` die gedrehte Ebene richtig
  übernimmt, sonst `--redo-ocr`
- **Globaler Papierkorb-Filter** in EF: jeder bestehende `db.Documents`-Zugriff ändert sein Verhalten; Absicherung durch
  Architekturtest und je Abfrage einen Postgres-Test
- **Dateien nahe 250 Zeilen** (`DocumentViewer.razor.cs`, `Review.razor.cs`, `DocumentApi.cs`, `document-view.css`): neue
  Logik in eigene Dateien
- **Zuschnitt im Browser** rendert die Seite neu (PDF.js, 200 dpi) und ersetzt sie durch ein Bild; eine Seite
  mit echter Textebene (digitales PDF) verliert sie und wird neu erkannt – Hinweis in der Ansage
- **Trennung** kostet einen LLM-Aufruf je mehrseitigem Scan; läuft mit Erkennungs-Vorrang über `LlmGate`
- **Speicher:** Original + bearbeitet + Archiv = bis 3× Größe; Papierkorb hält 30 Tage zusätzlich
- **Neue Oberfläche:** Entwürfe 14–16 freigegeben (2026-11-23); nicht entworfene Zustände und Elemente kommen über S0
  ([UI-Prozess](../kb/ui-prozess.md)); umgesetzt wird mit den M4u-Bausteinen
- Aus M4 offen: iCloud (Spur D von M4), iPhone-/NVDA-Prüfung
- Teile aus einem Split laufen wieder durch die Stapel-Trennung; wer über „Anpassen“ bewusst Seiten zusammenließ, bekommt ggf. erneut einen Vorschlag (Vorschlag: Split-Teile überspringen die Trennung – Entscheidung offen)
- Trennvorschlag: Textanfang „beginnt mit …“ aus Entwurf 06 fehlt, `SplitSuggestionDto` hat kein Textstück (Contract-Änderung nötig)
- Text „1 Dokumente“ bei nur einem Teil (Backend erzeugt das nie)
- `TrashErrorConflict` sagt „Lade den Papierkorb neu“, die Seite lädt aber selbst neu; `TrashRestoreFor`/`TrashPurgeFor` unbenutzt
- Zeitformat Papierkorb „g“ statt „28.02.2026, 11:15“ wie im Entwurf

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|
