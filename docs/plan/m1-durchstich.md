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

**Gemeinsamer Vertrag der Spuren A/B/C** (aus P0, Commit `8e8bf19`): Typen `Document`, `DocumentFile`,
`ProcessingState`, `RetryPolicy` (Domain), `IDocumentStorage`/`StoredFile` (Application),
`FileSystemDocumentStorage`, `ProcessingJob`, `AppDbContext.Documents/ProcessingJobs` (Infrastructure),
DTOs in `NoMorePaper.Contracts.Documents`. HTTP-Routen (A baut sie, C nutzt sie):
`POST /api/documents` (Formularfeld `file`) → 202 `UploadResultDto`; `GET /api/documents?page=1&pageSize=50`
→ `DocumentSummaryDto[]`; `GET /api/documents/{id}` → `DocumentDetailDto`;
`GET /api/documents/{id}/files/original|archive` → Datei; `GET /api/search?q=&limit=20` → `SearchHitDto[]`.
Fehler als `ProblemDetails`: 400 leer, 413 zu groß, 415 Typ nicht erlaubt, 404 unbekannt.

### Paket P3 – Upload, API, Suche (Spur A) · Modell: **Sonnet** · Effort: `medium`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Server/Documents/FileSignature.cs` | neu | `static string? DetectMediaType(ReadOnlySpan<byte> head)`: `%PDF-` → `application/pdf`, `FF D8 FF` → `image/jpeg`, `89 50 4E 47 0D 0A 1A 0A` → `image/png`, `ftyp` an Byte 4 mit Marke `heic`/`heix`/`mif1`/`heif` → `image/heic`; sonst `null` | Server.Tests | 50 |
| `src/NoMorePaper.Infrastructure/Documents/DocumentIntake.cs` | neu | `public sealed`; `Task<Guid> ReceiveAsync(string fileName, string mediaType, Stream content, CancellationToken)`: `Document.Receive(Titel = Dateiname ohne Endung, gekürzt auf 500)`, `SaveInboxAsync`, `AttachFile(Original)`, `ProcessingJob { Attempt = 0, NextAttemptAt = now }`, ein `SaveChangesAsync`; bei DB-Fehler Eingangsdatei wieder löschen | Infrastructure.Tests | 90 |
| `src/NoMorePaper.Infrastructure/Documents/DocumentQueries.cs` | neu | `ListAsync(int page, int pageSize, ct)` (neueste zuerst, `pageSize` 1–100), `GetAsync(Guid id, ct)` → DTOs | Infrastructure.Tests | 80 |
| `src/NoMorePaper.Infrastructure/Search/DocumentSearch.cs` | neu | `SearchAsync(string query, int limit, ct)`: `websearch_to_tsquery('german', q) \|\| websearch_to_tsquery('english', q)`, `ts_rank`, `ts_headline` mit `StartSel=,StopSel=` (reiner Text, max. 30 Wörter); keine Treffer → Rückfall `word_similarity(q, content_text) > 0.4` absteigend; nur `State = Done`; nur parametrisiertes SQL | Infrastructure.Tests | 110 |
| `src/NoMorePaper.Infrastructure/Composition/...ServiceCollectionExtensions.cs` | ändern | `DocumentIntake`, `DocumentQueries`, `DocumentSearch` scoped registrieren | – | 5 |
| `src/NoMorePaper.Server/Documents/DocumentEndpoints.cs` | neu | `MapDocumentEndpoints(this IEndpointRouteBuilder)`: Routen oben; Upload mit `[RequestSizeLimit(100 MB)]`/`FormOptions.MultipartBodyLengthLimit`, Kopf (16 Byte) lesen → `FileSignature`, Datei-Download mit `Results.File(stream, mediaType, fileName, enableRangeProcessing: true)` | Server.Tests | 140 |
| `src/NoMorePaper.Server/Search/SearchEndpoints.cs` | neu | `GET /api/search`; leere Anfrage → leere Liste; `limit` 1–50 | Server.Tests | 40 |
| `src/NoMorePaper.Server/Documents/DocumentsLog.cs` | neu | EventIds 1100–1119 (Upload angenommen/abgelehnt mit Grund und Größe, ohne Dateinamen) | – | 30 |
| `src/NoMorePaper.Server/Program.cs`, `Composition/ServerComposition.cs` | ändern | Endpunkte mappen, `AddProblemDetails()` | – | 10 |
| `tests/NoMorePaper.Server.Tests/` (neues Projekt, in `.slnx`) | neu | `WebApplicationFactory<Program>` + Testcontainers-Postgres (wie `UI.Tests`-Fixture), `NoMorePaper__DataPath` auf Temp-Ordner; Tests: Upload PDF/JPG/PNG/HEIC → 202, Text-Datei → 415, 101 MB → 413, Download unverändert (gleicher SHA-256), Liste, Detail, 404, Suche leer | selbst | 250 |
| `tests/NoMorePaper.Infrastructure.Tests/Search/DocumentSearchTests.cs` | neu | „Rechnungen“ findet „Rechnung“, englischer Text, Tippfehler „Rechnnug“ über Trigramme, `Queued` wird nicht gefunden | selbst | 120 |
- **Akzeptanz:** alle Tests grün, Architekturtests grün (Server kennt nur Infrastructure/Contracts)

### Paket P4 – Worker-Pipeline, Debian-Image (Spur B) · Modell: **Opus** · Effort: `high`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Application/Processing/IOcrEngine.cs`, `OcrResult.cs` | neu | `Task<OcrResult> CreateArchiveAsync(string inputPath, string mediaType, string outputPdfPath, CancellationToken)`; `OcrResult(string Text, int PageCount)` | – | 25 |
| `src/NoMorePaper.Infrastructure/Ocr/ExternalProcess.cs` | neu | `RunAsync(string fileName, IReadOnlyList<string> args, TimeSpan timeout, ct)` → `(ExitCode, StdErrTail)`; nur `ArgumentList`, kein Shell; bei Zeitüberschreitung `Kill(entireProcessTree: true)` | Infrastructure.Tests | 80 |
| `src/NoMorePaper.Infrastructure/Ocr/OcrMyPdfEngine.cs` | neu | Bilder: HEIC → `heif-convert` → JPG; JPG/PNG → `img2pdf`; dann `ocrmypdf --output-type pdfa --language deu+eng --rotate-pages --deskew --skip-text --jobs 2`; Text mit `pdftotext -enc UTF-8`; Zeitlimit 10 min; Text auf 500 000 Zeichen kürzen (tsvector-Grenze 1 MB) | Infrastructure.Tests (OCR) | 150 |
| `src/NoMorePaper.Infrastructure/Processing/JobQueue.cs` | neu | `ClaimNextAsync(TimeSpan lockFor, ct)` per SQL `... WHERE next_attempt_at <= now AND (locked_until IS NULL OR locked_until < now) ORDER BY next_attempt_at LIMIT 1 FOR UPDATE SKIP LOCKED` + `locked_until` setzen, `Attempt++`; `CompleteAsync(Guid id, string text, StoredFile archive, ct)` (Dokument `Complete`, Archiv-Datei anhängen, Job löschen); `FailAsync(Guid id, string technicalError, DateTimeOffset now, ct)` mit `RetryPolicy` (Fehlertext auf 1000 Zeichen kürzen; nach `MaxAttempts` → `Document.Fail`, Job löschen) | Infrastructure.Tests | 160 |
| `src/NoMorePaper.Worker/Processing/DocumentProcessor.cs` | neu | `BackgroundService`: alle 5 s Job holen; Ablauf: `StartProcessing` → Original aus Eingang lesen → `IOcrEngine` in Temp-Ordner → `SaveArchiveAsync(Archive)` → `ArchiveOriginalAsync` → `CompleteAsync`; Fehler → `FailAsync` mit Ausnahmetyp + Exit-Code (kein Inhalt); Temp-Ordner immer löschen | Infrastructure.Tests | 140 |
| `src/NoMorePaper.Worker/Processing/ProcessingLog.cs` | neu | EventIds 2100–2119; Activity `nomorepaper.processing.document`; Zähler `nomorepaper.processing.completed/failed`, Histogramm Dauer | – | 40 |
| `src/NoMorePaper.Worker/Composition/WorkerComposition.cs` | ändern | `DocumentProcessor`, `IOcrEngine` registrieren | – | 5 |
| `Dockerfile.worker` | ändern | Laufzeit `mcr.microsoft.com/dotnet/aspnet:10.0` (Debian) + `apt-get install --no-install-recommends ocrmypdf tesseract-ocr-deu tesseract-ocr-eng img2pdf libheif-examples poppler-utils curl`; Healthcheck auf `curl` umstellen (`docker-compose.yml` Hotspot) | compose | 30 |
| `.github/workflows/ci.yml` | ändern | vor „Test“: `sudo apt-get install -y ocrmypdf tesseract-ocr-deu img2pdf libheif-examples poppler-utils` | CI | 5 |
| `tests/NoMorePaper.Infrastructure.Tests/Ocr/*`, `Processing/JobQueueTests.cs` | neu | Queue: zwei parallele Claims bekommen nie denselben Job, Wiederholung nach Fehler, Aufgeben nach 4; OCR (übersprungen mit `Assert.Skip`, wenn `ocrmypdf` fehlt): selbst erzeugte PDF und PNG mit bekanntem Text → Text gefunden, Ausgabe ist PDF/A | selbst | 250 |
- **Akzeptanz:** Tests grün (lokal ohne OCR-Werkzeuge werden OCR-Tests übersprungen, in CI laufen sie); `docker compose up` → hochgeladene PDF wird `Done`

### Paket P5 – Upload-, Listen- und Suchseiten (Spur C) · Modell: **Sonnet** · Effort: `medium`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Web.Client/Documents/DocumentApi.cs` | neu | typisierter `HttpClient`: `UploadAsync(IBrowserFile, ct)` (max. 100 MB), `ListAsync`, `GetAsync`, `SearchAsync`; `ProblemDetails` → verständliche Meldung (lokalisiert) | UI.Tests | 120 |
| `src/NoMorePaper.Web.Client/Accessibility/LiveRegion.razor` | neu | `role="status"` bzw. `alert`, Methode `Announce(string)` (Text leeren, dann setzen, damit gleiche Meldungen wiederholt angesagt werden) | UI.Tests | 40 |
| `src/NoMorePaper.Web.Client/Pages/Upload.razor` | neu | `/hochladen`: `<InputFile>` mit `<label>`, erlaubte Typen im Hinweistext, Ablegen-Fläche nur zusätzlich, Ergebnis über `LiveRegion`, Link zum Dokument | UI.Tests | 120 |
| `src/NoMorePaper.Web.Client/Pages/Documents.razor` | neu | `/dokumente`: Tabelle (Titel, Eingang, Status als Text + Symbol), aktualisiert alle 3 s, solange etwas `Queued`/`Processing` ist; Statuswechsel per `LiveRegion` ansagen | UI.Tests | 130 |
| `src/NoMorePaper.Web.Client/Pages/Search.razor` | neu | `/suche?q=`: Suchfeld mit Label, Treffer als `<h2>`-Links mit Ausschnitt, „n Treffer“ per `LiveRegion` | UI.Tests | 110 |
| `src/NoMorePaper.Web.Client/Layout/MainLayout.razor`, `wwwroot/js/shortcuts.js` | ändern/neu | Navigation „Hochladen“, „Dokumente“, „Suche“; Taste `/` fokussiert die Suche (nicht in Eingabefeldern) | UI.Tests | 30 |
| `Localization/Strings*.resx` | ändern | alle neuen Texte DE/EN | – | 60 |
| `tests/NoMorePaper.UI.Tests/Documents/*`, `Search/*` | neu | axe 0 Verstöße auf allen neuen Seiten DE/EN; Upload per Tastatur (`SetInputFilesAsync`) → Ansage + Eintrag „In Warteschlange“; `/` fokussiert Suche; Suche ohne Treffer sagt „0 Treffer“ an | selbst | 250 |
- **Akzeptanz:** UI-Tests grün; keine Information nur über Farbe

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

### Paket P6 – Viewer mit Shader (Spur V) · Modell: **Opus** · Effort: `high`
Grundlage: [darstellung.md](../kb/darstellung.md) (Formeln, Werkswerte, Abschnitt „Empfehlungen für
den Viewer (P6)“), Prototyp `spikes/viewer-shader/` (vom Nutzer am 2026-11-18 mit „passt so“
abgenommen), PDF.js **6.5.438** wie im Spike.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Web.Client/wwwroot/lib/pdfjs/` | neu | `pdf.min.mjs`, `pdf.worker.min.mjs`, `cmaps/`, `standard_fonts/`, `wasm/`, `LICENSE` (6.5.438, aus dem npm-Paket `pdfjs-dist`) | – | – |
| `wwwroot/js/viewer/*.js` (+ Shader als `.js`-Module mit Template-Strings) | neu | Prototyp-Module übernehmen und produktiv machen: `shader-renderer.js` (ein WebGL-Kontext für Viewer und Vorschaubilder), `analysis.js` (Analyse 144 dpi, Maske, Fotorahmen aus `imageCoordinates`), `page-view.js` (nur sichtbarer Ausschnitt mit Rand, `renderTask.cancel()`, Geste skaliert Textur, nach 100 ms scharf), `text-layer.js` (PDF.js-`TextLayer` über die ganze Seite), `fallback.js` (CSS `invert(1) hue-rotate(180deg)` ohne WebGL), `viewer.js` (Export `createViewer(element, url, options)` → Objekt mit `zoomIn/zoomOut/reset/fitWidth/setPage/setOriginalColors/dispose` und Rückruf an .NET für Zoom/Seite/„kein Text“); Werkswerte aus `darstellung.md`, Regler entfallen | UI.Tests | je < 300 |
| `src/NoMorePaper.Web.Client/Viewer/ViewerInterop.cs` | neu | Dienst (§2.6) mit `IJSObjectReference`, `IAsyncDisposable`, `[JSInvokable]`-Rückrufe | – | 120 |
| `Viewer/ViewerPreferences.cs` | neu | Schalter „Originalfarben“ je Gerät in `localStorage` (Muster `LocalStorageCulturePreference`) | – | 50 |
| `Pages/DocumentViewer.razor` (+ `.razor.cs` falls nötig) | neu | `/dokumente/{id:guid}`: Titel als H1, Status; zeigt `archive` wenn vorhanden, sonst `original` falls PDF, sonst „wird verarbeitet“; Werkzeugleiste: Verkleinern, Vergrößern, 100 %, Breite anpassen, Originalfarben (`aria-pressed`), „Seite x von y“, Downloads Original/Archiv; Tasten `Strg`+`+`/`-`/`0`, `Strg`+Mausrad, Pfeile, `Bild auf/ab`; Zoom 25–1600 %; Zoomstufe/Seite/„kein Text“ über `LiveRegion` | UI.Tests | 200 |
| `Viewer/Thumbnail.razor` | neu | Vorschaubild über denselben Shader, erst wenn sichtbar (`IntersectionObserver`), `alt` = Titel; in Dokumentliste und Suchtreffern | UI.Tests | 80 |
| `Documents/DocumentLinks.cs`, `Pages/Documents.razor`, `Pages/Search.razor` | ändern | Links auf `dokumente/{id}`; Vorschaubild einbinden | UI.Tests | 20 |
| `Strings*.resx`, `app.css` | ändern | Texte DE/EN, Werkzeugleiste im Kontrast-Theme | – | 60 |
| `tests/NoMorePaper.UI.Tests/Viewer/*` | neu | API-Antworten per Playwright-Route vortäuschen (Muster `ApiMocks`), Beispiel-PDF aus `spikes/viewer-shader/samples/`; axe 0 Verstöße DE/EN; Tastatur (Zoom bis 1600 % und zurück, Seitenwechsel, Ansage); **Pixeltest**: Weiß → `#000000`, Rot `#c62828` → ≈ `#fe7368` (Toleranz 6 je Kanal), Pixel im Fotorahmen unverändert; Rückfall ohne WebGL setzt den CSS-Filter; Download-Links zeigen auf die Original-Routen | selbst | 280 |
- **Telemetrie:** Web.Client 4200–4299 (Viewer geöffnet mit Seitenzahl, WebGL-Rückfall, Renderdauer gerundet)
- **CI:** Headless Chromium braucht für WebGL ggf. `--use-angle=swiftshader`/`--enable-unsafe-swiftshader`;
  wenn nötig in `ServerFixture` setzen und in `build-und-tooling.md` notieren
- **Akzeptanz:** alle Tests grün, Pixeltest grün auf Windows und in CI; Sichtprüfung durch den Nutzer

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
