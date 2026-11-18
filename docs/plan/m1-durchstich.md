# M1 – Durchstich, Barrierefreiheit + iCloud-Spike

**Ziel:** Ein Dokument geht einmal komplett durch das System – und ist für den sehbehinderten
Hauptnutzer von Anfang an lesbar.
**Demo:** PDF oder Foto hochladen → Worker macht OCR und PDF/A → Dokument erscheint in der Liste
und wird per Volltextsuche gefunden → Viewer zeigt es intelligent invertiert (schwarz/weiß,
Farbton bleibt, Fotos original), zoomt scharf bis 1600 %, alles per Tastatur und Screenreader.
Dazu zwei Spike-Ergebnisse: Shader-Parameter und „iCloud in .NET machbar: ja/nein“.
**Voraussetzungen:** M0. **Größe:** L+.

Entscheidungen (Grilling 2026-11-18):
- OCR mit **OCRmyPDF im Worker** (Tesseract `deu+eng`, Ausgabe PDF/A)
- Darstellung mit **PDF.js** im Browser plus WebGL-Shader
- **Ohne Anmeldung, nur lokal** (`127.0.0.1`, wie M0); Anmeldung kommt in M2
- Shader-Spike auf dem **Windows-PC (Chrome/Edge)**
- iCloud-Spike **3 Tage**, bestanden bei Anmeldung inkl. 2FA, Ordner auflisten, Datei laden und
  einer Sitzung, die einen Neustart übersteht
- Upload: **PDF, JPG, PNG, HEIC**
- Zoom per Tastatur, Mausrad/Touchpad, „Breite anpassen“, **25–1600 %**
- Suche **Deutsch + Englisch** (Wortstamm) plus Trigramme gegen OCR-Fehler

## Aufgaben

### 1. Modell, Schema, Ablage (Schritt 0)
- [x] Domain: `Document` (Id, Titel, Eingangszeitpunkt, Status), `DocumentFile` (Art `Original`/`Archive`,
      Dateiname, MIME-Typ, Größe, SHA-256), `ProcessingState` (`Queued`, `Processing`, `Done`, `Failed`)
- [x] Postgres: Tabellen `documents`, `document_files`, `processing_jobs` (Warteschlange mit
      `FOR UPDATE SKIP LOCKED`, Versuche, nächster Versuch), Spalte `content_text`
- [x] Suche: `search_vector` als generierte Spalte aus `to_tsvector('german')` und `to_tsvector('english')`,
      GIN-Index; Extension `pg_trgm` mit Trigramm-Index auf `content_text`
- [x] `IDocumentStorage` (Application) + Dateisystem-Umsetzung: `/data/inbox/<id>/`,
      `/data/archive/<jjjj>/<mm>/<id>/original.<ext>` und `archive.pdf`; Schreiben atomar
      (temporäre Datei + Umbenennen)
- [x] Contracts: `DocumentSummaryDto`, `DocumentDetailDto`, `SearchHitDto`, `UploadResultDto`

### 2. Upload, API und Suche (Server)
- [ ] `POST /api/documents` (multipart, max. 100 MB, Typ nach **Dateisignatur** prüfen, nicht nach
      Endung): legt Dokument + Job an, Antwort 202 mit Id
- [ ] `GET /api/documents` (neueste zuerst, Seiten), `GET /api/documents/{id}`,
      `GET /api/documents/{id}/files/{original|archive}` (Download immer unverändert)
- [ ] `GET /api/search?q=` mit Rangfolge (`ts_rank`) und Textausschnitt (`ts_headline`);
      Trigramm-Ähnlichkeit als Rückfall, wenn die Wortsuche nichts findet
- [ ] Server bleibt auf `127.0.0.1`; keine Anmeldung in M1 (in README deutlich vermerkt)

### 3. Verarbeitung (Worker)
- [ ] Job-Schleife: Job holen (`SKIP LOCKED`), Status setzen, Fehler mit 3 Versuchen und
      wachsendem Abstand, danach `Failed` mit technischem Grund (ohne Inhalt)
- [ ] Bilder (JPG/PNG/HEIC) → PDF (HEIC über `heif-convert`, dann `img2pdf`)
- [ ] OCRmyPDF: `--output-type pdfa --language deu+eng --rotate-pages --deskew --skip-text`
      mit Zeitlimit je Dokument; Text mit `pdftotext` in `content_text`
- [ ] Worker-Image auf **Debian-Basis** (`aspnet:10.0`), da OCRmyPDF, Tesseract-Sprachdaten,
      Ghostscript, `libheif-examples` und `poppler-utils` dort als Pakete vorliegen; behebt nebenbei
      die fehlende ICU (M0-Review)

### 4. Oberfläche: Upload, Liste, Suche
- [ ] Seite „Hochladen“: Dateiauswahl mit sichtbarer Beschriftung, Ziehen-und-Ablegen nur
      zusätzlich, Fortschritt und Ergebnis über `aria-live`
- [ ] Seite „Dokumente“: Liste mit Titel, Datum, Status (Text, nicht nur Farbe); Status wird
      aktualisiert, solange etwas verarbeitet wird
- [ ] Suche im Kopfbereich (Tastenkürzel `/`), Trefferliste mit Ausschnitt, Treffer als Überschriften
      navigierbar
- [ ] Texte DE/EN, Navigation um die neuen Seiten ergänzt

### 5. Viewer mit Shader (nach dem Shader-Spike)
- [ ] PDF.js (feste Version, unter `wwwroot/lib/pdfjs/` ausgeliefert, kein CDN)
- [ ] WebGL-Shader „intelligent invertiert“ mit den Werten aus dem Spike; Rückfall ohne WebGL:
      Invertieren mit erhaltenem Farbton (CSS-Filter)
- [ ] Schalter „Originalfarben“ je Gerät (gespeichert im Browser)
- [ ] Zoom 25–1600 %: `Strg` + `+`/`-`/`0`, `Strg` + Mausrad und Zwei-Finger-Geste um den Zeiger,
      „Breite anpassen“; bei jeder Stufe scharf neu rendern
- [ ] Bewegen mit Pfeiltasten, Seiten mit `Bild auf`/`Bild ab`; Zoomstufe und Seite per `aria-live`
- [ ] Textebene von PDF.js für Screenreader; Hinweis, wenn ein Dokument keinen Text hat
- [ ] Vorschaubilder in Liste und Suche ebenfalls durch den Shader (ADR-0011)

### 6. Shader-Spike (2 Tage, zuerst)
- [ ] Testsatz: Brief mit farbigem Logo, Rechnung mit farbiger Tabelle, Foto im Dokument, grauer
      Scan, reiner Text
- [ ] Prototyp `spikes/viewer-shader/` (PDF.js + WebGL, nicht in der Solution)
- [ ] Festlegen: Farbraum für „Helligkeit drehen, Farbton halten“ (z. B. OKLab), Foto-Erkennung
      (Farbigkeit und Detailreichtum der Umgebung), Schwellen; GPU-Zeit je Seite bei 1600 %
- [ ] Ergebnis in `docs/kb/darstellung.md` und als Nachtrag zu ADR-0011

### 7. iCloud-Spike (3 Tage, parallel)
- [ ] Prototyp `spikes/icloud/` (.NET-Konsolenprogramm, nicht in der Solution, keine Zugangsdaten
      im Repo)
- [ ] Prüfen: Anmeldung mit Apple-ID, 2FA-Code eingeben, iCloud-Drive-Ordner auflisten, Datei
      laden, Sitzung speichern und nach Neustart weiterverwenden
- [ ] Ergebnis als ADR (bestanden → Umsetzung in M4; nicht bestanden → Fallback)

### 8. Tests und Doku
- [ ] Domain: Unit-Tests für Status- und Wiederholungslogik
- [ ] Infrastructure (Testcontainers): Warteschlange mit zwei Workern ohne Doppelverarbeitung,
      Suche findet „Rechnungen“ bei „Rechnung“ und bei OCR-Tippfehlern
- [ ] Worker: Integrationstest mit echtem OCRmyPDF im gebauten Worker-Image (Beispiel-PDF und -Foto)
- [ ] UI: axe ohne Verstöße auf allen neuen Seiten (DE/EN), Tastaturtests für Upload, Suche
      und Viewer, Pixeltest des Shaders (weiße Fläche → schwarz, rotes Logo bleibt rot, Foto unverändert)
- [ ] KB: `darstellung.md`, `verarbeitung.md` (Pipeline, Werkzeuge, Zeitlimits), Telemetrie-Katalog;
      ADR-0016 (OCRmyPDF, Debian-Worker-Image), ADR-0017 (PDF.js)

## Akzeptanzkriterien
- [ ] Upload → Dokument „Done“ in unter 2 Minuten für einen 3-seitigen Brief auf dem Server ⚠️ messen
- [ ] Suche findet das Dokument über ein Wort aus dem gescannten Text
- [ ] Viewer: schwarzer Hintergrund, weiße Schrift, farbiges Logo im Farbton erhalten, Foto original;
      Zoom bis 1600 % scharf; alles ohne Maus bedienbar
- [ ] Download liefert Original und PDF/A unverändert
- [ ] axe 0 Verstöße; Sichtprüfung mit NVDA durch den Nutzer
- [ ] Beide Spikes mit Ergebnis-ADR abgeschlossen
- [ ] CI grün

## Parallelisierung

Integrationsbranch `feat/m01-durchstich` von `develop`.

```
Spur S (Shader-Spike) ───────────────────────────┐
Spur I (iCloud-Spike) ─────────────────────────┐ │
Schritt 0 (1) ─┬─► Spur A (2) ─┐               │ │
               ├─► Spur B (3) ─┼─► Merge ──────┼─┴─► Spur V (5) ─► Spur D (8 Doku) ─► Review
               └─► Spur C (4) ─┘               └──► ADR
```

| Spur | Inhalt | Branch | wartet auf | EventIds |
|---|---|---|---|---|
| S | Shader-Spike (6) | `feat/m01-s-shader-spike` | – | – |
| I | iCloud-Spike (7) | `feat/m01-i-icloud-spike` | – | – |
| 0 | Modell, Schema, Ablage, Contracts (1) | `feat/m01-durchstich` | – | Infrastructure 3100–3199 |
| A | Upload, API, Suche (2) | `feat/m01-a-api` | 0 | Server 1100–1199, Infrastructure 3200–3299 |
| B | Worker-Pipeline, Debian-Image (3) | `feat/m01-b-worker` | 0 | Worker 2100–2199, Infrastructure 3300–3399 |
| C | Upload-/Listen-/Suchseiten (4) | `feat/m01-c-ui` | 0 | Web.Client 4100–4199 |
| V | Viewer mit Shader (5) | `feat/m01-v-viewer` | S, Merge A–C | Web.Client 4200–4299 |
| D | Tests-Lücken, KB, ADRs (8) | `feat/m01-durchstich` | alle | – |

- **Exklusiv-Dateien:** A → `Server/Documents/`, `Server/Search/`, `Infrastructure/Search/`;
  B → `Worker/Processing/`, `Infrastructure/Ocr/`, `Dockerfile.worker`; C → `Web.Client/Pages/`
  (Upload, Documents, Search); V → `Web.Client/Viewer/`, `wwwroot/lib/pdfjs/`, `wwwroot/js/viewer/`;
  S → `spikes/viewer-shader/`; I → `spikes/icloud/`
- **Hotspots** (nur ergänzen): `Strings*.resx`, `Directory.Packages.props`, `NoMorePaper.slnx`,
  `MainLayout.razor` (Navigation), `docker-compose.yml`, `docs/kb/telemetrie.md`
- **Merge-Reihenfolge:** A → B → C → V; S und I liefern nur Doku/ADR, der Spike-Code bleibt in `spikes/`

## Detailplan

**Erstellt von:** Opus (Stufe 3) · **Datum:** 2026-11-18 · **Offene Designfragen:** keine
(Shader-Werte und iCloud-Ergebnis sind ausdrücklich Spike-Ergebnisse)

### Einschätzung

| Aufgabenpunkt | Umfang | Risiko | Neuheit | Klarheit | Prüfbarkeit | Stufe |
|---|---|---|---|---|---|---|
| 1 Modell, Schema, Ablage | ~15 Dateien, ~600 Zeilen | mittel (Schema, atomares Schreiben) | neu | entschieden | Testcontainers | 3 |
| 2 Upload, API, Suche | ~12 Dateien, ~500 Zeilen | mittel (Upload-Sicherheit) | Standard | entschieden | Integrationstests | 2 |
| 3 Worker-Pipeline, Image | ~12 Dateien, ~600 Zeilen | hoch (externe Werkzeuge, Prozesse, Zeitlimits) | neu | entschieden | Image-Integrationstest | 3 |
| 4 UI-Seiten | ~10 Dateien, ~500 Zeilen | mittel (Barrierefreiheit) | Muster aus M0 | entschieden | axe + Tastatur | 2 |
| 5 Viewer mit Shader | ~12 Dateien, ~900 Zeilen | hoch (GPU, Hauptnutzer) | neu | nach Spike entschieden | Pixeltest + Sichtprüfung | 3 |
| 6 Shader-Spike | Prototyp | offen | neu | offen | Messung + Sicht | 4 |
| 7 iCloud-Spike | Prototyp | offen | neu, inoffiziell | offen | manuell mit Apple-ID | 4 |
| 8 Doku | ~6 Dateien | keins | Muster | entschieden | Lesen | 1 |

### Pakete

| Paket | Inhalt | Modell | Effort |
|---|---|---|---|
| P0 | Schritt 0 (Aufgabe 1) | **Opus** | `high` |
| P1 | Shader-Spike (6) | **Fable** | `high` |
| P2 | iCloud-Spike (7) | **Fable** | `high` |
| P3 | Upload, API, Suche (2) | **Sonnet** | `medium` |
| P4 | Worker-Pipeline, Debian-Image (3) | **Opus** | `high` |
| P5 | UI-Seiten (4) | **Sonnet** | `medium` |
| P6 | Viewer mit Shader (5) | **Opus** | `high` |
| P7 | KB, ADRs, Katalog (8, Doku-Teil) | **Haiku** | `low` |

Die Dateitabellen je Paket (`Datei | neu/ändern | Was genau | Tests | ca. Zeilen`) schreibt die
steuernde Sitzung **vor dem Start des jeweiligen Pakets** in diese Datei: P0, P1, P2 sofort;
P3–P5 nach P0 (sie hängen an dessen Typen und Signaturen); P6 nach P1 (hängt an den Spike-Werten).

### Paket P0 – Schritt 0 · Modell: **Opus** · Effort: `high`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Domain/Documents/Document.cs` | neu | Aggregat: `Id` (Guid v7), `Title`, `ReceivedAt`, `State`; Methoden `StartProcessing()`, `Complete(string contentText)`, `Fail(string reason)` mit erlaubten Übergängen | Domain.Tests | 80 |
| `src/NoMorePaper.Domain/Documents/DocumentFile.cs`, `DocumentFileKind.cs`, `ProcessingState.cs` | neu | `DocumentFile(Kind, FileName, MediaType, SizeBytes, Sha256)`; Enums | – | 50 |
| `src/NoMorePaper.Domain/Processing/RetryPolicy.cs` | neu | `NextAttemptAt(int attempt, DateTimeOffset now)`: 1 min, 5 min, 30 min, danach `null` (aufgeben) | Domain.Tests | 30 |
| `src/NoMorePaper.Application/Documents/IDocumentStorage.cs` | neu | `Task<StoredFile> SaveInboxAsync(Guid id, string fileName, Stream content, CancellationToken)`, `Task<StoredFile> SaveArchiveAsync(Guid id, DocumentFileKind kind, Stream content, CancellationToken)`, `Stream OpenRead(Guid id, DocumentFileKind kind)` | – | 25 |
| `src/NoMorePaper.Infrastructure/Documents/FileSystemDocumentStorage.cs` | neu | Pfade nach Aufgabe 1, Wurzel aus Konfiguration `NoMorePaper:DataPath` (Werk `/data`), SHA-256 beim Schreiben, temp + `File.Move` | Infrastructure.Tests | 120 |
| `src/NoMorePaper.Infrastructure/Documents/*Configuration.cs` | neu | EF-Mappings `documents`, `document_files` (snake_case), `search_vector` als `HasComputedColumnSql(... STORED)` + GIN-Index, Trigramm-Index `gin_trgm_ops` | Migrationstest | 90 |
| `src/NoMorePaper.Infrastructure/Processing/ProcessingJob.cs`, `ProcessingJobConfiguration.cs` | neu | Tabelle `processing_jobs`: `DocumentId`, `Attempt`, `NextAttemptAt`, `LockedUntil`, `LastError` (technisch) | – | 60 |
| `src/NoMorePaper.Infrastructure/Persistence/Migrations/*` | neu | Migration `Documents` inkl. `CREATE EXTENSION IF NOT EXISTS pg_trgm` | MigrationTests | gen. |
| `src/NoMorePaper.Contracts/Documents/*.cs` | neu | DTOs aus Aufgabe 1 als `sealed record` | – | 40 |
| `tests/NoMorePaper.Domain.Tests/Documents/DocumentTests.cs`, `Processing/RetryPolicyTests.cs` | neu | alle erlaubten/verbotenen Übergänge; Abstände | selbst | 120 |
| `tests/NoMorePaper.Infrastructure.Tests/Documents/FileSystemDocumentStorageTests.cs` | neu | Pfade, Hash, kein halbes File nach Abbruch | selbst | 90 |
- **Akzeptanz:** Build ohne Warnungen, alle Tests grün, Architekturtests grün

### Paket P1 – Shader-Spike · Modell: **Fable** · Effort: `high`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `spikes/viewer-shader/index.html`, `viewer.js`, `shader.frag` | neu | PDF.js rendert Seite in Canvas → WebGL-Textur → Fragment-Shader; Regler für Schwellen; Umschalter Original/Invertiert/Intelligent | Sicht | je < 300 |
| `spikes/viewer-shader/samples/` | neu | 5 Test-PDFs aus Aufgabe 6, **selbst erzeugt** (keine echten Dokumente) | – | – |
| `spikes/viewer-shader/README.md` | neu | Start (`npx http-server` o. ä.), Messwerte | – | 60 |
| `docs/kb/darstellung.md` | neu | Farbraum, Formeln, Foto-Kriterium, Schwellen, GPU-Zeiten, Screenshots nur von Testdokumenten | – | 120 |
- **Akzeptanz:** Sichtprüfung durch den Nutzer am Windows-PC (Chrome/Edge); GPU-Zeit je Neu-Rendern dokumentiert

### Paket P2 – iCloud-Spike · Modell: **Fable** · Effort: `high`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `spikes/icloud/ICloudSpike.csproj`, `Program.cs`, weitere Dateien je < 300 Zeilen | neu | Anmeldung (SRP-Verfahren wie in öffentlich dokumentierten Open-Source-Clients beschrieben, eigene Umsetzung), 2FA-Abfrage auf der Konsole, Drive-Ordner auflisten, Datei laden, Sitzung in Datei unter `%LOCALAPPDATA%` (außerhalb des Repos) | manuell | ~600 |
| `docs/adr/0018-icloud-spike-ergebnis.md` | neu | bestanden/nicht bestanden, Befunde, Sitzungsdauer, Folgen für M4 | – | 50 |
- **Akzeptanz:** Kriterien aus Aufgabe 7 mit der Apple-ID des Nutzers geprüft (Nutzer gibt Daten selbst ein)
- **Eskalation:** Nach 3 Tagen ohne Ergebnis abbrechen und als „nicht bestanden“ dokumentieren

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|

## Offene Fragen / Risiken
- Der iCloud-Spike braucht deine Apple-ID und einen 2FA-Code; das Programm läuft nur lokal und
  speichert nichts im Repo. Du gibst die Zugangsdaten selbst ein.
- OCR-Dauer auf CPU ⚠️ unverifiziert – Akzeptanzwert wird im Integrationstest gemessen.
- PDF.js-Lizenz Apache-2.0 (kein Copyleft) ✅
- Ohne Anmeldung darf M1 nicht über Tailscale erreichbar sein; der Port bleibt auf `127.0.0.1`.
- HTTPS (M0-Review) wird erst vor M4 gebraucht; der Viewer läuft lokal auf `localhost` (sicherer Kontext).
