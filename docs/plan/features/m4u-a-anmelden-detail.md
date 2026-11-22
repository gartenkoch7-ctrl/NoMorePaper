# M4u Spur A – Anmelden und Konto-Seiten: Detailplan

Aufgabe A aus [m4u-oberflaeche.md](../m4u-oberflaeche.md): `SignIn.razor` nach Stitch-Screen 01, dieselbe
Gestaltung für Ersteinrichtung, Einladung, Passwort zurücksetzen und Notfall. Regeln:
[AGENTS.md §12](../../../AGENTS.md), [Vorlage](../detailplan-vorlage.md).

Grundlagen: [notizen.md 01](../../ui/stitch/01-anmelden/notizen.md), [screen.png](../../ui/stitch/01-anmelden/screen.png),
[KB oberflaeche.md](../../kb/oberflaeche.md), [KB ui-prozess.md](../../kb/ui-prozess.md),
[ADR-0026](../../adr/0026-optik-nach-stitch-design-system.md), [KB konten.md](../../kb/konten.md).

## Offene Fragen an den Nutzer

> **Beantwortet 2026-11-22:** Der Nutzer hat alle Fragen wie empfohlen entschieden. Der Plan gilt so.

Diese Fragen müssen vor der Umsetzung beantwortet sein. **Der Plan unten ist für die jeweils empfohlene
Option geschrieben**; die letzte Spalte sagt, was sich bei der anderen Antwort im Plan ändert.

| Nr. | Frage | Optionen | Empfehlung | Bei der anderen Antwort |
|---|---|---|---|---|
| F1 | Screen 01 steht in [docs/ui/stitch/README.md](../../ui/stitch/README.md) auf „geprüft“. Nach dem UI-Prozess wird nur umgesetzt, was du freigegeben hast. Gibst du den Entwurf mit den Abweichungen aus der Tabelle „Abgleich mit der App“ in `notizen.md` frei? | **A:** Ja, freigegeben – Spur A setzt den Status am Ende auf „umgesetzt“. **B:** Nein, erst in Stitch nachbessern (z. B. SSO, Passkey, Registrierung im Entwurf entfernen). | A | B: Spur A wartet, bis der neue Entwurf abgelegt und freigegeben ist. |
| F2 | Die Feldbausteine (`FormField`, `CopyField`, Klassen `.form-input`, `.form-toggle`, `.form-check`, `.form-error` in `accounts.css`) werden auch auf Konto, Mitglieder, Mail-Einstellungen, Listen und im Angaben-Editor benutzt. Sollen die Felder überall im neuen Aussehen erscheinen, oder nur auf den fünf Anmeldeseiten? | **A:** Überall – Spur A stellt die Feldbausteine einmal um; die anderen Spuren übernehmen das nach dem Merge (A wird zuerst gemergt). **B:** Nur auf den Anmeldeseiten – die neuen Regeln gelten nur innerhalb der Anmeldekarte; die übrigen Seiten bleiben bis Spur E beim alten Feldaussehen. | A | B: P1 schreibt alle Feldregeln mit dem Präfix `.account-frame-card` in `account-frame.css`; `accounts.css` bleibt unverändert; `FormField` bekommt den Symbol-Knopf nur mit neuem Parameter `InlineToggle` (Standard `false`). |
| F3 | Welche Texte sollen auf den Anmeldeseiten stehen? Der Entwurf hat eigene Texte, die App hat schon eingeführte. | **(a) Überschrift Anmelden:** A „Anmelden“ bleibt (klar für Screenreader, Tests unverändert) · B „Willkommen zurück“ wie im Entwurf. **(b) Unterzeile Anmelden (neu):** A „Melde dich an, um papierlos zu arbeiten.“ / „Sign in to go paperless.“ wie im Entwurf · B keine Unterzeile. **(c) Passwort vergessen:** A kurz „Passwort vergessen? Frag den Verwalter.“ / „Forgot your password? Ask the administrator.“ (Notizen, mit dem App-Begriff „Verwalter“) · B der bisherige lange Satz bleibt. **(d) Notfall-Link:** A der bisherige Text „Mit dem Notfall-Code ein neues Passwort setzen“ bleibt (die Seite meldet nicht an, sie setzt ein Passwort) · B „Mit Notfall-Code anmelden“ wie im Meilensteinplan. **(e) Kleine Zeile über der Überschrift:** A „NoMorePaper“ in Großbuchstaben wie „NOMOREPAPER WORKSPACE“ im Entwurf · B weglassen (der Kopf zeigt den Namen schon). | (a) A, (b) A, (c) A, (d) A, (e) A | (a) B: Schlüssel `SignInHeading` bleibt unangetastet, neuer Schlüssel `SignInWelcome` als H1; `RealApiAccountTests` Zeile 52 und Testdaten in P3 anpassen. (b) B: Schlüssel `SignInIntro` entfällt. (c) B: `SignInForgot` statt `SignInForgotShort`. (d) B: neuer Schlüssel `SignInEmergencyShort`. (e) B: Zeile `.account-frame-overline` entfällt. |
| F4 | Welches Logo zeigt die Kachel über der Überschrift? Das vorhandene App-Symbol `icons/icon-192.png` ist noch gelb (altes Design) und damit nicht monochrom. | **A:** Dasselbe Symbol wie in der Seitenleiste (`Icons.Material.Outlined.Inventory2`, weiß). **B:** Das PWA-Symbol `icon-192.png` (gelb). **C:** Ein neues einfarbiges Logo als SVG – dann muss es jemand entwerfen, das gehört nicht in Spur A. | A | B: `<img src="icons/icon-192.png" alt="">` statt `MudIcon`. C: Spur A nimmt A und lässt eine Lücke für das Logo. |
| F5 | Wo stehen „Passwort vergessen“ und der Notfall-Link? Im Entwurf steht „Passwort vergessen?“ zwischen Passwortfeld und Knopf, darunter liegen dunkle Nebenknöpfe (Passkey, SSO), die es nicht gibt. | **A:** „Passwort vergessen“ als Text unter dem Passwortfeld (wie im Entwurf), der Notfall-Link als dunkler Nebenknopf mit Schlüssel-Symbol unter „Anmelden“ (an der Stelle des Passkey-Knopfs). **B:** Beides als einfacher Text unter der Karte (wie heute). | A | B: Beide Absätze kommen in den Parameter `Below` von `AccountFrame`; Klasse `.account-frame-secondary` entfällt. |
| F6 | Der Knopf „Passwort anzeigen“ steht heute als Textknopf neben dem Feld. Im Entwurf ist er ein Augen-Symbol im Feld. Wie soll er aussehen? | **A:** Augen-Symbol im Feld wie im Entwurf; der zugängliche Name bleibt „Passwort anzeigen: …“ und `aria-pressed` zeigt den Zustand; das Symbol wechselt zwischen durchgestrichenem und offenem Auge. **B:** Textknopf „Passwort anzeigen“ neben dem Feld bleibt, nur im neuen Knopfstil. | A | B: Kein `.form-toggle-icon`; der Knopf bleibt außerhalb von `.form-input-wrap` und bekommt nur den Stil des dunklen Nebenknopfs. |
| F7 | Der Entwurf zeigt nur die Handy-Ansicht. Wie sieht die Seite auf breiten Bildschirmen aus? | **A:** Genau wie auf dem Handy: Spalte 28 rem breit, mittig, Überschrift 22 px. **B:** Ab 1024 px Überschrift 28 px (Stufe `headline-lg` des Design-Systems), sonst gleich. | A | B: zusätzliche Regel `@media (min-width: 1024px) { .account-frame-title { font-size: 1.75rem; line-height: 2.25rem; letter-spacing: -0.02em; } }`. |

Durch Vorgaben schon entschieden (keine Frage): Werte nur aus den Tokens in `base.css` (der Entwurf hat an
Karte und Logo-Kachel `rounded-xl` = 0,75 rem; es gibt kein Token dafür, also `--nmp-radius-lg` = 1 rem wie
alle Karten laut KB); Fokus wie in KB `oberflaeche.md` (Feldrand weiß, sonst Browser); der Kopf für
Abgemeldete (`MainLayout.razor`) bleibt, wie er ist – gesperrt nach Schritt 0, Schließen-Kreuz, Avatar und
„LOGIN“ aus dem Entwurf kommen nicht. Der Entwurf färbt den weißen Hauptknopf beim Überfahren `#393939` mit
dunkler Schrift (unlesbar) – wird nicht übernommen, der Hauptknopf behält das Verhalten aus `design.css`.

## Detailplan

**Erstellt von:** Opus (Stufe 3) · **Datum:** 2026-11-22 · **Offene Designfragen:** F1–F7 (oben), vor dem
Start zu beantworten

Branch `feat/m04u-a-anmelden` von `feat/m04u-oberflaeche`, eigener Worktree. Alle Pfade unter
`src/NoMorePaper.Web.Client/` sofern nicht anders angegeben.

**Nicht ändern:** `Layout/MainLayout.razor`, `Theme/ContrastTheme.cs` (gesperrt), `css/base.css`,
`css/layout.css`, `css/design.css`, `css/components.css`, `Design/*`, Dateien anderer Spuren
(`Documents.razor`, `DocumentFilterBar.razor`, `DocumentViewer*`, `DocumentFields*`, `Viewer/`, `Inputs*`,
`MailSettings*`). Hotspots `Strings*.resx` nur ergänzen (keine Werte ändern, keine Schlüssel löschen).

### Einschätzung

| Aufgabenpunkt | Umfang | Risiko | Neuheit | Klarheit | Prüfbarkeit | Stufe |
|---|---|---|---|---|---|---|
| Rahmen `AccountFrame` + `account-frame.css` | 2 neue Dateien, ~175 Zeilen | gering | Muster (`Design/NoticeBox`, `.link-tile`) | nach F2–F7 entschieden | UI-Tests + Sichtvergleich mit `screen.png` | 2 |
| Feldbausteine umstellen (`FormField`, `accounts.css`) | 2 Dateien, ~70 Zeilen geändert | mittel: wirkt auf Seiten anderer Spuren (F2-A) | Muster `.search-input` in `components.css` | entschieden | ganze UI-Testsuite | 2 |
| Fünf Seiten + `EmergencyCodeView` umstellen | 6 Dateien, je ~10–25 Zeilen | gering: Bedienlogik bleibt | Muster | entschieden | bestehende Account-UI-Tests | 2 |
| Texte | 2 Dateien, ~12 Zeilen | keins | Muster | nach F3 entschieden | Build + UI-Tests | 1 |
| UI-Tests neu/ergänzt | 2 Dateien, ~190 Zeilen | gering | Muster (`SignInTests`, `NavigationTests`) | entschieden | selbst | 2 |
| KB, Stitch-README, Häkchen | 3 Dateien, ~10 Zeilen | keins | – | entschieden | Lesen | 1 |
| Review §8, Abnahme mit Bildern | – | – | – | – | Review | 3 |

Kleine Pakete gleicher Stufe laufen in **einem** Sonnet-Subagenten (P1 → P2 → P3 → P4); nur das Review
(P5) läuft auf Opus.

### Maße aus dem Entwurf → Umsetzung

Quelle `code.html` (nur abgelesen, nie kopiert). Links Tailwind-Klasse im Entwurf, rechts der Wert im Code.

| Element | Entwurf | Umsetzung |
|---|---|---|
| Spalte | `max-w-md mx-auto py-space-sm` | `max-width: 28rem; margin-inline: auto; padding-block: var(--nmp-space-sm)` |
| Kopfblock | zentriert, `mt-space-xs mb-space-lg` | `text-align: center; margin: var(--nmp-space-xs) 0 var(--nmp-space-lg)` |
| Logo-Kachel | `w-12 h-12 rounded-xl bg-surface-container-low mb-space-md shadow-sm`, Bild `w-7` `opacity-95` | `3rem` Quadrat, `--nmp-radius-lg`, `--nmp-paper-low`, `margin-bottom: var(--nmp-space-md)`, `box-shadow: 0 1px 2px rgba(0,0,0,.05)`; Symbol `font-size: 1.75rem`, `color: var(--nmp-primary)`, `opacity: .95` |
| Überzeile | `label-sm uppercase tracking-[0.2em] text-on-surface-variant mb-1` | Mono `0.625rem/0.875rem`, `letter-spacing: .2em`, `text-transform: uppercase`, `--nmp-text-variant`, `margin: 0 0 .25rem` |
| H1 | `headline-lg-mobile` (22/28, −0,015 em, 600), `text-primary` | `1.375rem/1.75rem`, `letter-spacing: -0.015em`, `600`, `--nmp-primary`, `margin: 0` |
| Unterzeile | `body-md text-on-surface-variant mt-1` | `0.875rem/1.375rem`, `--nmp-text-variant`, `margin: .25rem 0 0` |
| Karte | `bg-surface-container-low rounded-xl p-space-md shadow-md gap-space-md` | `--nmp-paper-low`, `--nmp-radius-lg`, `padding: var(--nmp-space-md)`, `box-shadow: 0 4px 6px -1px rgba(0,0,0,.1), 0 2px 4px -2px rgba(0,0,0,.1)`, Flex-Spalte `gap: var(--nmp-space-md)`, kein Rand |
| Feld (Abstand) | `gap-1.5` | `.form-field { gap: .375rem }` |
| Feldbeschriftung | `label-md text-on-surface tracking-wide` (Mono 11/16, .04em, 500) | Mono `0.6875rem/1rem`, `letter-spacing: .04em`, `500`, `--nmp-text` |
| Eingabefeld | `h-11 bg-surface-container-lowest text-primary body-md rounded-lg pl-10 pr-3` (`pr-11` mit Auge) | `min-height: 2.75rem`, `--nmp-canvas-lowest`, `--nmp-primary`, `0.875rem/1.375rem`, `--nmp-radius`, Rand `1px solid var(--nmp-border)`, `padding: .5rem .75rem`; mit Symbol `padding-inline-start: 2.5rem`; mit Auge `padding-inline-end: 2.75rem` |
| Feld im Fokus | `focus:bg-surface-container` (+ KB: Rand weiß) | `background: var(--nmp-surface-container); border-color: var(--nmp-primary); box-shadow: 0 0 0 1px var(--nmp-primary)` |
| Symbol im Feld | `absolute left-3`, 20 px, `text-on-surface-variant`, im Fokus `text-primary` | `position: absolute; inset-inline-start: .75rem; font-size: 1.25rem; color: var(--nmp-text-variant); pointer-events: none`; `.form-input-wrap:focus-within .form-field-icon { color: var(--nmp-primary) }` |
| Auge im Feld | `absolute right-2 w-8 h-8 rounded text-on-surface-variant hover:text-primary`, 19 px | `position: absolute; inset-inline-end: .5rem; width/height: 2rem; --nmp-radius-sm; transparent; color: var(--nmp-text-variant)`, Hover `--nmp-primary`, Symbol `1.1875rem` |
| Hinweis „Passwort vergessen“ | `body-sm text-on-surface-variant`, `pt-0.5` | `0.75rem/1.125rem`, `--nmp-text-variant`, `margin: 0; padding-top: .125rem` |
| Hauptknopf | `w-full h-11 bg-primary text-on-primary headline-sm font-semibold rounded-lg gap-2 shadow-sm mt-1`, Pfeil 18 px | Klassen `nmp-button nmp-button-primary account-frame-submit`: `width: 100%; min-height: 2.75rem; display: inline-flex; justify-content: center; align-items: center; gap: .5rem; font-size: 1rem; line-height: 1.5rem; font-weight: 600; box-shadow: 0 1px 2px rgba(0,0,0,.05); margin-top: .25rem`; Pfeil `1.125rem` |
| Nebenknopf | `w-full h-11 bg-surface-container hover:bg-surface-container-high text-primary body-md rounded-lg gap-2.5`, Symbol 20 px | `.account-frame-secondary` (ein `<a>`): `display: flex; justify-content: center; align-items: center; gap: .625rem; min-height: 2.75rem; background: var(--nmp-surface-container); color: var(--nmp-primary); border-radius: var(--nmp-radius); text-decoration: none; font-size: .875rem`, Hover `--nmp-elevated-high`; Symbol `1.25rem` |
| Fehlermeldung | im Entwurf keine | wie `.nmp-notice-error`: `padding: 1rem; background: var(--nmp-paper); border: 1px solid var(--nmp-error); border-radius: var(--nmp-radius-lg); color: var(--nmp-primary); font-weight: 500` |
| Kontrollkästchen | `w-[18px] h-[18px] rounded bg-surface-container-lowest`, Text `body-sm text-on-surface-variant` | natives `input[type=checkbox]` `1.125rem`, `accent-color: var(--nmp-primary)`; Label `0.75rem/1.125rem`, `--nmp-text-variant`, `font-weight: 400` |

Weggelassen (Notizen „Abgleich“ und „Nicht mitnehmen“): Angemeldet bleiben, Passkey, Trenner „Oder weiter
mit“, Microsoft/Google, Registrieren, DSGVO/GoBD-Zeile mit pulsierendem Punkt, Fußzeile, Platzhalter in den
Feldern, Lade- und Erfolgstexte im Knopf, `user-scalable=no`, `outline-none`.

### Paket P1 – Rahmen und Feldbausteine · Modell: **Sonnet** · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Accounts/AccountFrame.razor` | neu | Rahmen aller Seiten für Abgemeldete. Parameter: `[Parameter, EditorRequired] string Heading`, `[Parameter] string? Intro`, `[Parameter] RenderFragment? ChildContent` (Inhalt der Karte), `[Parameter] RenderFragment? Below` (unter der Karte, für F5-B; bleibt leer bei F5-A). Markup: `<div class="account-frame">` → `<div class="account-frame-brand">` mit `<span class="account-frame-logo" aria-hidden="true"><MudIcon Icon="@Icons.Material.Outlined.Inventory2" /></span>`, `<p class="account-frame-overline">NoMorePaper</p>`, `<h1 class="account-frame-title">@Heading</h1>`, bei `Intro` `<p class="account-frame-intro">@Intro</p>` → `<div class="account-frame-card">@ChildContent</div>` → `@Below`. Kommentar oben (Warum: ein Rahmen für alle fünf Seiten, native `h1` statt `MudText`, weil die Stufe vom Theme-H1 abweicht). Muster: `Design/NoticeBox.razor` | P3 | 45 |
| `wwwroot/css/account-frame.css` | neu | Kopfkommentar wie `design.css`. Klassen mit den Werten aus „Maße“: `.account-frame`, `.account-frame-brand` (Flex-Spalte, `align-items: center`), `.account-frame-logo` (Flex, zentriert), `.account-frame-logo .mud-icon-root`, `.account-frame-overline`, `.account-frame-title`, `.account-frame-intro`, `.account-frame-card`, `.account-frame-card .account-form { gap: var(--nmp-space-md); max-width: none; margin: 0 }` (die Grundregel `.account-form` in `accounts.css` bleibt für Konto/Mail-Seiten), `.account-frame-card p { margin: 0 }`, `.account-frame-help` (Hinweis), `.account-frame-submit`, `.account-frame-submit .mud-icon-root`, `.account-frame-secondary` + `:hover` + `.mud-icon-root`. **Keine** `outline`-Regeln (ADR-0026). | – | 135 |
| `wwwroot/index.html` | ändern | eine Zeile `<link rel="stylesheet" href="css/account-frame.css" />` direkt nach `css/accounts.css` | – | 1 |
| `wwwroot/css/accounts.css` | ändern | Nur diese Regeln umstellen, Werte aus „Maße“ (Muster: `.search-input` in `components.css`): `.form-field` (gap), `.form-field label` (Mono-Beschriftung statt `700`), `.form-hint` (`0.75rem/1.125rem`, `--nmp-text-variant`), `.form-field-row` (bleibt Flex-Zeile für Felder mit Textknopf), **neu** `.form-input-wrap { position: relative; display: flex; align-items: center; flex: 1 1 14rem }`, `.form-input` (Werte „Eingabefeld“, `width: 100%`, `transition: border-color 150ms ease, background-color 150ms ease`), **neu** `.form-input:focus`, `.form-input[aria-invalid="true"]` → `border: 2px dashed var(--nmp-error)` (nicht nur Farbe: gestrichelt), **neu** `.form-input-with-icon`, **neu** `.form-input-with-toggle`, **neu** `.form-field-icon`, **neu** `.form-input-wrap:focus-within .form-field-icon`, `.form-input-large` + `font-family: var(--nmp-font-mono)` (Größe bleibt), `.form-toggle` → dunkler Nebenknopf (`min-height: 2.75rem; padding: 0 1rem; background: var(--nmp-surface-container); color: var(--nmp-primary); border: 1px solid var(--nmp-border); border-radius: var(--nmp-radius); font-weight: 500`), `.form-toggle[aria-pressed="true"]` → `border-color: var(--nmp-primary)` (Zustand steht zusätzlich in `aria-pressed`), **neu** `.form-toggle-icon` + `:hover` (Werte „Auge im Feld“, überschreibt Fläche/Rand von `.form-toggle`), `.form-check input` (`1.125rem`, `accent-color: var(--nmp-primary)`), `.form-check label` (Werte „Kontrollkästchen“), `.live-region.form-error:not(:empty)` (Werte „Fehlermeldung“), `.emergency-code` aus der gemeinsamen Regel mit `.link-result`/`.confirm-box` lösen: `margin: 0; padding: 0; border: 0`, **neu** `.emergency-code h2 { margin: 0 0 .5rem; font-size: 1.25rem; line-height: 1.75rem; letter-spacing: -0.01em; font-weight: 500; color: var(--nmp-primary) }` (Stufe headline-md). Kopfkommentar der Datei auf die neuen Werte anpassen. **Nicht** ändern: `.success-box` (Kamera nutzt sie), `.account-menu`, `.link-result`, `.confirm-box`, `.private-badge`, `.reset-notice*`, `.button-row`, `.doc-table …` | ganze UI-Suite | 179 → ~230 |
| `Accounts/FormField.razor` | ändern | Neuer Parameter `[Parameter] public string? Icon { get; set; }` (MudBlazor-Symbol, nur Schmuck). Markup: `.form-field-row` bleibt äußerer Container; darin `<div class="form-input-wrap">` mit optional `<MudIcon Icon="@Icon" Class="form-field-icon" aria-hidden="true" />`, dem `input` (zusätzliche Klassen `form-input-with-icon` wenn `Icon` gesetzt, `form-input-with-toggle` wenn `Password`) und bei `Password` dem Knopf `<button type="button" class="form-toggle form-toggle-icon" aria-pressed=… aria-label="@L["ShowPasswordFor", Label]" @onclick="ToggleVisible"><MudIcon Icon="@(_visible ? Icons.Material.Outlined.Visibility : Icons.Material.Outlined.VisibilityOff)" aria-hidden="true" /></button>`. Kein sichtbarer Text mehr im Knopf (Name bleibt per `aria-label`, Zustand per `aria-pressed`). Alle übrigen Attribute des `input` unverändert. Kopfkommentar um Symbol und Augen-Knopf ergänzen. | `SignInTests.ShowPasswordToggle_…` (unverändert grün) | 59 → ~70 |
| `Accounts/CopyField.razor` | – | keine Änderung (bekommt das neue Aussehen über `.form-input`/`.form-toggle`) | `SetupTests` | 0 |

- **Reihenfolge:** `account-frame.css` + `index.html` → `AccountFrame.razor` → `accounts.css` → `FormField.razor` → Build.
- **Telemetrie:** keine (nur Darstellung).
- **Akzeptanz:** `dotnet build NoMorePaper.slnx` ohne Warnungen; Zeilenprüfung grün.

### Paket P2 – Seiten umstellen und Texte · Modell: **Sonnet** · Effort: `medium`

Gemeinsames Muster für alle fünf Seiten: `<PageTitle>` bleibt; `<MudText Typo="Typo.h1">…</MudText>` fällt weg
und wird `<AccountFrame Heading="@L["…Heading"]" Intro="…">` um den **ganzen** übrigen Inhalt; jeder Zustand
(lädt, Fehler, Formular, Ergebnis) steht in der Karte. `@code` bleibt unverändert bis auf die genannten
Stellen. `data-ready`, `role`, `id`s, `autocomplete`, `LiveRegion`, Fokusführung bleiben exakt. Absende-Knopf
überall: `<button type="submit" class="nmp-button nmp-button-primary account-frame-submit" disabled="@_busy"><span>@L["…Button"]</span><MudIcon Icon="@Icons.Material.Filled.ArrowForward" aria-hidden="true" /></button>`
statt `MudButton`. Feldsymbole: Benutzername `Icons.Material.Outlined.Person`, Anzeigename
`Icons.Material.Outlined.Badge`, Passwortfelder `Icons.Material.Outlined.Lock`, Notfall-Code
`Icons.Material.Outlined.Key`.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Pages/SignIn.razor` | ändern | `AccountFrame Heading=SignInHeading Intro=SignInIntro`. Karte: `form.account-form` (unverändert `novalidate`, `data-ready`) mit Feld Benutzername (Symbol Person), Feld Passwort (Symbol Lock), danach `<p class="account-frame-help">@L["SignInForgotShort"]</p>`, `LiveRegion`, Absende-Knopf; **nach** dem `form`, noch in der Karte: `<a class="account-frame-secondary" href="notfall"><MudIcon Icon="@Icons.Material.Outlined.Key" aria-hidden="true" /><span>@L["SignInEmergencyLink"]</span></a>`. Die alten Absätze `.form-help` und Notfall-Link unter dem Formular fallen weg. | `SignInTests` (alle), P3 | 78 → ~85 |
| `Pages/Setup.razor` | ändern | `AccountFrame Heading=SetupHeading`, `Intro` = `SetupIntro` nur im Formular-Zustand (sonst `null`; der `<p>@L["SetupIntro"]</p>` fällt weg). Zustände in der Karte: `<p role="status">` (lädt), „schon eingerichtet“ (`<p data-ready>` + Link) , `EmergencyCodeView` (in `<div data-ready>`), Formular mit 4 Feldern + Symbolen + Knopf. | `SetupTests`, `AccountPagesAccessibilityTests` | 100 → ~104 |
| `Pages/AcceptInvitation.razor` | ändern | `AccountFrame Heading=InvitationHeading`, `Intro` = `L["InvitationIntro", _expires]` nur im Formular-Zustand; das `data-ready` des bisherigen Intro-Absatzes wandert auf das `form`. Ungültige Einladung: `<div data-ready><NoticeBox Kind="NoticeKind.Error" Announce="true" Message="@_invalid" /><p><a href="anmelden">@L["SetupToSignIn"]</a></p></div>` statt `div.error-box`. | `LinkPagesTests`, `AccountPagesAccessibilityTests.InvalidInvitation_…` | 101 → ~103 |
| `Pages/ResetPassword.razor` | ändern | `AccountFrame Heading=ResetHeading`, `Intro` = `ResetIntro` nur im Formular-Zustand. Erfolg: `<div role="status" data-ready tabindex="-1" @ref="_result"><NoticeBox Kind="NoticeKind.Success" Message="@L["ResetDone"]" /></div>` + Link „Zur Anmeldung“ (statt `div.success-box`; der Kommentar darüber bleibt sinngemäß). | `LinkPagesTests.ResetPassword_…` | 81 → ~83 |
| `Pages/EmergencyReset.razor` | ändern | `AccountFrame Heading=EmergencyResetHeading`, `Intro` = `EmergencyResetIntro` nur im Formular-Zustand. Neuer-Code-Zustand unverändert in der Karte (`<div data-ready>` mit `EmergencyResetDone` + `EmergencyCodeView`). 4 Felder mit Symbolen. | `LinkPagesTests.EmergencyReset_…` | 82 → ~84 |
| `Accounts/EmergencyCodeView.razor` | ändern | Nur `MudButton` „Weiter“ → `<button type="button" class="nmp-button nmp-button-primary account-frame-submit" @onclick="ContinueAsync"><span>@L["Continue"]</span><MudIcon Icon="@Icons.Material.Filled.ArrowForward" aria-hidden="true" /></button>`. Klasse `emergency-code`, `h2`, Fokus, Fehlerregion bleiben. | `SetupTests.…EmergencyCode…` | 55 → ~57 |
| `Localization/Strings.resx`, `Strings.en.resx` | ändern (Hotspot, nur ergänzen) | Neu, alphabetisch neben den `SignIn*`-Schlüsseln: `SignInIntro` = „Melde dich an, um papierlos zu arbeiten.“ / „Sign in to go paperless.“; `SignInForgotShort` = „Passwort vergessen? Frag den Verwalter.“ / „Forgot your password? Ask the administrator.“. `SignInForgot` und `ShowPassword` bleiben stehen (`ShowPassword` nutzt `MailSettings` weiter; `SignInForgot` ist danach ungenutzt → im Gesamt-Review des Meilensteins entfernen). | Build | +12 |

- **Reihenfolge:** Texte → `SignIn` → `EmergencyCodeView` → `Setup` → `EmergencyReset` → `ResetPassword` → `AcceptInvitation` → Build → `dotnet test` Namespace Accounts.
- **Telemetrie:** keine neuen Logs oder Abläufe. Begründung: Spur A ändert nur Darstellung; die bestehenden
  Einträge `AccountsLog.SignedIn`, `HouseholdSetUp`, `InvitationAccepted`, `PasswordChanged`, `CopyFailed`
  bleiben an derselben Stelle im `@code` und werden nicht verschoben. Katalog `docs/kb/telemetrie.md` bleibt
  unverändert.
- **Akzeptanz:** alle bisherigen Tests in `NoMorePaper.UI.Tests.Accounts` grün **ohne** Änderung an
  bestehenden Testdateien (bei F3a-B nur `RealApiAccountTests` Zeile 52, die das `h1` „Anmelden“ prüft).

### Paket P3 – UI-Tests · Modell: **Sonnet** · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `tests/NoMorePaper.UI.Tests/Accounts/AccountFrameTests.cs` | neu | `public sealed class AccountFrameTests(ServerFixture server)`, Öffnen wie `SignInTests.OpenSignedOutAsync` (für `/einrichten` `FakeAccounts { SetupNeeded = true }`, für `/einladung/abc` Mock wie in `AccountPagesAccessibilityTests`). Fälle: **(1)** `[Theory] SignedOutPage_ShowsOneHeading_InTheFrame_WithADecorativeLogo(path, heading)` für die fünf Pfade mit „Anmelden“, „Haushalt einrichten“, „Einladung annehmen“, „Neues Passwort setzen“, „Passwort mit Notfall-Code ersetzen“: genau ein `h1` mit dem Text, `.account-frame-card` sichtbar, `.account-frame-logo` hat `aria-hidden="true"`. **(2)** `SignIn_TabOrder_RunsFromTheFieldsToTheButtonAndTheEmergencyLink`: Fokus `#signin-user`, dann je `Tab` → `#signin-password` → Knopf „Passwort anzeigen: Passwort“ → Knopf „Anmelden“ → Link `SignInEmergencyLink` (`ToBeFocusedAsync`). **(3)** `FieldSymbols_AreHidden_AndTheFieldsKeepTheirLabels` auf `/einrichten`: 4 × `.form-field-icon` mit `aria-hidden="true"`, `GetByLabel("Benutzername")` = `#setup-user`. **(4)** `FocusedField_GetsAWhiteBorder`: nach `FocusAsync` auf `#signin-user` ist `getComputedStyle(el).borderTopColor` = `rgb(255, 255, 255)`. **(5)** `InvalidField_IsMarkedByADashedBorder_NotOnlyByColour`: leeres Absenden → `#signin-user` `borderTopStyle` = `dashed`. **(6)** `[Theory] SignIn_NeedsNoSideScrolling(width, height)` mit (390, 844) und (320, 256) (= 1280 × 1024 bei 400 %): `prepare` setzt `SetViewportSizeAsync` (Muster `Layout/NavigationTests`), dann `document.documentElement.scrollWidth <= window.innerWidth`. **(7)** `SignIn_LoadsNothingFromOtherHosts`: in `prepare` `page.Request +=` sammelt Hosts (Muster `PrivacyTests`), nach `data-ready` alle Hosts = `server.BaseAddress.Host`. | selbst | 170 |
| `tests/NoMorePaper.UI.Tests/Accounts/AccountPagesAccessibilityTests.cs` | ändern | Neu: `[Theory] DE/EN ResetDone_HasNoAxeViolations` (POST `/api/password-reset/abc` → 204, Formular ausfüllen wie `LinkPagesTests`, warten auf `main [role=status]`); `[Theory] DE/EN EmergencyResetNewCode_HasNoAxeViolations` (POST `/api/emergency-reset` → 200 `EmergencyCodeDto`, warten auf `#emergency-code`); `[Theory] DE/EN SignInOnAPhone_HasNoAxeViolations` (Viewport 390 × 844). | selbst | 139 → ~195 |

- **Reihenfolge:** `AccountFrameTests` → Axe-Ergänzungen → ganze Suite.
- **Telemetrie:** –
- **Akzeptanz:**
  - `dotnet test --project tests/NoMorePaper.UI.Tests/NoMorePaper.UI.Tests.csproj -- --filter-namespace NoMorePaper.UI.Tests.Accounts` grün
  - danach `dotnet test --solution NoMorePaper.slnx` grün (Pflicht wegen F2-A: Feldbausteine wirken auf
    `Classification`, `Inputs`, `Documents`)
  - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-file-length.ps1` grün

### Paket P4 – Doku und Häkchen · Modell: **Sonnet** (im selben Subagenten, eigener Start lohnt nicht) · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `docs/kb/oberflaeche.md` | ändern (nur ergänzen) | Zeile in Tabelle „Bausteine“: `AccountFrame` (`Accounts/AccountFrame.razor`, Rahmen der Seiten für Abgemeldete: Logo, Überzeile, H1, Unterzeile, Karte). Absatz „Felder“: Beschriftung Mono 11 px, Feld 44 px, Symbol links, Auge rechts, Fehler gestrichelt in `--nmp-error`. | – | +8 |
| `docs/ui/stitch/README.md` | ändern | Status Screen 01 → „umgesetzt“ | – | 1 |
| `docs/plan/m4u-oberflaeche.md` | ändern | nur Checkbox unter „A. Anmelden (Screen 01)“ abhaken | – | 1 |

- **Akzeptanz:** Commit auf `feat/m04u-a-anmelden` erst nach P5.

### Paket P5 – Review und Abnahme · Modell: **Opus** · Effort: `high` (steuernde Sitzung)

- Review-Schleife nach AGENTS.md §8 über die ganze Spur (Diff gegen `feat/m04u-oberflaeche`).
- Prüfliste: keine Zeile aus `code.html` übernommen (kein Tailwind-Klassenname, kein `material-symbols`); jedes
  Symbol `aria-hidden`; genau ein `h1`; keine `outline`-Regel; Werte nur über `--nmp-*`; Bedienlogik im `@code`
  unverändert (`git diff` der `@code`-Blöcke leer bis auf Knopf-Markup); Dateien anderer Spuren unberührt.
- Sichtvergleich: Playwright-Bilder von `/anmelden` bei 390 × 844 und 1280 × 800 nach
  `TestResults/m4u-a/` (über das Browser-Fenster oder ein kurzes Skript, kein Test) neben `screen.png`;
  danach Sichtprüfung durch den Nutzer, NVDA/VoiceOver durch den Nutzer.
- Push auf `feat/m04u-a-anmelden` nach grüner Review-Schleife; Merge in den Integrationsbranch durch die
  steuernde Sitzung (Merge-Reihenfolge A zuerst).

### Eskalation (alle Pakete)

- Zwei rote Läufe am selben Paket (Build oder Tests) → stoppen und melden.
- axe meldet **`target-size`** am Kontrollkästchen „Notfall-Code aufbewahrt“: Größe auf `1.5rem` (24 px)
  setzen – Barrierefreiheit geht vor dem Entwurf (M4u-Grundsatz), keine weitere Rückfrage.
- axe meldet **`color-contrast`** oder ein anderer Verstoß, den der Plan nicht abdeckt → stoppen und melden,
  keine eigene Farbe wählen.
- Ein Test außerhalb `Accounts` wird durch die neuen Feldstile rot (z. B. `Classification`, `Inputs`): nur
  beheben, wenn es ein Selektor oder eine Wartebedingung ist; prüft der Test Aussehen oder Verhalten, das der
  Plan ändert → stoppen und melden.
- Jede Stelle, an der eine Datei aus „Nicht ändern“ geändert werden müsste → stoppen und melden.

### Risiken

- **Wirkung auf andere Seiten (F2-A):** Feldstil und Augen-Knopf ändern sich auch auf Konto, Mitglieder,
  Mail-Einstellungen, Listen, Filterleiste und Angaben-Editor. Spuren B, C, D sehen das erst nach dem Merge von
  A; deshalb A zuerst mergen und die ganze Suite laufen lassen.
- **Kleine Schrift:** Feldbeschriftung 11 px Mono und Hinweise 12 px sind vom Nutzer freigegeben (ADR-0026),
  bleiben aber die kleinsten Texte der App; Sichtprüfung mit Lupe durch den Nutzer.
- **Leere Fehlerregion:** Die `LiveRegion` bleibt im DOM (sonst keine Ansage); sie erzeugt im Formular einen
  zusätzlichen Abstand von 1 rem vor dem Knopf. Nicht mit `display: none` verstecken.
- **PWA-Symbol** `icons/icon-192.png` bleibt gelb – nicht Teil von Spur A (siehe F4).

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|
| P1 | 2 | | |
| P2 | 2 | | |
| P3 | 2 | | |
| P4 | 1 (im Sonnet-Lauf) | | |
| P5 | 3 | | |
