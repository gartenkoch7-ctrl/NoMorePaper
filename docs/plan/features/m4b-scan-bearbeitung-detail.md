# M4b – Detailplan „Scan-Bearbeitung“

Gehört zu [m4b-scan-bearbeitung.md](../m4b-scan-bearbeitung.md). Regeln: [AGENTS.md §12](../../../AGENTS.md),
[Vorlage](../detailplan-vorlage.md), [UI-Prozess](../../kb/ui-prozess.md). Format der UI-Pakete wie
[M4u Spur E](m4u-e-rest-detail.md).

**Erstellt von:** Opus (Stufe 3) · **Datum:** 2026-11-19 · **Aufgefrischt:** 2026-11-23 (nach M3, M4, M4u; Stand
`develop` `a95ed00`) · **Offene Designfragen:** keine (F1, F2 vom Nutzer 2026-11-23 wie empfohlen beantwortet: Nachlauf ja, `NavigationLock` ja)

Grundlagen der Oberfläche: freigegebene Entwürfe [14](../../ui/stitch/14-seiten-bearbeiten/notizen.md),
[15](../../ui/stitch/15-papierkorb/notizen.md), [16](../../ui/stitch/16-fassungen/notizen.md). **Anforderung** sind dort
„Übernehmen“, „Abgleich mit dem Plan M4b“ (rechte Spalte gilt) und „Nicht mitnehmen (§2.5)“. „Nachbessern in Stitch?“ entfällt
(Nutzer 2026-11-23: Restpunkte beim Umsetzen nach App-Texten und Theme). Bestätigen-Knöpfe in Rückfragen **neutral**, nie Rot.

## Was sich seit 2026-11-19 geändert hat

- M4u ist in `develop`: Spur B/C/D warten **nicht** mehr auf M4u. Bausteine `Design/` (`ConfirmBox`, `NoticeBox`, `Badge`,
  `Breadcrumb`, `StatusText`, `TabList`), Seiten-CSS je Datei, Viewer mit Reitern, Startseite, UI-Test-Helfer
  (`FakeAccounts`, `ApiMocks`, `AxeCheck`, `StartPageMocks`, `ViewerPages`) sind da und werden benutzt.
- ADR-0025 existiert (Angenommen) – nicht mehr Teil von P0.
- Code-Abgleich: `OcrStage` ist `internal` und wird in `ProcessNextDocument` erzeugt (kein DI); `JobQueue.CompleteAsync`
  ruft `Document.Complete` (nur Zustand `Processing`) – eine Bearbeitung darf **nicht** über diesen Weg laufen. Es gibt
  genau einen Auftrag je Dokument (`processing_jobs` PK `document_id`). `DocumentDetailDto.Original` ist heute Pflicht,
  `DownloadAsync` kennt nur `original`/`archive`, `ReviewDeleteExplain` sagt „nicht rückgängig“. `/papierkorb` fehlt in
  `Layout/NavSection.cs`. Papierkorb liegt laut prompts.md §2 unter „Einstellungen“ (Kachel in `Pages/Settings.razor`),
  **nicht** in `MainLayout`.

## Entschieden (Planer, 2026-11-23 – nicht neu fragen)

- **Neubau des Archivs** (Versionswahl „nur Original“, Wiederherstellen einer Fassung) läuft als Bearbeitungsauftrag
  `EditKind.Rebuild` über die Stufe `Edit`, nicht über die OCR-Stufe: das Dokument bleibt `Done`, der Viewer zeigt die
  alte Fassung, bis die neue fertig ist. Die OCR-Stufe bleibt für neue Dokumente unverändert (liest das Original).
- **Bearbeiten trotz wartender Erkennung:** Steht für das Dokument nur ein **nicht gesperrter** `Recognition`-Auftrag an,
  übernimmt `EnqueueEditAsync` ihn (Stufe → `Edit`, `document_edits.recognize_after = true`); nach der Bearbeitung geht er
  zurück auf `Recognition`. Läuft gerade ein Auftrag (gesperrt) → `false` → 409.
- **Teilen = Liste von Seitenplänen:** `SplitPlan(IReadOnlyList<PagePlan> Parts)`. So kann „Seiten bearbeiten“ Drehen,
  Löschen, Zuschnitt **und** Trennstellen in einem „Speichern“ senden; ohne Trennstelle geht `edit`, mit Trennstellen `split`.
- **Seitentexte** schreibt die OCR-Stufe in derselben Transaktion wie das Archiv (`CompleteAsync` mit Seiten).
- **Besitz von Papierkorb-Einträgen** folgt `documents.owner_id` (Join), keine eigene `owner_id`-Spalte: nach einer
  Übergabe (M4 `HandOverDocument`) gehört der Eintrag dem neuen Besitzer.
- **„In den Papierkorb“ im Viewer** ist ein eigener Endpunkt `POST /api/documents/{id}/trash`. `DELETE /api/documents/{id}`
  bleibt der Duplikat-Fall aus „Zu prüfen“ (409, wenn kein mögliches Duplikat), legt aber ab P0 in den Papierkorb.
- **Fassung herunterladen:** je Eintrag der Liste „Fassungen“ ein Download-Link (Plan B). Kopf des Viewers behält
  „Original“ (nur wenn vorhanden) und „Archiv-PDF“. Download-Antworten bekommen `ETag` (SHA-256) und
  `Cache-Control: no-cache`, damit Viewer und Miniaturen nach einer Bearbeitung nicht die alte Datei zeigen.
- **Werkswert je Konto** über eigenen Endpunkt `GET/PUT /api/account/after-editing` (Spalte in `account_preferences`);
  `AccountPreferences`/`AccountPreferencesDto` bleiben unverändert.
- **„Seiten bearbeiten“ (aus Notizen 14 „im Detailplan entscheiden“):** gelöschte Seite bleibt als zusammengeklappte Karte
  in der Liste („Gelöschte Seite, vorher Seite 3“, Knopf „Löschen rückgängig“, keine Vorschau); gedrehte/zugeschnittene/
  verschobene Seite zeigt eine Zustandszeile (Symbol `aria-hidden` + Text) und dreht die Vorschau per CSS; Trennstelle =
  eigener Listeneintrag zwischen zwei Karten („Trennstelle: Dokument 1 endet nach Seite 2“, Knopf „Trennstelle entfernen“);
  „Dokument anhängen“ = Abschnitt in der Seite unter der Knopfleiste (Suche, Treffer als Knöpfe, `ConfirmBox` mit
  Privat-Hinweis), nur ohne ungespeicherte Änderungen; nach „Speichern“ `NoticeBox` Erfolg mit Link zum Dokument (bzw. „Zu
  prüfen“ beim Teilen), Liste schreibgeschützt. Kein Modaldialog.
- **Papierkorb schmal:** ab 48 rem Tabelle (Entwurf 15) im benannten Scrollbereich der App (Sicherheitsnetz), darunter
  dieselben Einträge als Liste (`ul` mit `article` je Eintrag); die jeweils andere Darstellung ist `display:none` (nicht im
  Lesebaum). Grund: `display:block` auf Tabellenzeilen nimmt VoiceOver die Tabellen-Semantik.
- **Seitenleiste:** kein neuer Punkt; `/papierkorb` markiert „Einstellungen“.

## Stitch-Nachtrag (S0) – vor den UI-Paketen

AGENTS §2.5: jede UI-Änderung zuerst in Stitch. Nicht entworfen und daher nachzutragen (einzeln nacheinander, als „… v3“
mit `generate_screen_from_text`, KB ui-prozess „Stitch-MCP-Eigenheiten“), dann Freigabe durch den Nutzer:

| Screen | Nachtrag | gebraucht von |
|---|---|---|
| 14 Seiten bearbeiten v3 | Zustände aus „Entschieden“: gelöschte Seite, Trennstelle, Abschnitt „Dokument anhängen“ mit Privat-Hinweis, nach „Speichern“, Rückfrage „Alles verwerfen“; Restpunkte der Notizen (Karte 3 echte Seite, keine Fußzeile, kein „#01 / 03“) | P2b, P2c |
| 16 Fassungen v3 | Kopf: „In den Papierkorb“ (nur Besitzer, neutral) + „Original“ nur wenn vorhanden; Download je Fassung; Zustand „Wird bearbeitet“; Dokument ohne Bearbeitung; Restpunkte der Notizen | P2d, P3c |
| 09 Konto v? | Karte „Bearbeitete Dokumente“ mit Wahl „Nach dem Bearbeiten behalten“ (drei Radios, je eine Erklärzeile) unter „Eingänge und neue Dokumente“ | P2d |
| 06 Zu prüfen v? | Abschnitt „Trennvorschlag“ in einer Prüfkarte (Satz, Liste der Teile, Knöpfe „Teilen“, „Anpassen“, „Nicht teilen“) | P4d |
| 15 Papierkorb v3 | Anzahl an „Gelöschte Einträge“, Rückfrage „Wiederherstellen mit Tausch“, schmale Liste | P3c |

Kachel „Papierkorb“ in Einstellungen: kein Screen vorhanden, Kachel nach bestehendem Muster (`.link-tile`), kein Entwurf nötig.
Text „Dieses löschen“ in 06 ändert nur Wörter (Papierkorb statt endgültig), kein Layout. **Wer:** steuernde Sitzung (Opus,
Prompt in prompts.md §4.1–4.3 ergänzen, prüfen, Notizen), Abholen Haiku. Backend-Spuren warten nicht darauf.

## Einschätzung

| Paket | Umfang | Risiko | Neuheit | Klarheit | Prüfbarkeit | Stufe |
|---|---|---|---|---|---|---|
| S0 Stitch-Nachtrag | 5 Screens | gering | Muster | entschieden | Sichtprüfung Nutzer | 3 (+1 Abholen) |
| P0 Backend-Bausteine | ~60 Dateien, ~2 400 Zeilen | Migration, Papierkorb-Filter in jeder Abfrage, Auftragsübernahme | teils Muster | entschieden | Unit + Postgres + Architektur | 3 |
| P0u UI-Grundlagen | ~15 Dateien, ~450 Zeilen | gering | Muster (M4u E0) | nach S0 entschieden | Build | 2 |
| P1 Bearbeitung im Worker (A) | ~25 Dateien, ~1 800 Zeilen | Datenverlust, zwei Sperren, externe Werkzeuge | neu (qpdf) | entschieden (F1 offen: Nachlauf) | Integration Linux | 3 |
| P2a Seitenplan im Client | 2 Dateien, ~350 Zeilen | gering | reine Logik | entschieden | Unit | 2 |
| P2b Seite „Seiten bearbeiten“ | ~10 Dateien, ~1 100 Zeilen | Fokusführung, viele Zustände | Muster (Review, Viewer) | nach S0 entschieden | UI-Tests, axe | 2 |
| P2c Zuschnitt | ~5 Dateien, ~600 Zeilen | Interop PDF.js + OpenCV, Speicher | Muster Kamera | entschieden | UI-Test Standbild | 3 |
| P2d Fassungen + Werkswert | ~8 Dateien, ~600 Zeilen | gering | Muster | nach S0 entschieden | UI-Tests, axe | 2 |
| P3a Papierkorb-Anwendungsfälle (C) | ~10 Dateien, ~900 Zeilen | endgültiges Löschen, Tausch | Muster | entschieden | Unit + Postgres | 3 |
| P3b Aufräumdienst | 3 Dateien, ~200 Zeilen | Uhr | Muster `InputScheduler` | entschieden | Worker.Tests | 2 |
| P3c Papierkorb-Seite, Löschen im Viewer | ~8 Dateien, ~600 Zeilen | Fokus nach Aktion | Muster (Listen, Review) | nach S0 entschieden | UI-Tests, axe | 2 |
| P4a Trennheuristik + Testsatz (D) | ~4 Dateien, ~500 Zeilen | gering | reine Logik | entschieden | Unit | 2 |
| P4b LLM-Prüfung | 4 Dateien, ~300 Zeilen | LLM-Qualität | Muster `OllamaMailAssessor` | entschieden | Infrastructure.Tests | 2 |
| P4c Stufe `Separation`, Antwort | ~6 Dateien, ~500 Zeilen | Nebenläufigkeit (Gate, Claim) | teils neu | entschieden | Unit + Postgres | 3 |
| P4d Trennvorschlag in „Zu prüfen“ | ~4 Dateien, ~300 Zeilen | gering | Muster `OwnerCheckSection` | nach S0 entschieden | UI-Tests, axe | 2 |
| P5 Doku, Katalog, Häkchen | ~6 Dateien | keins | – | entschieden | Lesen | 1 |
| P6 Review + Abnahme | – | – | – | – | Review, NVDA | 3 |

## Abkürzungen, Telemetrie, Sperren

Pfade: `D/` = `src/NoMorePaper.Domain/`, `App/` = `src/NoMorePaper.Application/`, `Inf/` = `src/NoMorePaper.Infrastructure/`,
`Srv/` = `src/NoMorePaper.Server/`, `Wrk/` = `src/NoMorePaper.Worker/`, `Ctr/` = `src/NoMorePaper.Contracts/`,
`WC/` = `src/NoMorePaper.Web.Client/`. Tests: `DT`, `AT`, `IT`, `ST`, `WT` (Domain/Application/Infrastructure/Server/Worker),
`ArchT`, `UI` = `tests/NoMorePaper.UI.Tests/`, `WCT` = `tests/NoMorePaper.Web.Client.Tests/`.

| Block | Projekt | Paket |
|---|---|---|
| 1600–1649 | Server | P1 Bearbeiten, Anhängen, Teilen, Seiten, Fassungen |
| 1650–1669 | Server | P4c Trennvorschlag annehmen/verwerfen |
| 1700–1749 | Server | P3a Papierkorb |
| 2400–2499 | Worker | P1 Stufe `Edit` (Composition-Logs) |
| 2500–2599 | Worker | P3b Aufräumdienst |
| 2600–2699 | Worker | P4c Stufe `Separation` |
| 3950–3969 | Infrastructure | P0 qpdf, Seitenanalyse |
| 3970–3989 | Infrastructure | P0 Papierkorb-Ablage, Bearbeitungsseiten, Auftragsübernahme |
| 3990–3999 | Infrastructure | P4b LLM-Prüfung Trennung |
| 4300–4349 | Web.Client | P2b/P2c Seiten bearbeiten |
| 4350–4379 | Web.Client | P2d Fassungen, Werkswert |
| 4800–4849 | Web.Client | P3c Papierkorb, Löschen im Viewer |
| 4900–4919 | Web.Client | P4d Trennvorschlag |
| 5000–5099 | Application | Verarbeitung (neu, M4b P0: 5000 `StageRunnerMissing`) |
| 5500–5599 | Application | P1 Bearbeitung |
| 5600–5699 | Application | Papierkorb (5600–5609 P0 `MoveDocumentToTrash`, 5610–5649 P3a) |
| 5700–5799 | Application | P4 Stapel-Trennung |

Geprüft 2026-11-23: alle Blöcke frei (4950–4999 = Startseite). Nur neutrale Platzhalter (`{DocumentId}`, `{Count}`,
`{Pages}`, `{ElapsedMs}`, `{Reason}`), nie Titel, Dateinamen, Seitentexte.

**Nur in P0/P0u geändert, danach für Spuren gesperrt** (Lehre M4u): `WC/Localization/Strings*.resx`, `WC/wwwroot/index.html`,
`WC/_Imports.razor`, `WC/Design/*`, `design*.css`, alle `Ctr/`-Typen, Migration. Fehlt etwas → Spur stoppt und meldet; die
steuernde Sitzung ergänzt im Integrationsbranch.

---

## Paket P0 – Backend-Bausteine · Modell: **Opus** · Effort: `high`

Integrationsbranch `feat/m04b-scan-bearbeitung`, steuernde Sitzung. Reihenfolge: Domain → Ports → Schema/Migration →
Papierkorb-Filter (mit Architekturtest) → Speicher → Stufen-Weiche → Seiten in der OCR → Löschen in den Papierkorb →
Werkswert → Contracts/Mapping.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `D/Documents/DocumentFileKind.cs` | ändern | `Edited` (bearbeitete Fassung) | – | 3 |
| `D/Documents/Document.Files.cs` | neu | `FindFile`/`AttachFile` aus `Document.cs` (158 Z.) hierher; `ReplaceFile(DocumentFile)` (nur `Edited`/`Archive`), `RemoveFile(DocumentFileKind)` (nur wenn danach Original **oder** Edited bleibt), `CurrentSource` (Edited ?? Original), `EditedAt?`, `ReplaceContent(string text)` (nur `Done`, für Neubau nach Bearbeitung) | DT | 100 |
| `D/Documents/Document.Trash.cs` | neu | `DeletedAt?`, `DeletedBy?`, `IsInTrash`; `MoveToTrash(Guid actor, DateTimeOffset at)`, `RestoreFromTrash(Guid actor)` – beide nur Besitzer (`RequireOwner`); `IsVisibleTo`/`CanEdit` liefern `false` im Papierkorb | DT | 70 |
| `D/Documents/Document.Editing.cs` | neu | `VersionPolicy? VersionPolicy` + `ChooseVersions(Guid actor, VersionPolicy)` (nur Besitzer); `SplitSuggestion? SplitSuggestion`, `SuggestSplit(SplitSuggestion)`, `DismissSplit()` | DT | 60 |
| `D/Documents/VersionPolicy.cs` | neu | `KeepBoth, EditedOnly, OriginalOnly` | – | 10 |
| `D/Editing/PageRotation.cs`, `PageStep.cs`, `PagePlan.cs` | neu | `PageRotation { None = 0, Right = 90, Half = 180, Left = 270 }`; `PageStep(int SourcePage, PageRotation Rotation, bool Replaced)`; `PagePlan(IReadOnlyList<PageStep>)` mit `Validate(int sourcePageCount)` (≥ 1 Seite, Seiten 1…n, jede höchstens einmal) | DT | 3 × 40 |
| `D/Editing/SplitPlan.cs` | neu | `SplitPlan(IReadOnlyList<PagePlan> Parts)`; `Validate(pageCount)`: ≥ 2 Teile, jede Quellseite höchstens einmal über alle Teile | DT | 50 |
| `D/Editing/PageRange.cs`, `SplitReason.cs`, `SplitSuggestion.cs` | neu | `PageRange(int First, int Last)`; `SplitReason { Blank, PageCounter, Salutation, SenderChange }`; `SplitSuggestion(IReadOnlyList<PageRange> Parts, IReadOnlyList<int> BlankPages, IReadOnlyList<SplitReason> Reasons)` + `ToPlan()` (leere Seiten fallen weg) | DT | 3 × 30 |
| `D/Editing/AppendRule.cs` | neu | rein: `ResultIsPrivate(target, appended)`; `Check(target, appended, actor)` → Grund (`Same`, `NotDone`, `NotEditable`, `InTrash`) oder `null` | DT | 50 |
| `App/Trash/TrashItem.cs`, `TrashItemKind.cs`, `ITrashStore.cs`, `TrashRules.cs` | neu | `TrashItemKind { Document, Original, Edited }`; `TrashItem(Guid Id, TrashItemKind Kind, Guid DocumentId, DocumentFile? File, string Title, DateTimeOffset DeletedAt, DateTimeOffset PurgeAfter)`; `ITrashStore`: `AddAsync`, `ListAsync(Guid ownerId)`, `FindAsync(Guid id, Guid ownerId)`, `FindTrashedDocumentAsync(Guid id, Guid ownerId)` (sperrt, ignoriert Filter), `RemoveAsync`, `DueAsync(DateTimeOffset now, int max)`; `TrashRules.Retention = 30 Tage`, `PurgeAfter(at)` | AT | 4 × 25 |
| `App/Trash/MoveDocumentToTrash.cs`, `TrashLog.cs` | neu | Muster `DeleteDuplicate`: `DocumentChangeGuard.LoadAsync(…, found => found.OwnerId == current.Id)`; `Document.MoveToTrash`; `TrashItem(Document)`; Auftrag in `processing_jobs` und `document_edits` löschen (gleiche Transaktion); Dateien bleiben. 5600–5603 | AT | 80 + 30 |
| `App/Classification/DeleteDuplicate.cs` | ändern | ruft nach der Duplikat-Prüfung `MoveDocumentToTrash` statt hart zu löschen; `IDocumentFieldsStore.DeleteAsync` entfällt (samt Test) | AT (bestehende angepasst) | ±20 |
| `App/Documents/IDocumentStorage.cs` | ändern | `SaveEditedAsync(Guid id, Stream, ct)` → `edited.pdf` im Archivordner (atomar wie `SaveArchiveAsync`); `MoveToTrashAsync(Guid id, DocumentFileKind, Guid trashItemId, ct)`; `RestoreFromTrashAsync(Guid trashItemId, Guid id, DocumentFileKind, ct)`; `PurgeTrash(Guid trashItemId)`; `OpenRead(id, Edited)` | – | +25 |
| `App/Editing/IEditPageStorage.cs` | neu | `SaveAsync(Guid id, int page, Stream jpeg, ct)`, `OpenRead(Guid id, int page)`, `Discard(Guid id)` (Ordner `edits/<id>/`) | – | 20 |
| `App/Editing/IPdfTool.cs`, `PdfPageSource.cs` | neu | `ComposeAsync(IReadOnlyList<PdfPageSource>, string outputPath, ct)`, `CountPagesAsync(path, ct)`; `record PdfPageSource(string Path, int Page, PageRotation Rotation)` | – | 30 + 10 |
| `App/Editing/IPageAnalyzer.cs`, `PageInfo.cs`, `IDocumentPages.cs` | neu | `AnalyzeAsync(string pdfPath, string originalPath, ct)` → `IReadOnlyList<PageInfo(int Page, string Head, bool ProbablyBlank, bool HadTextLayer)>`; `IDocumentPages.ListAsync(Guid id, ct)` | – | 3 × 20 |
| `App/Editing/DocumentEdit.cs`, `EditKind.cs`, `IDocumentEdits.cs`, `ISplitRequester.cs` | neu | `EditKind { Pages, Append, Split, Rebuild }`; `DocumentEdit(Guid DocumentId, EditKind Kind, string Data, Guid RequestedBy, DateTimeOffset RequestedAt, bool RecognizeAfter)`; `SaveAsync`, `GetAsync`, `DeleteAsync`; `ISplitRequester.RequestAsync(Guid id, SplitPlan, ct)` (Umsetzung P1, Nutzer P4c) | – | 4 × 20 |
| `App/Processing/JobStage.cs` | ändern | `Edit`, `Separation` | – | 8 |
| `App/Processing/IJobQueue.cs` + `Inf/Processing/JobQueue.cs`, `JobQueue.Enqueue.cs` (neu) | ändern/neu | `ClaimNextAsync(lockFor, bool includeLlmStages, ct)` (umbenannt; `stage IN ('Ocr','Edit') OR {includeLlmStages}`; Dokumente im Papierkorb nie); `CompleteAsync(claim, text, archive, IReadOnlyList<PageInfo> pages, ct)` schreibt `document_pages` in derselben Transaktion; `EnqueueEditAsync(Guid id, bool recognizeAfter, ct)` → `bool` (Regel aus „Entschieden“, Priorität 0) in `JobQueue.Enqueue.cs` | IT (Postgres) | ±60 |
| `App/Processing/IStageRunner.cs`, `OcrStageRunner.cs`, `RecognitionStageRunner.cs` | neu | `Task RunAsync(IJobQueue queue, JobClaim claim, CancellationToken)`; OCR-Runner = heutiges `OcrStage` + `FindContentDuplicate` aus `ProcessNextDocument.RunStageAsync`; Recognition-Runner ruft `RecognizeDocument` | WT (bestehende grün) | 40 + 20 |
| `App/Processing/ProcessNextDocument.cs` | ändern | Weiche: `scope.ServiceProvider.GetRequiredKeyedService<IStageRunner>(claim.Stage)`; Stopp-Behandlung bleibt hier; `OcrStage` wird scoped (Konstruktor-Abhängigkeiten aus DI) | WT | −30 |
| `App/Processing/ProcessingAttempt.cs` | ändern | nach der OCR `IPageAnalyzer.AnalyzeAsync(archive, original)` → an `CompleteAsync` | WT | 10 |
| `App/Accounts/IAccountPreferencesStore.cs`, `GetAfterEditing.cs`, `ChangeAfterEditing.cs` | ändern/neu | `GetAfterEditingAsync(Guid accountId, ct)`, `SetAfterEditingAsync(…, VersionPolicy, ct)` | AT | 30 |
| `App/Documents/DocumentDetail.cs`, `OpenDocumentFile.cs` | ändern | `DocumentFile? Original`, `DocumentFile? Edited`, `DateTimeOffset? EditedAt`, `bool EditPending`, `VersionPolicy VersionPolicy` (Dokument ?? Konto); Datei-Art `Edited` lesbar | AT | 20 |
| `App/Composition/ApplicationServiceCollectionExtensions.cs` | ändern (Hotspot) | Runner keyed nach `JobStage`, `MoveDocumentToTrash`, Werkswert-Fälle | – | 10 |
| `Inf/Documents/DocumentConfiguration.cs` | ändern | `deleted_at`, `deleted_by`, `version_policy` (string 16, null), `edited_at`, `split_suggestion` (jsonb, null); **benannter** Abfragefilter `HasQueryFilter("NotInTrash", d => d.DeletedAt == null)`; Index `deleted_at` | MigrationTests | 30 |
| `Inf/Persistence/TrashSql.cs` | neu | `NotInTrash = "d.deleted_at IS NULL"` und `NotInTrashOf(string alias)` | – | 20 |
| Rohe Abfragen | ändern | `Inf/Search/PostgresSearchIndex.cs` (`Visible`), `Inf/Recognition/PostgresDuplicateFinder.cs` (`d` **und** `o`), `Inf/Documents/EfDocumentRepository.cs` (`ListAsync`, `FindVisibleBySha256Async`, `FindOwnedBySha256Async`; `GetAsync` mit `EditPending` = `EXISTS document_edits`), `Inf/Documents/EfDocumentCounts.cs` (`Visible`), `Inf/Classification/EntryTables.cs` (`usages` mit Filter → wirkt in `VisibilitySql`/`EntrySets`), `Inf/Recognition/EfRecognitionExamples.cs`, `Inf/Processing/JobQueue.Recognition.cs` (Rückstand: zwei `INSERT … FROM documents d`). Ausgenommen: `EntryMergeSql` (UPDATE soll auch Papierkorb-Dokumente umhängen), Sperrzeilen `FOR NO KEY UPDATE` | IT: je Abfrage „im Papierkorb → unsichtbar“ | 40 |
| `tests/NoMorePaper.Architecture.Tests/TrashFilterTests.cs` | neu | über `SourceFiles`: jede Infrastructure-Datei mit `FROM documents`/`JOIN documents` nutzt `TrashSql` oder steht auf der Ausnahmeliste (oben) | selbst | 60 |
| `Inf/Trash/TrashItemConfiguration.cs`, `EfTrashStore.cs` | neu | `trash_items` (id, kind, document_id FK documents cascade, file jsonb, title varchar 500, deleted_at, purge_after; Index `(purge_after)`, `(document_id)`); Liste über Join `documents.owner_id` mit `IgnoreQueryFilters(["NotInTrash"])` | IT | 40 + 90 |
| `Inf/Editing/DocumentPagesConfiguration.cs`, `EfDocumentPages.cs`, `DocumentEditsConfiguration.cs`, `EfDocumentEdits.cs` | neu | `document_pages(document_id FK cascade, page, head varchar(300), probably_blank, had_text_layer)` PK `(document_id, page)`; `document_edits(document_id PK FK cascade, kind, data jsonb, requested_by, requested_at, recognize_after)` | IT | 4 × 50 |
| `Inf/Editing/QpdfTool.cs`, `PopplerPageAnalyzer.cs`, `EditingLog.cs` | neu | qpdf über `Inf/Ocr/ExternalProcess.cs` (`--empty --pages <datei> <seite> … -- out`, Drehung `--rotate=+90:n`), Zeitlimit 2 min; Seitenanalyse `pdftotext -f n -l n` (Kopf 300 Zeichen), leer = < 15 Zeichen **und** Tinte < 0,5 % (`pdftoppm -r 30 -gray`), `HadTextLayer` über `pdffonts` des Originals; 3950–3955 | IT (Linux, sonst `OcrSamples.SkipUnlessToolsInstalled`) | 130 + 130 + 30 |
| `Inf/Documents/FileSystemDocumentStorage.Trash.cs`, `EditPageStorage.cs`, `DocumentPaths.cs` | neu/ändern | `FileSystemDocumentStorage` (161 Z.) wird `partial`; Papierkorb-Teil `trash/<trashItemId>/<datei>`; `edited.pdf` neben `archive.pdf`; `EditPageStorage` `edits/<id>/page-<n>.jpg`; Logs 3970–3975 | IT | 90 + 70 + 10 |
| `Inf/Inputs/AccountPreferencesConfiguration.cs`, `EfAccountPreferencesStore.cs` | ändern | Spalte `after_editing` (string 16, Werk `KeepBoth`) | IT | 20 |
| `Inf/Persistence/Migrations/<ts>_ScanEditing.cs` | neu | `dotnet ef migrations add ScanEditing` | MigrationTests | gen. |
| `Inf/Composition/InfrastructureServiceCollectionExtensions.cs` | ändern (Hotspot) | Stores, `QpdfTool`, `PopplerPageAnalyzer`, `EditPageStorage` | – | 10 |
| `Dockerfile.worker` | ändern | `qpdf` in die vorhandene `apt-get`-Zeile (poppler-utils ist schon da) | Compose-Build | 1 |
| `Srv/Documents/DocumentEndpoints.cs`, `DocumentMapping.cs` | ändern | Art `edited`; `ETag` + `Cache-Control: no-cache`; neue Felder im DTO | ST | 25 |
| `Srv/Accounts/PreferencesEndpoints.cs` | ändern | `GET/PUT /api/account/after-editing` | ST | 30 |
| `Ctr/Documents/DocumentDetailDto.cs` | ändern | `DocumentFileDto? Original`; neu am Ende `DocumentFileDto? Edited = null`, `DateTimeOffset? EditedAt = null`, `bool EditPending = false`, `VersionPolicyDto VersionPolicy = KeepBoth` | – | 8 |
| `Ctr/Editing/*` | neu | je Datei ein Typ: `PageRotationDto`, `PageStepDto`, `PagePlanDto`, `SplitPlanDto`, `PageInfoDto`, `DocumentPagesDto(Guid Id, string Title, bool CanEdit, bool EditPending, IReadOnlyList<PageInfoDto> Pages)`, `AppendRequestDto(Guid AppendedId)`, `VersionPolicyDto`, `VersionChoiceDto`, `AfterEditingDto`, `SplitSuggestionDto`, `PageRangeDto` | – | 12 × 10 |
| `Ctr/Trash/TrashItemDto.cs`, `TrashItemKindDto.cs` | neu | `TrashItemDto(Guid Id, TrashItemKindDto Kind, string Title, DateTimeOffset DeletedAt, DateTimeOffset PurgeAfter, bool RestoreSwaps, bool DocumentInTrash)` | – | 20 |
| `Ctr/Classification/ReviewItemDto.cs` | ändern | `SplitSuggestionDto? Split = null` am Ende | – | 3 |
| `WC/Pages/DocumentViewer.razor(.cs)` | ändern (nur Kompilieren) | `Original` nullbar: Download-Link nur bei `Original is not null`, `SourceOf` null-sicher | bestehende Viewer-Tests | ±6 |
| `WC/Localization/Strings*.resx` | ändern | `ReviewDeleteExplain` → „Das Dokument kommt 30 Tage in den Papierkorb und lässt sich bis dahin wiederherstellen. Das vorhandene Dokument bleibt erhalten.“, `ReviewDeleted` → „„{0}“ in den Papierkorb gelegt.“ (EN entsprechend) | `ReviewPageTests` | ±4 |

- **Telemetrie:** 3950–3955, 3970–3975, 3976–3977 (`JobQueue.Enqueue`: Erkennung übernommen, Auftrag läuft), 5600–5603.
- **Akzeptanz:** Build ohne Warnungen; alle bisherigen Tests grün; neu: jede Abfrage blendet Papierkorb-Dokumente aus
  (inkl. Listeneinträge, Lernbeispiele, Rückstand, Claim), Architekturtest, Duplikat löschen → Papierkorb-Eintrag, OCR schreibt
  Seiten, `EnqueueEditAsync` (frei → true; wartende Erkennung → übernommen, `recognize_after`; gesperrt → false), Download mit
  ETag.

## Paket P0u – UI-Grundlagen · Modell: **Sonnet** · Effort: `medium`

Nach P0 **und** Freigabe von S0, im Integrationsbranch. Muster: [M4u E0](m4u-e-rest-detail.md#paket-e0--gemeinsame-bausteine-und-texte--modell-sonnet--effort-medium).

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `WC/Editing/EditingApi.cs` | neu | Muster `Inputs/PreferencesApi.cs`/`ApiResult`: `GetPagesAsync(id)`, `SaveEditAsync(id, PagePlanDto, IReadOnlyList<(int Page, byte[] Jpeg)>)` (multipart `plan` + `page-<n>`), `AppendAsync`, `SplitAsync`, `ChooseVersionsAsync`, `GetAfterEditingAsync`, `SetAfterEditingAsync`, `AcceptSplitAsync`, `DismissSplitAsync` | – | 120 |
| `WC/Trash/TrashApi.cs` | neu | `ListAsync`, `RestoreAsync(id)`, `PurgeAsync(id)`, `EmptyAsync()`, `MoveDocumentAsync(documentId)` | – | 60 |
| `WC/Documents/DocumentLinks.cs` | ändern | `Edited(Guid id)`, `PageEditor(Guid id, bool split = false)` (`dokumente/{id}/seiten[?trennen=1]`) | – | 6 |
| `WC/Program.cs` | ändern (Hotspot) | `EditingApi`, `TrashApi` registrieren | – | 2 |
| `WC/_Imports.razor` | ändern | `@using …Editing`, `…Trash`, `…Contracts.Editing`, `…Contracts.Trash` | – | 4 |
| `WC/wwwroot/css/{page-editor,page-crop,versions,trash,split-suggestion}.css` | neu | nur Kopfkommentar `/* … (Stitch NN), M4b Spur X. */` | – | 5 × 2 |
| `WC/wwwroot/index.html` | ändern | nach `camera.css` die fünf Dateien | – | 5 |
| `WC/Localization/Strings*.resx` | ändern | **alle** Texte der Pakete P2b–P4d (Präfixe `Pe*`, `Crop*`, `Ver*`, `Trash*`, `Split*`); Wortlaut aus „Übernehmen“ der freigegebenen Entwürfe 14–16 und S0, Rest wie unten in den Paketen genannt | – | +90 je Datei |
| `UI/Viewer/ViewerPages.cs` | ändern | `OpenAsync(…, DocumentDetailDto? detail = null)` überschreibt die Antwort von `/api/documents/{id}` (für P2d/P3c) | bestehende | ±8 |

- **Akzeptanz:** Build ohne Warnungen, alle UI-Tests grün.

---

## Paket P1 – Bearbeitung im Worker (Spur A) · Modell: **Opus** · Effort: `high`

**Aus dem P0-Review (2026-11-23), Pflicht in P1:**
- **Atomare Fassungen:** `edited.pdf`/`archive.pdf` nicht vor dem DB-Commit an festen Namen überschreiben. Neue Datei
  in einen Staging-Pfad (`*.pending` bzw. `edits/<id>/<editId>/`) schreiben, nach dem Commit idempotent umbenennen;
  die vorige bearbeitete Fassung **vorher** per `RemoveFile` + `TrashItem` (Commit) und danach `MoveToTrashAsync` in den
  Papierkorb (ADR-0025). Scheitert der Commit, bleiben Platte und Hash konsistent.
- **Seitenbilder je Bearbeitung:** `EditPageStorage` legt Bilder unter `edits/<id>/<editId>/page-<n>.jpg` ab
  (`editId` in `document_edits`), oder `RequestPageEdit` prüft `EnqueueEditAsync` **vor** dem Speichern der Bilder – ein
  abgelehnter zweiter Auftrag (409) darf die Bilder des laufenden nicht überschreiben.
- Runner für `Edit` registrieren; der fail-soft-Pfad aus P0 (Stufe ohne Runner) bleibt als Absicherung.
- **Aufgegebener `Edit`-Auftrag:** `JobQueue.GiveUpAsync` löscht bei Stufe `Edit` in derselben Transaktion die Zeile in
  `document_edits` und verwirft die Bearbeitungsseiten (`edits/<id>/<editId>/`); bei `recognize_after` legt es wieder einen
  `Recognition`-Auftrag an. IT-Test „Edit nach MaxAttempts → `EditPending` false, Erkennung wieder geplant“.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `App/Editing/RequestPageEdit.cs` | neu | `DocumentChangeGuard.LoadAsync(…, d => d.CanEdit(current.Id))`, `State == Done`; `PagePlan.Validate(PageCount)`; ersetzte Seiten (JPEG über `FileSignature`, ≤ 15 MB, ≤ 50) über `IEditPageStorage`; `IDocumentEdits.SaveAsync` + `EnqueueEditAsync` in der Transaktion der Zeilensperre; `false` → `Conflict` | AT | 140 |
| `App/Editing/AppendDocument.cs`, `IDocumentFieldsStore.FindVisiblePairAsync` | neu/ändern | beide sperren (**kleinere Id zuerst**), `AppendRule.Check`; Ergebnis privat → `MakePrivate` sofort; Auftrag `Append` | AT + IT (Sperrreihenfolge) | 140 |
| `App/Editing/SplitDocument.cs` | neu | implementiert `ISplitRequester`; `CanEdit`, `SplitPlan.Validate`; Auftrag `Split` | AT | 100 |
| `App/Editing/ChooseVersions.cs` | neu | nur Besitzer; `EditedOnly` → Original in den Papierkorb (`RemoveFile` + `MoveToTrashAsync` + `TrashItem`); `OriginalOnly` → Fassung in den Papierkorb + Auftrag `Rebuild`; `KeepBoth` nur Wert | AT | 120 |
| `App/Editing/GetDocumentPages.cs` | neu | Sichtbarkeit, `CanEdit`, `EditPending`, Seiten aus `IDocumentPages` | AT | 40 |
| `App/Editing/EditStageRunner.cs`, `EditSources.cs` | neu | `IStageRunner` (keyed `Edit`): `DocumentEdit` lesen; Arbeitsordner (`IWorkingFolder`); Pages: `CurrentSource` + Plan (ersetzte Seite: JPEG → `IPdfComposer.ComposeImagesAsync`) → `IPdfTool.ComposeAsync`; Rebuild: nur Quelle; → OCR **ohne** `--rotate-pages`/`--deskew` (neuer Parameter an `IOcrEngine.CreateArchiveAsync`, die Wahl des Nutzers gilt) → `SaveEditedAsync` + `SaveArchiveAsync` → Commit (`CompleteEditAsync`) → Versionsregel → Bearbeitungsseiten verwerfen; Fehler → `FailAsync`/`RetryPolicy`, danach bleibt die alte Fassung | WT (Fakes) + IT (Linux) | 200 + 60 |
| `App/Editing/AppendStep.cs`, `SplitStep.cs` | neu | Append: Ziel + Angehängtes → Compose → wie Pages; Angehängtes in den Papierkorb im selben Commit. Split: je Teil Compose → `IntakeDocument.AcceptAsync` (Besitzer, Privat, Quelle wie Ausgang, `DuplicateScope` ohne Abbruch) → Bearbeiter kopieren; Ausgang in den Papierkorb erst nach allen Teilen | AT | 2 × 150 |
| `Inf/Processing/JobQueue.Edit.cs` | neu | `CompleteEditAsync(claim, text, edited, archive, pages, ct)`: Sperre Auftrag + Dokument (Reihenfolge Dokument, dann Auftrag wie `JobQueue.Recognition`), `ReplaceFile`, `ReplaceContent`, `EditedAt`, Seiten ersetzen, `document_edits` löschen, Auftrag → `Recognition` wenn `recognize_after`, sonst löschen | IT | 120 |
| `App/Editing/EditingLog.cs` | neu | 5500–5515 | – | 60 |
| `Srv/Editing/EditingEndpoints.cs`, `EditingLog.cs` | neu | `GET …/pages`, `POST …/edit` (multipart), `POST …/append`, `POST …/split`, `PUT …/versions`; 202/400/403/404/409; 1600–1612; `Srv/Program.cs` eine Zeile | ST | 200 + 40 |
| `Wrk/Composition/WorkerComposition.cs` | ändern (Hotspot) | nichts außer Logs, falls nötig (Runner kommt über Application) | – | 2 |
| Nachlauf Seitentexte (nur bei F1 = ja) | neu | `EnqueuePageBacklogAsync` (Muster `EnqueueRecognitionBacklogAsync`), `EditKind.Analyze` ohne OCR, Priorität niedrig | IT | 80 |
| Tests | neu | Rechte, 409 (Auftrag läuft), Privat-Regel, Sperrreihenfolge (Postgres, zwei Anfragen gegenläufig), qpdf echt mit 3-Seiten-PDF (drehen, löschen, umsortieren, ersetzte Seite, anhängen, teilen, Neubau) inkl. Seitenzahl und Text; Abbruch zwischen Speichern und Commit lässt die alte Fassung gültig | selbst | ~800 |

- **Telemetrie:** 1600–1612, 2400–2405, 5500–5515.
- **Akzeptanz:** alle Tests grün; qpdf/ocrmypdf-Tests in der CI (Linux) grün.

---

## Spur B – „Seiten bearbeiten“ (Entwurf 14 v3)

Branch `feat/m04b-b-seiten`. Pakete nacheinander: P2a → P2b → P2c. **Exklusiv:** `WC/Editing/PagePlanModel.cs`,
`WC/Editing/PageEditor*`, `WC/Editing/PageCard.razor`, `WC/Editing/AppendSection.razor`, `WC/Editing/Crop*`,
`WC/Pages/PageEditor*`, `WC/Viewer/Thumbnail.razor`, `ThumbnailRenderer.cs`, `wwwroot/js/viewer/thumbnails.js`,
`wwwroot/js/editing/*`, `wwwroot/js/camera/capture.js` (nur `export`), `page-editor.css`, `page-crop.css`, `UI/Editing/*`,
`WCT/Editing/*`.

### Paket P2a – Seitenplan im Client · Modell: **Sonnet** · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `WC/Editing/PageEntry.cs` | neu | `record PageEntry(int SourcePage, PageRotationDto Rotation, bool Deleted, string? ReplacedJpegKey, string? CropText)` (Web.Client kennt nur Contracts) | – | 15 |
| `WC/Editing/PagePlanModel.cs` | neu | Zustand: `IReadOnlyList<PageEntry> Entries`, `IReadOnlySet<int> SplitsAfter` (Index), Rückgängig-Stapel; `RotateRight/Left(i)`, `ToggleDelete(i)`, `MoveUp/Down(i)`, `ToggleSplitAfter(i)`, `Replace(i, key, text)`, `ClearCrop(i)`, `Undo()`; `IsDirty`; `NumberOf(i)` (Nummer unter den nicht gelöschten), `ToPlan()` → `PagePlanDto` bzw. `ToSplit()` → `SplitPlanDto` (Teile an Trennstellen); `FromSuggestion(SplitSuggestionDto)` (Trennstellen gesetzt, leere Seiten gelöscht) | `WCT/Editing/PagePlanModelTests.cs` | 180 |
| `WCT/Editing/PagePlanModelTests.cs` | neu | jede Methode; Undo über alle Arten; letzte Seite ohne Trennstelle; alle gelöscht → `ToPlan` wirft nicht, `CanSave` false; Vorschlag → Plan | selbst | 170 |

### Paket P2b – Seite „Seiten bearbeiten“ · Modell: **Sonnet** · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `WC/Pages/PageEditor.razor` + `.razor.cs` | neu | `@page "/dokumente/{Id:guid}/seiten"`, Query `trennen`; `<Breadcrumb>` „Dokumente › Titel (Link Viewer) › Seiten bearbeiten“; H1 `Typo.h2 HtmlTag="h1"` „Seiten bearbeiten – {Titel}“; Knopfleiste `div.pe-actions` (kein `header`): „Speichern“ `nmp-button nmp-button-primary` (`disabled` ohne Änderung), „Alles verwerfen“, „Dokument anhängen“ (`nmp-button`); Satz `PeSaveExplain`. Zustände: lädt (`p role=status`), Fehler (`NoticeBox Announce` + Retry), `!CanEdit` → `NoticeBox` „Du darfst dieses Dokument nicht bearbeiten.“, `EditPending` → `NoticeBox` „Wird gerade bearbeitet …“ + Link Viewer, nach Speichern → `NoticeBox Success` + Link (Viewer bzw. `zu-pruefen`), Liste schreibgeschützt. Liste `<ol class="pe-pages">` mit `PageCard` und Trennstellen-`li`. „Alles verwerfen“ → `ConfirmBox Id="pe-discard"` (neutral). `NavigationLock` nur bei F2 = ja. `LiveRegion` für jede Aktion, `PendingFocus` für Fokus | UI | 130 + 200 |
| `WC/Editing/PageCard.razor` | neu | `<li @key="entry.SourcePage">`, `<article class="pe-card" aria-labelledby="pe-h-{n}">` (kein `section aria-label`); links `Thumbnail` (Seite n, Breite 240, `aria-hidden`, gedreht per Klasse `pe-rot-90|180|270`), rechts H2 „Seite x von y“ (`tabindex=-1`), „Beginnt mit: …“ (nicht bei vermutlich leer), `<Badge Icon=… Text="Vermutlich leer" />`, Zustandszeile (gedreht/zugeschnitten/verschoben, Symbol `aria-hidden`), Knopfgruppe `role="group" aria-label="Seite x bearbeiten"`: „Nach links drehen“, „Nach rechts drehen“, „Nach oben“ (nicht Seite 1), „Nach unten“ (nicht letzte), „Zuschneiden“, „Ränder und Schräglage automatisch“, „Seite löschen“, „Hier teilen“ (nicht letzte) – sichtbarer Text, Symbole `aria-hidden`. Gelöschte Seite: H2 „Gelöschte Seite (vorher Seite n)“ + „Löschen rückgängig“, keine Vorschau. Tasten auf der Karte (`@onkeydown`, nur wenn Fokus in der Karte): `R`/`Umschalt+R` drehen, `Entf` löschen/rückgängig, `Alt+↑/↓` verschieben, `T` Trennstelle, `Z` zuschneiden; `Strg+Z` seitenweit. Hinweis `p#pe-keys` (`aria-describedby` der Gruppe) | UI | 180 |
| `WC/Editing/AppendSection.razor` | neu | `section aria-labelledby` H2 „Dokument anhängen“; mit ungespeicherten Änderungen nur `NoticeBox` „Speichere oder verwirf zuerst deine Änderungen.“; sonst `FormField` Suche (`DocumentApi.SearchAsync`, eigenes Dokument ausgeblendet), Treffer `ul` mit Knöpfen „Anhängen: {Titel}“ (+ `PrivateBadge`); Wahl → `ConfirmBox Id="pe-append-{id}" @key=id` Titel „„{B}“ an „{A}“ anhängen?“, Text „Die Seiten kommen ans Ende. „{B}“ kommt danach in den Papierkorb.“ + bei B privat, A geteilt: „„{B}“ ist privat. Das Ergebnis wird deshalb privat.“; neutral; 409/403 → `NoticeBox Announce` | UI | 140 |
| `WC/Editing/PageEditorLog.cs` | neu | 4300 Seite geladen (`{Pages}`), 4301 gespeichert (`{Steps}`, `{Splits}`), 4302 Anhängen gesendet, 4303 Fehler (`{Status}`) | – | 30 |
| `WC/Viewer/Thumbnail.razor`, `ThumbnailRenderer.cs`, `wwwroot/js/viewer/thumbnails.js` | ändern | Parameter `Page` (Standard 1) und `CssWidth` (Standard 80); `render(url, width, mode, page)`; Cache-Schlüssel mit Seite | `UI/Viewer/ThumbnailTests` grün + neuer Fall Seite 2 | ±25 |
| `WC/wwwroot/css/page-editor.css` | ändern | Werte aus 14: Liste ohne Punkte, `gap: var(--nmp-space-lg)`, `max-width: 64rem`; `.pe-card` `--nmp-surface-container`, `--nmp-radius-lg`, `padding: var(--nmp-space-lg)`, ab 48 rem zwei Spalten (Vorschau 15 rem), darunter gestapelt; Vorschau-Fläche `--nmp-canvas-lowest`, quadratisch, Drehung per `transform`; Knopfgruppe `flex-wrap`, Knöpfe Höhe nicht fest (Umbruch); Trennstelle `--nmp-paper-low`, gestrichelte Linie `--nmp-border-strong`; gelöschte Karte niedrig; < 250 Zeilen | – | 150 |
| `UI/Editing/FakePageEditor.cs` | neu | Muster `UI/Classification/FakeReview.cs`: `FakeAccounts.InstallAsync(page, signedIn: true)`, Routen `GET /api/documents/{id}`, `…/pages`, `…/files/archive` (3-Seiten-PDF aus neuem `ViewerPdfs.ThreePages()`), `POST …/edit|split|append` zeichnen den Körper auf; Status je Aufruf einstellbar | – | 110 |
| `UI/Editing/PageEditorTests.cs`, `PageEditorKeyboardTests.cs`, `PageEditorAccessibilityTests.cs` | neu | Randseiten ohne Knöpfe; drehen/verschieben/löschen per Knopf und Taste mit Ansage im `LiveRegion` und Fokus (nach „Nach oben“ bleibt Fokus auf „Nach oben“ der verschobenen Karte, auf Seite 1 auf deren H2); Undo; „Speichern“ sendet richtigen `PagePlanDto` bzw. `SplitPlanDto`; 409 → Fehlertext; „Alles verwerfen“ mit Rückfrage (Fokus Überschrift, Escape); `?trennen=1` setzt Trennstellen aus dem Vorschlag; Anhängen mit Privat-Hinweis; axe DE/EN: Normal, gelöscht+Trennstelle, Anhängen offen, nach Speichern; 375 px ohne waagerechtes Scrollen | selbst | 3 × 180 |

- **Telemetrie:** 4300–4303. **Akzeptanz:** `--filter-namespace NoMorePaper.UI.Tests.Editing` und `WCT` grün; Viewer-Tests
  grün (`Thumbnail` geändert); Tab-Reihenfolge: Pfad → Knopfleiste → Karten in Lesereihenfolge.

### Paket P2c – Zuschnitt und Geraderichten · Modell: **Opus** · Effort: `high`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `wwwroot/js/camera/capture.js` | ändern | `warp` exportieren (sonst unverändert) | Kamera-Tests grün | 1 |
| `wwwroot/js/editing/crop.js` | neu | Seite mit PDF.js (`viewer/pdf-document.js`) auf 200 dpi, `findQuad` (`camera/edges.js`) + Schräglage (`minAreaRect`) → Ecken; ohne Fund Seitenecken; `warp` → JPEG mit `toJfif`; Mats freigeben; Ergebnis Winkel (Grad) und entfernte Ränder (cm je Seite) | UI (Standbild) | 180 |
| `WC/Editing/CropInterop.cs` | neu | Muster `Camera/CameraInterop.cs`: `DetectAsync(url, page)`, `ApplyAsync(corners)` → Bytes, `DisposeAsync` | – | 100 |
| `WC/Editing/PageCropPanel.razor` | neu | **in der Karte** (kein Dialog): Abzeichen „Zuschnitt aktiv“, Satz „Die Seite wird als Bild ersetzt und neu erkannt.“; vier **fokussierbare** Knöpfe „Ecke oben links“ … mit Position in `aria-label`, Pfeiltasten 1 %, mit Umschalt 0,2 %, jede Bewegung angesagt („Ecke oben links: 3 % von links, 2 % von oben“); Maus ziehen zusätzlich; Rahmenfarbe `--nmp-primary` (kein Inline-`rgba`); „Zuschnitt übernehmen“ / „Abbrechen“ (Fokus zurück auf „Zuschneiden“). „Ränder und Schräglage automatisch“ wendet direkt an und sagt „Um 3 Grad gedreht, Rand oben 2 cm entfernt“ (Zeile mit Häkchen, monochrom); nichts gefunden → „Kein Blattrand gefunden. Ecken von Hand setzen?“ | UI | 200 |
| `WC/wwwroot/css/page-crop.css` | ändern | Rahmen, Griffe 2,75 rem, Fokus wie Design-System (kein eigener Ring) | – | 80 |
| `UI/Editing/PageCropTests.cs` + Test-PDF schiefes Blatt (`ViewerPdfs`) | neu | automatisch: Ansage enthält Grad; Ecken per Taste mit Ansage; Speichern sendet `page-<n>` als JPEG mit JFIF-Kopf und `Replaced = true`; axe DE/EN im Zuschnitt-Zustand | selbst | 180 |

- **Telemetrie:** 4310 Zuschnitt (`{ElapsedMs}`, gefunden ja/nein), 4311 Zuschnitt fehlgeschlagen (`{Reason}`).
- **Akzeptanz:** Namespace `Editing` + `Camera` grün; Speicher: Mats nach 20 Zuschnitten freigegeben (Test zählt offene Mats).

## Spur V – Fassungen und Werkswert (Entwurf 16 v3, 09) · Paket P2d · Modell: **Sonnet** · Effort: `medium`

Branch `feat/m04b-v-fassungen`. **Exklusiv:** `WC/Editing/VersionsSection*`, `WC/Editing/VersionsLog.cs`,
`WC/Accounts/AfterEditingCard.razor`, `WC/Pages/Account.razor`, `WC/Pages/DocumentViewer.razor.cs`, `versions.css`,
`account-page.css`, `UI/Viewer/VersionsTests.cs`, `UI/Accounts/AfterEditingTests.cs`. **Hotspot:** `DocumentViewer.razor`.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `WC/Editing/VersionsSection.razor` + `.razor.cs` | neu | im Reiter „Angaben“ unter `DocumentFieldsPanel`: `<section class="versions" aria-labelledby="versions-heading">` H2 „Fassungen“; `ul`: je Eintrag Art („Original“/„Bearbeitet“), Datum (`ReceivedAt` bzw. `EditedAt`, Mono), Zustand „Aktiv – wird angezeigt“ als Symbol `aria-hidden` + Text bei der aktuellen Quelle, Link „Herunterladen: Original/Bearbeitete Fassung“; ohne Bearbeitung nur Original + Satz „Noch keine bearbeitete Fassung.“ und **keine** Wahl. Besitzer mit Fassung: `fieldset`/`legend` „Behalten“, drei Radios mit Erklärzeile (`aria-describedby`), „Auswahl speichern“ primär; bei Wechsel auf „Nur bearbeitete Fassung“/„Nur Original“ `ConfirmBox Id="versions-confirm" @key=policy` („Original in den Papierkorb legen?“ bzw. „Bearbeitete Fassung in den Papierkorb legen?“, Text aus 16, neutral). Bearbeiter: Liste ohne Wahl. Hinweis mit Symbol „Nicht behaltene Fassungen kommen 30 Tage in den Papierkorb.“ Link „Seiten bearbeiten“ (`nmp-button`) bei `CanEdit` (aus `DocumentFieldsDto`), `Done`, `!EditPending`. `EditPending` → `StatusText`-artige Zeile „Wird bearbeitet“ und Nachfragen alle 5 s (`DocumentApi.GetAsync`, höchstens 10 min) → `OnEditFinished`. Erfolg/Fehler über `LiveRegion` bzw. `NoticeBox Announce` | UI | 120 + 150 |
| `WC/Pages/DocumentViewer.razor` | ändern (Hotspot) | eine Zeile `<VersionsSection Detail="_detail" Fields="_fields" OnEditFinished="ReloadAsync" />` im Panel `doc-fields-panel` nach `DocumentFieldsPanel` | – | 1 |
| `WC/Pages/DocumentViewer.razor.cs` | ändern | `ReloadAsync()`: `_loadedId = Guid.Empty`, `OnParametersSetAsync`, `StateHasChanged`, danach Ansage „Die neue Fassung ist fertig.“ über `_announcer`; Datei bleibt < 250 Zeilen (sonst `DocumentViewer.Load.cs` abtrennen) | UI | +12 |
| `WC/Accounts/AfterEditingCard.razor` | neu | Karte `section.account-card` nach S0-Entwurf 09: H2 „Bearbeitete Dokumente“, `fieldset` „Nach dem Bearbeiten behalten“ (drei Radios + Erklärzeilen), „Speichern“; Laden/Fehler wie `PreferencesSection` | UI | 100 |
| `WC/Pages/Account.razor` | ändern | `<AfterEditingCard />` nach `<PreferencesSection />` (Lesereihenfolge F1 aus M4u bleibt) | `AccountPageTests` grün | 1 |
| `WC/Editing/VersionsLog.cs` | neu | 4350 Wahl gespeichert (`{Policy}`), 4351 Fehler (`{Status}`), 4352 Bearbeitung fertig erkannt | – | 25 |
| `WC/wwwroot/css/versions.css`, `account-page.css` | ändern | Liste ohne Punkte, Einträge `--nmp-paper-low`, Radius; Radios 1,125 rem; Bestätigungsknopf ohne feste Höhe (Text bricht um) | – | 80 + 10 |
| `UI/Viewer/VersionsTests.cs`, `UI/Accounts/AfterEditingTests.cs` | neu | über `ViewerPages.OpenAsync(detail: …)`: Liste, Downloads (Original fehlt → kein Link im Kopf), Wahl + Rückfrage (Fokus Überschrift, Escape, neutral), PUT-Körper, Bearbeiter ohne Wahl, „Wird bearbeitet“ → Nachfragen → Ansage; Konto: Laden, Speichern, Fehler; axe DE/EN je Zustand | selbst | 200 + 100 |

- **Telemetrie:** 4350–4352. **Akzeptanz:** Namespaces `Viewer`, `Accounts` grün (bestehende Reiter-/Layouttests unverändert).

## Spur C – Papierkorb

Branch `feat/m04b-c-papierkorb`. Pakete P3a → P3b → P3c. **Exklusiv:** `App/Trash/*` (außer P0-Dateien), `Srv/Trash/*`,
`Wrk/Trash/*`, `WC/Trash/*`, `WC/Pages/Trash*`, `WC/Pages/Settings.razor`, `WC/Layout/NavSection.cs`, `trash.css`, `UI/Trash/*`.
**Hotspot:** `DocumentViewer.razor` (eine Zeile).

### Paket P3a – Anwendungsfälle und Endpunkte · Modell: **Opus** · Effort: `high`

**Aus dem P0-Review (2026-11-23), Pflicht in P3a:**
- **Wiederherstellen vor/während der Verarbeitung:** Ein Dokument, das im Zustand `Queued`/`Processing` in den
  Papierkorb kam, hat keinen Auftrag mehr. `RestoreTrashItem` legt in diesem Fall einen neuen OCR-Auftrag an.
- **Eine Transaktion je Scope:** `EfDocumentFieldsStore.FindVisibleAsync` (`_change ??= BeginTransactionAsync`) wirft,
  wenn `EfTrashStore` schon eine Transaktion geöffnet hat – beide nutzen `db.Database.CurrentTransaction` mit.
- **Endgültiges Löschen eines Dokuments:** Die Cascade entfernt auch dessen `Original`/`Edited`-Papierkorbeinträge; deren
  Ordner `trash/<entryId>/` vorher über `PurgeTrash` für alle Einträge des Dokuments löschen.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `App/Trash/ListTrash.cs`, `RestoreTrashItem.cs`, `PurgeTrashItem.cs`, `EmptyTrash.cs` | neu | nur Besitzer (über `documents.owner_id`); Dokument → `RestoreFromTrash`; Datei → zurück an ihren Platz; Platz belegt → vorhandene Fassung in den Papierkorb (Tausch); wiederhergestellte `Edited` → Auftrag `Rebuild`; Original zurück bei Wert `EditedOnly` → Wert `KeepBoth`; Datei eines Dokuments im Papierkorb → 409; endgültig: Zeile + Dateien (Zeile zuerst, Dateireste geloggt wie `DeleteDuplicate`) | AT | 4 × 90 |
| `App/Trash/PurgeDueTrash.cs` | neu | `DueAsync(now, 200)`; je Eintrag wie `PurgeTrashItem` ohne Besitzerprüfung | AT | 80 |
| `Srv/Trash/TrashEndpoints.cs`, `TrashLog.cs` | neu | `GET /api/trash`, `POST /api/trash/{id}/restore`, `DELETE /api/trash/{id}`, `DELETE /api/trash`, `POST /api/documents/{id}/trash`; 1700–1706; `Srv/Program.cs` eine Zeile | ST | 140 + 30 |
| Tests | neu | Postgres: Tausch, Rebuild-Auftrag, endgültig löscht Dateien, Fremde sehen nichts, nach Übergabe gehört der Eintrag dem neuen Besitzer | selbst | ~400 |

### Paket P3b – Aufräumdienst · Modell: **Sonnet** · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Wrk/Trash/TrashPurger.cs`, `TrashPurgerLog.cs` | neu | `BackgroundService` nach `Wrk/Inputs/InputScheduler.cs`: beim Start und alle 24 h (`PeriodicTimer` mit `TimeProvider`), eigener Scope, `PurgeDueTrash`, Fehler beenden den Host nicht; 2500–2503 | `WT/Trash/TrashPurgerTests.cs` (`ManualTimeProvider`) | 90 + 25 |
| `Wrk/Composition/WorkerComposition.cs` | ändern (Hotspot) | `AddHostedService<TrashPurger>()` | – | 1 |

### Paket P3c – Seite „Papierkorb“, Löschen im Viewer · Modell: **Sonnet** · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `WC/Pages/Trash.razor` + `.razor.cs` | neu | `@page "/papierkorb"`; `<Breadcrumb>` „Einstellungen › Papierkorb“ (Muster `Pages/Lists.razor`); H1 `Typo.h2 HtmlTag="h1"` „Papierkorb“; Einleitung aus 15; `section aria-labelledby` H2 „Gelöschte Einträge (n)“; ab 48 rem `div.trash-table-scroll role=region tabindex=0 aria-labelledby` (Muster `Documents/DocumentTable.razor`) mit `table` (`caption` sichtbar oder `nmp-visually-hidden`, `th scope=col`: Titel, Was, Gelöscht am, Endgültig gelöscht am, Aktionen; `th scope=row` Titel, nur Text, `overflow-wrap:anywhere`); darunter `ul.trash-cards` mit `article` je Eintrag (H3 Titel, `dl`). Knöpfe mit sichtbarem Text, `aria-label` „{Titel} wiederherstellen“ / „{Titel} endgültig löschen“; Rückfrage **unter der Zeile** bzw. im Artikel: `ConfirmBox Id="trash-confirm-{id}" @key=id` („„{Titel}“ endgültig löschen?“, „Das kann nicht rückgängig gemacht werden.“, „Ja, endgültig löschen“ als `PrimaryConfirm`), Tausch-Rückfrage bei `RestoreSwaps`; „Papierkorb leeren“ mit `ConfirmBox Id="trash-empty"` („{n} Einträge werden endgültig gelöscht …“, „Ja, leeren“ `PrimaryConfirm`). Fokus nach Aktion: nächste Zeile (gleicher Knopf), sonst H2; nach Abbrechen auf den Auslöser (macht `ConfirmBox`). Leer: ruhige Fläche mit Symbol `aria-hidden` + „Der Papierkorb ist leer.“; Rückfrage, Liste und Leerzustand nie gleichzeitig. Zeiten „g“-Format | UI | 140 + 180 |
| `WC/Trash/MoveToTrashButton.razor` | neu | im Viewer-Kopf (`doc-view-actions`) nur bei `IsMine`: Knopf „In den Papierkorb“ (`doc-view-action`, Symbol `aria-hidden`), `ConfirmBox Id="viewer-trash"` („„{Titel}“ in den Papierkorb legen?“, „Es bleibt 30 Tage im Papierkorb und lässt sich bis dahin wiederherstellen.“, neutral). Danach **kein** Seitenwechsel (eine Ansage vor `NavigateTo` ginge verloren, die Dokumentliste hat keinen Platz dafür): `div tabindex=-1` mit `NoticeBox Kind=Success` „„{Titel}“ liegt jetzt im Papierkorb.“ + Link „Zur Dokumentliste“, Fokus darauf; Knopf verschwindet | UI | 90 |
| `WC/Pages/DocumentViewer.razor` | ändern (Hotspot) | eine Zeile `<MoveToTrashButton Detail="_detail" />` in `doc-view-actions` | – | 1 |
| `WC/Pages/Settings.razor` | ändern | Kachel `@Tile("papierkorb", Icons.Material.Outlined.Delete, L["NavTrash"], L["SettingsTrashText"])` für alle Konten, nach „Listen“ | `UI/Layout/NavigationTests` | 1 |
| `WC/Layout/NavSection.cs` | ändern | `"papierkorb"` zu `NavItem.Settings` | `WCT/Layout/*` | 1 |
| `WC/Trash/TrashLog.cs` | neu | 4800 geladen (`{Count}`), 4801 wiederhergestellt, 4802 endgültig gelöscht, 4803 geleert (`{Count}`), 4804 in den Papierkorb gelegt, 4805 Fehler (`{Status}`) | – | 40 |
| `WC/wwwroot/css/trash.css` | ändern | `table-layout: fixed`, Spaltenbreiten; Knöpfe neutral (`surface-container`), Text immer sichtbar; Umschaltung Tabelle/Liste bei 48 rem; Leerfläche; < 250 Zeilen | – | 120 |
| `UI/Trash/FakeTrash.cs`, `TrashPageTests.cs`, `TrashAccessibilityTests.cs`, `UI/Viewer/MoveToTrashTests.cs` | neu | Muster `FakeReview`; Liste, Wiederherstellen (Ansage, Fokus nächste Zeile), Tausch-Rückfrage, endgültig/leeren (Rückfrage, Escape, `@key` beim Wechsel der Zeile), leer, Fehler; 375 px Listenansicht; axe DE/EN (Liste, Rückfrage offen, leer, schmal); Viewer: Knopf nur für Besitzer, Rückfrage, Erfolg; Navigation: `/papierkorb` markiert „Einstellungen“ | selbst | 4 × 150 |

- **Telemetrie:** 1700–1706, 2500–2503, 4800–4805, 5610–5625.
- **Akzeptanz:** AT/IT/ST/WT grün; `--filter-namespace NoMorePaper.UI.Tests.Trash` + `Viewer` + `Layout` grün.

## Spur D – Stapel-Trennung

Branch `feat/m04b-d-trennung`. Pakete P4a → P4b → P4c → P4d. **Exklusiv:** `App/Separation/*`, `Inf/Recognition/*Separation*`,
`Inf/Processing/JobQueue.cs` (nur `CompleteAsync`-Weiche), `Srv/Editing/SplitSuggestionEndpoints.cs`,
`WC/Classification/SplitSuggestionSection.razor`, `WC/Pages/Review.razor(.cs)`, `split-suggestion.css`,
`UI/Classification/SplitSuggestionTests.cs`, `IT/Separation/*`.

| Paket | Modell / Effort | Dateien und Inhalt | Tests |
|---|---|---|---|
| **P4a** Heuristik + Testsatz | **Sonnet** / `medium` | `App/Separation/SeparationHeuristics.cs` (rein, ~150 Z.): aus `PageInfo` Grenzen – leere Seite (Grenze danach, Seite auslassen), „Seite 1 von n“/„1/3“/„Page 1 of n“ (Regex), Anrede („Sehr geehrte“, „Dear“) nur am Seitenanfang; `SeparationCandidates.cs`; `tests/…/Separation/Samples/*.json` + `expected.json` (12 selbst erzeugte Stapel als Seitentexte) | AT: jede Regel, Testsatz ohne LLM |
| **P4b** LLM-Prüfung | **Sonnet** / `medium` | `App/Separation/ISeparationAssessor.cs`; `Inf/Recognition/OllamaSeparationAssessor.cs`, `SeparationPrompt.cs`, `SeparationSchema.cs` nach `OllamaMailAssessor`/`MailPrompt`/`MailSchema`: über `LlmGate` mit **`LlmPriority.Recognition`** (hält eine Auftragssperre wie die Erkennung), Seitenanfänge ≤ 300 Zeichen, max. 30 Seiten, Schema `{boundaries:[int], senders:[string]}`, Plausibilität (Grenzen im Bereich); Logs 3990–3993; Registrierung in `Inf/Composition/RecognitionRegistration.cs` | IT mit `FakeOllamaHandler`; `SeparationLiveTests.cs` nur mit erreichbarem Ollama |
| **P4c** Stufe + Antwort | **Opus** / `high` | `App/Separation/SeparationStageRunner.cs` (keyed `Separation`): nur Seiten > 1 und keine Seite `HadTextLayer`; Heuristik + LLM → `Document.SuggestSplit` (nur ≥ 2 Teile oder leere Seiten); danach Stufe `Recognition`; `LlmBusyException` → Auftrag zurück ohne Fehlversuch; `Inf/Processing/JobQueue.cs` `CompleteAsync`: mehrseitiger Scan → `Separation`, sonst `Recognition`; `App/Separation/AnswerSplitSuggestion.cs` (annehmen → `SplitSuggestion.ToPlan()` → `ISplitRequester`; verwerfen → `DismissSplit`); `ReviewFor` in `Inf/Classification/EfDocumentFieldsStore.cs` + Grund `SplitSuggestion != null`, `ReviewItem`/Mapping; `Srv/Editing/SplitSuggestionEndpoints.cs` (`POST …/split-suggestion/accept|dismiss`, 1650–1653); Logs 2600–2605, 5700–5706 | AT, IT (Postgres: Stufenfolge, Gate), ST |
| **P4d** UI „Zu prüfen“ | **Sonnet** / `medium` | `WC/Classification/SplitSuggestionSection.razor` nach `OwnerCheckSection` (eigene API-Aufrufe, `OnAnswered`): `section aria-labelledby` H3 „Trennvorschlag“, Satz „Trennvorschlag: 3 Dokumente (Seiten 1–2, 3, 4–6), Seite 5 vermutlich leer“, `ul` der Teile, Knöpfe „Teilen“ (`nmp-button nmp-button-primary`), „Anpassen“ (Link `DocumentLinks.PageEditor(id, split: true)`, `nmp-button`), „Nicht teilen“ (`nmp-button`); „Teilen“ mit `ConfirmBox Id="review-split-{key}" @key` neutral; `Review.razor` eine Zeile nach `OwnerCheckSection`, `Review.razor.cs` ein Einzeiler `SplitAnsweredAsync` über `DoneAsync` (Datei hat 218 Z.); `split-suggestion.css`; Logs 4900–4903 | `UI/Classification/SplitSuggestionTests.cs` mit `FakeReview` (+ `Split`): Text, Teilen/Anpassen/Nicht teilen, Rückfrage, axe DE/EN |

- **Akzeptanz:** Tests grün; Messung in `docs/kb/scan-bearbeitung.md` (Ziel ≥ 80 % der Grenzen; ohne Bestätigung keine Trennung).

---

## Paket P5 – Doku, Katalog, Häkchen · Modell: **Haiku** · Effort: `low`

Nach dem Merge aller Spuren. `docs/kb/telemetrie.md` (Blöcke oben in „EventId-Bereiche“, Zeilen je Ereignis aus den
`*Log.cs`), `docs/kb/scan-bearbeitung.md` (neu: Ablauf, Fassungen, Papierkorb, Werkzeuge, Messwerte), `docs/kb/verarbeitung.md`
(Stufen `Edit`, `Separation`, Weiche `IStageRunner`), `docs/kb/oberflaeche.md` (Abschnitte „Seiten bearbeiten“, „Papierkorb“,
„Fassungen“ mit den Abweichungen von den Notizen), `docs/ui/stitch/README.md` (14–16 „umgesetzt“ nach P6), Häkchen in
`m4b-scan-bearbeitung.md`.

## Paket P6 – Review und Abnahme · Modell: **Opus** · Effort: `high` (steuernde Sitzung)

- Je Spur ein Review per Subagent, dann Gesamt-Review §8 bis ohne Befund; Prüfliste: jede Zeile „Abgleich“/„Nicht mitnehmen“
  der Notizen 14–16 und der S0-Notizen.
- Abnahme UI-Prozess Schritt 8: Playwright-Bilder (1440 px, 375 px) neben `screen.png` in `TestResults/`; Zoom 200 %/400 %;
  Tastaturweg je Seite; NVDA „Seiten bearbeiten“ und „Papierkorb“ durch den Nutzer. Merge nach `develop` nur mit Freigabe (§8.5).

## Risiken

- **Globaler Abfragefilter:** Jeder `db.Documents`-Zugriff sieht Papierkorb-Dokumente nicht mehr; `JobQueue.FindDocumentAsync`
  (`SingleAsync`) würde werfen – deshalb löscht `MoveDocumentToTrash` den Auftrag, und der Claim überspringt Papierkorb-Dokumente.
  Papierkorb-Fälle nutzen nur `ITrashStore` (`IgnoreQueryFilters`).
- **`DocumentDetailDto.Original` nullbar:** betrifft Viewer-Kopf, `SourceOf`, Server-Mapping und Tests – in P0 mitgezogen.
- **OCR nach Bearbeitung:** `--rotate-pages`/`--deskew` würden die Wahl des Nutzers „korrigieren“ → in der Stufe `Edit` aus.
  `--skip-text` bei gedrehten Seiten mit Textebene: ⚠️ im qpdf-Test prüfen, sonst `--redo-ocr`.
- **Dateigrößen nahe 250:** `DocumentViewer.razor.cs` (210), `Review.razor.cs` (218), `DocumentApi.cs` (202),
  `ClassificationApi.cs` (205), `document-view.css` (235), `capture.css` (196) – neue Logik in eigene Dateien, nichts anhängen.
- **Viewer-Tests:** Abschnitt „Fassungen“ im Reiter „Angaben“ bringt eine H2 und Knöpfe dazu; Tests, die Überschriften oder
  Tab-Stopps im Panel zählen, ggf. anpassen (nur in Spur V).
- **Einzelbuchstaben-Kürzel** (R, T, Z, Entf) wirken nur im Fokusmodus; NVDA im Lesemodus fängt sie ab. Knöpfe bleiben der
  Hauptweg, Kürzel nur zusätzlich (WCAG 2.1.4: nur bei Fokus aktiv).
- **Last der UI-Läufe:** vier Worktrees mit Playwright waren in M4u wackelig – bei zeitabhängigem Scheitern erst wiederholen, dann
  B und V vor C und D laufen lassen.

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|
