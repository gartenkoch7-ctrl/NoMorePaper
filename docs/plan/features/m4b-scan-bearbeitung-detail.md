# M4b – Detailplan „Scan-Bearbeitung“

Gehört zu [m4b-scan-bearbeitung.md](../m4b-scan-bearbeitung.md). Regeln: [AGENTS.md §12](../../../AGENTS.md).

**Erstellt von:** Opus (Stufe 3) · **Datum:** 2026-11-19 · **Offene Designfragen:** keine (Grilling 2026-11-19)

## Einschätzung

| Aufgabenpunkt | Umfang | Risiko | Neuheit | Klarheit | Prüfbarkeit | Stufe |
|---|---|---|---|---|---|---|
| 0 Gemeinsame Bausteine | ~60 Dateien, ~2 500 Zeilen | Migration, Papierkorb-Filter in **jeder** Abfrage (Datenschutz), Speicher | teils Muster | entschieden | Unit + Postgres | 3 |
| A Bearbeitung im Worker | ~25 Dateien, ~1 800 Zeilen | Datenverlust (Dateien tauschen), Sperren über zwei Dokumente, externe Werkzeuge | neu (qpdf) | entschieden | Integration (Linux) | 3 |
| B Oberfläche | ~25 Dateien, ~2 000 Zeilen | Interop (PDF.js, OpenCV), Barrierefreiheit | Muster (Viewer, Kamera) | entschieden | UI-Tests + Sichtprüfung | 3 |
| C Papierkorb | ~15 Dateien, ~1 000 Zeilen | endgültiges Löschen, Uhr | Muster (InputScheduler) | entschieden | Unit + Postgres + UI | 2 (Aufräumdienst 3) → Opus |
| D Stapel-Trennung | ~15 Dateien, ~1 100 Zeilen | LLM-Qualität | Muster (Mail-Prüfung) | entschieden | Testsatz + Messung | 3 |

Lehre aus M3/M4 umgesetzt: **Papierkorb-Filter, Sperrreihenfolge und Privat-Regel stehen in Schritt 0 an
einer Stelle**; das Gesamt-Review ist fest eingeplant.

## Telemetrie-Blöcke

| Block | Projekt | Feature |
|---|---|---|
| 1600–1699 | Server | Bearbeiten, Anhängen, Teilen, Fassungen |
| 1700–1799 | Server | Papierkorb |
| 2400–2499 | Worker | Stufe `Edit` (Bearbeitung, Anhängen, Teilen) |
| 2500–2599 | Worker | Aufräumdienst Papierkorb |
| 2600–2699 | Worker | Stufe `Separation` |
| 3950–3999 | Infrastructure | qpdf, Seitenanalyse, Papierkorb-Ablage |
| 4300–4399 | Web.Client | Seiten bearbeiten, Fassungen |
| 4800–4899 | Web.Client | Papierkorb |
| 4900–4949 | Web.Client | Trennvorschlag in „Zu prüfen“ |
| 5500–5599 | Application | Bearbeitung, Anhängen, Teilen |
| 5600–5699 | Application | Papierkorb |
| 5700–5799 | Application | Stapel-Trennung |

Nur neutrale Platzhalter (`{Count}`, `{Pages}`, `{ElapsedMs}`, `{Reason}`, Dokument-Ids); nie Titel, Dateinamen,
Seitentexte.

---

## Paket P0 – Gemeinsame Bausteine · Modell: **Opus** · Effort: `high`

**Domain**

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Domain/Documents/DocumentFileKind.cs` | ändern | `Edited` ergänzen | – | 2 |
| `src/NoMorePaper.Domain/Documents/Document.Trash.cs` | neu | partial: `DeletedAt?`, `DeletedBy?`; `MoveToTrash(Guid actor, DateTimeOffset at)` (nur Besitzer), `RestoreFromTrash(Guid actor)` (nur Besitzer); `IsInTrash`; `IsVisibleTo`/`CanEdit` liefern `false` im Papierkorb | Domain.Tests | 80 |
| `src/NoMorePaper.Domain/Documents/Document.Files.cs` | neu | `ReplaceFile(DocumentFile)` (nur `Edited`/`Archive`), `RemoveFile(kind)` (nur wenn danach eine Quelle bleibt: Original **oder** Edited), `CurrentSource` (= Edited ?? Original). Bisherige Datei-Methoden aus `Document.cs` hierher, damit `Document.cs` < 250 bleibt | Domain.Tests | 90 |
| `src/NoMorePaper.Domain/Documents/VersionPolicy.cs` | neu | `enum VersionPolicy { KeepBoth, EditedOnly, OriginalOnly }` | – | 10 |
| `src/NoMorePaper.Domain/Editing/PagePlan.cs`, `PageStep.cs`, `PageRotation.cs` | neu | `PagePlan(IReadOnlyList<PageStep>)`: Reihenfolge der Quellseiten (1-basiert), je `PageStep(int SourcePage, PageRotation Rotation, bool Replaced)`; `Validate(int sourcePageCount)` (jede Seite höchstens einmal, mindestens eine, Drehung 0/90/180/270) | Domain.Tests | 3 × 50 |
| `src/NoMorePaper.Domain/Editing/SplitPlan.cs` | neu | Liste von Seitenbereichen (lückenlos oder mit ausgelassenen leeren Seiten), mind. 2 Teile; `Validate(pageCount)` | Domain.Tests | 60 |
| `src/NoMorePaper.Domain/Editing/SplitSuggestion.cs` | neu | Vorschlag: Teile (Seitenbereiche), vermutlich leere Seiten, Gründe je Grenze (`Blank`, `PageCounter`, `Salutation`, `SenderChange`) | Domain.Tests | 60 |
| `src/NoMorePaper.Domain/Editing/AppendRule.cs` | neu | rein: `bool ResultIsPrivate(Document target, Document appended)` (privat, wenn eines privat ist) und Vorbedingungen (beide fertig, beide bearbeitbar, nicht dasselbe, keins im Papierkorb) | Domain.Tests | 50 |

**Application**

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Application/Documents/IDocumentStorage.cs` + `FileSystemDocumentStorage` | ändern | `SaveEditedAsync(Guid id, Stream, ct)` → `edited.pdf` im Archivordner (atomar); `MoveToTrashAsync(Guid id, DocumentFileKind kind, Guid trashItemId, ct)`; `RestoreFromTrashAsync(Guid trashItemId, Guid id, DocumentFileKind kind, ct)`; `PurgeTrash(Guid trashItemId)`; `OpenRead(id, Edited)`; `SaveEditPageAsync(Guid id, int page, Stream)` / `OpenEditPage` / `DiscardEditPages(Guid id)` (Ordner `edits/<id>/`). `FileSystemDocumentStorage` ist 161 Zeilen → Papierkorb-Teil in `FileSystemDocumentStorage.Trash.cs`, Bearbeitungsseiten in `EditPageStorage.cs` (eigener Port `IEditPageStorage`) | Infrastructure.Tests | 150 |
| `src/NoMorePaper.Application/Trash/TrashItem.cs`, `TrashItemKind.cs`, `ITrashStore.cs` | neu | `TrashItem(Guid Id, Guid OwnerId, TrashItemKind Kind, Guid DocumentId, DocumentFileKind? FileKind, DocumentFile? File, string Title, DateTimeOffset DeletedAt, DateTimeOffset PurgeAfter)`; `ITrashStore`: `AddAsync`, `ListAsync(Guid ownerId)`, `FindAsync(Guid id, Guid ownerId)`, `RemoveAsync`, `DueAsync(DateTimeOffset now, int max)` | – | 3 × 25 |
| `src/NoMorePaper.Application/Trash/TrashRules.cs` | neu | rein: `Retention = 30 Tage`; `PurgeAfter(at)` | Unit | 20 |
| `src/NoMorePaper.Application/Trash/MoveDocumentToTrash.cs` | neu | Besitzer, Zeilensperre (`DocumentChangeGuard`), `Document.MoveToTrash`, `TrashItem(Document)`; ersetzt das harte Löschen in `DeleteDuplicate` (das ruft diesen Fall auf). Dateien bleiben am Platz, bis sie endgültig gelöscht werden | Application.Tests | 90 |
| `src/NoMorePaper.Application/Editing/IPdfTool.cs` | neu | `ComposeAsync(IReadOnlyList<PdfPageSource> pages, string outputPath, ct)` (Quelle = Datei + Seite + Drehung); `CountPagesAsync(path)`; `ExtractAsync(path, IReadOnlyList<int> pages, outputPath)` | – | 30 |
| `src/NoMorePaper.Application/Editing/PdfPageSource.cs` | neu | `record PdfPageSource(string Path, int Page, PageRotation Rotation)` | – | 10 |
| `src/NoMorePaper.Application/Editing/IPageAnalyzer.cs`, `PageInfo.cs` | neu | `AnalyzeAsync(string pdfPath, ct)` → `IReadOnlyList<PageInfo(int Page, string Head, bool ProbablyBlank, bool HadTextLayer)>` | – | 2 × 20 |
| `src/NoMorePaper.Application/Editing/IDocumentPages.cs` | neu | `ReplaceAsync(Guid documentId, IReadOnlyList<PageInfo>, ct)`, `ListAsync(Guid documentId, ct)` | – | 20 |
| `src/NoMorePaper.Application/Editing/IDocumentEdits.cs`, `DocumentEdit.cs`, `EditKind.cs` | neu | geplanter Auftrag je Dokument: `EditKind { Pages, Append, Split }`, Daten als JSON (PagePlan / angehängte Id / SplitPlan), `RequestedBy`, `RequestedAt`; `SaveAsync`, `GetAsync`, `DeleteAsync` | – | 3 × 25 |
| `src/NoMorePaper.Application/Processing/JobStage.cs`, `IJobQueue.cs` | ändern | Stufen `Edit`, `Separation`; `EnqueueEditAsync(Guid documentId, ct)` (in der offenen Transaktion, Priorität 0, nur wenn kein Auftrag läuft → sonst `false`); `EnqueueSeparationAsync` | Infrastructure.Tests | 20 |
| `src/NoMorePaper.Application/Processing/ProcessNextDocument.cs` | ändern | Weiche je Stufe auf `IStageRunner`-Dienste (keyed nach `JobStage`), damit die Spuren ihre Stufe ohne Konflikt einhängen; OCR/Recognition werden zu Runnern | Application.Tests (bestehende grün) | 40 |
| `src/NoMorePaper.Application/Processing/IStageRunner.cs` | neu | `Task RunAsync(IJobQueue queue, JobClaim claim, CancellationToken)` | – | 15 |
| `src/NoMorePaper.Application/Processing/OcrStage.cs` / `ProcessingAttempt.cs` | ändern | OCR liest `CurrentSource`; nach der OCR `IPageAnalyzer` auf das Archiv → `IDocumentPages.ReplaceAsync`; `JobQueue.CompleteAsync` ersetzt ein vorhandenes Archiv (`ReplaceFile`) statt zu werfen | Worker.Tests | 40 |
| `src/NoMorePaper.Application/Accounts/AccountPreferences.cs` + Store/DTO/Endpunkt | ändern | `VersionPolicy AfterEditing` (Werk `KeepBoth`) | Application/Server.Tests | 30 |
| `src/NoMorePaper.Application/Documents/DocumentDetail.cs`, `OpenDocumentFile.cs` | ändern | `Edited?`, `VersionPolicy`, `EditPending`, `PageCount`; Datei-Art `edited` lesbar | Application.Tests | 30 |

**Infrastructure / Server / Worker / Contracts**

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Infrastructure/Documents/DocumentConfiguration.cs` | ändern | `deleted_at`, `deleted_by`, `version_policy` (string 16, null = Konto-Werk), `split_suggestion` (jsonb, null); **globaler Abfragefilter** `DeletedAt == null` auf `Document` | MigrationTests | 30 |
| `src/NoMorePaper.Infrastructure/Persistence/TrashSql.cs` | neu | eine Konstante `NotInTrash = "deleted_at IS NULL"` (mit Alias-Variante) für **jede** rohe Abfrage; Architekturtest: jede `.sql`/raw-Abfrage über `documents` enthält sie (Test sucht `FROM documents`/`JOIN documents` in Infrastructure-Quellen) | Architecture.Tests | 30 |
| Rohe Abfragen | ändern | `PostgresSearchIndex`, `PostgresDuplicateFinder`, `EfDocumentRepository.FindVisibleBySha256Async/FindOwnedBySha256Async`, `VisibilitySql`/`EntrySets` (Eintrag sichtbar „in sichtbarem Dokument“), `EfRecognitionExamples`, `DocumentFilterSql`, `JobQueue`-Claim (kein Auftrag für Dokumente im Papierkorb) | Infrastructure.Tests: je Abfrage ein Fall „im Papierkorb → unsichtbar“ | 40 |
| `src/NoMorePaper.Infrastructure/Trash/*` | neu | `trash_items` (id, owner_id FK users cascade, kind, document_id FK documents cascade, file_kind, file jsonb, title, deleted_at, purge_after; Index `(owner_id, deleted_at)`, `(purge_after)`), `EfTrashStore` | Infrastructure.Tests | 2 × 80 |
| `src/NoMorePaper.Infrastructure/Editing/*` | neu | `document_pages(document_id FK cascade, page, head varchar(300), probably_blank, had_text_layer)` PK `(document_id, page)`; `document_edits(document_id PK FK cascade, kind, data jsonb, requested_by, requested_at)`; `EfDocumentPages`, `EfDocumentEdits` | Infrastructure.Tests | 4 × 60 |
| `src/NoMorePaper.Infrastructure/Editing/QpdfTool.cs`, `PopplerPageAnalyzer.cs` | neu | qpdf über `ExternalProcess` (`--empty --pages … --` und `--rotate`), Zeitlimit 2 min; Seitenanalyse: `pdftotext -f n -l n` je Seite (Kopf 300 Zeichen), leer = < 15 Zeichen Text **und** Tintenanteil < 0,5 % (`pdftoppm -r 30 -gray`, Pixel zählen); `HadTextLayer` über `pdffonts` des Originals | Infrastructure.Tests (nur Linux, sonst übersprungen wie OCR) | 2 × 130 |
| `src/NoMorePaper.Infrastructure/Persistence/Migrations/<ts>_ScanEditing.cs` | neu | `dotnet ef migrations add ScanEditing`; PK von `document_files` bleibt `(document_id, kind)` | MigrationTests | gen. |
| `Dockerfile.worker` | ändern | `qpdf` (Apache-2.0) | Compose-Build | 1 |
| `src/NoMorePaper.Server/Documents/DocumentEndpoints.cs`, `DocumentMapping.cs` | ändern | Datei-Art `edited`; `DocumentDetailDto` mit `Edited`, `VersionPolicy`, `EditPending`, `PageCount`; Download-URLs mit `?v=<sha256-Anfang>` (Vorschau-Cache) | Server.Tests | 30 |
| `src/NoMorePaper.Contracts/Editing/*`, `Trash/*` | neu | `PagePlanDto`, `PageStepDto`, `PageInfoDto`, `AppendRequestDto`, `SplitPlanDto`, `SplitSuggestionDto`, `VersionChoiceDto`, `TrashItemDto` – je Datei ein Typ | – | 9 × 15 |
| `src/NoMorePaper.Contracts/Classification/ReviewItemDto.cs` | ändern | `SplitSuggestionDto? Split` | – | 3 |
| `src/NoMorePaper.Web.Client/Editing/EditingApi.cs`, `Trash/TrashApi.cs` | neu | Clients (Endpunkte der Spuren A und C, Signaturen hier festgelegt: `GET /api/documents/{id}/pages`, `POST …/edit` (multipart: Plan + ersetzte Seiten), `POST …/append`, `POST …/split`, `PUT …/versions`; `GET /api/trash`, `POST /api/trash/{id}/restore`, `DELETE /api/trash/{id}`, `DELETE /api/trash`) | – | 2 × 90 |
| `docs/adr/0025-scan-bearbeitung-umsetzung.md`, `docs/kb/scan-bearbeitung.md`, Katalog | neu/ändern | – | – | – |

- **Reihenfolge:** Domain → Ports → Schema/Migration → Papierkorb-Filter in allen Abfragen (mit Architekturtest) → Speicher → `IStageRunner`-Weiche → OCR schreibt Seiten → Löschen in den Papierkorb → Einstellung → Contracts/Clients.
- **Akzeptanz:** Build ohne Warnungen; alle bisherigen Tests grün; neue Tests: jede Abfrage blendet Papierkorb-Dokumente aus (inkl. Listeneinträge und Lernbeispiele), Löschen eines Duplikats legt einen Papierkorb-Eintrag an statt zu löschen, OCR ersetzt ein vorhandenes Archiv, Seitentexte nach OCR vorhanden, `EnqueueEditAsync` liefert `false` bei laufendem Auftrag.

---

## Paket P1 – Bearbeitung im Worker (Spur A) · Modell: **Opus** · Effort: `high`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Application/Editing/RequestPageEdit.cs` | neu | `CanEdit`, fertig verarbeitet, kein Auftrag offen (sonst `Conflict`); `PagePlan.Validate(pageCount)`; ersetzte Seiten (JPEG, `FileSignature`, ≤ 15 MB, ≤ 50) über `IEditPageStorage`; `IDocumentEdits.SaveAsync` + `EnqueueEditAsync` in der Transaktion der Zeilensperre | Application.Tests | 140 |
| `src/NoMorePaper.Application/Editing/AppendDocument.cs` | neu | beide Dokumente sperren (**kleinere Id zuerst**, neue Methode `IDocumentFieldsStore.FindVisiblePairAsync`), `AppendRule`; Ergebnis privat → `MakePrivate` sofort (Ansage vorher im Client); Auftrag `Append` | Application.Tests + Postgres (Sperrreihenfolge) | 140 |
| `src/NoMorePaper.Application/Editing/SplitDocument.cs` | neu | `CanEdit`, `SplitPlan.Validate`; Auftrag `Split` | Application.Tests | 100 |
| `src/NoMorePaper.Application/Editing/ChooseVersions.cs` | neu | je Dokument `VersionPolicy`; `EditedOnly` → Original in den Papierkorb (nur Besitzer, weil „Original entfernen“), `OriginalOnly` → Fassung in den Papierkorb + OCR-Auftrag auf das Original | Application.Tests | 120 |
| `src/NoMorePaper.Application/Editing/EditStageRunner.cs` | neu | `IStageRunner` für `Edit`: liest `DocumentEdit`; Arbeitsordner; Pages: Quelle (`CurrentSource`) + Plan (ersetzte Seiten: JPEG → `IPdfComposer.ComposeImagesAsync` je Seite) → `IPdfTool.ComposeAsync` → OCR (`IOcrEngine`) → `SaveEditedAsync` + `SaveArchiveAsync` → Versionsregel (alte Fassung bzw. Original in den Papierkorb) → Seiten neu → Auftrag weg, Bearbeitungsseiten verwerfen; Fehler → `FailAsync` mit `RetryPolicy`, nach Aufgeben bleibt die alte Fassung | Worker.Tests (Fakes) + Infrastructure.Tests (Linux, echt) | 200 |
| `src/NoMorePaper.Application/Editing/AppendStep.cs`, `SplitStep.cs` | neu | Append: Quelle Ziel + Quelle angehängtes → Compose → wie Pages; angehängtes in den Papierkorb (in derselben Commit-Transaktion). Split: je Teil `ExtractAsync` → neues Dokument über `IntakeDocument` (Besitzer, Privat, Quelle wie Ausgang; Bearbeiter danach kopiert) → normale OCR + Erkennung; Ausgangsdokument in den Papierkorb erst nach allen Teilen | Application.Tests | 2 × 150 |
| `src/NoMorePaper.Application/Editing/EditingLog.cs` | neu | 5500–5512 | – | 60 |
| `src/NoMorePaper.Server/Editing/EditingEndpoints.cs` | neu | Endpunkte aus P0; multipart für `edit`; Antworten 202/400/403/404/409; Logs 1600–1610 | Server.Tests | 200 |
| `src/NoMorePaper.Worker/Composition/WorkerComposition.cs` | ändern | `EditStageRunner` keyed `JobStage.Edit` | – | 2 |
| Tests | neu | Domain: Plan-Fälle; Application: Rechte, 409, Privat-Regel, Sperrreihenfolge; Infrastructure (Linux): echte qpdf-Abläufe mit 3-Seiten-PDF (drehen, löschen, umsortieren, ersetzte Seite, anhängen, teilen) inkl. Seitenzahl und Text; Datenverlust: Abbruch zwischen Speichern und Commit lässt die alte Fassung gültig | selbst | ~800 |

- **Telemetrie:** 1600–1610, 2400–2410, 5500–5512.
- **Akzeptanz:** alle Tests grün; Integrationstests mit qpdf/ocrmypdf in der CI (Linux).

---

## Paket P2 – Oberfläche „Seiten bearbeiten“ und Fassungen (Spur B) · Modell: **Opus** · Effort: `high`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Web.Client/Pages/PageEditor.razor` + `.razor.cs` + `PageEditor.Actions.cs` | neu | `@page "/dokumente/{Id:guid}/seiten"`; Liste `<ol>` der Seiten, je Eintrag Knopf mit Ansage „Seite 3 von 6, gedreht um 90 Grad, beginnt mit: …, vermutlich leer“; Kürzel auf dem fokussierten Eintrag: `R` drehen, `Entf` löschen (rückgängig mit `Strg+Z`), `Alt+↑/↓` verschieben, `Z` zuschneiden, `T` Trennstelle; Vorschau der fokussierten Seite (Shader); „Speichern“, „Verwerfen“ (Bestätigung bei Änderungen); Live-Region für jede Aktion | UI.Tests | 3 × 200 |
| `src/NoMorePaper.Web.Client/Editing/PagePlanModel.cs` | neu | Zustand im Client (Reihenfolge, Drehung, Löschung, ersetzte Seite, Trennstellen, Rückgängig-Stapel) → `PagePlanDto`/`SplitPlanDto` | `tests/NoMorePaper.Web.Client.Tests/Editing/*` | 180 |
| `wwwroot/js/viewer/thumbnails.js`, `Viewer/Thumbnail.razor`, `ThumbnailRenderer.cs` | ändern | Parameter `page` (Cache-Schlüssel mit Seite) | UI.Tests | 20 |
| `wwwroot/js/editing/crop.js` | neu | Seite mit PDF.js auf 200 dpi rendern, `findQuad` (aus `camera/edges.js`) + Schräglage (`minAreaRect`) → Ecken; `warp` (aus `camera/capture.js` exportieren) → JPEG mit JFIF-Kopf; Mats freigeben | UI.Tests (Standbild) | 180 |
| `src/NoMorePaper.Web.Client/Editing/CropDialog.razor` + `CropInterop.cs` | neu | Ansage des Ergebnisses („um 3 Grad gedreht, Rand oben 2 cm entfernt“); Ecke 1–4 wählen, Pfeiltasten verschieben (Schritt 1 %, mit Umschalt 0,2 %), jede Bewegung angesagt; Maus ziehen zusätzlich; Übernehmen/Abbrechen | UI.Tests | 200 + 100 |
| `src/NoMorePaper.Web.Client/Editing/AppendDialog.razor` | neu | Dokument suchen (vorhandene Suche), Auswahl, Hinweis bei Privat (aus `IsPrivate` beider), Bestätigen | UI.Tests | 160 |
| `src/NoMorePaper.Web.Client/Editing/VersionsSection.razor` | neu | im Viewer: Fassungen (Original, bearbeitet) mit Datum, Download, Wahl „behalten“ (Besitzer: alle drei; Bearbeiter: nur ansehen), „wird bearbeitet“-Hinweis; Knopf „Seiten bearbeiten“ | UI.Tests | 160 |
| `src/NoMorePaper.Web.Client/Pages/DocumentViewer.razor` | ändern (Hotspot) | eine Zeile `<VersionsSection …/>`; Quelle = Archiv (wie bisher) | – | 2 |
| `src/NoMorePaper.Web.Client/Accounts/PreferencesSection.razor` | ändern | „Nach dem Bearbeiten behalten“ (Auswahlgruppe) | UI.Tests | 40 |
| Texte, Navigation | ändern | DE/EN | – | 120 |
| `tests/NoMorePaper.UI.Tests/Editing/*` | neu | axe DE/EN; alle Kürzel; Ansagen; Speichern sendet richtigen Plan (Mock); Zuschnitt mit Test-PDF eines schiefen Blatts; Anhängen mit Privat-Hinweis; Fassungen | selbst | ~900 |

- **Telemetrie:** 4300–4312 (Seite geladen: Seitenzahl; gespeichert: Anzahl Schritte; Zuschnitt: ms, gefunden ja/nein; Fehler-Code).
- **Akzeptanz:** UI-Tests grün; Sichtprüfung mit NVDA durch den Nutzer (Liste in `scan-bearbeitung.md`).

---

## Paket P3 – Papierkorb (Spur C) · Modell: **Opus** · Effort: `high`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Application/Trash/ListTrash.cs`, `RestoreTrashItem.cs`, `PurgeTrashItem.cs`, `EmptyTrash.cs` | neu | nur Besitzer; Wiederherstellen: Dokument → `RestoreFromTrash`; Datei → zurück an ihren Platz, ist der Platz belegt (z. B. neuere Fassung), wandert die vorhandene in den Papierkorb (Tausch) und das Archiv wird per OCR-Auftrag neu gebaut; endgültig: Zeile + Dateien | Application.Tests | 4 × 90 |
| `src/NoMorePaper.Application/Trash/PurgeDueTrash.cs` | neu | `purge_after < now`, höchstens 200 je Lauf; Dokument: Zeile löschen (Kaskaden) + `storage.Delete`; Datei: `PurgeTrash`; Reihenfolge wie bisher: Zeile zuerst, Dateireste werden geloggt | Application.Tests | 100 |
| `src/NoMorePaper.Worker/Trash/TrashPurger.cs` | neu | `BackgroundService` nach Muster `InputScheduler`: beim Start und dann alle 24 h (`PeriodicTimer` mit `TimeProvider`), eigener Scope, Fehler beenden den Host nicht; Logs 2500–2503 | Worker.Tests (ManualTimeProvider) | 100 |
| `src/NoMorePaper.Server/Trash/TrashEndpoints.cs` | neu | Endpunkte aus P0; Logs 1700–1705 | Server.Tests | 140 |
| `src/NoMorePaper.Web.Client/Pages/Trash.razor` + `.razor.cs` | neu | `@page "/papierkorb"`; Liste mit Titel, Art („Dokument“, „Original von …“, „ältere Fassung von …“), gelöscht am, „wird endgültig gelöscht am“; Knöpfe „Wiederherstellen: <Titel>“, „Endgültig löschen: <Titel>“ (Bestätigung), „Papierkorb leeren“ (Bestätigung mit Anzahl); Fokus nach Aktion auf nächsten Eintrag | UI.Tests | 2 × 160 |
| `src/NoMorePaper.Web.Client/Trash/DeleteDocumentButton.razor` | neu | im Viewer für den Besitzer: „In den Papierkorb“ mit Bestätigung; danach zur Dokumentliste mit Ansage | UI.Tests | 90 |
| `DocumentViewer.razor`, `MainLayout.razor`, Texte | ändern (Hotspot) | je eine Zeile; Navigation „Papierkorb“ | – | 60 |
| Tests | neu | Postgres: Wiederherstellen mit Tausch, endgültig löscht Dateien, Fremde sehen nichts; Worker: Aufräumen nach 30 Tagen (Uhr gestellt); UI: axe DE/EN, Tastatur | selbst | ~700 |

- **Telemetrie:** 1700–1705, 2500–2503, 4800–4805, 5600–5608.

---

## Paket P4 – Stapel-Trennung (Spur D) · Modell: **Opus** · Effort: `high`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Application/Separation/SeparationHeuristics.cs` | neu | rein: aus `PageInfo` Grenzen ableiten – leere Seite (Grenze danach, Seite auslassen), „Seite 1 von n“/„1/3“ (Regex DE/EN), Anrede („Sehr geehrte“, „Dear“) nur am Seitenanfang; Ergebnis Kandidaten mit Gründen | Unit | 150 |
| `src/NoMorePaper.Application/Separation/ISeparationAssessor.cs` | neu | `AssessAsync(IReadOnlyList<PageInfo>, Candidates, ct)` → bestätigte Grenzen + Absenderwechsel | – | 20 |
| `src/NoMorePaper.Infrastructure/Recognition/OllamaSeparationAssessor.cs`, `SeparationPrompt.cs`, `SeparationSchema.cs` | neu | über `LlmGate` (Priorität wie Mail), Seitenanfänge je ≤ 300 Zeichen, max. 30 Seiten; Schema `{boundaries: [int], senders: [string]}`; Plausibilität: Grenzen im Seitenbereich | Infrastructure.Tests (`FakeOllamaHandler`) | 3 × 90 |
| `src/NoMorePaper.Application/Separation/SeparationStageRunner.cs` | neu | `IStageRunner` für `Separation`: nur wenn Seiten > 1 und keine Seite `HadTextLayer`; Heuristik + LLM → `SplitSuggestion` am Dokument (nur wenn ≥ 2 Teile oder leere Seiten); danach Stufe `Recognition` wie bisher; `LlmBusyException` → Auftrag zurück ohne Fehlversuch | Application.Tests | 160 |
| `src/NoMorePaper.Infrastructure/Processing/JobQueue.cs` | ändern (Hotspot) | nach OCR: mehrseitig + Scan → Stufe `Separation`, sonst `Recognition` | Infrastructure.Tests | 15 |
| `src/NoMorePaper.Application/Separation/AnswerSplitSuggestion.cs` | neu | annehmen (→ `SplitDocument` aus Spur A über Port `ISplitRequester`, in P0 als Interface), verwerfen (Vorschlag löschen) | Application.Tests | 90 |
| „Zu prüfen“ | ändern | `ReviewFor` + Grund `SplitSuggestion != null` (in P0 vorbereitet: Spalte und DTO-Feld; hier die Bedingung), Endpunkte `POST /api/documents/{id}/split-suggestion/accept|dismiss` | Server.Tests | 80 |
| `src/NoMorePaper.Web.Client/Classification/SplitSuggestionSection.razor` | neu | Text „Trennvorschlag: 3 Dokumente (Seiten 1–2, 3, 4–6), Seite 5 vermutlich leer“; Knöpfe „Teilen“, „Anpassen“ (→ `/dokumente/{id}/seiten?trennen=1`), „Nicht teilen“; in `Review.razor` eine Zeile | UI.Tests | 140 |
| `tests/…/Separation/Samples/*` + `expected.json`, `SeparationLiveTests.cs` | neu | 12 selbst erzeugte Stapel als Seitentexte (Briefe, Rechnungen, leere Seiten, Zähler, englisch) mit Sollgrenzen; Live-Messung nur mit erreichbarem Ollama | selbst | ~300 |

- **Telemetrie:** 2600–2605, 4900–4903, 5700–5706.
- **Akzeptanz:** Tests grün; Messung in `scan-bearbeitung.md` (Ziel: ≥ 80 % der Grenzen, keine falsche Trennung ohne Bestätigung möglich).
