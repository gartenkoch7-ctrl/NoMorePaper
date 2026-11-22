# M4u Spur D – Eingänge: Detailplan

Aufgabe D aus [m4u-oberflaeche.md](../m4u-oberflaeche.md): `/eingaenge` mit Reitern je Eingang und Karte je
Quelle, `/eingaenge/mail` mit Postfach-Karte und Regelliste. Regeln: [AGENTS.md §12](../../../AGENTS.md),
[Vorlage](../detailplan-vorlage.md).

Grundlagen: [notizen.md 04](../../ui/stitch/04-eingaenge/notizen.md) (nur „Übernehmen“),
[screen.webp](../../ui/stitch/04-eingaenge/screen.webp), [Stitch-README](../../ui/stitch/README.md) (verworfene
Screens), [KB oberflaeche.md](../../kb/oberflaeche.md), [KB ui-prozess.md](../../kb/ui-prozess.md),
[ADR-0026](../../adr/0026-optik-nach-stitch-design-system.md), [ADR-0024](../../adr/0024-eingaenge.md),
[KB eingaenge.md](../../kb/eingaenge.md) (Abschnitte „Hintergrund-Eingänge“, „Seite Eingänge“, „Mail“).

## Offene Fragen an den Nutzer

> **Beantwortet 2026-11-22:** Der Nutzer hat alle Fragen wie empfohlen entschieden. Der Plan gilt so.

Diese Fragen müssen vor dem Start beantwortet sein. **Der Plan unten ist für die empfohlene Option
geschrieben**; die letzte Spalte sagt, was sich bei einer anderen Antwort ändert.

Was die API heute liefert (`GET /api/inputs` → `InputStatusDto`): Art, Konto-Name, Zustand, Problem,
„braucht Aufmerksamkeit“, **Zuletzt gelaufen** (`LastRunAt`), **Zuletzt erfolgreich** (`LastSuccessAt`).
**Nicht** im DTO, aber in der Datenbank: die Summe der angenommenen Dokumente seit Einrichtung
(`input_status.received_total`, in `Application/Inputs/InputStatus.ReceivedTotal`). **Gar nicht** gespeichert:
Dokumente der letzten 24 Stunden und Dokumente des letzten Laufs. Das Postfach (Server, Benutzer,
Verschlüsselung) liefert `GET /api/inputs/mail` nur dem Verwalter. Der Abruf-Takt ist fest im Code
(Scanner-Ordner 30 s, Mail 5 min, iCloud 15 min).

| Nr. | Frage | Optionen | Empfehlung | Bei der anderen Antwort |
|---|---|---|---|---|
| F1 | Der Entwurf zeigt in jedem Reiter eine Zahl („2 Aktiv“, „2 Konten“). Welche Angabe soll im Reiter neben dem Namen stehen? Mit Reitern sieht man immer nur einen Eingang; die Angabe im Reiter ist daher die einzige Übersicht über alle drei. | **A:** Der Zustand kurz: „1 Problem“ / „2 Probleme“, sonst „läuft“, „nicht eingerichtet“ oder „noch nicht gelaufen“ (auch die Idee „Status-Zähler je Eingang“ aus dem verworfenen Screen). Keine Server-Änderung. **B:** Die Zahl der angenommenen Dokumente seit Einrichtung (braucht F2-B). **C:** Die Zahl der Einträge („2 Konten“) – sinnvoll nur bei iCloud, Scanner-Ordner und Mail hätten immer „1“. **D:** Keine Angabe im Reiter. | A | B: `InputTabs.Summary` liefert die Summe von `ReceivedTotal`, Texte `InpTabDocuments*` statt `InpTab*`. C: Summary zählt Zeilen, nur bei `icloud`. D: `TabItem.Count` bleibt `null`, P1 ohne Count-Teil. |
| F2 | Der Entwurf zeigt auf der Karte „Dokumente (24 h): 12“ und „Letzter Lauf … (14 Seiten)“. Beides speichert NoMorePaper nicht. Was soll die Karte stattdessen zeigen? | **A:** Keine Dokumentzahl, nur Zustand und Zeiten. Keine Server-Änderung. **B:** „Angenommen insgesamt: 128 Dokumente“ aus der vorhandenen Summe (`received_total`). Kleine Server-Änderung: ein Feld mehr im DTO. Mitglieder sehen dann auch die Gesamtzahl des gemeinsamen Scanner-Ordners und Postfachs (nur eine Zahl, kein Inhalt, keine Namen). **C:** Echte Zahl der letzten 24 Stunden: neue Abfrage über `documents.source` und Eingangszeit, mit Sichtbarkeitsregel – eigener Backend-Plan (Stufe 3), läuft neben M4b. | B | A: P0 entfällt, Zeile „Angenommen insgesamt“ in `InputStateFacts` entfällt. C: P0 wird ein eigener Backend-Detailplan (Opus), Spur D wartet mit der Zeile auf ihn. |
| F3 | Wo steht die Regelliste „So behandelt NoMorePaper Mails“ (Absender je Konto, Spam-Prüfung, Mails ohne Anhang, Anhänge, Ordner im Postfach)? Der Meilensteinplan nennt die Mail-Einstellungen; die sieht aber nur der Verwalter. Die Regeln sind fest und nicht einstellbar. | **A:** Nur auf `/eingaenge/mail` (Verwalter). **B:** Nur im Reiter „Mail“ auf `/eingaenge` (alle Konten). **C:** An beiden Stellen, ein gemeinsamer Baustein; er ersetzt auf `/eingaenge` den Satz zum Spam-Ordner und auf `/eingaenge/mail` den Abschnitt „Ordner im Postfach“. | C | A: `MailRules` nur in `MailSettings.razor`, `InpMailSpamFolder` bleibt im Reiter. B: `MailRules` nur in `InputsPanel` (Mail), „Ordner im Postfach“ bleibt auf `/eingaenge/mail`. |
| F4 | Die Reiter aus Schritt 0 (`.nmp-tab`) sind in Geist 14 px, der gewählte fett. Im Entwurf 04 – dem einzigen Entwurf mit Reitern – sind sie in JetBrains Mono 11 px (`label-md`), Text `#c4c7c8`, gewählt weiß mit weißer Linie, 1,5 rem Abstand, mit Symbol. Welches Aussehen sollen Reiter haben? | **A:** Wie Entwurf 04, **überall** (`design.css` wird angepasst; der Viewer in Spur C bekommt dasselbe Aussehen). **B:** Wie Entwurf 04, nur auf den Eingängen (eigene Klasse). **C:** Schritt-0-Aussehen bleibt. | A | B: Regeln mit Präfix `.inputs-tabs` in `inputs.css`, `TabList` bekommt Parameter `Class`. C: Nur Symbol- und Count-Regeln kommen dazu. |

Durch Vorgaben schon entschieden (keine Frage):
- Werte nur aus den Tokens in `base.css`. Der Entwurf nutzt `surface-container-highest` `#353534`,
  `outline-variant` `#444748` und `rounded-xl` 0,75 rem – dafür gibt es keine Tokens. Ersatz:
  `--nmp-elevated-high`, `--nmp-border`, `--nmp-radius-lg` (Karten 1 rem laut KB).
- Schriftstufen über das Theme: Seiten-H1 im Entwurf ist `headline-lg` (28 px) → `MudText Typo="Typo.h2"
  HtmlTag="h1"`; Abschnitts-H2 und Karten-H3 sind `headline-sm` (16 px) → `Typo.h4` mit `HtmlTag` h2/h3.
- Kein eigener Fokusrahmen (ADR-0026), axe AA. Kein `truncate`: Adressen umbrechen (`overflow-wrap: anywhere`).
- Statuspunkt ohne Puls; Status ist `Badge` mit Symbol und Wort.
- Kein Knopf „Verbindung testen“ auf der Postfach-Karte: Der Test steht schon im Formular daneben und prüft
  die eingegebenen Werte; zwei Test-Knöpfe auf einer Seite wären doppelt.
- Regeln ohne Zahnrad, Abzeichen „Immer an“ statt „Aktiv“ (nicht einstellbar).
- Gewählter Reiter steht nicht in der Adresse (jeder Seitenwechsel setzt per `FocusOnNavigate` den Fokus auf
  die H1); nur der **Start**-Reiter kommt aus `?quelle=mail|scanner-folder|icloud`, damit der Pfad auf
  `/eingaenge/mail` zurück zum Reiter „Mail“ führt. Ohne gültige Angabe: Scanner-Ordner (vorhersehbar).
- Alle drei Panels werden gerendert, die nicht gewählten mit `hidden`; die Live-Region meldet Änderungen
  aller Eingänge wie bisher (`Inputs.LoadAsync` bleibt).
- Kein `<aside>` in den Panels (axe `landmark-complementary-is-top-level`), sondern `div`.
- `Inputs.ICloudPageExists` bleibt `false` und unverändert benannt (M4 Spur D schaltet ihn).

## Detailplan

**Erstellt von:** Opus (Stufe 3) · **Datum:** 2026-11-22 · **Offene Designfragen:** F1–F4 (oben), vor dem
Start zu beantworten

Branch `feat/m04u-d-eingaenge` von `feat/m04u-oberflaeche`, eigener Worktree. Pfade ohne Präfix liegen unter
`src/NoMorePaper.Web.Client/`.

**Exklusiv (Spur D):** `Pages/Inputs*`, `Pages/MailSettings*`, neue Dateien in `Inputs/`, `Inputs/InputTexts.cs`,
`wwwroot/css/inputs.css`, `tests/NoMorePaper.UI.Tests/Inputs/Inputs*`, `…/Inputs/Mail*`,
`tests/NoMorePaper.Web.Client.Tests/Inputs/`. **Nur ergänzen (Hotspot):** `Strings*.resx` (Inp*/Mail*-Werte, die
nur diese Seiten nutzen, dürfen geändert werden; vor dem Löschen eines Schlüssels per Grep prüfen, dass nur
Spur-D-Dateien ihn nutzen), `wwwroot/css/design.css`, `wwwroot/index.html` (eine Zeile),
`Design/TabItem.cs`, `Design/TabList.razor` (nur optionale Parameter, bestehende Aufrufe bleiben gültig).
**Nicht ändern:** `MainLayout.razor`, `ContrastTheme.cs`, `base.css`, `layout.css`, `components.css`,
`accounts.css`, `Inputs/OwnerCheckApi.cs`, `Inputs/PreferencesApi.cs`, Dateien anderer Spuren.

### Einschätzung

| Aufgabenpunkt | Umfang | Risiko | Neuheit | Klarheit | Prüfbarkeit | Stufe |
|---|---|---|---|---|---|---|
| P0 `ReceivedTotal` ins DTO (F2-B) | 3 Dateien, ~8 Zeilen | gering (Feld mit Standardwert, alte Clients ignorieren es) | Muster `InputsMapping.ToDto` | entschieden | Server-Test | 1–2 |
| P1 Reiter mit Symbol/Angabe, Pfad-Baustein | 5 Dateien, ~110 Zeilen | mittel: `TabList` ist geteilt (Spur C) | Muster `TabList`, `ConfirmBox` | nach F1/F4 entschieden | UI-Tests in P2 | 2 |
| P2 Seite `/eingaenge` | 9 Dateien, ~600 Zeilen | mittel: ARIA Reiter/Panels, bestehende Live-Region-Logik | Muster vorhanden | nach F1–F3 entschieden | Unit + UI-Tests, axe | 2 |
| P3 Seite `/eingaenge/mail` | 5 Dateien, ~250 Zeilen | gering: Formularlogik bleibt | Muster | nach F2/F3 entschieden | UI-Tests, axe | 2 |
| P4 KB, Katalog, Häkchen, Stitch-Status | 5 Dateien, ~30 Zeilen | keins | – | entschieden | Lesen | 1 |
| P5 Review §8, Sichtvergleich | – | – | – | – | Review | 3 |

P0–P3 laufen in **einem** Sonnet-Subagenten nacheinander (P0 ist zu klein für einen eigenen Start). P4 Haiku,
P5 Opus.

### Paket P0 – Dokumentsumme im DTO · Modell: **Sonnet** · Effort: `medium` (im Lauf von P1–P3)

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Contracts/Inputs/InputStatusDto.cs` | ändern | letzter Parameter `long ReceivedTotal = 0` mit `<param>`: „Documents taken in since the row exists.“ | – | +2 |
| `src/NoMorePaper.Server/Inputs/InputsMapping.cs` | ändern | `ToDto(InputStatusEntry)`: `status.ReceivedTotal` als letztes Argument | – | +1 |
| `tests/NoMorePaper.Server.Tests/Inputs/InputStatusApiTests.cs` | ändern | im ersten Test: Admin-Zeile `icloud`/`inputs-ben Name` hat `ReceivedTotal == 2` (wird in `RecordAsync` schon mit 2 geschrieben) | selbst | +2 |

- **Akzeptanz:** `dotnet test --project tests/NoMorePaper.Server.Tests/NoMorePaper.Server.Tests.csproj -- --filter-class NoMorePaper.Server.Tests.Inputs.InputStatusApiTests` grün.

### Paket P1 – Reiter und Pfad · Modell: **Sonnet** · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Design/TabItem.cs` | ändern | `public sealed record TabItem(string Id, string Label, string? Icon = null, string? Count = null);` – `Icon`: MudBlazor-Symbol, nur Schmuck; `Count`: kurze Angabe neben dem Namen | – | 10 |
| `Design/TabList.razor` | ändern | im Knopf: `@if (item.Icon is not null) { <MudIcon Icon="@item.Icon" aria-hidden="true" /> }`, `<span>@item.Label</span>`, `@if (item.Count is not null) { <span class="nmp-tab-count">@item.Count</span> }`. Mit `Count` bekommt der Knopf `aria-label="@($"{item.Label}, {item.Count}")"` (Blazor entfernt Leerraum zwischen Elementen; ohne Label hieße der Name „Mail1 Problem“). Tastaturlogik unverändert | P2-UI-Tests | 70 |
| `Design/BreadcrumbItem.cs` | neu | `public sealed record BreadcrumbItem(string Label, string? Href);` – `Href` `null` = aktuelle Seite | – | 5 |
| `Design/Breadcrumb.razor` | neu | `<nav class="nmp-breadcrumb" aria-label="@L["PathLabel"]"><ol>` je Eintrag `<li>`: ab dem zweiten zuerst `<MudIcon Icon="@Icons.Material.Filled.ChevronRight" aria-hidden="true" />`; mit `Href` `<a href>`, sonst `<span aria-current="page">`. Parameter `[EditorRequired] IReadOnlyList<BreadcrumbItem> Items`. Kopfkommentar wie `ConfirmBox`: Spur C soll ihn für die Zurück-Leiste wiederverwenden | P2/P3-UI-Tests | 35 |
| `wwwroot/css/design.css` | ändern (Hotspot) | F4-A: `.nmp-tabs` `gap: var(--nmp-space-lg)`; `.nmp-tab` `display:inline-flex; align-items:center; gap:var(--nmp-space-xs); padding:0 0 0.75rem; min-height:38px; font-family:var(--nmp-font-mono); font-size:0.6875rem; line-height:1rem; letter-spacing:0.02em; font-weight:500; color:var(--nmp-text-variant)`; `.nmp-tab:hover { color: var(--nmp-primary) }`; `.nmp-tab-selected` Farbe/Linie weiß, **`font-weight` bleibt 500** (Auswahl = Linie + Farbe + `aria-selected`); `.nmp-tab .mud-icon-root { font-size:1.125rem }`; `.nmp-tab-count` `padding:0.125rem 0.375rem; border-radius:var(--nmp-radius-sm); background:var(--nmp-paper-low); color:var(--nmp-outline); font-size:0.625rem; line-height:0.875rem; letter-spacing:0.05em`; `.nmp-tab-selected .nmp-tab-count` `background:var(--nmp-elevated-high); color:var(--nmp-primary)`; `.nmp-breadcrumb ol` `display:flex; flex-wrap:wrap; gap:var(--nmp-space-xs); list-style:none; margin:0; padding:0; font-family:var(--nmp-font-mono); font-size:0.6875rem; color:var(--nmp-outline)`; `li` `display:inline-flex; align-items:center; gap:var(--nmp-space-xs)`; `a` Farbe `--nmp-outline`, Hover `--nmp-text`; `[aria-current]` `color:var(--nmp-primary); font-weight:500`; Symbol 0,875 rem | – | +55 |
| `Localization/Strings.resx`, `Strings.en.resx` | ändern (Hotspot) | `PathLabel` „Pfad“ / „Breadcrumb“ | – | +6 |

- **Reihenfolge:** `TabItem` → `TabList` → `Breadcrumb*` → CSS → Texte → Build.
- **Akzeptanz:** `dotnet build NoMorePaper.slnx` ohne Warnungen; bestehende `TabNavigationTests` grün.

### Paket P2 – Seite „Eingänge“ · Modell: **Sonnet** · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Inputs/InputRowState.cs` | neu | `public enum InputRowState { NeverRun, Running, NotSetUp, Problem, NeedsAttention }` | – | 12 |
| `Inputs/InputTabs.cs` | neu | `public static class InputTabs` (rein): `Kinds` = `["scanner-folder","mail","icloud"]`; `TabId(string kind) => "inputs-" + kind`; `string Initial(string? query)` → `query`, wenn in `Kinds`, sonst `"scanner-folder"`; `InputRowState StateOf(InputStatusDto row)`: `LastRunAt null` → `NeverRun`; `Problem == "not-configured"` → `NotSetUp`; `Problem != null` → `NeedsAttention ? NeedsAttention : Problem`; sonst `Running`; `(string Key, int Problems) Summary(IEnumerable<InputStatusDto> rows, string kind)`: Zeilen der Art; Anzahl mit `Problem`/`NeedsAttention` > 0 → (`"InpTabProblem"` bei 1, sonst `"InpTabProblems"`, n); keine Zeilen → `kind == "icloud" ? "InpTabNotSetUp" : "InpTabNever"`; irgendeine `NotSetUp` → `"InpTabNotSetUp"`; alle `NeverRun` → `"InpTabNever"`; sonst `"InpTabOk"`; `string BadgeKey(InputRowState)` → `InpBadgeNever/Ok/NotSetUp/Problem/Attention`; `string BadgeIcon(InputRowState)` → `Icons.Material.Filled.Schedule/CheckCircle/Info/Error/Error`; `string KindIcon(string kind)` → `Icons.Material.Outlined.FolderShared/Mail/Cloud` | `tests/NoMorePaper.Web.Client.Tests/Inputs/InputTabsTests.cs` | 95 |
| `tests/NoMorePaper.Web.Client.Tests/Inputs/InputTabsTests.cs` | neu | wie `Design/TabNavigationTests`: `Initial` (gültig, ungültig, `null`); `StateOf` alle fünf Fälle; `Summary`: 1 und 2 Probleme, keine Zeilen icloud/mail, `not-configured`, nur nie gelaufen, gemischt ok+nie → ok, andere Art wird ignoriert; `BadgeKey` je Zustand | selbst | 120 |
| `Inputs/InputStateFacts.razor` | neu | Parameter `Row`, `Now`. `<div class="input-facts">`: `<Badge Icon=… Text=L[BadgeKey]>`; bei `Row.Problem != null` `<p class="input-problem">@L["InpProblemDetail", InputTexts.Problem(L, Row.Problem)]</p>`; bei `LastRunAt != null` `<dl class="input-facts-list">` mit `InpLastRun`/`InputTexts.When`, `InpLastSuccess`/When oder `InpNever`, `InpReceivedTotal`/`InputTexts.Documents(L, Row.ReceivedTotal)` (F2-B) | P2-UI | 45 |
| `Inputs/InputTexts.cs` | ändern | `public static string Documents(IStringLocalizer<Strings> l, long count)` → `count == 1 ? l["InpDocumentsOne"] : l["InpDocumentsMany", count]` (Zahl mit `CurrentCulture`, `N0`) | über UI-Test | +6 |
| `Pages/InputsEntry.razor` | ändern | Karte: `<div class="input-card">` → Kopf `<div class="input-card-head">` mit Symbolkachel `<span class="input-card-icon"><MudIcon Icon="@InputTabs.KindIcon(Row.Kind)" aria-hidden="true" /></span>` und `<MudText Typo="Typo.h4" HtmlTag="h3">`: `AccountName` oder `InpCardScanner`/`InpCardMail`; darunter `<InputStateFacts Row Now />`; `ChildContent` (Aktionen) in `<div class="input-card-actions">` nur wenn gesetzt. Kommentar zum Warum (Zustand nie nur Farbe) bleibt | P2-UI | 50 |
| `Inputs/InputsPanel.razor` | neu | Parameter `Kind`, `Selected` (bool), `Rows`, `Now`, `string EmptyText`, `RenderFragment? Below` (unter den Karten), `[EditorRequired] RenderFragment About`. `<section role="tabpanel" id="@(InputTabs.TabId(Kind))-panel" aria-labelledby="@(InputTabs.TabId(Kind))-tab" tabindex="0" hidden="@(!Selected)" class="input-panel">` → `<div class="input-panel-head">` Symbol (`aria-hidden`) + `<MudText Typo="Typo.h4" HtmlTag="h2" id="inputs-@(Kind)-heading">@InputTexts.Kind(L, Kind)</MudText>`; `<div class="input-panel-grid">`: `<div class="input-panel-main">` Karten (`InputsEntry` je Zeile, für `mail` mit Aktion siehe `Inputs.razor`) oder `<p>@EmptyText</p>`, dann `Below`; `<div class="input-about">` mit `<MudText Typo="Typo.h4" HtmlTag="h3">@L["InpAboutHeading"]</MudText>` und `About` | P2-UI | 70 |
| `Inputs/MailRules.razor` | neu | Parameter `HeadingLevel` (2/3, Muster `ConfirmBox`). `<section class="mail-rules" aria-labelledby="mail-rules-heading-@HeadingLevel">` Überschrift `MailRulesHeading` (`Typo.h4` + `HtmlTag`), `<ol class="mail-rules-list">` fünf `<li>`: `<span class="mail-rule-number" aria-hidden="true">n</span>` (die Liste nennt die Nummer schon), `<div>` mit `<p class="mail-rule-title">` Titel + `<Badge Text="@L["MailRulesAlways"]" />` und `<p>` Text. Schlüssel `MailRule{Senders,Spam,NoAttachment,Attachments,Folders}{Title,Text}` | P2/P3-UI | 55 |
| `Pages/Inputs.razor` | ändern | `[SupplyParameterFromQuery(Name = "quelle")]` siehe `.cs`. Aufbau: `<Breadcrumb Items>` (Einstellungen → `einstellungen`, Eingänge aktuell); `<div class="inputs-header">` links `<MudText Typo="Typo.h2" HtmlTag="h1" id="inputs-heading" tabindex="-1">` + `<p>InpIntro</p>`, rechts `#inputs-refresh` (`nmp-button`, Symbol `Refresh` `aria-hidden` + Text) und `<p data-ready>InpUpdated</p>`; `LiveRegion` wie heute. Ladefehler: `<div data-ready><NoticeBox Kind="NoticeKind.Error" Announce="true" Message="@L["InpLoadError", _loadError]" /></div>` + `#inputs-retry` wie heute; Laden: `<p role="status">` wie heute. Sonst `<TabList Items="_tabs" @bind-SelectedId="_selected" AriaLabel="@L["InpHeading"]" />` und drei `InputsPanel`: Scanner (About: `InpScannerText`, `InpIntervalScanner`); Mail (Karten mit Aktion: Admin `<a class="nmp-button" href="eingaenge/mail">` `InpMailSetUp`/`InpMailSettings`, Mitglied `<p>InpMailAdminOnly</p>`; `Below`: `<MailRules HeadingLevel="3" />`; About: `InpMailText`, `InpIntervalMail`); iCloud (Empty `InpICloudNone`; About: `InpICloudText`, `InpIntervalICloud`, `<p id="inputs-icloud-action">` mit der heutigen Logik unverändert). `InpMailSpamFolder` entfällt (Regel 2) | P2-UI | 90 |
| `Pages/Inputs.razor.cs` | ändern | Felder `string _selected`, `IReadOnlyList<TabItem> _tabs = []`; `[SupplyParameterFromQuery(Name = "quelle")] public string? Source { get; set; }`; in `OnInitializedAsync` vor dem Laden `_selected = InputTabs.Initial(Source)`; nach jedem erfolgreichen Laden `_tabs = BuildTabs()` (`InputTabs.Kinds` → `new TabItem(InputTabs.TabId(k), InputTexts.Kind(L, k), InputTabs.KindIcon(k), CountText(k))`, `CountText` aus `Summary` + `L[key, n]`). `_selected` hält die **Tab-Id** (`InputTabs.TabId(Initial(...))`), Panel `Selected = _selected == InputTabs.TabId(kind)`. `LoadAsync`, Signatur, Ansagen, Timer **unverändert** | P2-UI | 200 |
| `wwwroot/css/inputs.css` | neu | Entwurfswerte: `.inputs-header` flex, `justify-content:space-between`, `align-items:flex-end`, `flex-wrap:wrap`, `gap:var(--nmp-space-md)`; Intro-Text `--nmp-text-variant`; `.input-panel` `margin-top:var(--nmp-space-lg)`; `.input-panel-head` flex, `gap:var(--nmp-space-sm)`; `.input-panel-grid` eine Spalte, `gap:var(--nmp-space-lg)`, ab `@media (min-width: 80rem)` `grid-template-columns: 2fr 1fr`; `.input-card`, `.input-about`, `.mail-card` `background:var(--nmp-paper-low); border:1px solid var(--nmp-border); border-radius:var(--nmp-radius-lg); padding:var(--nmp-space-md); display:flex; flex-direction:column; gap:var(--nmp-space-md)`; Hover der Karte `border-color:var(--nmp-border-strong)`; `.input-card-head` flex, `gap:var(--nmp-space-md)`, Badge rechts (`margin-left:auto`, `align-self:flex-start`); `.input-card-icon` 3 rem Quadrat, `background:var(--nmp-elevated-high)`, Rand `--nmp-border`, Radius `--nmp-radius`, Symbol 1,625 rem weiß; `.input-card .nmp-badge, .mail-card .nmp-badge` `color:var(--nmp-primary)`; `.input-facts-list` Raster `auto 1fr`, `font-family:var(--nmp-font-mono); font-size:0.6875rem`, `dt` `--nmp-outline`, `dd` `--nmp-text`, `overflow-wrap:anywhere`; `.input-card-actions` `border-top:1px solid var(--nmp-border); padding-top:var(--nmp-space-sm)`; `.mail-rules-list` `list-style:none; padding:0; border:1px solid var(--nmp-border); border-radius:var(--nmp-radius-lg)`, `li` flex mit Trennlinie, `.mail-rule-number` 1,5 rem Quadrat `--nmp-elevated-high`, Mono; Regeltext `--nmp-text-variant` | Sichtprüfung | 150 |
| `wwwroot/index.html` | ändern | `<link rel="stylesheet" href="css/inputs.css" />` nach `classification.css` | – | +1 |
| `Localization/Strings.resx`, `Strings.en.resx` | ändern (Hotspot) | neue Schlüssel siehe Tabelle „Texte“; Werte ändern: `InpLastRun`, `InpLastSuccess`; löschen: `InpMailSpamFolder` | – | +100 je Datei |
| `tests/NoMorePaper.UI.Tests/Inputs/InputsPageTests.cs` | ändern | Panels statt Regionen: `GetByRole(AriaRole.Tabpanel, new() { Name = "Mail" })` (Teilnamen-Treffer, weil der Name die Angabe enthält) nach Wechsel per Klick auf den Reiter; H2-Erwartung je sichtbarem Panel (`["Scanner-Ordner"]`); Texte: Badge „Läuft“ / „Problem“ / „Braucht Aufmerksamkeit“ + `InpProblemDetail`, „Zuletzt erfolgreich noch nie“; `NoMorePaper-Spam` steht in der Regelliste im Mail-Panel; aktueller Menüpunkt über `page.Locator("nav.side-nav").GetByRole(AriaRole.Link, new() { Name = "Einstellungen", Exact = true })` (der Pfad hat denselben Link); Fehlerbox `main .nmp-notice-error`; Live-Region-Tests unverändert; axe DE/EN **je Reiter** (Schleife über drei Reiter, nach jedem Klick `AxeCheck.NoViolationsAsync`) | selbst | 220 |
| `tests/NoMorePaper.UI.Tests/Inputs/InputsTabsTests.cs` | neu | (1) Reiternamen mit Angabe: Admin-Daten → `"Scanner-Ordner, läuft"`, `"Mail, 1 Problem"`, `"iCloud Drive, 1 Problem"`; (2) Tastatur: Fokus auf ersten Reiter, `ArrowRight` → Reiter Mail `aria-selected=true`, fokussiert, Mail-Panel sichtbar, Scanner-Panel `hidden`; `End` → iCloud, `Home` → Scanner; (3) `?quelle=mail` startet auf Mail, `?quelle=unsinn` auf Scanner; (4) Pfad: `nav[aria-label=Pfad]` Link „Einstellungen“ → `einstellungen`, „Eingänge“ mit `aria-current=page`; (5) Karte: `ReceivedTotal = 128` → „128 Dokumente“, `1` → „1 Dokument“; (6) `not-configured` → Reiter „Mail, nicht eingerichtet“, Badge „Nicht eingerichtet“, Link „Mail einrichten“; (7) Englisch: „Mail, 1 problem“, „Last successful run“. `FakeInputs` unverändert nutzen (`Rows` mit `ReceivedTotal`) | selbst | 160 |

**Texte (DE / EN)** – neue Schlüssel:

| Schlüssel | Deutsch | Englisch |
|---|---|---|
| `InpTabOk` / `InpTabNever` / `InpTabNotSetUp` | läuft / noch nicht gelaufen / nicht eingerichtet | running / not run yet / not set up |
| `InpTabProblem` / `InpTabProblems` | 1 Problem / {0} Probleme | 1 problem / {0} problems |
| `InpBadgeOk` / `InpBadgeNever` / `InpBadgeNotSetUp` | Läuft / Noch nicht gelaufen / Nicht eingerichtet | Running / Not run yet / Not set up |
| `InpBadgeProblem` / `InpBadgeAttention` | Problem / Braucht Aufmerksamkeit | Problem / Needs attention |
| `InpProblemDetail` | {0}. | {0}. |
| `InpReceivedTotal` | Angenommen insgesamt | Taken in so far |
| `InpDocumentsOne` / `InpDocumentsMany` | 1 Dokument / {0} Dokumente | 1 document / {0} documents |
| `InpCardScanner` / `InpCardMail` | Ordner im Heimnetz / Postfach des Haushalts | Folder in the home network / Household mailbox |
| `InpAboutHeading` | Über diesen Eingang | About this input |
| `InpIntervalScanner` | Prüft den Ordner alle 30 Sekunden. | Checks the folder every 30 seconds. |
| `InpIntervalMail` | Ruft das Postfach alle 5 Minuten ab. | Fetches the mailbox every 5 minutes. |
| `InpIntervalICloud` | Ruft jeden iCloud-Ordner alle 15 Minuten ab. | Fetches every iCloud folder every 15 minutes. |
| `MailRulesHeading` / `MailRulesAlways` | So behandelt NoMorePaper Mails / Immer an | How NoMorePaper handles mail / Always on |
| `MailRuleSendersTitle` / `…Text` | Absender je Konto / Kommt eine Mail von einer Adresse, die ein Konto auf seiner Konto-Seite eingetragen hat, gehört das Dokument diesem Konto. Das gilt nur, wenn der eigene Mailserver den Absender bestätigt hat (SPF oder DMARC). Alle anderen Mails gehören dem Verwalter, sind privat und stehen bei ihm unter „Zu prüfen“. | Sender per account / If a mail comes from an address that an account has entered on its account page, the document belongs to that account. This only applies if your own mail server has confirmed the sender (SPF or DMARC). All other mails belong to the administrator, are private and wait in their "To review". |
| `MailRuleSpamTitle` / `…Text` | Spam-Prüfung / Mails unbekannter Absender prüft das Sprachmodell auf dem eigenen Server auf Werbung und Betrug. Spam wird nicht abgelegt, sondern in den Ordner „NoMorePaper-Spam“ verschoben. Schiebst du eine Mail von dort zurück in den Posteingang, wird sie ohne Prüfung eingelesen. | Spam check / The language model on your own server checks mails from unknown senders for advertising and fraud. Spam is not filed but moved to the folder "NoMorePaper-Spam". If you move a mail from there back to the inbox, it is read in without a check. |
| `MailRuleNoAttachmentTitle` / `…Text` | Mails ohne Anhang / Das Sprachmodell entscheidet, ob die Mail selbst ein Dokument ist, zum Beispiel eine Rechnung im Mailtext. Dann wird sie als PDF abgelegt. | Mails without attachment / The language model decides whether the mail itself is a document, for example an invoice in the text. Then it is filed as a PDF. |
| `MailRuleAttachmentsTitle` / `…Text` | Anhänge / PDF, JPEG, PNG und HEIC werden Dokumente. Bilder unter 30 KB oder mit einer Seite unter 500 Pixeln, etwa Logos in der Signatur, werden übergangen. | Attachments / PDF, JPEG, PNG and HEIC become documents. Images under 30 KB or with a side under 500 pixels, such as logos in a signature, are skipped. |
| `MailRuleFoldersTitle` / `…Text` | Ordner im Postfach / Eingelesene Mails wandern in den Ordner „Verarbeitet“. Eine Mail, die dreimal nicht eingelesen werden konnte, landet in „NoMorePaper-Fehler“. | Folders in the mailbox / Mails that were read in move to the folder "Verarbeitet". A mail that could not be read in three times ends up in "NoMorePaper-Fehler". |

Geänderte Werte: `InpLastRun` „Zuletzt gelaufen“ / „Last run“; `InpLastSuccess` „Zuletzt erfolgreich“ / „Last
successful run“.

- **Reihenfolge:** `InputRowState` → `InputTabs` + Unit-Tests → Texte → `InputTexts.Documents` →
  `InputStateFacts` → `InputsEntry` → `MailRules` → `InputsPanel` → `Inputs.razor(.cs)` → CSS + `index.html` →
  Build → UI-Tests anpassen und neu.
- **Telemetrie:** keine neuen Ereignisse. 4700/4702 bleiben unverändert (Reiterwechsel wird nicht geloggt:
  keine Aussage für den Betrieb).
- **Akzeptanz:**
  - `dotnet test --project tests/NoMorePaper.Web.Client.Tests/NoMorePaper.Web.Client.Tests.csproj` grün (neue `InputTabsTests`)
  - `dotnet test --project tests/NoMorePaper.UI.Tests/NoMorePaper.UI.Tests.csproj -- --filter-namespace NoMorePaper.UI.Tests.Inputs` grün, axe 0 Verstöße DE/EN in allen drei Reitern
  - keine Datei > 250 Zeilen (`Inputs.razor.cs` ≤ 210, sonst `BuildTabs`/`CountText` nach `Inputs/InputTabItems.cs` auslagern)

### Paket P3 – Seite „Mail-Eingang“ · Modell: **Sonnet** · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Inputs/MailboxCard.razor` | neu | Parameter `string Host, int Port, string Security, string UserName` (gespeicherter Stand), `InputStatusDto? Row`, `bool StateFailed`, `DateTimeOffset Now`. `<section class="mail-card" aria-labelledby="mail-card-heading">`: `<MudText Typo="Typo.h4" HtmlTag="h2" id="mail-card-heading">@L["MailCardHeading"]</MudText>`. `Host` leer → `<Badge Icon=Info Text=InpBadgeNotSetUp>` + `<p>MailCardNotSetUp</p>`, sonst `<p class="mail-card-address">@UserName</p>` (body-lg, weiß, 600, `overflow-wrap:anywhere`), `<p class="mail-card-meta">@L["MailCardKind", SecurityLabel, Host, Port]</p>` (`SecurityLabel` = `MailSecuritySsl`/`MailSecurityStartTls`), `<p>InpIntervalMail</p>`, dann `Row` → `<InputStateFacts Row Now />`, `StateFailed` → `<p>MailCardStateUnavailable</p>`. **Kein** `role`, keine Knöpfe | P3-UI | 60 |
| `Pages/MailSettings.razor` | ändern | `@inject InputsApi StatusApi` (nicht `Inputs`: kollidiert mit Seite und Namensraum `Inputs`; `InputsApi` ist in `Program.cs` registriert); oben `<Breadcrumb>` (Einstellungen → `einstellungen`; Eingänge → `eingaenge?quelle=mail`; Mail aktuell). H1 → `MudText Typo="Typo.h2" HtmlTag="h1"`. Admin-Zweig: `<p data-ready>MailExplain</p>`, dann `<div class="mail-layout">` mit drei Kindern in **dieser** DOM-Reihenfolge: `<MailboxCard …>` (Klasse `mail-layout-card`), `<section class="mail-layout-form" aria-labelledby="mail-form-heading">` mit `<MudText Typo="Typo.h4" HtmlTag="h2" id="mail-form-heading">MailFormHeading</MudText>` und dem **unveränderten** Formular, `<div class="mail-layout-rules"><MailRules HeadingLevel="2" /></div>`. Abschnitt „Ordner im Postfach“ entfällt | P3-UI | 110 |
| `Pages/MailSettings.razor.cs` | ändern | Felder `InputStatusDto? _mailRow`, `bool _stateFailed`, `DateTimeOffset _now`; `[Inject] TimeProvider Clock`. Nach erfolgreichem `LoadAsync`: `await LoadStateAsync()` = `StatusApi.ListAsync()` → Erfolg: `_mailRow` = erste Zeile `Kind == "mail" && AccountName == null`, `_now = Clock.GetUtcNow()`; Fehler: `_stateFailed = true`, `_logger.InputsPageFailed("load-mail-status")` (Erweiterung aus `Inputs/InputsLog.cs`). Die Karte zeigt `_storedSignIn` (nach Speichern schon aktualisiert). Formularlogik **unverändert** | P3-UI | 200 |
| `wwwroot/css/inputs.css` | ändern | `.mail-layout` eine Spalte, `gap:var(--nmp-space-lg)`; ab `80rem` `grid-template-columns:2fr 1fr; grid-template-areas:"card form" "rules form"`, Kinder per `grid-area` (Lesereihenfolge Karte → Formular → Regeln bleibt, Bild wie Entwurf: links Karte und Regeln, rechts Formular); `.mail-layout-form` Karte wie `.input-about`; `.mail-card-address`, `.mail-card-meta` (Mono 0,6875 rem, `--nmp-text-variant`) | Sichtprüfung | +30 |
| `Localization/Strings.resx`, `Strings.en.resx` | ändern (Hotspot) | neu: `MailCardHeading` „Postfach“ / „Mailbox“; `MailCardNotSetUp` „Noch kein Postfach eingerichtet. Trage die Zugangsdaten ein.“ / „No mailbox set up yet. Enter the sign-in details.“; `MailCardKind` „IMAP · {0} · {1}:{2}“ / gleich; `MailCardStateUnavailable` „Der Zustand des Postfachs konnte nicht geladen werden.“ / „The state of the mailbox could not be loaded.“; `MailFormHeading` „Zugangsdaten“ / „Sign-in details“. Wert ändern: `MailExplain` „Hier richtest du das Postfach des Haushalts ein. Das Passwort wird verschlüsselt gespeichert und nie wieder angezeigt.“ / „Set up the household's mailbox here. The password is stored encrypted and never shown again.“ Löschen: `MailFoldersHeading`, `MailFoldersExplain` | – | +20 je Datei |
| `tests/NoMorePaper.UI.Tests/Inputs/MailboxCardTests.cs` | neu | Aufbau wie `MailSettingsTests.OpenAsync` plus `mocks.RespondAsync(page, "GET", "/api/inputs", 200, rows)`: (1) Karte zeigt „dokumente“, „IMAP · SSL/TLS · imap.haushalt.example:993“, „alle 5 Minuten“, Badge „Läuft“, „Zuletzt gelaufen“; (2) `/api/inputs` 500 → „Der Zustand des Postfachs konnte nicht geladen werden.“, Formular bleibt bedienbar; (3) leerer Host → „Nicht eingerichtet“; (4) nach Speichern mit neuem Host zeigt die Karte den neuen Host; (5) Regelliste: H2 „So behandelt NoMorePaper Mails“, fünf `listitem`; (6) Pfad-Link „Eingänge“ → `eingaenge?quelle=mail`; (7) axe DE/EN; (8) Englisch: „How NoMorePaper handles mail“ | selbst | 150 |
| `tests/NoMorePaper.UI.Tests/Inputs/MailSettingsTests.cs` | ändern, nur falls rot | Selektoren `main [role=status]`/`[role=alert]` `.First` und `main [data-ready]` `.First` müssen weiter auf Formular bzw. `MailExplain` zeigen (die Karte hat bewusst keine Rolle). Ändern nur, wenn ein Test wegen der neuen Struktur scheitert | selbst | ±5 |

- **Reihenfolge:** Texte → `MailboxCard` → `MailSettings.razor(.cs)` → CSS → Build → `MailSettingsTests` laufen
  lassen → `MailboxCardTests`.
- **Telemetrie:** neuer Code `load-mail-status` für Ereignis 4702 (`InputsPageFailed`) – Eintrag im Katalog (P4).
- **Akzeptanz:** `dotnet test --project tests/NoMorePaper.UI.Tests/NoMorePaper.UI.Tests.csproj -- --filter-namespace NoMorePaper.UI.Tests.Inputs`
  grün; danach `dotnet test --solution NoMorePaper.slnx` grün;
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-file-length.ps1` grün; Build ohne Warnungen.
- **Eskalation (P0–P3):** zwei rote Läufe am selben Paket, `MudText HtmlTag` rendert nicht das erwartete
  Element, `hidden` an `role="tabpanel"` lässt axe oder Playwright scheitern, oder eine Entscheidung, die
  dieser Plan nicht trifft (z. B. andere Texte, andere Reihenfolge, zusätzlicher Knopf) → stoppen und der
  steuernden Sitzung melden. Kein Raten, keine eigenen Texte.

### Paket P4 – Doku und Häkchen · Modell: **Haiku** · Effort: `low`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `docs/kb/oberflaeche.md` | ändern | Bausteine-Tabelle: Zeile „Pfad“ `Breadcrumb.razor` – `nav` mit `aria-label` „Pfad“, Liste, aktuelle Seite `aria-current="page"`, Trenner-Symbol `aria-hidden`; Zeile „Reiter“ ergänzen: optional Symbol (`aria-hidden`) und Angabe (`Count`, dann `aria-label` „Name, Angabe“); F4-A: Reiter in JetBrains Mono 11 px wie Entwurf 04 | – | +4 |
| `docs/kb/eingaenge.md` | ändern | Abschnitt „Seite Eingänge“: Reiter je Eingang mit Zustand kurz, Start-Reiter aus `?quelle=`, Panels mit `hidden`, Karte je Zeile (Badge, Problem, Zeiten, Summe), „Über diesen Eingang“ mit Takt (fest im Text – ändert sich ein Takt im Code, Texte `InpInterval*` mitziehen), Regelliste `MailRules` an beiden Stellen; Mail-Einrichtung: Postfach-Karte aus gespeichertem Stand + `GET /api/inputs`; `InputStatusDto.ReceivedTotal` | – | +12 |
| `docs/kb/telemetrie.md` | ändern | Zeile 4702: Code `load-mail-status` ergänzen | – | 1 |
| `docs/plan/m4u-oberflaeche.md` | ändern | Aufgabe D: beide Punkte abhaken | – | 2 |
| `docs/ui/stitch/README.md` | ändern | Screen 04 Status „umgesetzt“ (erst nach Abnahme in P5) | – | 1 |

- **Akzeptanz:** Lesen durch die steuernde Sitzung; Links gültig.

### Paket P5 – Review und Abnahme · Modell: **Opus** · Effort: `high` (steuernde Sitzung)

- Review-Schleife §8 über die ganze Spur, bis ein Review keine Änderung mehr ergibt.
- Sichtvergleich im Browser-Pane: `/eingaenge` (Reiter Mail, 1440 px breit) und `/eingaenge/mail` neben
  `screen.webp`; Zoom 200 % und 400 % (Reiter brechen um, eine Spalte, kein waagerechtes Scrollen); Tastaturweg
  Pfad → H1 → Aktualisieren → Reiter (Pfeile) → Panel → Links.
- Danach Commit/Push auf `feat/m04u-d-eingaenge`; Merge in den Integrationsbranch nach A (Reihenfolge
  A → D → B → C → E). **Hinweis an Spur C:** `TabItem`/`TabList` haben dann optionale Parameter und neues
  Aussehen (F4-A); `Design/Breadcrumb` für die Zurück-Leiste wiederverwenden.
- NVDA/VoiceOver durch den Nutzer (Akzeptanz M4u).

## Risiken

- **Takt doppelt:** Die Takte stehen im Code (`ScannerFolderSource.RunInterval`, `MailSource.Interval`) und als
  Text. Ändert jemand einen Takt, stimmt der Text nicht mehr – Hinweis in KB `eingaenge.md` (P4).
- **Geteilter Baustein:** `TabList`/`design.css` ändern sich für alle Reiter (F4-A). Spur C baut erst danach auf;
  bis zum Merge von D sieht C noch das Schritt-0-Aussehen.
- **Kleine Schrift:** 11 px Mono in Reitern und Fakten ist so freigegeben (ADR-0026); die Lupe des Nutzers
  vergrößert. Kontrast `#8e9192` auf `#1c1b1b` ≈ 5,5 : 1 (AA).
- **Übersicht mit Reitern:** Nur ein Eingang ist sichtbar. Ausgleich: Zustand im Reiternamen (F1-A) und die
  Live-Region, die Änderungen aller Eingänge ansagt.

### Umsetzung (Spur D, 2026-11-23)
P0–P4 umgesetzt auf `feat/m04u-d-eingaenge`; P5 (Review, Sichtvergleich) steht aus. Abweichungen: Reitername
ohne Komma („Mail 1 Problem“) und ohne `aria-label` (axe `label-content-name-mismatch`, Namen kommen aus dem
sichtbaren Text); Mail-Aktion („Mail-Einstellungen“/„nur Verwalter“) steht einmal unter den Karten statt je Karte
(sonst fehlt sie bei einem Postfach ohne Zeile); `InputTabs`/`InputRowState` sind `public` (Web.Client hat kein
`InternalsVisibleTo`).

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|
