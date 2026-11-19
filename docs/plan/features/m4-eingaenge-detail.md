# M4 – Detailplan „Eingänge“

Gehört zu [m4-eingaenge.md](../m4-eingaenge.md). Regeln: [AGENTS.md §12](../../../AGENTS.md).

**Erstellt von:** Opus (Stufe 3) · **Datum:** 2026-11-19 · **Offene Designfragen:** keine (Grilling
2026-11-19, Nachfrage „unbekannter Besitzer“ nach dem Plan-Review); iCloud-Spur wartet auf den Test mit
echter Apple-ID (ADR-0018)

## Einschätzung

| Aufgabenpunkt | Umfang | Risiko | Neuheit | Klarheit | Prüfbarkeit | Stufe |
|---|---|---|---|---|---|---|
| 0 Gemeinsame Bausteine | ~45 Dateien, ~2 000 Zeilen | Migration, Datenschutz (Besitzer, Privat, Review), Geheimnisse | teils Muster | entschieden | Unit + Postgres | 3 |
| A Kamera | ~16 Dateien, ~1 400 Zeilen | Interop (Kamera, WebAudio, OpenCV), iOS | neu | entschieden | Fake-Kamera + Sichtprüfung | 3 |
| B Scanner-Ordner | ~10 Dateien, ~700 Zeilen | Datei fertig geschrieben?, Löschen, Rechte im Container, offener Port | Muster (Speicher) | entschieden | Integration + Compose | 3 |
| C1 Mail-Abruf und -Prüfung | ~22 Dateien, ~1 700 Zeilen | Datenverlust, Spam-Fehler, gefälschte Absender, Fremdinhalt | neu | entschieden | GreenMail + Messung | 3 |
| C2 Mail-Einstellungen, „Aus der Mail“ | ~8 Dateien, ~550 Zeilen | Passwort nie zurückgeben | Muster (Konto-Seiten) | entschieden | UI-Tests | 2 |
| D iCloud | ~35 Dateien, ~3 000 Zeilen | Apple-Protokoll, Sitzung, Kontosperre | Spike vorhanden | entschieden, Test offen | Fake-Apple-Server + Nutzertest | 3 |
| E Einstellungen, „Eingänge“, Besitzer prüfen | ~10 Dateien, ~750 Zeilen | gering | Muster | entschieden | UI-Tests | 2 |
| Texte, Katalog, KB, Häkchen | je Spur | keins | Muster | entschieden | Build | 1 (in der Spur mit) |

Rückblick M3 umgesetzt: Besitzer-, Privat- und Review-Regeln, Verschlüsselung von Zugangsdaten, der
gemeinsame LLM-Zugang und die API-Clients, die mehrere Spuren brauchen, stehen **in Schritt 0**.

## Telemetrie-Blöcke

| Block | Projekt | Feature |
|---|---|---|
| 1400–1499 | Server | Einstellungen, Eingangsstatus, Besitzer prüfen, Mail- und iCloud-Einrichtung |
| 1500–1599 | Server | Seiten-Upload (Kamera) |
| 2300–2399 | Worker | `InputScheduler` |
| 3200–3204 | Application | **bestehendes** `DocumentIntakeLog` – bekommt den Parameter `{Source}`, keine neuen Ids für „angenommen/fehlgeschlagen“ |
| 3600–3699 | Infrastructure | Scanner-Ordner |
| 3700–3799 | Infrastructure | IMAP |
| 3800–3899 | Infrastructure | iCloud |
| 3900–3949 | Infrastructure | PDF-Erzeugung, Geheimnisse, LLM-Zugang |
| 4600–4699 | Web.Client | Kamera |
| 4700–4799 | Web.Client | Eingänge, Einstellungen, Besitzer prüfen |
| 5300–5399 | Application | nur **neue** Ereignisse der Annahme (still verworfen, Besitzer fehlt, weitergegeben) |
| 5400–5499 | Application | Mail-Prüfung |

Platzhalter in Logs nur neutral (`{Count}`, `{Accepted}`, `{Verdict}`, `{Source}`, `{Problem}`,
`{ElapsedMs}`); `TelemetryConventionTests` lehnt Namen mit `file|folder|name|mail|user|address|text` ab.
Metriken (ohne Inhalte): `nomorepaper.inputs.received{source}`, `nomorepaper.inputs.skipped{source,reason}`,
`nomorepaper.inputs.failed{source,problem}`, `nomorepaper.mail.assessed{verdict}`.

---

## Paket P0 – Gemeinsame Bausteine · Modell: **Opus** · Effort: `high`

**Domain**

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Domain/Documents/DocumentSource.cs` | neu | `enum DocumentSource { Upload, Camera, ScannerFolder, Mail, ICloud }` | – | 15 |
| `src/NoMorePaper.Domain/Documents/MailOrigin.cs` | neu | `sealed record MailOrigin(string SenderAddress, string Subject, string? Note)`; Adresse ≤ 320, Betreff ≤ 300 (gekürzt), Hinweis ≤ 500 | Domain.Tests | 50 |
| `src/NoMorePaper.Domain/Documents/Document.cs` | ändern | `Source`; `Receive(title, at, ownerId, isPrivate, DocumentSource source = Upload)`. Datei hat 154 Zeilen → alles Neue in `Document.Intake.cs` | Domain.Tests | 10 |
| `src/NoMorePaper.Domain/Documents/Document.Intake.cs` | neu | partial: `MailOrigin?`, `NeedsOwnerCheck`; `AttachMailOrigin(MailOrigin)`; `MarkOwnerCheck()` (setzt auch `IsPrivate = true`); `ConfirmOwnerCheck(Guid actor)` (nur Besitzer; Flag aus, Privat bleibt wie gesetzt); `HandOver(Guid actor, Guid newOwner, bool isPrivate)` (nur Besitzer und nur bei `NeedsOwnerCheck`; Besitzer wechselt, Bearbeiter werden geleert, Flag aus; ist `Recognition != Confirmed`, werden Absender, Art, Tags, Titel-Vorschlag und Zusammenfassung geleert und `Recognition = None` – die Erkennung lief mit der Sicht des Verwalters, ADR-0023). `Confirm`/`Edit` bleiben unverändert | Domain.Tests | 90 |

**Application**

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Application/Documents/IntakeRequest.cs` | neu | `sealed record IntakeRequest(Guid? OwnerId, DocumentSource Source, string FileName, string MediaType, Stream Content, MailOrigin? Mail)`; `OwnerId = null` heißt „unbekannt“ → Verwalter + `NeedsOwnerCheck` | – | 25 |
| `src/NoMorePaper.Application/Documents/IntakeResult.cs`, `IntakeStatus.cs` | neu | `IntakeStatus { Received, AlreadyThere, OwnerGone, NoAdministrator }`; `IntakeResult(IntakeStatus, Guid? DocumentId)` | – | 2 × 20 |
| `src/NoMorePaper.Application/Documents/IntakeDocument.cs` | neu | Kern aus `ReceiveDocument` herausgelöst (Speichern im Eingang, Anhängen, `AddReceivedAsync`, Aufräumen wie bisher inkl. `IsUnrecordedAsync`). `AcceptAsync(IntakeRequest, bool? isPrivate, DuplicateScope scope, ct)`: Besitzer = `OwnerId` (muss `IAccountStore.ExistsAsync` sein, sonst `OwnerGone`) oder Verwalter über `IAccountStore.FindAdministratorIdAsync` (keiner → `NoAdministrator`); privat = `isPrivate` (nur Browser) ?? Einstellung des Besitzers; unbekannt → `MarkOwnerCheck` (privat). Dublette: `DuplicateScope.Visible` (Browser, wie bisher 409) oder `Owned` (Hintergrund: nur **eigene** Dokumente, `FindOwnedBySha256Async`) | Application.Tests | 190 |
| `src/NoMorePaper.Application/Documents/DuplicateScope.cs` | neu | `enum DuplicateScope { Visible, Owned }` | – | 10 |
| `src/NoMorePaper.Application/Documents/ReceiveDocument.cs` | ändern | wird dünn: `ExistsAsync` → `IntakeDocument.AcceptAsync(new(_current.Id, source, …), isPrivate, Visible)`; Überladung mit `DocumentSource` für die Kamera; `ReceiveResult` bleibt | bestehende Tests grün | −80 |
| `src/NoMorePaper.Application/Documents/DocumentIntakeLog.cs` | ändern | 3200/3201 bekommen `{Source}`; neue 5300 still verworfen (`{Source}`), 5301 Besitzer fehlt (`{Source}`), 5302 kein Verwalter, 5303 weitergegeben (Dokument-Id) – in eigener Datei `InputIntakeLog.cs`, falls > 120 Zeilen | – | 40 |
| `src/NoMorePaper.Application/Documents/IDocumentRepository.cs` | ändern | `FindOwnedBySha256Async(Guid ownerId, string sha256, ct)` | Infrastructure.Tests | 5 |
| `src/NoMorePaper.Application/Documents/FileSignature.cs` | verschoben aus `Server/Documents/` | rein (`static` erlaubt); Server, Scanner-Ordner und Mail nutzen sie | Tests umziehen | 0 |
| `src/NoMorePaper.Application/Accounts/IAccountStore.cs` | ändern | `FindAdministratorIdAsync(ct)` (ältester Verwalter); Datei 85 Zeilen | Infrastructure.Tests | 5 |
| `src/NoMorePaper.Application/Accounts/AccountPreferences.cs` | neu | `sealed record AccountPreferences(bool NewDocumentsPrivate, IReadOnlyList<string> MailSenders)`; `Default` = (false, []); Adressen klein, getrimmt, max. 20, gültige Form | Application.Tests | 60 |
| `src/NoMorePaper.Application/Accounts/IAccountPreferencesStore.cs` | neu | `GetAsync(Guid)`, `SaveAsync(Guid, AccountPreferences)`, `FindOwnerOfSenderAsync(string address)` → `Guid?` | – | 30 |
| `src/NoMorePaper.Application/Accounts/ChangePreferences.cs` | neu | für das angemeldete Konto; Adresse bei anderem Konto → `SenderTaken`. Bewusst: ein Konto kann jede Adresse eintragen (Haushalt vertraut sich; Mails dieser Adresse gehören dann ihm) – in `eingaenge.md` dokumentieren | Application.Tests | 70 |
| `src/NoMorePaper.Application/Classification/ConfirmOwnerCheck.cs` | neu | Besitzer bestätigt „passt“ → `Document.ConfirmOwnerCheck`; lädt über `IDocumentFieldsStore.FindVisibleAsync` (Zeilensperre wie M3) | Application.Tests | 60 |
| `src/NoMorePaper.Application/Classification/HandOverDocument.cs` | neu | Besitzer gibt ein Dokument mit `NeedsOwnerCheck` an ein bestehendes Konto weiter; privat nach dessen Einstellung; war es nicht bestätigt, wird die Erkennung neu eingereiht (`IJobQueue.RequeueRecognitionAsync`, Priorität 0) und läuft mit der Sicht des neuen Besitzers – **in derselben Transaktion** wie die Weitergabe (gleicher Scope-`AppDbContext`, vor `CommitChangeAsync` von `EfDocumentFieldsStore`); Log 5303 | Application.Tests: privater Eintrag des Verwalters erreicht den neuen Besitzer nicht | 90 |
| `src/NoMorePaper.Application/Processing/IJobQueue.cs`, `src/NoMorePaper.Infrastructure/Processing/JobQueue.Recognition.cs`, `src/NoMorePaper.Application/Recognition/RecognitionAttempt.cs` | ändern | `RequeueRecognitionAsync(Guid documentId, ct)`; `CompleteRecognitionAsync` vergleicht unter der Zeilensperre zusätzlich den **Besitzer** zum Zeitpunkt des Lesens (`wasOwner`) – geändert → nichts übernehmen, Auftrag zurück wie bei 5212 (neues Ergebnis `RecognitionCommit.OwnerChanged`, Log 5213) | Infrastructure.Tests: Weitergabe während der Erkennung | 40 |
| `src/NoMorePaper.Application/Inputs/IInputSource.cs` | neu | `InputKind Kind`, `TimeSpan Interval`, `Task<InputRunResult> RunAsync(CancellationToken)` | – | 25 |
| `src/NoMorePaper.Application/Inputs/InputKind.cs`, `InputRunResult.cs`, `InputProblem.cs`, `InputProblemKind.cs` | neu | je eine Datei. `InputKind { ScannerFolder, Mail, ICloud }`; `InputRunResult(int Received, int Skipped, InputProblem? Problem)`; `InputProblemKind { NotConfigured, SignInFailed, CodeNeeded, Unreachable, LlmUnavailable, Throttled, ProtocolChanged, UnknownFolder, StuckItems, Failed }`; `InputProblem(InputProblemKind, Guid? AccountId, bool NeedsAttention)` | – | 4 × 20 |
| `src/NoMorePaper.Application/Inputs/IInputStatusStore.cs`, `InputStatus.cs` | neu | `RecordAsync(InputKind, Guid? accountId, InputRunResult, DateTimeOffset)`, `ListAsync()` | – | 2 × 30 |
| `src/NoMorePaper.Application/Inputs/IInputConnections.cs`, `InputConnection.cs` | neu | gespeicherte Einrichtung je Eingang/Konto: `GetAsync(kind, accountId)`, `SaveAsync`, `DeleteAsync`, `ListAsync(kind)`; Einstellungen als JSON-Text, Geheimnis als `byte[]` (schon verschlüsselt) | – | 2 × 30 |
| `src/NoMorePaper.Application/Inputs/ISeenItems.cs`, `SeenList.cs` | neu | `SeenList { Imported, Spam, Failed }`; `HasSeenAsync(InputKind, Guid? accountId, SeenList, string key)`, `MarkSeenAsync(...)`, `IncrementFailuresAsync(...)` → Anzahl; Schlüssel nur als SHA-256 gespeichert | – | 2 × 25 |
| `src/NoMorePaper.Application/Inputs/ListInputStatus.cs` | neu | Verwalter sieht alle Eingänge mit Kontonamen, Mitglied Scanner/Mail allgemein und sein eigenes iCloud | Application.Tests | 80 |
| `src/NoMorePaper.Application/Security/ISecretProtector.cs`, `SecretPurposes.cs` | neu | `byte[] Protect(string purpose, string secret)`, `string Unprotect(string purpose, byte[] data)`; Zwecke `MailPassword`, `ICloudSession` | – | 2 × 20 |
| `src/NoMorePaper.Application/Documents/IPdfComposer.cs`, `TextPage.cs`, `TextPageField.cs` | neu | `ComposeImagesAsync(IReadOnlyList<Stream> jpegs, Stream target, ct)`, `ComposeTextAsync(TextPage, Stream target, ct)`; `TextPage(string Heading, IReadOnlyList<TextPageField> Header, string Body)`; `TextPageField(string Label, string Value)` | – | 3 × 20 |
| `src/NoMorePaper.Application/Composition/ApplicationServiceCollectionExtensions.cs` | ändern | `IntakeDocument` in `AddApplication()` **und** `AddDocumentProcessing()`; `ChangePreferences`, `ConfirmOwnerCheck`, `HandOverDocument`, `ListInputStatus` in `AddApplication()`; Platzhalter-Kommentar je Spur nicht nötig – Spuren ergänzen (Hotspot) | – | 10 |

**Infrastructure, Server, Worker, Contracts, Web.Client**

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Infrastructure/Inputs/*Configuration.cs` | neu | `account_preferences(account_id PK)`, `account_mail_senders(address PK, account_id)`, `input_connections(kind, account_key, account_id null, settings jsonb, secret bytea, updated_at)` PK `(kind, account_key)` mit `account_key = coalesce(account_id, 00000000-…)`, `input_status(kind, account_key, account_id null, last_run_at, last_success_at, problem, needs_attention, received_total)`, `input_seen_items(kind, account_key, list, key_hash char(64), failures, seen_at)` PK `(kind, account_key, list, key_hash)`. **Jede `account_id` mit FK auf `users` und `ON DELETE CASCADE`** (entferntes Konto → Sitzung, Adressen, Status weg) | Infrastructure.Tests | 5 × 40 |
| `src/NoMorePaper.Infrastructure/Documents/DocumentConfiguration.cs` | ändern | `source` (string 16, Standard `Upload`), `mail_sender`, `mail_subject`, `mail_note` (owned, nullable), `needs_owner_check` | MigrationTests | 20 |
| `src/NoMorePaper.Infrastructure/Persistence/Migrations/<ts>_Inputs.cs` | neu | `dotnet ef migrations add Inputs` | MigrationTests | gen. |
| `src/NoMorePaper.Infrastructure/Inputs/EfAccountPreferencesStore.cs`, `EfInputStatusStore.cs`, `EfInputConnections.cs`, `EfSeenItems.cs` | neu | Umsetzungen; Adressvergleich normalisiert | Infrastructure.Tests | 4 × 70 |
| `src/NoMorePaper.Infrastructure/Documents/EfDocumentRepository.cs` (127 Zeilen), `src/NoMorePaper.Infrastructure/Accounts/AccountStore.cs` | ändern | `FindOwnedBySha256Async`; `FindAdministratorIdAsync` (AccountStore hat 199 Zeilen → in partial `AccountStore.Administrators.cs`) | Infrastructure.Tests | 30 |
| `src/NoMorePaper.Infrastructure/Security/DataProtectionSecretProtector.cs` | neu | `CreateProtector("NoMorePaper.Inputs." + purpose)`; Fehler → `SecretUnreadableException` | Infrastructure.Tests: Klartext nie in `input_connections.secret` | 50 |
| `src/NoMorePaper.Infrastructure/Documents/Pdf/ImagePdfWriter.cs`, `TextPdfWriter.cs`, `PdfSharpComposer.cs`, `EmbeddedFontResolver.cs`, `Fonts/DejaVuSans*.ttf` | neu | PDFsharp 6 (MIT). Bilder: je Bild eine Seite in Bildgröße. Text: A4, Ränder 20 mm, Umbruch, Seitenumbruch; Schrift eingebettet. Composer verteilt nur | Infrastructure.Tests (Seitenzahl, gültiges PDF) | 100 + 120 + 30 + 50 |
| `src/NoMorePaper.Infrastructure/Recognition/LlmGate.cs`, `LlmPriority.cs` | neu | Singleton: **ein** Zugriff auf Ollama zur Zeit für Erkennung und Mail-Prüfung; `EnterAsync(LlmPriority, TimeSpan maxWait, ct)`; **Erkennung hat Vorrang** (Mail wartet, solange eine Erkennung wartet); das Zeitlimit einer Anfrage beginnt erst mit dem Halten. Die Erkennung wartet höchstens `LockFor − Timeout − 1 min`; danach wirft das Gate `LlmBusyException` (neu, `src/NoMorePaper.Application/Recognition/LlmBusyException.cs`). `RecognizeDocument.RunAsync` fängt sie **vor** dem allgemeinen `catch` und ruft `queue.ReleaseAsync(claim)` – kein Fehlversuch, Log 5214. `OllamaMailAssessor` macht daraus `LlmUnavailable` (nichts gezählt) | Infrastructure.Tests: Erkennung verliert ihren Auftrag nicht, während 20 langsame Mail-Anfragen laufen; Application.Tests: Wartezeit überschritten → Versuchszähler unverändert, Auftrag wieder fällig | 90 + 10 |
| `src/NoMorePaper.Infrastructure/Recognition/OllamaRecognizer.cs`, `RecognitionRegistration.cs` | ändern | Anfrage über `LlmGate`; Gate registrieren | bestehende Tests | 15 |
| `src/NoMorePaper.Infrastructure/Classification/EfDocumentFieldsStore.cs` + Lesekette `FieldsRow`, `FieldsReader`, `DocumentFieldsView`, `ReviewItem` | ändern | `ReviewFor`: zusätzlich `document.NeedsOwnerCheck && document.OwnerId == viewerId`; `FieldsRow`/View um `Source`, `MailOrigin`, `NeedsOwnerCheck` ergänzen | Infrastructure.Tests | 40 |
| `src/NoMorePaper.Infrastructure/Composition/InputRegistration.cs` | neu | `AddInputs()` (aus `AddInfrastructure`, Server **und** Worker): Stores, `ISecretProtector`, `IPdfComposer`. `AddInputSources()` (nur aus `WorkerComposition`): hier registrieren die Spuren ihre `IInputSource` | – | 50 |
| `src/NoMorePaper.Worker/Inputs/InputScheduler.cs`, `InputSchedulerLog.cs` | neu | `BackgroundService`; `PeriodicTimer` 10 s prüft fällige Quellen; jeder Lauf im eigenen Scope; Ausnahme → `Failed`; Status schreiben. Logs 2300 Lauf (`{Kind}`, `{Accepted}`, `{Skipped}`, `{ElapsedMs}`), 2301 Problem (`{Kind}`, `{Problem}`), 2302 Ausnahme (`{ExceptionType}`) | Worker.Tests (Fake-Quelle, ManualTimeProvider) | 140 + 30 |
| `src/NoMorePaper.Worker/Composition/WorkerComposition.cs` | ändern | `AddInputSources()`, `AddHostedService<InputScheduler>()` | – | 3 |
| `src/NoMorePaper.Server/Accounts/PreferencesEndpoints.cs` | neu | `GET/PUT /api/account/preferences`; `SenderTaken` → 409 mit Feldfehler; Logs 1410/1411 | Server.Tests | 100 |
| `src/NoMorePaper.Server/Classification/OwnerCheckEndpoints.cs` | neu | `POST /api/documents/{id}/owner-check` (bestätigen), `POST /api/documents/{id}/hand-over` (`{accountId}`); 404 für Unsichtbares, 403 ohne Recht; Logs 1420/1421 | Server.Tests | 90 |
| `src/NoMorePaper.Server/Program.cs` | ändern | neue `Map…`-Aufrufe (Hotspot) | – | 3 |
| `src/NoMorePaper.Contracts/Inputs/*` | neu | je Datei: `AccountPreferencesDto`, `InputStatusDto(Kind, AccountName?, State, LastRunAt?, LastSuccessAt?, Problem?, NeedsAttention)`, `MailSettingsDto(Host, Port, Security, UserName, string? TrustedAuthServId, bool HasPassword)`, `MailSettingsRequestDto(…, string? Password)`, `MailOriginDto(Sender, Subject, Note)`, `HandOverRequestDto(Guid AccountId)` | – | 6 × 15 |
| `src/NoMorePaper.Contracts/Classification/DocumentFieldsDto.cs` | ändern | `MailOriginDto? Mail`, `bool NeedsOwnerCheck`, `string Source` | – | 5 |
| `src/NoMorePaper.Server/Classification/ClassificationMapping.cs` | ändern | Abbildung der neuen Felder | Server.Tests | 10 |
| `src/NoMorePaper.Web.Client/Inputs/PreferencesApi.cs`, `InputsApi.cs` | neu | Clients für `/api/account/preferences` und `/api/inputs` (Status); Mail-/iCloud-Methoden ergänzen Spuren C/D in **eigenen** Dateien (`MailSettingsApi.cs`, `ICloudApi.cs`) | – | 2 × 70 |
| `Directory.Packages.props`, Infrastructure-`csproj` | ändern | `PDFsharp` (MIT) | – | 3 |
| `docs/kb/eingaenge.md`, `docs/kb/telemetrie.md` | neu/ändern | Grundgerüst; Einträge 1410/1411, 1420/1421, 2300–2302, 3200/3201 (neuer Parameter), 3900–3902, 5300–5303 | – | 80 |

- **Reihenfolge:** Domain → Ports → `IntakeDocument` + Umbau `ReceiveDocument` (bestehende Upload-Tests grün) → Tabellen/Migration → Stores → Composer, Gate → Scheduler → Owner-Check/Weitergabe → Endpunkte, Contracts, Clients.
- **Akzeptanz:** Build ohne Warnungen; alle Tests grün; Architektur- und Telemetrietests grün; neue Tests: Privat nach Konto-Einstellung, unbekannt → Verwalter **und privat**, `OwnerGone`, `NoAdministrator`, Hintergrund-Dublette nur gegen eigene Dokumente, Weitergabe (Besitzer, Privat des Ziels, Flag aus, nur bei `NeedsOwnerCheck`), Review mit `NeedsOwnerCheck` nur beim Besitzer, entferntes Konto löscht Einstellungen und Verbindungen, Geheimnis nicht im Klartext, LLM-Zugang nacheinander, Scheduler: ein Fehler hält die anderen Quellen nicht an.

---

## Paket P1 – Kamera (Spur A) · Modell: **Opus** · Effort: `high`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Web.Client/wwwroot/lib/opencv/opencv.js` + `LICENSE` | neu | OpenCV.js 4.x (Apache-2.0), feste Version, lokal | – | gen. |
| `wwwroot/js/camera/camera.js` | neu | `createCamera(host, dotnet, options)`: `getUserMedia({video:{facingMode:'environment', width:{ideal:3840}}})`, Schleife über `requestVideoFrameCallback` (~8 Bilder/s, Analyse auf 640 px), `dispose()` | UI.Tests | 200 |
| `wwwroot/js/camera/edges.js` | neu | Graustufen, Blur, Canny, `findContours`, größtes konvexes Viereck (`approxPolyDP`), Ecken normiert 0–1; Mats freigeben | UI.Tests (Standbild) | 150 |
| `wwwroot/js/camera/guidance.js` | neu | rein, ohne DOM und ohne Uhr (Zeit kommt als Argument): `assess(quad, history, nowMs)` → `{hint, quality, ready}`; Mittelpunkt-Abweichung > 12 % → Richtung, Fläche < 35 % → „closer“, Ecke am Rand → „farther“, Ecken bewegen sich < 1,5 % über 800 ms → `ready` | UI.Tests (`page.evaluate`, Tabellenfälle) | 120 |
| `wwwroot/js/camera/feedback.js` | neu | WebAudio-Ton (300–900 Hz nach `quality`, Puls 600–120 ms), Klick beim Auslösen, `navigator.vibrate?.(60)`; Start erst nach Nutzergeste (iOS) | – | 100 |
| `wwwroot/js/camera/capture.js` | neu | volles Bild aus dem Video (Canvas), `warpPerspective` (Zielgröße aus Kantenlängen, max. 3000 px), JPEG 0,85 | UI.Tests | 120 |
| `src/NoMorePaper.Web.Client/Camera/CameraInterop.cs` | neu | `StartAsync`, `CaptureAsync`, `DisposeAsync`; Rückrufe `OnHint(string)`, `OnCaptured(int)`, `OnError(string)` (`no-camera`, `denied`, `opencv-failed`) | – | 120 |
| `src/NoMorePaper.Web.Client/Camera/CameraAnnouncer.cs` | neu | **Dienst** mit `TimeProvider` (§2.6): Hinweis → Text-Schlüssel, höchstens alle 2,5 s, gleicher Hinweis erst nach 6 s wieder | `tests/NoMorePaper.Web.Client.Tests/Camera/CameraAnnouncerTests.cs` (neues Testprojekt, xUnit, nur Web.Client-Referenz) | 60 |
| `src/NoMorePaper.Web.Client/Pages/Camera.razor` + `.razor.cs` | neu | `@page "/kamera"`; Überschrift, Live-Region (polite), Video (`aria-hidden`), Knöpfe „Auslösen“, „Letzte Seite verwerfen“, „Fertig“, Schalter „Töne“ (localStorage), „Privat“ (aus `PreferencesApi`); Seitenliste `<ol>`; Ergebnis mit Link; Fehler mit Verweis auf `/hochladen`; Ladeansage für OpenCV | UI.Tests | 2 × 150 |
| `src/NoMorePaper.Web.Client/Documents/PageUploadApi.cs` | neu | `UploadPagesAsync(IReadOnlyList<byte[]> jpegs, bool isPrivate)` | – | 60 |
| `src/NoMorePaper.Server/Documents/PageUpload.cs`, `PageUploadLog.cs` | neu | `POST /api/documents/pages`: 1–50 Teile `page`, je ≤ 15 MB, gesamt ≤ 150 MB, nur JPEG (`FileSignature`), `IPdfComposer.ComposeImagesAsync` in Temp-Datei unter `DataPath/tmp` (immer gelöscht) → `ReceiveDocument` (Quelle `Camera`, Name „Kamera JJJJ-MM-TT HH-mm.pdf“ aus dem Feld `takenAt` des Clients – ISO mit Versatz, Ortszeit des Handys; nur ±1 Tag um die Serverzeit gültig, sonst Serverzeit); Antworten wie Upload; Logs 1500 angenommen (`{Pages}`, `{SizeBytes}`), 1501 abgelehnt (`{Reason}`), 1502 PDF fehlgeschlagen | Server.Tests | 150 + 30 |
| `src/NoMorePaper.Server/Documents/DocumentEndpoints.cs` | ändern | Route anhängen | – | 3 |
| `Layout/MainLayout.razor`, `Localization/Strings*.resx` | ändern | Navigation „Kamera“; Texte DE/EN | – | 60 |
| `tests/NoMorePaper.UI.Tests/Camera/CameraPageTests.cs`, `CameraCaptureTests.cs`, `GuidanceTests.cs`, `Media/sheet.y4m` + Erzeugungsskript | neu | Chromium mit Fake-Kamera (`--use-fake-device-for-media-stream --use-file-for-fake-video-capture`): axe DE/EN, Tastatur; Hinweis „weiter nach rechts“; Auto-Auslöser → „Seite 1 aufgenommen“; „Fertig“ → Mock `POST /api/documents/pages` mit 1 Teil; Tabellentest `guidance.js` | selbst | 3 × 130 |
| `tests/NoMorePaper.Server.Tests/Documents/PageUploadTests.cs` | neu | 2 JPEGs → 202, Quelle `Camera`; PNG → 400; 51 Teile → 400 | selbst | 120 |
| `NoMorePaper.slnx`, `tests/NoMorePaper.Web.Client.Tests/*.csproj` | neu/ändern | Testprojekt anlegen (Muster `Domain.Tests`) | – | 30 |

- **Telemetrie:** 1500–1502; 4600 Kamera gestartet, 4601 Fehler (`{Code}`), 4602 Seite aufgenommen (`{Index}`, `{ElapsedMs}`), 4603 hochgeladen (`{Pages}`).
- **Akzeptanz:** UI- und Server-Tests grün; Sichtprüfung am iPhone mit VoiceOver durch den Nutzer (Liste in `eingaenge.md`).
- **Eskalation:** OpenCV.js lädt im Test nicht oder die Fake-Kamera liefert kein Viereck → stoppen und melden.

---

## Paket P2 – Scanner-Ordner (Spur B) · Modell: **Opus** · Effort: `high`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `docker-compose.yml` | ändern | Dienst `samba` im **Profil `scanner`** (ohne Profil startet nichts Neues, `docker compose config` bleibt ohne neue Variablen gültig): Image mit Version + Digest (Wahl in ADR-0024 nachtragen), Benutzer `scanner` (Passwort `NOMOREPAPER_SMB_PASSWORD`), `force user`/uid **1654** (= `APP_UID` aus `mcr.microsoft.com/dotnet/aspnet:10.0`, für Compose nicht sichtbar – Kommentar mit Verweis aufs Image), nur SMB2/3, Freigabe `scan` → Volume `scaninbox`, Port `${NOMOREPAPER_SMB_BIND:-127.0.0.1}:445:445`. Worker: **komplette** `volumes:`-Liste wiederholen (`data`, `dpkeys`, Zertifikat) plus `scaninbox:/scan` – YAML-Merge verbindet Listen nicht; Variable `NoMorePaper__ScannerFolder__Path: /scan` | `docker compose config` mit/ohne Profil | 40 |
| `Dockerfile.worker` | ändern | `mkdir -p /scan && chown $APP_UID /scan` (neues Volume übernimmt Besitzer aus dem Image) | Compose-Lauf | 3 |
| `.env.example` | ändern | `NOMOREPAPER_SMB_BIND`, `NOMOREPAPER_SMB_PASSWORD`, Hinweis `--profile scanner` | – | 8 |
| `src/NoMorePaper.Infrastructure/Inputs/ScannerFolder/ScannerFolderSettings.cs` | neu | `NoMorePaper:ScannerFolder:Path` (leer = aus) – Muster `OllamaSettings` | – | 50 |
| `.../ScannerFolder/AccountFolders.cs` | neu | Unterordner je bestehendem Konto (Anmeldename, nur `[a-z0-9._-]`, sonst Id) anlegen; Zuordnung Ordner → Konto bei jedem Lauf neu aus den bestehenden Konten. **Eine Regel:** jeder Unterordner ohne passendes Konto (außer `Fehler/`) – etwa eines entfernten Kontos oder von Hand angelegt – wird nicht gelesen, seine Dateien bleiben; Problem `UnknownFolder` mit Ordneranzahl auf der Zeile des ganzen Scanner-Eingangs (`account_id = null`, nie eine Konto-Id eines entfernten Kontos) | Unit | 90 |
| `.../ScannerFolder/StableFiles.cs` | neu | merkt sich (Pfad → Größe, Änderungszeit, Zähler); fertig, wenn zwei Läufe gleich und exklusiv zu öffnen | Unit | 80 |
| `.../ScannerFolder/ScannerFolderSource.cs` | neu | `IInputSource`, 30 s; Dateien ohne `Fehler/`, versteckte, `~$`; Typ per `FileSignature`; `IntakeDocument.AcceptAsync(…, Owned)`; Wurzelordner → `OwnerId = null`; **gelöscht wird erst, wenn `Received`/`AlreadyThere` zurückkam** (also nach dem Commit); unbekannter Typ → `Fehler/<Zeit>-<Name>` | Infrastructure.Tests | 180 |
| `.../ScannerFolder/ScannerFolderLog.cs` | neu | 3600 Lauf (`{Count}`, `{Accepted}`), 3601 nach Fehler verschoben (`{Reason}`), 3602 Löschen fehlgeschlagen (`{ExceptionType}`), 3603 Kontoordner angelegt (Konto-Id) | – | 40 |
| `src/NoMorePaper.Infrastructure/Composition/InputRegistration.cs` | ändern | in `AddInputSources()`: `ScannerFolderSource`, wenn Pfad gesetzt | – | 5 |
| `tests/NoMorePaper.Infrastructure.Tests/Inputs/ScannerFolder/ScannerFolderSourceTests.cs`, `StableFilesTests.cs`, `AccountFoldersTests.cs` | neu | halb geschriebene Datei erst im 2. Lauf; Kontoordner → Besitzer, Privat nach Einstellung; Wurzel → Verwalter, privat, `NeedsOwnerCheck`; Ordner ohne Konto (auch nach Neustart) → Datei bleibt, Problem `UnknownFolder` ohne Konto-Id; Textdatei → `Fehler/`; eigene Dublette → gelöscht, kein zweites Dokument; fremde geteilte Dublette → **trotzdem angenommen**; Namen mit `..`/Unicode sicher; Commit schlägt fehl → Datei bleibt | selbst | 3 × 120 |
| `docs/kb/betrieb.md` | ändern | Scanner einrichten (`\\<host>\scan\<anmeldename>`), Profil `scanner`, kein SMB1, Port 445 nur LAN/Tailscale | – | 30 |

- **Akzeptanz:** Tests grün; `docker compose config` mit und ohne `--profile scanner` gültig; einmal lokal `docker compose --profile scanner up samba worker` und Datei per `smbclient`-Container ablegen (Ergebnis in `eingaenge.md`).

---

## Paket P3 – Mail-Abruf und -Prüfung (Spur C, Teil 1) · Modell: **Opus** · Effort: `high`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Directory.Packages.props`, Infrastructure-`csproj` | ändern | `MailKit` (MIT) | – | 3 |
| `src/NoMorePaper.Application/Inputs/Mail/IMailbox.cs`, `MailHandle.cs`, `MailFolder.cs`, `MailContent.cs`, `MailAttachment.cs`, `SenderCheck.cs` | neu | je Datei ein Typ. `IMailbox`: `NewMailAsync(int max)`, `ReadAsync(MailHandle)`, `MoveAsync(MailHandle, MailFolder)`; `MailFolder { Processed, Spam, Failed }`; `MailContent(string ItemKey, string From, SenderCheck FromCheck, string Subject, DateTimeOffset Date, string PlainText, IReadOnlyList<MailAttachment>)`; `ItemKey` = Message-ID, sonst Hash aus Datum+Absender+Betreff+Größe; `SenderCheck { Passed, Failed, Unknown }` aus `Authentication-Results` (DMARC oder SPF pass für die Absender-Domain) | – | 6 × 20 |
| `.../Mail/IMailAssessor.cs`, `MailAssessment.cs`, `MailText.cs` | neu | `AssessAsync(MailText, bool knownSender, ct)` → `MailAssessment(bool IsSpam, bool IsDocument, string? Note)` (bei bekanntem Absender `IsSpam` = false); `MailText` rein: Absender-Domain, Betreff, Text, Anhang-Typen; Kürzung 3 000 Zeichen (Kopf 2 200 + Fuß 800) | Unit | 30 + 20 + 70 |
| `.../Mail/MailRouting.cs` | neu | rein: Besitzer (Absender in Einstellungen **und** `FromCheck == Passed` → Konto, sonst unbekannt) und ob die Spam-Prüfung nötig ist | Unit | 60 |
| `.../Mail/ProcessMailbox.cs` | neu | je neue Mail (max. 20, **und** Lauf endet nach 5 min Gesamtzeit): `ISeenItems(Imported)` → nur noch verschieben; `ISeenItems(Spam)` → ohne Prüfung annehmen; `MailRouting`; `AssessAsync`; **LLM nicht erreichbar oder Zeitlimit → Lauf endet mit `LlmUnavailable`, Mail bleibt unberührt, nichts gezählt**; Spam → `MarkSeen(Spam)` + `Move(Spam)`; sonst `MailIntake`; danach `MarkSeen(Imported)` **vor** `Move(Processed)`. Gezählt (`IncrementFailures`) werden nur Fehler der Mail selbst (MIME unlesbar, Annahme oder PDF scheitert); 3 → `Move(Failed)`, Zähler zurücksetzen, Problem `StuckItems`. Registrierung in `AddDocumentProcessing()` (braucht `IMailAssessor`, nur Worker) | Application.Tests (Fakes): Ollama aus, 3 Läufe → Mail noch in `INBOX` | 170 |
| `.../Mail/MailIntake.cs` | neu | (Registrierung in `AddDocumentProcessing()`) Anhänge: nur `Content-Disposition: attachment` oder PDF; Bilder nur ≥ 30 KB **und** ≥ 500 px (Größe aus `MailAttachment`); je Teil `IntakeDocument` (`Owned`) mit `MailOrigin(note)`; keiner und `IsDocument` → `ComposeTextAsync` → Intake (Name „Mail JJJJ-MM-TT.pdf“, Betreff nur im PDF und in `MailOrigin`) | Application.Tests | 120 |
| `.../Mail/MailLog.cs` | neu | 5400 verarbeitet (`{Documents}`, `{Verdict}`), 5401 Prüfung fehlgeschlagen (`{ExceptionType}`), 5402 Teil übersprungen (`{MediaType}`), 5403 Verschieben fehlgeschlagen, 5404 als hängend aussortiert | – | 50 |
| `src/NoMorePaper.Infrastructure/Inputs/Mail/ImapMailbox.cs` | neu | MailKit: Verbindung aus `InputConnection` (Passwort über `ISecretProtector`), Ordner anlegen, `UID SEARCH` in `INBOX`, `MOVE` (Fallback COPY + `UidExpunge` nur dieser UID), HTML → Text (MimeKit), Bildmaße aus dem Kopf (ohne Dekodieren: JPEG/PNG-Header) | Infrastructure.Tests (GreenMail) | 200 |
| `.../Mail/AuthenticationResults.cs` | neu | rein: nur `Authentication-Results`-Köpfe mit der vom Verwalter eingetragenen `authserv-id` des eigenen Mailservers zählen (Einstellung `TrustedAuthServId`); keiner oder keine Id eingetragen → `SenderCheck.Unknown`; DMARC oder SPF `pass` für die Absender-Domain → `Passed` | Unit: gefälschter Kopf ohne/mit fremder Id → unbekannt | 70 |
| `.../Mail/MailSource.cs`, `ImapMailboxFactory.cs` | neu | `IInputSource`, 5 min; nicht eingerichtet → `NotConfigured`; Anmeldefehler → `SignInFailed`; Netz → `Unreachable`; Registrierung in `AddInputSources()` | Infrastructure.Tests | 120 |
| `src/NoMorePaper.Infrastructure/Recognition/OllamaMailAssessor.cs`, `MailPrompt.cs`, `MailSchema.cs` | neu | über `LlmGate`; Temperatur 0, Seed 1, Schema `{isSpam, isDocument, note}`; Prompt Deutsch, „note“ nur Fakten aus dem Text (Fristen, Zahlweise, Kundennummer), sonst leer; Plausibilität: Datumsangaben im Hinweis kommen im Text vor; Registrierung in `RecognitionRegistration` (nur Worker) | Infrastructure.Tests (`FakeOllamaHandler`) | 3 × 90 |
| `tests/NoMorePaper.Infrastructure.Tests/Inputs/Mail/Samples/*.eml` + `expected.json` | neu | 24 selbst geschriebene Mails (8 Spam/Phishing, 5 Newsletter, 4 Rechnung im Text, 5 mit PDF-Anhang, 2 englisch), dazu eine mit Signatur-Logo und Tracking-Pixel; keine echten Daten | – | – |
| `tests/…/Inputs/Mail/MailAssessmentLiveTests.cs` | neu | nur mit erreichbarem Ollama (sonst übersprungen): Trefferquote ausgeben; Assert: kein wichtiges Dokument als Spam | selbst | 100 |
| `tests/…/Inputs/Mail/GreenMailFixture.cs`, `ImapMailboxTests.cs`, `MailSourceTests.cs` | neu | Testcontainers `greenmail/standalone` (Apache-2.0): Anhang → Dokument + Mail in `Verarbeitet`; Spam → `NoMorePaper-Spam`; zurückgeschoben → ohne Prüfung angenommen; Annahme scheitert → Mail bleibt; Verschieben scheitert → nächster Lauf legt **kein** zweites Dokument an; drei Fehlschläge → `NoMorePaper-Fehler`; Logo/Pixel → kein Dokument; gefälschter Konto-Absender ohne `pass` → unbekannt | selbst | 60 + 2 × 140 |
| `docs/kb/eingaenge.md` | ändern | Abschnitt Mail: Ablauf, Prompt, Messwerte, „jede Adresse eintragbar“ | – | 60 |

- **Akzeptanz:** Tests grün; Messlauf gegen lokales Ollama mit Ergebnis in `eingaenge.md`. Keine Mailinhalte, Betreffs, Adressen in Logs (Test mit `RecordingLogger`).

## Paket P4 – Mail-Einstellungen und „Aus der Mail“ (Spur C, Teil 2) · Modell: **Sonnet** · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Application/Inputs/Mail/ConfigureMailbox.cs`, `IMailboxProbe.cs` | neu | nur Verwalter (`ICurrentAccount.IsAdmin`); speichert (auch `TrustedAuthServId`, Hilfetext: steht im Kopf `Authentication-Results` eingegangener Mails), Passwort nur wenn gesetzt, verschlüsselt; `TestAsync` über `IMailboxProbe` (Infrastructure, MailKit) → `Ok`/`SignInFailed`/`Unreachable`; in `AddApplication()` registrieren | Application.Tests | 120 + 20 |
| `src/NoMorePaper.Infrastructure/Inputs/Mail/ImapMailboxProbe.cs` | neu | Verbindung + Anmeldung + Abmelden, Zeitlimit 20 s; in `AddInputs()` (Server braucht ihn) | Infrastructure.Tests (GreenMail) | 60 |
| `src/NoMorePaper.Server/Inputs/MailSettingsEndpoints.cs` | neu | `GET /api/inputs/mail` (`HasPassword`, nie das Passwort), `PUT`, `POST /api/inputs/mail/test`; Logs 1400–1402 (ohne Host/Benutzer) | Server.Tests | 120 |
| `src/NoMorePaper.Web.Client/Pages/MailSettings.razor`, `Inputs/MailSettingsApi.cs` | neu | `@page "/eingaenge/mail"`, nur Verwalter; Labels, Passwortfeld `autocomplete="new-password"` mit Hinweis „gespeichert“, „Verbindung testen“ → Live-Region | UI.Tests | 160 + 60 |
| `src/NoMorePaper.Web.Client/Classification/MailOriginSection.razor` | neu | „Aus der Mail“ (Absender, Betreff, Hinweis); `DocumentFieldsPanel.razor` (225 Zeilen) bekommt nur eine Zeile, die die Komponente nach der Zusammenfassung einbindet | UI.Tests | 50 |
| `tests/NoMorePaper.UI.Tests/Inputs/MailSettingsTests.cs` | neu | axe DE/EN; Passwort nie zurückgeschrieben; Test-Knopf meldet Ergebnis; Mitglied → keine Seite | selbst | 150 |

---

## Paket P5 – iCloud (Spur D) · Modell: **Opus** · Effort: `high` · **Start erst nach bestandenem Test**

Detail wird nach dem Test ergänzt (Befunde ändern Teile des Ablaufs). Gerüst:

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Infrastructure/Inputs/ICloud/{Auth,Drive,Http,Session}/*` | neu | aus `spikes/icloud/`; `WindowsDataProtector` → `ISecretProtector`; Sitzung in `input_connections(ICloud, account)` | Infrastructure.Tests (Fake-Apple-Server aus dem Spike nach `tests/`) | ~2 500 |
| `src/NoMorePaper.Application/Inputs/ICloud/ConnectICloud.cs` | neu | Anmeldung → Code → Ordner; je Konto; Passwort nur im Speicher der Anfrage; in `AddApplication()` | Application.Tests | 200 |
| `src/NoMorePaper.Server/Inputs/ICloudEndpoints.cs` | neu | `POST /api/inputs/icloud/sign-in`, `/code`, `GET /folders`, `PUT /folder`, `DELETE` | Server.Tests | 150 |
| `src/NoMorePaper.Infrastructure/Inputs/ICloud/ICloudSource.cs` | neu | je verbundenes Konto, 15 min; neue Datei-Ids → Intake (`Owned`) → `MarkSeen(Imported)`; Fehlerklassen → `InputProblem`; 429/503 → frühestens nach 1 h | Infrastructure.Tests | 180 |
| `src/NoMorePaper.Web.Client/Pages/ICloudSettings.razor`, `Inputs/ICloudApi.cs` | neu | Anmeldung, Code (`autocomplete="one-time-code"`), Ordnerliste mit Knöpfen | UI.Tests | 200 + 60 |

---

## Paket P6 – Einstellungen, „Eingänge“, Besitzer prüfen (Spur E) · Modell: **Sonnet** · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Server/Inputs/InputStatusEndpoints.cs` | neu | `GET /api/inputs` über `ListInputStatus` | Server.Tests | 50 |
| `src/NoMorePaper.Web.Client/Pages/Inputs.razor` | neu | `@page "/eingaenge"`; je Eingang ein Abschnitt mit h2, Zustand als Text, Zeiten relativ und absolut, Aktion als Link (Mail-Einstellungen nur für Verwalter); Aktualisieren alle 60 s, Änderung über Live-Region; Hinweis auf Postfach-Ordner `NoMorePaper-Spam` | UI.Tests | 180 |
| `src/NoMorePaper.Web.Client/Pages/Account.razor` | ändern | „Neue Dokumente privat ablegen“; Liste der Mail-Absender (Entfernen, Hinzufügen, Fehler als Text). Wird die Datei > 250 Zeilen → Abschnitte als `Accounts/PreferencesSection.razor` | UI.Tests | 120 |
| `src/NoMorePaper.Web.Client/Pages/Upload.razor` | ändern | Privat-Schalter aus den Einstellungen vorbelegt | UI.Tests | 10 |
| `src/NoMorePaper.Web.Client/Classification/OwnerCheckSection.razor` | neu | bei `NeedsOwnerCheck`: Text „Herkunft unklar – nur für dich sichtbar“, Knopf „Passt so“, Auswahl „Weitergeben an“ (Mitglieder) + Bestätigen; in `DocumentFieldsPanel` und auf „Zu prüfen“ eingebunden (je eine Zeile) | UI.Tests | 120 |
| `Layout/MainLayout.razor`, `Localization/Strings*.resx` | ändern | Navigation „Eingänge“; Texte DE/EN | – | 60 |
| `tests/NoMorePaper.UI.Tests/Inputs/InputsPageTests.cs`, `PreferencesTests.cs`, `OwnerCheckTests.cs`, `FakeInputs.cs` | neu | axe DE/EN; Tastatur; Fehlerzustand als Text; Live-Region; Weitergeben per Tastatur | selbst | 4 × 90 |

- **Telemetrie:** 4700 Seite geladen (`{Count}`), 4701 Einstellung gespeichert, 4702 Fehler (`{Code}`), 4703 weitergegeben.
- **Akzeptanz:** alle Tests grün; axe 0.
