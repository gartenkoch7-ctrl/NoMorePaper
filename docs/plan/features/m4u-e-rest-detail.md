# M4u Spur E – Restliche Seiten angleichen: Detailplan

Aufgabe E aus [m4u-oberflaeche.md](../m4u-oberflaeche.md): Start (mit Übersicht), Zu prüfen, Suche, Listen, Konto,
Mitglieder, Hinzufügen, Hochladen, Kamera nach den freigegebenen Entwürfen 05–13; Rückfragen auf `Design/ConfirmBox`.
Regeln: [AGENTS.md §12](../../../AGENTS.md), [Vorlage](../detailplan-vorlage.md), [UI-Prozess](../../kb/ui-prozess.md).

Grundlagen: `notizen.md` + `screen.png` in [05](../../ui/stitch/05-startseite/notizen.md) …
[13](../../ui/stitch/13-kamera/notizen.md). **Anforderung** sind dort „Übernehmen“, „Abgleich mit der App“ (rechte
Spalte gilt) und „Beim Übernehmen nicht mitnehmen (§2.5)“. „Nachbessern in Stitch?“ entfällt (Nutzer 2026-11-23:
Restpunkte beim Umsetzen nach App-Texten und Theme). Entwürfe **14–16 gehören zu M4b** und sind nicht Teil dieses Plans
(Übergabe an [m4b-scan-bearbeitung-detail.md](m4b-scan-bearbeitung-detail.md), Spur B).

## Entschieden (nicht neu fragen)

- Nutzer 2026-11-22/23: Startseite bekommt die Übersicht aus 05; Bestätigen-Knöpfe in Rückfragen **neutral**, Rot nur für
  Fehler; kein eigener Fokusrahmen; Zoom nie sperren; „Obsidian Slate“ unverändert; Rückfragen Review/Members/ListSection
  auf `ConfirmBox` mit UI-Test.
- Aus den Notizen abgeleitet (Planer, 2026-11-23):
  - **Start:** H1 „Guten Tag, {Anzeigename}“ (05 „Übernehmen“), keine Pfadzeile, Einleitung `HomeIntro`, Eingang mit
    Problem zuerst, iCloud je Konto, keine Pfade/Adressen. Daten nur aus **vorhandenen Endpunkten**:
    `GET api/review/count` (über `ReviewCounter`), `GET api/documents?page=1&pageSize=5` (Server sortiert schon
    `received_at DESC`, `EfDocumentRepository.cs:51`), `GET api/inputs` (`InputsApi`). **Keine Server-Änderung.**
  - **Suche:** markiert weiter „Dokumente“ (prompts.md §2: Suchergebnisse gehören zu „Dokumente“; so in Schritt 0
    umgesetzt und getestet). Der Hinweis in 07 „keinen Punkt markieren“ wird nicht übernommen (steuernde Sitzung,
    2026-11-23); `NavSection.cs` bleibt unverändert.
  - **Hinzufügen:** H2 je Kachel wie im freigegebenen Entwurf 11 (Link in der H2, Symbolfläche darüber); ganze Kachel bleibt
    klickbar (gestreckter Link wie heute), damit der Kachel-Hover nicht lügt.
  - **Konto:** keine Karte „Sprache“, keine Nummern 01–04, Rolle „Verwalter“/„Mitglied“, App-Texte bleiben.
  - **Listen:** „Ausgewählte zusammenführen“ bleibt bedienbar (nicht `disabled`).
  - **ConfirmBox:** Fokus beim Öffnen auf die Überschrift (Baustein-Vertrag aus Schritt 0, KB `oberflaeche.md`); nur
    „Umbenennen“ setzt den Fokus ins Namensfeld. Bestehende Tests, die „Abbrechen“ fokussiert erwarten, werden angepasst.
    Bestätigen neutral (`nmp-button`); nur „Neuen Namen speichern“ weiß wie in Entwurf 08 (keine Lösch-/Zusammenführ-Aktion).
  - Alte Boxen (`error-box` mit `role=alert`, `success-box`, `confirm-box`, `link-result`) auf den E-Seiten →
    `NoticeBox`/`ConfirmBox`/Seitenklassen. `RequireSignIn` und `EditorsPicker` (nicht Spur E) behalten `error-box`.
  - Seiten-H1 wie Spur D: `<MudText Typo="Typo.h2" HtmlTag="h1">` (headline-lg), nur Start nutzt `Typo.h1`.

## Offene Fragen an den Nutzer

Alle beantwortet: **F1 wie empfohlen** (Nutzer, 2026-11-23) – Lesereihenfolge Passwort → Abmelden → Eingänge.

| Nr. | Frage | Empfehlung | Bei der anderen Antwort |
|---|---|---|---|
| F1 | Der Konto-Entwurf 09 zeigt „Passwort ändern“ links, „Abmelden“ rechts daneben und „Eingänge und neue Dokumente“ darunter über die ganze Breite. Bisher liest der Screenreader Passwort → Eingänge → Abmelden. Soll die Lesereihenfolge dem Bild folgen (Passwort → Abmelden → Eingänge), oder soll die bisherige Reihenfolge bleiben und nur die Optik per CSS umgestellt werden (dann springt die Tab-Taste von unten nach oben)? | **Bild folgen** (Passwort → Abmelden → Eingänge): Lese-, Tab- und Bildreihenfolge sind gleich, nichts springt. | E3: In `Account.razor` steht `<PreferencesSection />` vor der Abmelden-Karte; `account-page.css` legt Abmelden per `grid-area` nach rechts oben. |

## Detailplan

**Erstellt von:** Opus (Stufe 3) · **Datum:** 2026-11-23 · **Offene Designfragen:** keine (F1 beantwortet 2026-11-23)

Pfade ohne Präfix liegen unter `src/NoMorePaper.Web.Client/`. Tests: `UI` = `tests/NoMorePaper.UI.Tests/`,
`WCT` = `tests/NoMorePaper.Web.Client.Tests/`.

**Gesperrt:** `Layout/MainLayout.razor`, `Theme/ContrastTheme.cs` (nur über die steuernde Sitzung), dazu `base.css`,
`layout.css`, Dateien der Spuren A–D (`Documents*`, `DocumentViewer*`, `Viewer/`, `DocumentFields*`, `Inputs*`-Seiten).
**Nur in E0 geändert (danach für Pakete gesperrt):** `Strings.resx`, `Strings.en.resx`, `wwwroot/index.html`,
`Design/*`, `design.css`, `design-boxes.css`. Braucht ein Paket einen Text, der unten fehlt → stoppen und melden.

### Einschätzung

| Aufgabenpunkt | Umfang | Risiko | Neuheit | Klarheit | Prüfbarkeit | Stufe |
|---|---|---|---|---|---|---|
| E0 Bausteine (`ConfirmBox`, `NoticeBox`), CSS-Teilung, alle Texte, CSS-Dateien anlegen | ~10 Dateien, ~250 Zeilen | mittel: geteilter Baustein, ARIA | Muster (Schritt 0) | entschieden | Build, UI-Tests in E2/E3 | 2 |
| E1 Startseite mit Übersicht | ~12 Dateien, ~650 Zeilen | mittel: drei Ladevorgänge, Ereignis `ReviewCounter.Changed`, Tests auf `/` | teils neu (reine Logik nach `InputTabs`) | entschieden | Unit + UI-Tests, axe | 2 |
| E2 Zu prüfen + Listen (+ ConfirmBox) | ~12 Dateien, ~550 Zeilen | mittel: Fokusführung nach Aktionen | Muster | entschieden | UI-Tests, axe | 2 |
| E3 Konto + Mitglieder (+ ConfirmBox) | ~8 Dateien, ~400 Zeilen | gering | Muster | nach F1 entschieden | UI-Tests, axe | 2 |
| E4 Suche, Hinzufügen, Hochladen, Kamera | ~12 Dateien, ~450 Zeilen | gering–mittel: Kamera-Fokus | Muster | entschieden | UI-Tests, axe | 2 |
| E5 Aufräumen, KB, Katalog, Häkchen | ~7 Dateien, ~60 Zeilen | keins | – | entschieden | Lesen, Build | 1 |
| E6 Review §8, Abnahme Schritt 8 | – | – | – | – | Review, Sichtvergleich | 3 |

### Paket E0 – Gemeinsame Bausteine und Texte · Modell: **Sonnet** · Effort: `medium`

Im Integrationsbranch `feat/m04u-oberflaeche`, **vor** allen anderen Paketen, Review durch die steuernde Sitzung.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Design/ConfirmBox.razor` | ändern | Wurzel `<div class="nmp-confirm" role="group" aria-labelledby="@(Id)-heading" @onkeydown="OnKeyDownAsync">`; Überschrift (Ebenen-Switch bleibt) mit `id="@(Id)-heading"`; `<p>@Text</p>` nur bei nicht leerem `Text`; danach `@ChildContent`; Knöpfe: „Abbrechen“ `nmp-button nmp-button-outlined`, Aktion `class="@(PrimaryConfirm ? "nmp-button nmp-button-primary" : "nmp-button")"`. Neue Parameter: `[EditorRequired] string Id`, `string? Text` (nicht mehr Pflicht), `RenderFragment? ChildContent`, `bool PrimaryConfirm` (Standard `false` = neutral), `string? InitialFocusId`. `OnAfterRenderAsync(firstRender)`: mit `InitialFocusId` → `Js.InvokeVoidAsync("nmpForms.focusById", InitialFocusId)`, sonst `_heading.FocusAsync()`. `OnKeyDownAsync(KeyboardEventArgs e)`: `Escape` → `OnCancel.InvokeAsync()`. Kopfkommentar um Escape/Gruppe ergänzen | in E2/E3 | 80 |
| `Design/NoticeBox.razor` | ändern | optionaler `[Parameter] string? Icon` überschreibt das Standardsymbol (`Icon ?? (Kind == Error ? Error : CheckCircle)`); Feld umbenennen in `SymbolOf` o. ä., damit Parameter und Property nicht kollidieren | – | +4 |
| `wwwroot/css/design.css` | ändern | Blöcke `.nmp-notice*` und `.nmp-confirm*` (Zeilen ~31–85) **verschieben** nach `design-boxes.css` (Datei hat 240 Zeilen) | – | −55 |
| `wwwroot/css/design-boxes.css` | neu | verschobene Blöcke + `.nmp-confirm .form-field { margin-bottom: var(--nmp-space-md); }`, `.nmp-notice-body ul { margin: 0; }` | – | 70 |
| `wwwroot/css/{start,review,lists,search,account-page,capture}.css` | neu | je nur Kopfkommentar (`/* Page „…“ (Stitch NN), M4u Spur E. */`), Inhalt kommt aus E1–E4 | – | 6 × 2 |
| `wwwroot/index.html` | ändern | nach `design.css`: `design-boxes.css`; nach `document-tabs.css`: `start.css`, `review.css`, `lists.css`, `search.css`, `account-page.css`, `capture.css` | – | +7 |
| `Localization/Strings.resx`, `Strings.en.resx` | ändern | alle Schlüssel der Tabelle „Texte“ unten | – | +45 je Datei |

**Texte (DE / EN)** – neu:

| Schlüssel | Deutsch | Englisch |
|---|---|---|
| `HomeGreeting` | Guten Tag, {0} | Hello, {0} |
| `StartReviewUnknown` | Wie viele Dokumente warten, konnte nicht geladen werden. | The number of documents waiting could not be loaded. |
| `StartReviewOpen` | Jetzt prüfen | Review now |
| `StartRecentHeading` | Zuletzt hinzugefügt | Recently added |
| `StartRecentCaption` | Die fünf zuletzt eingegangenen Dokumente | The five documents received last |
| `StartRecentAll` | Alle Dokumente anzeigen | Show all documents |
| `StartRecentEmpty` | Noch keine Dokumente. Lade eine Datei hoch oder nimm ein Blatt mit der Kamera auf. | No documents yet. Upload a file or take a sheet with the camera. |
| `StartRecentError` | Die zuletzt hinzugefügten Dokumente konnten nicht geladen werden: {0} | The recently added documents could not be loaded: {0} |
| `StartInputsError` | Der Zustand der Eingänge konnte nicht geladen werden: {0} | The state of the inputs could not be loaded: {0} |
| `StartInputProblemFor` | {0}: {1}. | {0}: {1}. |
| `StartInputOpen` | Eingang {0} öffnen | Open input {0} |
| `StartInputsAll` | Alle Eingänge anzeigen | Show all inputs |
| `StartActionsHeading` | Dokument hinzufügen | Add a document |
| `StartUpload` | Datei hochladen | Upload a file |
| `StartCamera` | Mit Kamera aufnehmen | Take with the camera |
| `SearchResultsLabel` | Suchergebnisse | Search results |

Vorhandene Schlüssel, die die Pakete nutzen (nicht neu anlegen): `HomeIntro`, `NavReview`, `NavReviewCount`, `ReviewCount`,
`ReviewCountOne`, `ReviewEmpty`, `NavInputs`, `ColTitle`, `ColReceived`, `ColStatus`, `ClsFieldCorrespondent`, `ClsNoValue`,
`Retry`, `NavSettings`, `NavMembers`, `ListsHeading`, `AccountHeading`, `RoleAdmin`, `RoleMember`, `InpBadge*`, `InpKind*`.

- **Reihenfolge:** Texte → `NoticeBox` → `ConfirmBox` → CSS-Teilung → leere CSS-Dateien + `index.html` → Build → alle
  UI-Tests (ConfirmBox hat noch keine Nutzer; `NoticeBox`-Aufrufe bleiben gültig).
- **Telemetrie:** keine (Bausteine).
- **Akzeptanz:** `dotnet build NoMorePaper.slnx` ohne Warnungen; `dotnet test --solution NoMorePaper.slnx` grün;
  Zeilenprüfung grün; `design.css` < 200 Zeilen.

### Paket E1 – Startseite mit Übersicht (Entwurf 05) · Modell: **Sonnet** · Effort: `medium`

Branch `feat/m04u-e1-start`. Neuer Feature-Ordner `Start/`.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Start/StartInputLine.cs` | neu | `public sealed record StartInputLine(string Kind, InputRowState State, IReadOnlyList<InputStatusDto> Problems);` | – | 10 |
| `Start/StartInputs.cs` | neu | `public static class StartInputs` (rein, wie `Inputs/InputTabs.cs`): `IReadOnlyList<StartInputLine> Lines(IEnumerable<InputStatusDto> rows)`: je Art aus `InputTabs.Kinds` eine Zeile; Zustand = schlechtester der Zeilen nach Rang `NeedsAttention > Problem > NotSetUp > NeverRun > Running` (über `InputTabs.StateOf`); Art ohne Zeilen: `icloud` → `NotSetUp`, sonst `NeverRun` (wie `InputTabs.Summary`); `Problems` = Zeilen mit `Problem`/`NeedsAttention`; Sortierung: Zeilen mit Problem zuerst, sonst stabil in `Kinds`-Reihenfolge. Unbekannte Arten ignorieren | `WCT/Start/StartInputsTests.cs` | 60 |
| `WCT/Start/StartInputsTests.cs` | neu | Muster `WCT/Inputs/InputTabsTests.cs`: leer → 3 Zeilen (scanner Never, mail Never, icloud NotSetUp); Problem bei iCloud → iCloud zuerst, Reihenfolge Rest stabil; zwei iCloud-Konten (ok + Problem) → Zustand Problem, `Problems` nur die eine Zeile; `NeedsAttention` schlägt `Problem`; `not-configured` → NotSetUp, keine Problems; andere Art ignoriert | selbst | 110 |
| `Start/StartLog.cs` | neu | `internal static partial class StartLog` (Muster `Inputs/InputsLog.cs`): 4800 Debug `StartPartLoaded(string part, int count)`, 4801 Warning `StartPartFailed(string part)`; Teile `recent`, `inputs` (Zähler loggt `ReviewCounter` schon mit 4514) | – | 20 |
| `Start/StartReviewCard.razor` | neu | `@implements IDisposable`, `@inject ReviewCounter Counter`. `OnInitializedAsync`: `await Counter.RefreshAsync()`; `Counter.Changed += OnChanged` (→ `InvokeAsync(StateHasChanged)`), `Dispose` meldet ab. `<section class="start-card start-review" aria-labelledby="start-review-heading">`: Symbolkachel `FactCheck` `aria-hidden`; `<MudText Typo="Typo.h3" HtmlTag="h2" id="start-review-heading">` = `Count > 0 ? NavReviewCount : NavReview`; Satz: `null` → `StartReviewUnknown`, 0 → `ReviewEmpty`, 1 → `ReviewCountOne`, n → `ReviewCount`; Link `<a href="zu-pruefen" class="nmp-button nmp-button-primary">StartReviewOpen</a>` nur bei `Count is null or > 0` | UI | 50 |
| `Start/StartRecent.razor` | neu | `@inject DocumentApi Api`, `ILogger<StartRecent>`. Lädt `Api.ListAsync(1, 5)`; Zustände: lädt (`<p role="status">` mit `DocumentsLoading`), Fehler → `<NoticeBox Kind="Error" Message="@L["StartRecentError", err]">` **ohne** `Announce` (Start hat sonst drei Alarme beim Laden; `AdminResetNoticeTests` verlangt `main [role=alert]` = 0) + Knopf `Retry` (`nmp-button`) im `ChildContent`, `StartPartFailed("recent")`; leer → `StartRecentEmpty`; sonst `StartPartLoaded("recent", n)` und Tabelle in `<div class="start-table-scroll" role="region" aria-label="@L["StartRecentCaption"]" tabindex="0">` (Muster `Documents/DocumentTable.razor`): `<table class="doc-table"><caption class="nmp-visually-hidden">StartRecentCaption</caption>`, Spalten `ColTitle` (Link `DocumentLinks.Viewer(id)` + `<PrivateBadge />` bei `IsPrivate`), `ClsFieldCorrespondent` (Name oder `ClsNoValue`), `ColReceived` (`ToLocalTime().ToString("g", CurrentCulture)`), `ColStatus` (`<StatusText Kind="@DocumentStatus.KindOf(doc.State)" />`). Kopf der Karte: H2 `StartRecentHeading`, rechts Link `dokumente` `StartRecentAll` (Pfeil `aria-hidden`). Kein Nachfragen/Polling | UI | 90 |
| `Start/StartInputsCard.razor` | neu | (nicht `StartInputs.razor`: kollidiert mit `StartInputs.cs`). `@inject InputsApi Api`. Lädt `Api.ListAsync()`; Fehler wie `StartRecent` (`StartInputsError`, Teil `inputs`). H2 `NavInputs`; `<ul class="start-inputs">` je `StartInputLine`: `<li class="start-input @(problem ? "start-input-problem" : null)">` Symbol `InputTabs.KindIcon` `aria-hidden`, Name `InputTexts.Kind(L, kind)` in `<span class="start-input-name">`, `<Badge Icon="@InputTabs.BadgeIcon(state)" Text="@L[InputTabs.BadgeKey(state)]" />`; je Problemzeile `<p>` `StartInputProblemFor` mit `AccountName ?? InputTexts.Kind(...)` und `InputTexts.Problem(L, row.Problem!)`; bei Problem Link `eingaenge?quelle=@kind` mit Text `StartInputOpen` (Art-Name). Unter der Liste Link `eingaenge` `StartInputsAll`. Keine Pfade, keine Adressen (§2.3) | UI | 80 |
| `Pages/Home.razor` | ändern | H1 `<MudText Typo="Typo.h1">` = `Auth.Me is { } me ? L["HomeGreeting", me.DisplayName] : "NoMorePaper"`; `<AdminResetNotice />`; `<MudText Typo="Typo.body1">HomeIntro</MudText>` (bleibt erstes `main p`: `AccessibilityTests` prüft Farbe/16 px); `<div class="start-cards">` mit `StartReviewCard`, `StartRecent`, `StartInputsCard`, dann `<section class="start-actions" aria-labelledby="start-actions-heading">` mit H2 `StartActionsHeading` (`nmp-visually-hidden`) und zwei Links `hochladen` (`nmp-button nmp-button-primary start-action`, `UploadFile` `aria-hidden` + `StartUpload`) und `kamera` (`nmp-button start-action`, `PhotoCamera` + `StartCamera`). Kein `<nav>`, kein `header` (Tests zählen je 1) | UI | 40 |
| `Accounts/AdminResetNotice.razor` | ändern | `MudButton` → `<button type="button" class="nmp-button nmp-button-primary" disabled="@_busy" @onclick="AcknowledgeAsync">`; Symbol `aria-hidden="true"` | bestehende `AdminResetNoticeTests` | ±3 |
| `wwwroot/css/accounts.css` | ändern | Block `.reset-notice*` (Ende der Datei) auf Design-Werte: Fläche `--nmp-paper`, Rand 1px `--nmp-border-strong`, Radius `--nmp-radius-lg`, Symbol `--nmp-error`, Text 500 statt 700 – sonst nichts in dieser Datei | – | ±12 |
| `wwwroot/css/start.css` | ändern | `.start-cards` Spalte, `gap: var(--nmp-space-xl)`, `max-width: 64rem`, `margin-top: var(--nmp-space-xl)`; `.start-card` `--nmp-paper-low`, Rand `--nmp-border`, `--nmp-radius-lg`, `padding: var(--nmp-space-lg)`; `.start-review` flex, `flex-wrap`, Link rechts (`margin-inline-start:auto`); Kartenkopf flex `space-between`; `.start-table-scroll` `overflow-x:auto`; `.start-inputs` Liste ohne Punkte, je `li` Fläche `--nmp-elevated`, Radius, Raster Name/Abzeichen; `.start-input-problem .nmp-badge { border-color: var(--nmp-error); color: var(--nmp-error); }` (Fehler = Rot erlaubt, Text trägt die Info); `.start-actions` zwei Spalten ab 48rem, eine darunter; `.start-action` `min-height: 4rem; justify-content:center; font-size:1rem` | – | 130 |
| `UI/Start/FakeStart.cs` | neu | Muster `UI/Classification/FakeReview.cs`: Eigenschaften `int? ReviewCount` (null → 500), `DocumentSummaryDto[] Recent`, `int RecentStatus = 200`, `List<InputStatusDto> Inputs`, `int InputsStatus = 200`; `OpenAsync(ServerFixture, string culture)` mit `FakeAccounts.InstallAsync(page, signedIn: true)` und Routen `/api/review/count$`, `/api/documents\?page=1&pageSize=5` (Regex), `/api/inputs$` | – | 80 |
| `UI/Start/StartPageTests.cs` | neu | (1) H1 „Guten Tag, Anna Beispiel“; (2) Zähler 3 → H2 „Zu prüfen (3)“, „3 Dokumente zu prüfen.“, Link „Jetzt prüfen“ `href=zu-pruefen`; Zähler 0 → „Nichts zu prüfen …“, kein Link; Zähler-Fehler → `StartReviewUnknown`; (3) `SampleDocuments.OneOfEachState()` → Tabelle mit Spaltenköpfen, Statuswörtern, Titel-Links; Link „Alle Dokumente anzeigen“; (4) Eingänge: scanner ok + iCloud-Zeile „Ben“ `sign-in-failed` → erste `li` ist iCloud, Text „Ben Beispiel: …“, Link „Eingang iCloud Drive öffnen“ `href=eingaenge?quelle=icloud`; (5) Liste 500 + Eingänge 500 → beide Fehlertexte, `main [role=alert]` = 0, „Erneut versuchen“ lädt nach (Status danach 200); (6) Links „Datei hochladen“/„Mit Kamera aufnehmen“; (7) axe DE/EN mit Daten und mit Fehlern (`AxeCheck.NoViolationsAsync`) | selbst | 220 |
| `UI/Accounts/SignInTests.cs` | ändern, falls rot | Zeilen ~63/120 erwarten H1 „NoMorePaper“ nach der Anmeldung auf `/`: nur wenn sie die Startseite meinen → `ToHaveTextAsync(new Regex("^Guten Tag, "))` | selbst | ±4 |

- **Reihenfolge:** `StartInputLine` → `StartInputs` + Unit-Tests → `StartLog` → drei Karten → `Home.razor` → CSS →
  `AdminResetNotice` → Build → bestehende Tests auf `/` (`Accessibility`, `Keyboard`, `Layout/NavigationTests`,
  `AdminResetNoticeTests`, `SignInTests`) → neue UI-Tests.
- **Telemetrie:** 4800 `StartPartLoaded` (Debug), 4801 `StartPartFailed` (Warning), Block 4800–4899 „Startseite“ – Katalog in E5.
- **Akzeptanz:** `dotnet test --project tests/NoMorePaper.Web.Client.Tests/NoMorePaper.Web.Client.Tests.csproj` grün;
  `dotnet test --project tests/NoMorePaper.UI.Tests/NoMorePaper.UI.Tests.csproj -- --filter-namespace NoMorePaper.UI.Tests.Start`
  grün; dazu die Klassen `AccessibilityTests`, `KeyboardTests`, `NavigationTests`, `AdminResetNoticeTests`, `SignInTests` grün;
  Tab-Reihenfolge Kopf → Seitenleiste unverändert; jede Datei < 250 Zeilen.

### Paket E2 – Zu prüfen und Listen (Entwürfe 06, 08) · Modell: **Sonnet** · Effort: `medium`

Branch `feat/m04u-e2-pruefen-listen`. Ein Paket, weil beide `ClassificationAccessibilityTests.cs` und `classification.css` ändern.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Classification/ReviewDuplicateSection.razor` | neu | aus `Review.razor` ausgelagert (hält `Review.razor` < 200): Parameter `ReviewItemDto Item`, `DuplicateCandidateDto Other`, `EventCallback OnKeep`, `EventCallback OnAskDelete`. Innere Fläche `.review-duplicate`, H3 mit Symbol `ContentCopy` `aria-hidden`, Erklärung, Vergleichstabelle **unverändert** (caption, `scope`), Knöpfe „Beide behalten“ (`nmp-button`) und „Dieses löschen“ (`nmp-button`, Symbol `Delete` `aria-hidden`, **kein Rot**), IDs/`aria-label` wie heute (`review-delete-{key}`) | bestehende | 60 |
| `Pages/Review.razor` | ändern | H1 `Typo.h2 HtmlTag="h1"` (id/tabindex bleiben); Ladefehler → `<div data-ready><NoticeBox Kind="Error" Announce="true" Message=…/></div>` + Retry `#review-retry`; Karte `article.review-item` (Klassen `review-summary`, `fields-recognition` bleiben, Tests nutzen sie): Abzeichen „Automatisch erkannt/Bestätigt“ als `<Badge Icon Text>` in `<p class="fields-recognition">`, Symbol `aria-hidden`; Angaben `dl.review-fields` (3 Spalten ab 48rem, Etikett Mono groß); Tags als `<ul class="review-tags">` mit `<li><Badge Text="@tag.Name" /></li>`, leer `ClsNoValue`; `ReviewDuplicateSection`; Rückfrage → `<ConfirmBox Id="@("review-confirm-" + key)" HeadingLevel="3" Title="@L["ReviewDeleteQuestion", item.Title]" Text="@L["ReviewDeleteExplain"]" ConfirmLabel="@L["ReviewDeleteConfirm"]" OnConfirm="DeleteAsync" OnCancel="CancelDelete" />`; Knöpfe „Bestätigen“ `nmp-button nmp-button-primary` mit `Check`, „Bearbeiten“ `nmp-button` mit `Edit` (Symbole `aria-hidden`, `aria-label` wie heute) | UI | 130 |
| `Pages/Review.razor.cs` | ändern | `OnConfirmKey` entfernen; `AskDelete` setzt nur `_deleting` (kein `_focus.On("review-cancel")`) | UI | −10 |
| `Classification/OwnerCheckSection.razor` | ändern | `div.error-box[role=alert]` → `<NoticeBox Kind="Error" Announce="true" Message=…>` (Inhalt/Knopf gleich) | `UI/Inputs/OwnerCheckTests` | ±4 |
| `Pages/Lists.razor` | ändern | `<Breadcrumb Items="_path" />` (`NavSettings` → `einstellungen`, `ListsHeading` aktuell; Muster `Pages/Inputs.razor`), H1 `Typo.h2 HtmlTag="h1"`, `ListsIntro` | UI | 20 |
| `Classification/ListSection.razor` | ändern | Abschnitt als Karte (`.list-section` bleibt); Ladefehler → `NoticeBox Announce` + Retry; Tabelle unverändert (caption, `th scope=row`, Namen der Knöpfe); „Umbenennen“ mit Symbol `Edit` `aria-hidden`; Zeile unter der Tabelle `.list-footer` (Zähler links, Knopf rechts). Umbenennen → `<ConfirmBox Id="@(Prefix + "-rename")" HeadingLevel="3" Title="@L["ListsRenameHeading", _renaming.Name]" ConfirmLabel="@L["ListsRenameConfirm"]" PrimaryConfirm="true" InitialFocusId="@(Prefix + "-rename-input")" OnConfirm="RenameAsync" OnCancel="CancelRename">` mit Feld als `ChildContent`; Enter nur im Feld: `@onkeydown="OnRenameInputKey"`. Zusammenführen → `<ConfirmBox Id="@(Prefix + "-merge-box")" HeadingLevel="3" Title=ListsMergeQuestion Text=ListsMergeExplain ConfirmLabel=ListsMergeConfirm OnConfirm="MergeAsync" OnCancel="CancelMerge">` mit Auswahlfeld als `ChildContent` | UI | 95 |
| `Classification/ListSection.razor.cs` | ändern | `OnRenameKey` → `OnRenameInputKey(KeyboardEventArgs)` nur `Enter` → `RenameAsync()`; `OnMergeKey` entfernen (Escape macht `ConfirmBox`); `AskRename` ohne `_focus.On` (macht `InitialFocusId`); `AskMerge` ohne `_focus.On(…-merge-cancel)` | UI | ±15 |
| `wwwroot/css/classification.css` | ändern | Blöcke `.review-list`, `.review-item`, `.review-duplicate`, `.list-section`, `.list-select`, `.list-selected` **verschieben** nach `review.css`/`lists.css` (`.fields-*` bleiben, der Viewer nutzt sie) | – | −50 |
| `wwwroot/css/review.css` | ändern | Werte aus 06: Liste ohne Punkte, `gap: var(--nmp-space-lg)`, `max-width: 56rem`; `.review-item` `--nmp-surface-container`, Radius `--nmp-radius-lg`, `padding: var(--nmp-space-lg)`; `.review-fields` Raster, `dt` Mono 11 px groß `--nmp-outline`, Datum/Betrag Mono; `.review-tags` Chips in einer Zeile; `.review-duplicate` `--nmp-paper-low`, innerer Radius, Abstand | – | 110 |
| `wwwroot/css/lists.css` | ändern | Werte aus 08: `.list-section` Karte (wie `.review-item`), Abstand zwischen Karten `--nmp-space-lg`; `.list-footer` flex `space-between`, `flex-wrap`; Checkbox 1.125rem | – | 60 |
| `UI/Classification/ReviewPageTests.cs` | ändern | `Delete_…`: statt „Abbrechen“ fokussiert → `box.GetByRole(AriaRole.Heading)` fokussiert, dann `Tab` → „Abbrechen“ fokussiert; Escape → „Löschen: …“ fokussiert (wie heute); neuer Test: Bestätigen-Knopf der Rückfrage hat Klasse `nmp-button` ohne `nmp-button-primary` (neutral) | selbst | ±20 |
| `UI/Classification/ListsPageTests.cs` | ändern | `Merge_…`: Überschrift der Gruppe fokussiert statt „Abbrechen“; `Rename_…` unverändert (Feld fokussiert, Enter speichert, Escape bricht ab); neu: Enter auf „Abbrechen“ speichert **nicht** (Server nicht gerufen); Pfad „Einstellungen“ ist ein Link nach `einstellungen` | selbst | ±25 |
| `UI/Classification/ClassificationAccessibilityTests.cs` | ändern, nur falls rot | Selektoren auf `.nmp-confirm` statt `.confirm-box`; axe-Fälle (Review mit offener Rückfrage, Listen mit beiden Boxen) bleiben | selbst | ±6 |

- **Reihenfolge:** `ReviewDuplicateSection` → `Review.razor(.cs)` → `OwnerCheckSection` → `Lists`/`ListSection(.cs)` → CSS →
  Build → Tests anpassen → Namespace `Classification` und `Inputs.OwnerCheckTests` laufen lassen.
- **Telemetrie:** keine neuen Ereignisse (4506–4511 bleiben unverändert).
- **Akzeptanz:** `dotnet test --project tests/NoMorePaper.UI.Tests/NoMorePaper.UI.Tests.csproj -- --filter-namespace NoMorePaper.UI.Tests.Classification`
  grün, axe 0 Verstöße DE/EN mit offenen Rückfragen; `OwnerCheckTests` grün; keine Datei > 250 Zeilen.

### Paket E3 – Konto und Mitglieder (Entwürfe 09, 10) · Modell: **Sonnet** · Effort: `medium`

Branch `feat/m04u-e3-konto-mitglieder`. Ein Paket, weil beide `AccountPagesAccessibilityTests.cs` und `account-page.css` teilen.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Pages/Account.razor` | ändern | `<Breadcrumb>` (`NavSettings` → `einstellungen`, `AccountHeading` aktuell); Kopfzeile `.account-head`: H1 `Typo.h2 HtmlTag="h1"` links, `<p data-ready>` „Angemeldet als … Verwalter“ rechts (**Text unverändert**, `AccountPageTests` prüft ihn); `<div class="account-grid">` in Lesereihenfolge (F1-Empfehlung) Passwort-Karte (`section.account-card.account-password`), Abmelden-Karte (`.account-signout`), `<PreferencesSection />` (volle Breite). `MudButton` → native Knöpfe: Passwort `type="submit" class="nmp-button nmp-button-primary" disabled="@_busy"` rechtsbündig; Abmelden zwei `nmp-button` | UI | 120 |
| `Accounts/PreferencesSection.razor` | ändern | Karte `section.account-card.account-prefs`; Ladefehler → `NoticeBox Announce` + Retry `#prefs-retry` als `ChildContent`; Kästchen + Hinweis in `.prefs-private` (eigene Fläche); Adressliste: je `li` Symbol `Mail` `aria-hidden` + Adresse (kein Abschneiden, `overflow-wrap:anywhere`) + „Entfernen“ (Name wie heute); Formular als Zeile `.prefs-add` (Feld + Knopf). Logik unverändert | `UI/Inputs/PreferencesTests` | ±25 |
| `Pages/Members.razor` | ändern | `<Breadcrumb>` (`NavSettings` → `einstellungen`, `NavMembers` aktuell), H1 `Typo.h2 HtmlTag="h1"`; Einladen-Karte `.members-invite`: H2 + Erklärung links, Knopf `nmp-button nmp-button-primary` rechts (Text `MembersInviteButton` bleibt); Ergebnisfläche `.members-link` statt `.link-result` (gilt auch für Rücksetzlink); Ladefehler → `NoticeBox Announce`; Rolle als `<Badge Icon="@(member.IsAdmin ? AdminPanelSettings : Person)" Text=…>`; Caption sichtbar; Rückfrage → `<ConfirmBox Id="members-remove" Title="@L["MembersRemoveQuestion", _removing.DisplayName]" Text="@L["MembersRemoveExplain"]" ConfirmLabel="@L["MembersRemoveConfirm"]" OnConfirm="RemoveAsync" OnCancel="CancelRemove" />` (H2, neutral) | UI | 110 |
| `Pages/Members.razor.cs` | ändern | `_cancelButton`, `OnConfirmKey` entfernen; `AskRemove` setzt nur `_removing` | UI | −10 |
| `wwwroot/css/account-page.css` | ändern | `.account-head` flex `space-between`, `align-items:flex-end`, `flex-wrap`; `.account-grid` eine Spalte, ab 64rem `grid-template-columns: 7fr 5fr`, Einstellungen `grid-column: 1 / -1`; `.account-card` `--nmp-surface-container`, `--nmp-radius-lg`, `padding: var(--nmp-space-lg)`; `.prefs-private` `--nmp-paper-low`, Radius; `.sender-list li` flex `space-between`; `.prefs-add` flex, `align-items:flex-end`, `flex-wrap`; `.members-invite` flex `space-between`; `.members-link` innere Fläche statt 3-px-Rahmen | – | 120 |
| `UI/Accounts/MembersTests.cs` | ändern | `Remove_AsksFirst…`: Überschrift der Gruppe fokussiert (statt „Abbrechen“), `Tab` → „Abbrechen“; Escape → „Neues Mitglied einladen“ fokussiert (bleibt); `.link-result` → `.members-link`; neu: Rolle „Verwalter“ als Text im Abzeichen | selbst | ±15 |
| `UI/Accounts/AccountPagesAccessibilityTests.cs` | ändern | Zeile 102 `.confirm-box` → `.nmp-confirm` | selbst | ±1 |
| `UI/Accounts/AccountPageTests.cs` | ändern, nur falls rot | Knopf-Suche per Rolle/Name bleibt gültig; nur anpassen, wenn ein Test auf `MudButton`-Klassen zielt | selbst | ±5 |

- **Reihenfolge:** `Account.razor` → `PreferencesSection` → `Members.razor(.cs)` → CSS → Build → Tests.
- **Telemetrie:** keine neuen Ereignisse (4403, 4407, 4409–4411, 4701/4702 bleiben).
- **Akzeptanz:** `dotnet test --project tests/NoMorePaper.UI.Tests/NoMorePaper.UI.Tests.csproj -- --filter-namespace NoMorePaper.UI.Tests.Accounts`
  und `--filter-class NoMorePaper.UI.Tests.Inputs.PreferencesTests` grün; axe 0 Verstöße DE/EN auf `/konto`, `/mitglieder`
  (mit offener Rückfrage); bei 200 % Zoom eine Spalte.

### Paket E4 – Suche, Hinzufügen, Hochladen, Kamera (Entwürfe 07, 11–13) · Modell: **Sonnet** · Effort: `medium`

Branch `feat/m04u-e4-suche-hinzufuegen`. Ein Paket, weil alle Regeln aus `components.css` verschoben werden.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Pages/Search.razor` | ändern | H1 `Typo.h2 HtmlTag="h1"`; Fehler → `<div data-ready><NoticeBox Kind="Error" Announce="true" Message=…/></div>`; Zusammenfassung `p.search-summary` direkt unter H1; Treffer in `<section class="search-results" aria-label="@L["SearchResultsLabel"]">` (**keine** H2 dazu: `SearchResults_HaveNoAxeViolations` zählt `main h2` = Treffer); `article.search-hit` als Raster: `Thumbnail` links, rechts H2-Link, `PrivateBadge`, Ausschnitt (reiner Text, kein Abschneiden), `p.search-hit-date` | `UI/Search`, `UI/Documents/DocumentPagesAccessibilityTests`, `UI/Accounts/PrivacyTests` | 120 |
| `Pages/Add.razor` | ändern | `ul.link-tiles.link-tiles-large`, je `li.link-tile`: `<span class="link-tile-icon"><MudIcon … aria-hidden="true" /></span>`, `<MudText Typo="Typo.h3" HtmlTag="h2"><a class="link-tile-title" href … aria-describedby …><span>NavUpload</span><MudIcon Icon="ArrowForward" aria-hidden="true" /></a></MudText>`, `<p id=…>` wie heute | `AccessibilityTests` (`/hinzufuegen`) | 30 |
| `Pages/Upload.razor` | ändern | H1 `Typo.h2 HtmlTag="h1"`; `PrivateSwitch` in `<div class="upload-private">` (Fläche); Ablagefeld: Kreis-Symbol `CloudUpload` `aria-hidden` über dem Label, `InputFile` bleibt sichtbar (App-Lösung); übersprungen → `<div class="upload-skipped"><NoticeBox Kind="Error" Icon="@Icons.Material.Filled.Warning" Message="@_skipped.Notice"><p>UploadSkippedListLabel</p><ul class="upload-skipped-names">…</ul></NoticeBox></div>` (kein `role`: Ansage kommt aus der `LiveRegion`); Ergebnis-Symbole `aria-hidden="true"` mit Klassen `upload-ok`/`upload-info`/`upload-failed` | `UI/Documents/UploadTests`, `ClassificationAccessibilityTests.Upload_…` | ±25 |
| `Pages/Camera.razor` | ändern | H1 `Typo.h2 HtmlTag="h1"`; Fehler → `<div class="camera-error" data-code=…><NoticeBox Kind="Error" Message=…><a href="hochladen">CamUploadInstead</a></NoticeBox></div>` (Stelle bleibt vor den Schaltern); Schalter in `<div class="capture-options">`; Inline-`style` an `.camera-stage` und `video` entfernen → CSS; Hinweiszeile `p.camera-hint` mit Symbol `Info` `aria-hidden`; Knöpfe `.camera-actions`: „Auslösen“ `nmp-button nmp-button-primary`, Rest `nmp-button`; gespeichert → `<div class="camera-saved" tabindex="-1" @ref="_savedBox"><NoticeBox Kind="Success" Icon="@(saved.IsDuplicate ? Info : null)" Message=…>Link</NoticeBox></div>` | `UI/Camera/*` | ±25 |
| `wwwroot/css/components.css` | ändern | `.search-hit*` → `search.css`; `.drop-zone*`, `.upload-results*`, `.upload-skipped` → `capture.css` (verschieben, nicht ändern); `.link-tile*` bleibt (Einstellungen) | – | −80 |
| `wwwroot/css/search.css` | ändern | `.search-hit` Raster `grid-template-columns: 5rem 1fr`, `gap: var(--nmp-space-md)`, Fläche `--nmp-paper-low`, Radius, Hover eine Stufe heller; `.search-hit-date` Mono 11 px `--nmp-outline`; unter 30rem eine Spalte | – | 50 |
| `wwwroot/css/capture.css` | ändern | verschobene Regeln + `.link-tiles-large` (zwei Spalten ab 48rem, `.link-tile-icon` Fläche 3rem, `h2` ohne Rand, Schrift `headline-md`); `.upload-private`, `.capture-options` `--nmp-paper-low`, Radius, `padding: 1.25rem`; Ablagefeld gestrichelt (`--nmp-border-strong`), Kreis 3rem; `.upload-skipped .nmp-notice-body { flex-direction: column; align-items: flex-start; }`, `.upload-skipped-names` Mono; `.upload-failed { color: var(--nmp-error) }`; `.camera-stage { margin: var(--nmp-space-md) 0; border: 3px solid var(--nmp-primary); max-width: 48rem; }`, `.camera-stage video { display:block; width:100%; max-height:60vh; object-fit:contain; background:#000; }`; `.camera-hint` `--nmp-elevated-high`, flex mit Symbol, `body-lg`; `.camera-actions .nmp-button` volle Breite unter 48rem, `min-height: 44px` | – | 190 |
| `UI/Search/SearchTests.cs` | ändern, nur falls rot | Selektoren `.search-hit` bleiben | selbst | ±3 |

- **Reihenfolge:** Suche → Hinzufügen → Hochladen → Kamera → CSS-Verschiebung → Build →
  Namespaces `Search`, `Documents`, `Camera`, `Layout`, `Accessibility` laufen lassen.
- **Telemetrie:** keine neuen Ereignisse (4100–4107, 4600–4605 bleiben).
- **Akzeptanz:** obige Namespaces + `WCT` grün; axe 0 Verstöße DE/EN auf `/suche`, `/suche?q=…`, `/hinzufuegen`,
  `/hochladen`, `/kamera`; Kamera-Tab-Reihenfolge Töne → Privat → Starten (`CameraPageTests.TabOrder_TonesPrivateStart`)
  unverändert; `capture.css` < 250 Zeilen (sonst `camera.css` abtrennen, Zeile in `index.html` meldet die Spur der
  steuernden Sitzung).

### Paket E5 – Aufräumen, Doku, Häkchen · Modell: **Haiku** · Effort: `low` (CSS-Teil: steuernde Sitzung)

Nach dem Merge von E1–E4 im Integrationsbranch.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `wwwroot/css/accounts.css` | ändern (steuernde Sitzung) | `.confirm-box`, `.link-result`, `.success-box` löschen, wenn `grep` in `src/` keinen Nutzer mehr findet | alle UI-Tests | −20 |
| `docs/kb/oberflaeche.md` | ändern | Bausteine: Zeile „Rückfrage“ (Gruppe mit Namen, Escape, `ChildContent`, `PrimaryConfirm`, `InitialFocusId`), „Hinweisbox“ (`Icon`); neue Abschnitte „Startseite“ (Karten, Datenquellen, Problem zuerst, keine Alarme beim Laden) und kurz je Seite 06–13, was nicht übernommen wurde | – | +40 |
| `docs/kb/telemetrie.md` | ändern | Block 4800–4899 „Startseite“ in „EventId-Bereiche“; Zeilen 4800/4801 | – | +3 |
| `docs/plan/m4u-oberflaeche.md` | ändern | Aufgabe E: alle fünf Punkte abhaken; Eintrag „14–16 an M4b übergeben“ | – | 6 |
| `docs/ui/stitch/README.md` | ändern | 05–13 „umgesetzt (Abnahme offen)“ – erst nach E6 | – | 9 |

- **Akzeptanz:** Lesen durch die steuernde Sitzung; Build und Tests nach der CSS-Löschung grün.

### Paket E6 – Review und Abnahme · Modell: **Opus** · Effort: `high` (steuernde Sitzung)

- Je Spur eine Review-Runde per Subagent (Lehre aus A–D), dann Gesamt-Review §8 über ganz E bis ohne Befund.
- Abnahme nach UI-Prozess Schritt 8: Playwright-Bilder je Seite (1440 px, Start zusätzlich 375 px) neben `screen.png` in
  `TestResults/`; Zoom 200 %/400 % (eine Spalte, kein waagerechtes Scrollen außer Tabellenbereich); Tastaturweg je Seite;
  Abgleich jeder Zeile „Abgleich mit der App“/„Nicht mitnehmen“ in den Notizen 05–13 (Checkliste im Review-Bericht).
- NVDA/VoiceOver durch den Nutzer; Merge nach `develop` nur mit Freigabe (§8.5).

## Parallelisierung in Spur E

| Spur | Pakete | Branch | Start | wartet auf |
|---|---|---|---|---|
| E0 | Bausteine, Texte, CSS-Dateien | `feat/m04u-oberflaeche` | sofort (nach F1) | – |
| S1 | E1 Start | `feat/m04u-e1-start` | nach E0 | E0 |
| S2 | E2 Zu prüfen + Listen | `feat/m04u-e2-pruefen-listen` | nach E0 | E0 |
| S3 | E3 Konto + Mitglieder | `feat/m04u-e3-konto-mitglieder` | nach E0 | E0, F1 |
| S4 | E4 Suche, Hinzufügen, Hochladen, Kamera | `feat/m04u-e4-suche-hinzufuegen` | nach E0 | E0 |

```
E0 ──┬─► S1 ─┐
     ├─► S2 ─┤
     ├─► S3 ─┼─► Merge S2, S3, S4, S1 ─► E5 ─► E6
     └─► S4 ─┘
```

- **Exklusiv:** S1 `Pages/Home.razor`, `Start/`, `AdminResetNotice.razor`, `accounts.css`, `start.css`, `UI/Start/`,
  `WCT/Start/`, `SignInTests.cs`; S2 `Review*`, `Lists.razor`, `ListSection*`, `OwnerCheckSection.razor`,
  `ReviewDuplicateSection.razor`, `classification.css`, `review.css`, `lists.css`, `UI/Classification/*`; S3 `Account.razor`,
  `PreferencesSection.razor`, `Members*`, `account-page.css`, `UI/Accounts/*` (außer `SignInTests.cs`); S4 `Search.razor`,
  `Add.razor`, `Upload.razor`, `Camera.razor`, `components.css`, `search.css`, `capture.css`, `UI/Search`,
  `UI/Documents`, `UI/Camera`.
- **Hotspots:** keine in S1–S4 – Texte, `index.html`, `Design/`, `design*.css` sind nach E0 fertig. Fehlt etwas, meldet die
  Spur es; die steuernde Sitzung ergänzt es im Integrationsbranch, die Spuren mergen ihn nach. Telemetrie-Katalog nur in E5.
- **Last:** vier Worktrees mit Playwright waren in A–D langsam und vereinzelt wackelig. Wenn ein UI-Lauf zeitabhängig
  scheitert: erst wiederholen, dann melden; bei wiederholtem Wackeln S1/S2 vor S3/S4 laufen lassen.
- **Merge-Reihenfolge:** S2 (erste `ConfirmBox`-Nutzer) → S3 → S4 → S1 (Start zuletzt, berührt Tests auf `/`). Nach jedem
  Merge Build und alle Tests.
- **Eskalation (alle Spuren):** zwei rote Läufe am selben Paket, ein fehlender Text, eine nötige Änderung an gesperrten
  Dateien oder eine Entscheidung, die dieser Plan nicht trifft → stoppen und der steuernden Sitzung melden.

## Risiken

- **Startseite und Tests auf `/`:** Viele Tests öffnen `/` (Tastatur, Navigation, Hinweis nach Zurücksetzen, axe). Die
  Übersicht lädt dort echte Daten aus der gemeinsamen Test-Datenbank; die Karten dürfen kein `role=alert`, kein `nav`,
  kein `header` und vor `HomeIntro` kein `p` bringen. Deshalb Fehler auf Start ohne `Announce`.
- **Doppelte Zähler-Anfrage:** `StartReviewCard` fragt `api/review/count` beim Öffnen selbst, `MainLayout` bei jeder
  Navigation auch – eine kleine Anfrage doppelt, dafür ist die Karte nach dem ersten Laden sicher aktuell.
- **Kein Nachfragen auf Start:** „Wird verarbeitet“ bleibt stehen, bis die Seite neu geöffnet wird (Polling nur in der
  Dokumentliste). Bewusst einfach; bei Wunsch später Muster `Documents.Polling.cs`.
- **Fokuswechsel der Rückfragen:** Mit `ConfirmBox` hört der Screenreader zuerst die Frage, „Abbrechen“ ist einen Tab
  weiter. Das ändert die bisherige Bedienung von Review, Listen und Mitglieder (bisher Fokus auf „Abbrechen“); es folgt dem
  Baustein-Vertrag aus Schritt 0. NVDA-Prüfung durch den Nutzer in E6.
- **CSS-Wachstum:** Lehre aus A–D – CSS-Dateien wuchsen über 250 Zeilen. Darum je Seite eine eigene Datei und `design.css`
  in E0 geteilt; `capture.css` ist mit ~190 Zeilen die größte.
- **Rest alter Boxen:** `RequireSignIn` und `EditorsPicker` behalten `error-box` (nicht Spur E) – im Gesamt-Review vermerken.

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|
