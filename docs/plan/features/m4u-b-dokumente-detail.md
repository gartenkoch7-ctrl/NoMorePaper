# M4u Spur B – Dokumente: Detailplan

Aufgabe B aus [m4u-oberflaeche.md](../m4u-oberflaeche.md): `Documents.razor` (`/dokumente`) und
`DocumentFilterBar.razor` nach Stitch-Screen 02. Regeln: [AGENTS.md §12](../../../AGENTS.md),
[Vorlage](../detailplan-vorlage.md).

Grundlagen: [notizen.md 02](../../ui/stitch/02-dokumente/notizen.md), [screen.png](../../ui/stitch/02-dokumente/screen.png),
`code.html` (nur abgelesen), [KB oberflaeche.md](../../kb/oberflaeche.md), [KB ui-prozess.md](../../kb/ui-prozess.md),
[ADR-0026](../../adr/0026-optik-nach-stitch-design-system.md), Mobil-Empfehlung in
[docs/ui/stitch/README.md](../../ui/stitch/README.md).

## Was die API heute liefert

Geprüft am Code (`GET /api/documents` → `DocumentSummaryDto[]`, `EfDocumentRepository.ListAsync`,
`DocumentFilterQuery`, `DocumentMapping`; Stand `feat/m04u-oberflaeche` 73a685e).

| Inhalt aus Aufgabe B | Liefert die API das heute? | Woher es käme |
|---|---|---|
| Titel, Link, Privat | ja (`Title`, `IsPrivate`) | – |
| Absender, Art, Dokumentdatum, Betrag | ja (`Correspondent`, `DocumentType`, `DocumentDate`, `Amount`) | – |
| Status | ja (`State`: Queued/Processing/Done/Failed) | – |
| Erkennung (automatisch / bestätigt) | ja (`Recognition`: None/Automatic/Confirmed) | – |
| Eingangszeit | ja (`ReceivedAt`) | – |
| Blättern: Seite und Seitengröße | ja (`page`, `pageSize` 1–100; Antwort ist ein nacktes Array) | – |
| **Gesamtanzahl** („142 Dokumente“, „Zeige 1–7 von 142“) | **nein** – kein Zähler, kein Header | neuer Zähl-Endpunkt (F1) |
| **Art-Chips mit Anzahl** | **nein** – Arten gibt es über `/api/classification/document-types`, aber ohne Anzahl | Zähl-Endpunkt (F2) |
| **Textausschnitt** | **nein** – `content_text` (OCR) und `summary` (LLM) stehen in der DB, nicht in der Liste | Feld in der Liste (F3) |
| **Eingangsweg** | **nein** – Spalte `documents.source` existiert, die Liste gibt sie nicht aus | Feld in der Liste (F4) |
| **Tags je Zeile** | **nein** – nur als Filter, nicht in der Zeile | zweite Abfrage in der Liste (F5) |
| **„Zuletzt bearbeitet“** | **nein** – es gibt keinen Änderungszeitpunkt in der DB, auch keinen Verlauf | Migration nötig (F6) |
| **Miniaturen** | **indirekt ja** – die API liefert das Archiv-PDF/A (`/files/archive`), der Client zeichnet Seite 1 selbst (`Viewer/Thumbnail.razor`, schon in Liste und Suche) | – (F7, F8) |

## Offene Fragen an den Nutzer

> **Beantwortet 2026-11-22:** Der Nutzer hat alle Fragen wie empfohlen entschieden. Der Plan gilt so.

Diese Fragen müssen vor der Umsetzung beantwortet sein. **Der Plan unten ist für die jeweils empfohlene
Option geschrieben**; die letzte Spalte sagt, was sich bei der anderen Antwort ändert.

| Nr. | Frage | Optionen | Empfehlung | Bei der anderen Antwort |
|---|---|---|---|---|
| F0 | Screen 02 steht in `docs/ui/stitch/README.md` auf „geprüft“. Nach dem UI-Prozess wird nur umgesetzt, was du freigegeben hast. Gibst du den Entwurf mit den Abweichungen aus „Abgleich mit der App“ in `notizen.md` frei? | **A:** Ja, freigegeben. **B:** Nein, erst in Stitch nachbessern (GoBD, WORM, Stapelverarbeitung, Export entfernen). | A | B: Spur B wartet auf den neuen Entwurf. |
| F1 | Die Liste kennt keine Gesamtzahl. Ohne sie gibt es weder „142 Dokumente“ neben der Überschrift noch „Zeige 1–50 von 142“ und keine Seitennummern. Soll Spur B den Server erweitern? | **A:** Ja: neuer Endpunkt `GET /api/documents/counts` mit denselben Filtern (Paket P1). **B:** Nein: Überschrift ohne Zahl; Blättern nur mit „Vorherige / Nächste Seite“ und „Seite 2“; „Nächste“ ist aktiv, solange eine volle Seite kam. **C:** Gesamtzahl erst mit M4b. | A | B/C: P1 ohne `Counts*`-Dateien; `DocumentPager` nur im Modus „ohne Gesamtzahl“ (ist ohnehin als Rückfall eingebaut); kein Zahl-Abzeichen. |
| F2 | Art-Chips mit Anzahl („Rechnungen 38“) brauchen eine Zählung je Art. Wie sollen die Chips entstehen? | **A:** Aus demselben Zähl-Endpunkt: nur Arten mit mindestens einem Dokument, mit Anzahl, die Anzahl beachtet die übrigen Filter. **B:** Chips ohne Anzahl aus der Listen-API (alle Arten, auch leere). **C:** Keine Chips, „Dokumentart“ bleibt Auswahlfeld im Filter. | A | B: `DocumentTypeChips` bekommt `IReadOnlyList<EntryDto>` statt `DocumentCountsDto`, keine `.type-chip-count`. C: Chips entfallen, Art-Feld bleibt in `DocumentFilterBar`, `Combine` entfällt. |
| F3 | Der Textausschnitt unter dem Titel fehlt in der Liste. Was soll dort stehen? Hinweis: Der Titel steht im Zeilenkopf; NVDA liest den Zeilenkopf beim senkrechten Wandern durch eine Spalte jedes Mal mit, darum bleibt der Ausschnitt kurz (≤ 160 Zeichen). | **A:** Die Zusammenfassung des LLM, wenn vorhanden, sonst der Anfang des OCR-Texts (Server-Erweiterung in P1). **B:** Nur der Anfang des OCR-Texts, wie im Entwurf („OCR: …“). **C:** Weglassen. **D:** Nach M4b (dort entsteht `document_pages` mit Textanfang je Seite). | A | B: SQL nur `d.content_text`. C/D: kein `Excerpt` im DTO, keine `.doc-excerpt`. |
| F4 | Der Eingangsweg (Hochladen, Kamera, Scanner-Ordner, Mail, iCloud) steht in der DB, aber nicht in der Liste. Soll die Liste ihn liefern? | **A:** Ja, Feld `Source` in `DocumentSummaryDto` (kleine Server-Änderung in P1). **B:** Weglassen. | A | B: Unterzeile nur „Eingang 18.11.2026 10:00“, ohne Symbol. |
| F5 | Tags je Zeile („#strom“ unter der Art) liefert die Liste nicht. Sollen sie in die Zeile? | **A:** Ja, Server lädt die Tags der Seite mit einer zweiten Abfrage (P1). **B:** Nein, Spalte heißt „Art“, Tags nur im Viewer. | A | B: Spalte `ColType` = vorhandener Text „Dokumentart“, kein `Tags` im DTO, keine `.doc-tags`. |
| F6 | „Zuletzt bearbeitet“ als Karten: Die DB kennt keinen Änderungszeitpunkt. Was soll mit dem Bereich geschehen? | **A:** In M4u weglassen; später mit einem Verlauf. **B:** Ersatz „Zuletzt eingegangen“: die vier neuesten Dokumente als Karten (ohne Server, wiederholt aber die ersten Tabellenzeilen). **C:** Server-Erweiterung: Spalte `documents.updated_at` mit Migration, gesetzt bei Bearbeiten/Bestätigen/Privat (überschneidet sich mit den Migrationen von M4b). **D:** „Zuletzt geöffnet“ je Gerät im Browser-Speicher. | A | B: neue Datei `Documents/RecentDocuments.razor` (H2 + `DocumentTiles` mit den ersten 4 der Seite 1, eigener Aufruf `page=1&pageSize=4` ohne Filter). C/D: eigener Plan nötig – nicht in diesem Detailplan. |
| F7 | Der Umschalter „Tabelle / Kacheln“ stand unter Vorbehalt „nur, wenn die API Miniaturen liefert“. Die Miniatur zeichnet heute schon der Browser aus dem Archiv-PDF (wie in Liste und Suche). Sollen die Kacheln in Spur B kommen? | **A:** Ja, mit der vorhandenen Miniatur (Paket P4). **B:** Nach M4b verschieben. | A | B: P4 entfällt, kein Umschalter. |
| F8 | Im Entwurf hat jede Tabellenzeile links einen 32-px-Kasten mit Symbol. Heute steht dort die Miniatur (80 px breit, macht die Zeile über 113 px hoch). Was soll in der Tabelle stehen? | **A:** Symbolkasten wie im Entwurf; die Miniatur gibt es in den Kacheln. **B:** Miniatur bleibt in der Tabelle, Zeilen höher als im Entwurf. | A (bei F7-A) | B: in `DocumentTable` statt `.doc-icon` `<Thumbnail>` wie heute (nur bei `Done`); `ThumbnailTests` bleibt unverändert. |
| F9 | Im Entwurf zeigt die Spalte „Datum“ die Eingangszeit mit Eingangsweg. Heute gibt es zwei Spalten: Dokumentdatum und Eingang. Was soll die Spalte „Datum“ zeigen? | **A:** Oben das Dokumentdatum (danach filtert der Filter), darunter klein „Eingang 18.11.2026 10:00 über Mail“. **B:** Wie im Entwurf nur Eingangszeit und Weg; das Dokumentdatum entfällt in der Liste. **C:** Zwei Spalten wie heute. | A | B: oben `ReceivedAt`, darunter nur Weg. C: 8 Spalten, Unterzeile nur Weg. |
| F10 | Laut Meilensteinplan hat der Filterbereich nur noch Datum, Betrag, Sichtbarkeit und Absender. Heute gibt es dort auch Dokumentart und Tags. Was passiert mit diesen beiden? | **A:** Art wandert in die Chips (Auswahlfeld entfällt), der Tag-Filter bleibt im Filterbereich – keine Funktion geht verloren. **B:** Auch der Tag-Filter entfällt. | A | B: `TagPicker`, „alle Tags“ und `_allTags` aus `DocumentFilterBar` entfernen; `DocumentFilterTests.Apply_…` (5 aktiv → 4) und `ClassificationAccessibilityTests` anpassen. |
| F11 | Für Handys empfiehlt die Stitch-README einen eigenen Entwurf „05-mobil-navigation“ (untere Leiste, Listenzeilen). Er ist noch nicht abgelegt. Wie soll `/dokumente` auf dem Handy aussehen? | **A:** Ohne eigenes Mobilmuster: Tabelle in einem waagerecht scrollbaren Bereich, Chips und Blättern brechen um, Kacheln sind die schmale Ansicht. Die untere Leiste wäre `MainLayout.razor` (gesperrt, Spur 0). **B:** Erst 05 ablegen und prüfen; Spur B setzt danach unter 768 px Listenzeilen statt Tabelle um. | A | B: Spur B wartet auf 05; zusätzliches Paket „Listenzeile mobil“. |
| F12 | Die Server-Arbeit (P1) liegt außerhalb der Exklusiv-Dateien von B. M4b Schritt 0 ändert gleichzeitig `EfDocumentRepository`, `DocumentMapping`, `DocumentEndpoints` und setzt den Papierkorb-Filter in jede Abfrage. Wo soll P1 laufen? | **A:** In Spur B; neue Abfragen in neuen Dateien, bestehende Dateien nur ergänzen; die steuernde Sitzung trägt `EfDocumentCounts` und die Tag-Abfrage in den M4b-Plan (Papierkorb-Filter) ein. **B:** P1 wird Teil von M4b Schritt 0, Spur B wartet darauf. **C:** P1 als eigener kleiner Branch, zuerst in den Integrationsbranch, B danach. | A | B/C: P1 fällt hier weg; B startet erst, wenn die DTO-Felder in `feat/m04u-oberflaeche` sind. |

Durch Vorgaben schon entschieden (keine Frage): Werte nur aus `--nmp-*` (Tabelle „Maße“); kein eigener
Fokusrahmen; Bedienlogik (Live-Regionen, Ansage der Trefferzahl, Nachfragen alle 3 s während der
Verarbeitung) bleibt; keine Aktionsspalte und keine klickbare Zeile (Link im Titel); kein zweites Suchfeld
(die Filterleiste hat heute keins, die Suche bleibt im Kopf); kein Export, keine Mehrfachauswahl, keine
Spalten-/Sortierwahl, kein Brotkrumenpfad, kein Knopf „Dokument hochladen“ in der Seite (steht im Kopf);
Status mit den Wörtern des Bausteins `StatusText` („Wartet“ statt „In Warteschlange“); Seitengröße
10/25/50/100, Standard 50 wie heute; Ansicht und Seitengröße werden nicht gespeichert.

## Detailplan

**Erstellt von:** Opus (Stufe 3) · **Datum:** 2026-11-22 · **Offene Designfragen:** F0–F12 (oben), vor dem
Start zu beantworten

Branch `feat/m04u-b-dokumente` von `feat/m04u-oberflaeche`, eigener Worktree. Pfade ohne Projekt beziehen
sich auf `src/NoMorePaper.Web.Client/`. Merge-Reihenfolge A → D → **B** → C → E: Spur A stellt vorher die
Feldstile um (`.form-input` u. a. in `accounts.css`), die Filterfelder bekommen das neue Aussehen dadurch.

**Nicht ändern:** `Layout/*` (gesperrt), `Theme/ContrastTheme.cs`, `css/base.css`, `css/layout.css`,
`css/design.css`, `css/components.css`, `css/classification.css`, `css/accounts.css`, `css/viewer.css`,
`Viewer/*`, `Accounts/PrivateBadge.razor`, `Pages/Search.razor`, `Infrastructure/Documents/DocumentFilterSql.cs`
(M4b), Dateien anderer Spuren (`SignIn…`, `DocumentViewer*`, `DocumentFields*`, `Inputs*`, `MailSettings*`).
Hotspots nur ergänzen (keine Schlüssel löschen, keine Werte ändern): `Strings*.resx`, `wwwroot/index.html`,
`docs/kb/telemetrie.md`, `Documents/DocumentApi.cs`, `Documents/DocumentsLog.cs`, `Design/StatusText.razor`,
`tests/NoMorePaper.UI.Tests/Documents/ApiMocks.cs`, `SampleDocuments.cs`, die zwei Composition-Dateien.

**Exklusiv für B (Ergänzung der Liste im Meilensteinplan):** `Pages/Documents.razor(.cs)`,
`Pages/Documents.Polling.cs`, `Classification/DocumentFilterBar.razor`, `Classification/DocumentFilterQuery.cs`,
neue `Documents/Document{Table,Tiles,TypeChips,Pager}.razor`, `Documents/{PageWindow,DocumentListView,
DocumentStatus,DocumentSourceText}.cs`, `css/documents.css`, `css/document-list.css`, alle neuen Server-Dateien
aus P1.

### Einschätzung

| Aufgabenpunkt | Umfang | Risiko | Neuheit | Klarheit | Prüfbarkeit | Stufe |
|---|---|---|---|---|---|---|
| P1 Server: Listenfelder + Zähl-Endpunkt | ~16 Dateien, ~550 Zeilen | mittel: Sichtbarkeit privater Dokumente in Zählern, Konflikt mit M4b | Muster (`ListDocuments`, `DocumentFilterSql`, `/review/count`) | SQL im Plan vorgegeben | Postgres- + API-Tests | 2 |
| P2 Client-Logik (rein) | 6 Dateien, ~250 Zeilen | gering | Muster (`NavSection`, `TabNavigation`) | entschieden | Unit-Tests | 2 |
| P3 Seite Dokumente + Filter + Blättern | ~10 Dateien, ~900 Zeilen | mittel: Fokus beim Blättern, bestehende UI-Tests | Muster (heutige Seite, Bausteine) | nach F1–F11 entschieden | UI-Tests + Sichtvergleich | 2 |
| P4 Kacheln | 3 Dateien, ~200 Zeilen | gering | Muster (`Thumbnail`, Karten) | entschieden | UI-Tests | 2 |
| P5 Doku, Katalog, Häkchen | 4 Dateien, ~30 Zeilen | keins | – | entschieden | Lesen | 1 |
| P6 Review §8, Abnahme | – | – | – | – | Review | 3 |

Ablauf: **P1** in einem Sonnet-Subagenten, danach **P2 → P3 → P4 → P5** in einem zweiten Sonnet-Subagenten
im selben Worktree (P3 braucht die DTO-Felder aus P1). P5 lohnt keinen Haiku-Start. Kein Commit vor P6.

### Maße aus dem Entwurf → Umsetzung

Quelle `code.html` (nur abgelesen, nie kopiert). Links Tailwind-Klasse im Entwurf, rechts der Wert im Code.
`outline-variant/30` wird überall `var(--nmp-border)`, `rounded-lg` (im Entwurf 0,5 rem) `var(--nmp-radius)`,
`rounded` `.25rem`. Mono = `var(--nmp-font-mono)`.

| Element | Entwurf | Umsetzung |
|---|---|---|
| Kopfzeile | `flex md:flex-row md:items-end justify-between gap-space-md` | `.doc-page-head { display:flex; flex-wrap:wrap; align-items:flex-end; justify-content:space-between; gap:var(--nmp-space-md); margin-bottom:var(--nmp-space-lg) }` |
| H1 | `headline-lg` 28/36, −0,02 em, 600, `text-primary` | natives `<h1 class="doc-page-title">` (Theme-H1 wäre 40 px): `font-size:1.75rem; line-height:2.25rem; letter-spacing:-0.02em; font-weight:600; color:var(--nmp-primary); margin:0` |
| Anzahl neben H1 | `px-2 py-0.5 rounded-full bg-surface-container-high text-on-surface label-sm border` | `.doc-count { padding:.125rem .5rem; border-radius:9999px; background:var(--nmp-elevated-high); color:var(--nmp-text); border:1px solid var(--nmp-border); font:400 .625rem/.875rem var(--nmp-font-mono); letter-spacing:.05em }` |
| Ansicht-Gruppe | `bg-surface-container-lowest rounded-lg p-0.5 border` | `.view-toggle { display:inline-flex; padding:.125rem; background:var(--nmp-canvas-lowest); border:1px solid var(--nmp-border); border-radius:var(--nmp-radius) }` |
| Ansicht-Knopf | `gap-1.5 px-space-sm py-1.5 rounded label-md`; aktiv `bg-surface-container-high text-primary`, sonst `text-on-surface-variant` | `.view-toggle button { display:inline-flex; align-items:center; gap:.375rem; padding:.375rem .5rem; border:0; border-radius:.25rem; background:transparent; color:var(--nmp-text-variant); font:500 .6875rem/1rem var(--nmp-font-mono); letter-spacing:.04em; cursor:pointer }`, `:hover { color:var(--nmp-primary) }`, `[aria-pressed="true"] { background:var(--nmp-elevated-high); color:var(--nmp-primary) }`, Symbol `1rem` |
| Filterkarte | `bg-surface-container-low rounded-lg p-space-md border space-y-space-md` | `.doc-filter-card { display:flex; flex-direction:column; gap:var(--nmp-space-md); padding:var(--nmp-space-md); background:var(--nmp-paper-low); border:1px solid var(--nmp-border); border-radius:var(--nmp-radius); margin-bottom:var(--nmp-space-lg) }` |
| Knopf „Filter“ (wie „Spalten“) | `h-9 px-3 rounded-lg bg-surface-container-lowest border text-on-surface-variant label-md gap-1.5`, Symbol 16 px | `.doc-filter-card #filter-toggle { min-height:2.25rem; padding:0 .75rem; display:inline-flex; align-items:center; gap:.375rem; background:var(--nmp-canvas-lowest); color:var(--nmp-text-variant); font:500 .6875rem/1rem var(--nmp-font-mono); letter-spacing:.04em }`, `:hover { color:var(--nmp-primary) }`, Symbol `1rem`; `.doc-filter-card .filter-heading { margin:0 }` |
| Chip-Reihe | `flex gap-space-xs overflow-x-auto` | `.type-chips { display:flex; flex-wrap:wrap; gap:var(--nmp-space-xs) }` – Umbruch statt Waagerecht-Scrollen (Zoom) |
| Chip | `px-3 py-1 rounded bg-surface-container-lowest text-on-surface-variant border label-sm gap-1.5`; Zahl `text-outline 10px`; Hover `text-primary bg-surface-container` | `.type-chip { display:inline-flex; align-items:center; gap:.375rem; padding:.25rem .75rem; border-radius:.25rem; background:var(--nmp-canvas-lowest); color:var(--nmp-text-variant); border:1px solid var(--nmp-border); font:400 .625rem/.875rem var(--nmp-font-mono); letter-spacing:.05em; cursor:pointer }`, `:hover { color:var(--nmp-primary); background:var(--nmp-surface-container) }`, `.type-chip-count { color:var(--nmp-outline) }` |
| Chip aktiv | `bg-primary text-on-primary font-medium`; Zahl `rounded-full bg-black/20 px-1` | `.type-chip[aria-pressed="true"] { background:var(--nmp-primary); color:var(--nmp-on-primary); border-color:var(--nmp-primary); font-weight:500 }`, `… .type-chip-count { color:var(--nmp-on-primary); background:rgb(0 0 0 / .2); border-radius:9999px; padding:0 .25rem }` |
| Tabellenrahmen | `bg-surface-container-lowest rounded-lg border overflow-hidden` | `.doc-list-frame { background:var(--nmp-canvas-lowest); border:1px solid var(--nmp-border); border-radius:var(--nmp-radius); overflow:hidden }`; darin `.doc-list-scroll { overflow-x:auto }` |
| Leiste über der Tabelle (im Entwurf „Auswahl“) | `h-10 px-space-md bg-surface-container-low border-b label-sm text-on-surface-variant` | trägt die `caption`: `.doc-list-table caption { caption-side:top; padding:.75rem 1rem; margin:0; background:var(--nmp-paper-low); border-bottom:1px solid var(--nmp-border); color:var(--nmp-text-variant); font:400 .625rem/.875rem var(--nmp-font-mono); letter-spacing:.05em; text-align:start }` |
| Zellen | `py-3 px-4` | `.doc-list-table :is(th, td) { padding:.75rem 1rem; vertical-align:top }`, `.doc-list-table tbody th { font-weight:400 }` (Kopfstil `thead th` kommt schon aus `.doc-table`) |
| Zeile Hover | `hover:bg-surface-container` | `.doc-list-table tbody tr:hover { background:var(--nmp-surface-container) }`, kein `cursor:pointer` |
| Symbolkasten | `w-8 h-8 rounded bg-surface-container-high text-primary mt-0.5`, Symbol 18 px; Zeile Hover `bg-primary text-on-primary` | `.doc-icon { flex:none; display:inline-flex; align-items:center; justify-content:center; width:2rem; height:2rem; margin-top:.125rem; border-radius:.25rem; background:var(--nmp-elevated-high); color:var(--nmp-primary) }`, `.doc-icon .mud-icon-root { font-size:1.125rem }`, `.doc-list-table tbody tr:hover .doc-icon { background:var(--nmp-primary); color:var(--nmp-on-primary) }` |
| Titelzelle | `flex items-start gap-3` | `.doc-title-cell { display:flex; align-items:flex-start; gap:.75rem }` |
| Titel | `headline-sm 14px font-medium tracking-tight text-primary`, `truncate` | `.doc-title-link { font-size:.875rem; line-height:1.375rem; font-weight:500; letter-spacing:-0.005em; color:var(--nmp-primary); text-decoration:none }`, `:hover { text-decoration:underline }`; **kein** Abschneiden |
| Ausschnitt | `body-sm 12px text-outline`, Mono, `truncate` | `.doc-excerpt { display:block; margin-top:.125rem; font:400 .75rem/1.125rem var(--nmp-font-mono); color:var(--nmp-outline) }`; nicht abschneiden (der Server kürzt) |
| Art | `px-2 py-0.5 rounded bg-surface-container-high text-primary label-sm 11px border` | `.doc-type { display:inline-block; padding:.125rem .5rem; border-radius:.25rem; background:var(--nmp-elevated-high); color:var(--nmp-primary); border:1px solid var(--nmp-border); font:400 .6875rem/1rem var(--nmp-font-mono) }` |
| Tags | `text-outline label-sm 10px`, „#tag“ | `.doc-tags { display:block; margin-top:.25rem; font:400 .625rem/.875rem var(--nmp-font-mono); color:var(--nmp-outline) }`; „#“ in `<span aria-hidden="true">` |
| Datum | `label-md text-primary` | `.doc-date { font:500 .6875rem/1rem var(--nmp-font-mono); letter-spacing:.04em; color:var(--nmp-primary) }` |
| Unterzeile Eingang | `body-sm 11px text-outline gap-1`, Symbol 13 px | `.doc-sub { display:flex; align-items:center; gap:.25rem; margin-top:.125rem; font-size:.6875rem; line-height:1rem; color:var(--nmp-outline) }`, Symbol `.8125rem` |
| Betrag | `text-right label-md font-semibold text-primary` | `.doc-amount { text-align:end; white-space:nowrap; font:600 .6875rem/1rem var(--nmp-font-mono); color:var(--nmp-primary) }`, Spaltenkopf `th.doc-col-amount { text-align:end }` |
| Status | `label-sm 11px text-primary`, Symbol 15 px | Baustein `StatusText`; `.doc-list-table .nmp-status { font-size:.6875rem; line-height:1rem }`, Symbol `.9375rem` |
| Erkennung | (im Entwurf GoBD-Spalte) | Baustein `Badge` (Stil aus `design.css`) |
| Fußleiste Blättern | `px-space-md py-space-sm border-t flex justify-between gap-space-sm label-sm text-on-surface-variant` | `.doc-pager { display:flex; flex-wrap:wrap; align-items:center; justify-content:space-between; gap:var(--nmp-space-sm); padding:var(--nmp-space-sm) var(--nmp-space-md); border-top:1px solid var(--nmp-border); font:400 .625rem/.875rem var(--nmp-font-mono); letter-spacing:.05em; color:var(--nmp-text-variant) }`, `.doc-pager label { color:var(--nmp-outline) }` |
| Zeilen pro Seite | `bg-surface-container-low border rounded px-2 py-1 text-primary` | `.doc-pager select { background:var(--nmp-paper-low); border:1px solid var(--nmp-border); border-radius:.25rem; padding:.25rem .5rem; color:var(--nmp-primary); font:inherit }` (natives Auswahlfeld mit eigenem Pfeil) |
| Seitenknöpfe | `w-7 h-7 rounded border text-on-surface`; aktuell `bg-primary text-on-primary font-medium`; Hover `bg-surface-container-high text-primary` | `.doc-pager-pages { display:flex; gap:.25rem; list-style:none; margin:0; padding:0 }`, `.doc-pager-pages button { min-width:1.75rem; height:1.75rem; display:inline-flex; align-items:center; justify-content:center; border-radius:.25rem; border:1px solid var(--nmp-border); background:transparent; color:var(--nmp-text); font:inherit; cursor:pointer }`, `:hover:not(:disabled) { background:var(--nmp-elevated-high); color:var(--nmp-primary) }`, `[aria-current="page"] { background:var(--nmp-primary); color:var(--nmp-on-primary); border-color:var(--nmp-primary); font-weight:500 }`, `:disabled { color:var(--nmp-text-muted); cursor:not-allowed }`, Lücke `<span aria-hidden="true">…</span>` mit `color:var(--nmp-outline); padding:0 .25rem` |
| Kacheln-Raster | `grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-space-sm` | `.doc-tiles { list-style:none; margin:0; padding:0; display:grid; gap:var(--nmp-space-sm); grid-template-columns:repeat(auto-fill, minmax(15rem, 1fr)) }` – `auto-fill` statt fester Spalten, damit 200–400 % Zoom umbricht |
| Kachel | `bg-surface-container-lowest hover:bg-surface-container p-space-md rounded-lg border hover:border-outline/50 h-40` | `.doc-tile { display:flex; flex-direction:column; justify-content:space-between; gap:var(--nmp-space-sm); min-height:10rem; padding:var(--nmp-space-md); background:var(--nmp-canvas-lowest); border:1px solid var(--nmp-border); border-radius:var(--nmp-radius) }`, `:hover { background:var(--nmp-surface-container); border-color:var(--nmp-border-strong) }` (`min-height` statt fester Höhe: Zoom) |
| Kachel Titel / Zeile / Fuß | Titel `headline-sm` 16/24 500; Zeile `body-sm text-on-surface-variant`; Fuß `pt-space-xs border-t outline-variant/20`, „Betrag“ `label-sm text-outline`, Wert `label-lg 500 text-primary` | `.doc-tile-title { margin:0; font-size:1rem; line-height:1.5rem; font-weight:500; letter-spacing:-0.005em }` (Link darin wie `.doc-title-link`, aber 1 rem), `.doc-tile-meta { margin:0; font-size:.75rem; line-height:1.125rem; color:var(--nmp-text-variant) }`, `.doc-tile-foot { display:flex; flex-wrap:wrap; align-items:flex-end; justify-content:space-between; gap:.5rem; padding-top:var(--nmp-space-xs); border-top:1px solid var(--nmp-border) }`, `.doc-tile-amount-label { font:400 .625rem/.875rem var(--nmp-font-mono); color:var(--nmp-outline) }`, `.doc-tile-amount { font:500 .8125rem/1.125rem var(--nmp-font-mono); color:var(--nmp-primary) }` |

Weggelassen (Notizen „Abgleich“ und „Nicht mitnehmen“): Brotkrumen „Workspace“, Export, Stapelverarbeitung,
Knopf „Dokument hochladen“ in der Seite, Kontrollkästchen und Auswahl-Leiste, WORM-/SHA-Hinweise, Netto/MwSt,
Aktionsspalte, Spalten/Sortierung, Zeitraum-/GoBD-/Signatur-Auswahl, zweites Suchfeld mit „⌘K“,
Compliance-Fußleiste, `truncate`, `cursor-pointer` an `tr`, pulsierende Punkte.

### Paket P1 – Server: Listenfelder und Zähler · Modell: **Sonnet** · Effort: `medium`

Stufe 2, weil die Sicherheitsentscheidung hier im Plan fällt: die Sichtbarkeitsbedingung ist wörtlich
vorgegeben und die Tests dazu sind Pflicht. Pfade ab Repo-Wurzel.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Contracts/Documents/DocumentSummaryDto.cs` | ändern | Am Ende drei optionale Parameter mit XML-Doku: `string Source = "upload"` (Werte wie `DocumentFieldsDto.Source`), `string? Excerpt = null` („Zusammenfassung oder Anfang des erkannten Texts, höchstens 160 Zeichen; nur als Text anzeigen“), `IReadOnlyList<EntryDto>? Tags = null` (nach Name sortiert). Bestehende Aufrufe bleiben gültig. | – | +10 |
| `src/NoMorePaper.Contracts/Documents/DocumentCountsDto.cs` | neu | `public sealed record DocumentCountsDto(int Total, int AllTypes, IReadOnlyList<DocumentTypeCountDto> ByType)`; Doku: `Total` = Treffer mit allen Filtern, `AllTypes` = Treffer ohne den Art-Filter, `ByType` = je Art ohne den Art-Filter, nur Arten mit ≥ 1 Dokument, nach Name | – | 15 |
| `src/NoMorePaper.Contracts/Documents/DocumentTypeCountDto.cs` | neu | `public sealed record DocumentTypeCountDto(EntryDto Type, int Count)` | – | 8 |
| `src/NoMorePaper.Application/Documents/DocumentSummary.cs` | ändern | Record um `DocumentSource Source, string? Excerpt, IReadOnlyList<EntrySummary> Tags` am Ende erweitern (Pflichtparameter; einziger Aufrufer ist `EfDocumentRepository`) | – | +4 |
| `src/NoMorePaper.Application/Documents/DocumentExcerpt.cs` | neu | rein: `public static class DocumentExcerpt { public const int MaxLength = 160; public static string? From(string? text) }`: `null`/nur Leerraum → `null`; Leerraum zu einem Leerzeichen zusammenfassen (`string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))`); ≤ 160 → unverändert; sonst auf 160 kürzen, am letzten Leerzeichen ab Position 80 abschneiden (sonst hart bei 160), `TrimEnd()` + „…“ | `DocumentExcerptTests` | 35 |
| `src/NoMorePaper.Application/Documents/DocumentCounts.cs`, `DocumentTypeCount.cs` | neu | `public sealed record DocumentCounts(int Total, int AllTypes, IReadOnlyList<DocumentTypeCount> ByType)`; `public sealed record DocumentTypeCount(EntrySummary Type, int Count)` | – | 2 × 10 |
| `src/NoMorePaper.Application/Documents/IDocumentCounts.cs` | neu | `Task<DocumentCounts> CountAsync(Guid viewerId, DocumentFilter filter, CancellationToken cancellationToken)`; Doku: „nur Dokumente, die `viewerId` sehen darf – wie `IDocumentRepository.ListAsync`“ | – | 15 |
| `src/NoMorePaper.Application/Documents/CountDocuments.cs` | neu | wie `ListDocuments`: `public sealed class CountDocuments(IDocumentCounts counts, ICurrentAccount current)`, `CountAsync(DocumentFilter filter, CancellationToken)` → `counts.CountAsync(current.Id, filter, ct)` | Server.Tests | 20 |
| `src/NoMorePaper.Application/Composition/ApplicationServiceCollectionExtensions.cs` | ändern (Hotspot) | `services.AddScoped<CountDocuments>();` nach `ListDocuments` | – | 1 |
| `src/NoMorePaper.Infrastructure/Documents/DocumentListRow.cs` | ändern | `public string Source { get; set; } = nameof(DocumentSource.Upload);`, `public string? Excerpt { get; set; }` | – | +4 |
| `src/NoMorePaper.Infrastructure/Documents/DocumentTagRow.cs` | neu | `internal sealed class DocumentTagRow { Guid DocumentId; Guid TagId; string TagName }` (Properties mit `get; set;`, Namen = Spaltenaliase) | – | 15 |
| `src/NoMorePaper.Infrastructure/Documents/DocumentTypeCountRow.cs` | neu | `internal sealed class DocumentTypeCountRow { Guid Id; string Name; int Count }` | – | 15 |
| `src/NoMorePaper.Infrastructure/Documents/EfDocumentRepository.cs` | ändern (nur `ListAsync`) | SELECT nach `"IsMine",` ergänzen: `d.source AS "Source", left(coalesce(nullif(btrim(d.summary), ''), d.content_text), 400) AS "Excerpt",`. Danach, wenn Zeilen da sind, zweite Abfrage `SELECT dt.document_id AS "DocumentId", tg.id AS "TagId", tg.name AS "TagName" FROM document_tags dt JOIN tags tg ON tg.id = dt.tag_id WHERE dt.document_id = ANY(@ids) ORDER BY tg.name, tg.id` (`@ids` `NpgsqlDbType.Array \| NpgsqlDbType.Uuid` mit den Ids der Seite) als private Methode `LoadTagsAsync(IReadOnlyList<Guid> ids, CancellationToken)` → `ILookup<Guid, EntrySummary>`. Abbildung: `Enum.Parse<DocumentSource>(row.Source)`, `DocumentExcerpt.From(row.Excerpt)`, `[.. tags[row.Id]]`. Kommentar: Tags kommen nur für Dokumente der Seite, die der Betrachter schon sehen darf. | `DocumentListFieldsTests` | 140 → ~175 |
| `src/NoMorePaper.Infrastructure/Documents/EfDocumentCounts.cs` | neu | `internal sealed class EfDocumentCounts(AppDbContext db) : IDocumentCounts` (Konstruktor wie `EfDocumentRepository`). `private const string Visible = " WHERE (d.owner_id = @viewer OR NOT d.is_private)";` mit Kommentar „gleiche Regel wie `EfDocumentRepository.ListAsync`; M4b setzt den Papierkorb-Filter in beide“. Je Abfrage eine **neue** Parameterliste mit `new("viewer", NpgsqlDbType.Uuid) { Value = viewerId }` (ein `NpgsqlParameter` gehört nur zu einem Befehl). (1) `Total`: `SELECT count(*)::int AS "Value" FROM documents d` + `Visible` + `DocumentFilterSql.Conditions(filter, p)` über `SqlQueryRaw<int>(…).SingleAsync`. (2) `var withoutType = filter with { DocumentTypeId = null };` `AllTypes` = `Total`, wenn `filter.DocumentTypeId` null ist, sonst dieselbe Abfrage mit `withoutType`. (3) `ByType`: `SELECT t.id AS "Id", t.name AS "Name", count(*)::int AS "Count" FROM documents d JOIN document_types t ON t.id = d.document_type_id` + `Visible` + `Conditions(withoutType, p)` + ` GROUP BY t.id, t.name ORDER BY t.name, t.id` über `SqlQueryRaw<DocumentTypeCountRow>`. | `DocumentCountsTests` | 75 |
| `src/NoMorePaper.Infrastructure/Composition/InfrastructureServiceCollectionExtensions.cs` | ändern (Hotspot) | `services.AddScoped<IDocumentCounts, EfDocumentCounts>();` nach `IDocumentRepository` | – | 1 |
| `src/NoMorePaper.Server/Documents/DocumentEndpoints.cs` | ändern | `group.MapGet("/counts", CountAsync);` nach `MapGet("/", ListAsync)`; `CountAsync(CountDocuments count, DocumentFilterQuery query, CancellationToken)` genau wie `ListAsync` (400 mit `query.Error`, sonst `Results.Ok(counts.ToDto())`); Kopfkommentar der Klasse um „counts“ ergänzen | `CountTests` | 88 → ~105 |
| `src/NoMorePaper.Server/Documents/DocumentMapping.cs` | ändern | `ToDto(DocumentSummary)`: zusätzlich `summary.Source.ToWord()` (aus `Server/Inputs/InputsMapping`), `summary.Excerpt`, Tags über die vorhandene Abbildung `EntrySummary → EntryDto` (wie in `Labels.ToDto()`). Neu `ToDto(this DocumentCounts counts)` → `DocumentCountsDto` | – | 51 → ~70 |
| `tests/NoMorePaper.Application.Tests/Documents/DocumentExcerptTests.cs` | neu | Fälle: `null` → `null`; `"  \n "` → `null`; `"a\n\n b"` → `"a b"`; 160 Zeichen → unverändert; 200 Zeichen mit Leerzeichen bei 150 → 150 Zeichen + „…“; 200 Zeichen ohne Leerzeichen → 160 + „…“; Leerzeichen nur bei 40 → hart bei 160 | selbst | 70 |
| `tests/NoMorePaper.Infrastructure.Tests/Documents/DocumentCountsTests.cs` | neu | Muster `DocumentVisibilityTests`/`DocumentFilterTests`. Fälle: (1) privates Dokument eines **anderen** Kontos zählt weder in `Total`, `AllTypes` noch `ByType`; eigenes privates zählt. (2) Absender-Filter verkleinert `Total`. (3) Art-Filter: `Total` = nur diese Art, `AllTypes` und `ByType` ignorieren ihn. (4) Art ohne Dokument fehlt in `ByType`; Reihenfolge nach Name. (5) leere DB → `0, 0, []`. | selbst | 150 |
| `tests/NoMorePaper.Infrastructure.Tests/Documents/DocumentListFieldsTests.cs` | neu | (1) Quelle `Mail` → `Source == DocumentSource.Mail`. (2) Ausschnitt: Zusammenfassung vor OCR-Text; ohne Zusammenfassung OCR-Text; ohne beides `null`. (3) Tags nach Name sortiert; ohne Tags leer. (4) Tags eines Dokuments auf einer anderen Seite (`pageSize=1`) kommen nicht mit. | selbst | 130 |
| `tests/NoMorePaper.Server.Tests/Documents/CountTests.cs` | neu | Muster `QueryTests`. (1) ohne Anmeldung 401. (2) `?from=gestern` → 400 `application/problem+json`. (3) `WithFreshDatabase("query_counts")`: zwei geteilte + ein privates Upload → `Total == 3`, `AllTypes == 3`, `ByType` leer. (4) Liste nach Upload: `Source == "upload"`, `Excerpt == null`, `Tags` leer. | selbst | 100 |

- **Reihenfolge:** Contracts → Application (+ Excerpt-Tests) → Infrastructure (+ Tests) → Server (+ Tests).
- **Telemetrie:** keine neuen Ereignisse (Lese-Abfragen werden wie die Liste nicht geloggt); Fehler laufen über
  die vorhandene Problembehandlung.
- **Akzeptanz:**
  - `dotnet build NoMorePaper.slnx` ohne Warnungen
  - `dotnet test --project tests/NoMorePaper.Application.Tests/NoMorePaper.Application.Tests.csproj -- --filter-class NoMorePaper.Application.Tests.Documents.DocumentExcerptTests` grün
  - `dotnet test --project tests/NoMorePaper.Infrastructure.Tests/NoMorePaper.Infrastructure.Tests.csproj -- --filter-namespace NoMorePaper.Infrastructure.Tests.Documents` grün
  - `dotnet test --project tests/NoMorePaper.Server.Tests/NoMorePaper.Server.Tests.csproj -- --filter-namespace NoMorePaper.Server.Tests.Documents` grün, ebenso `NoMorePaper.Server.Tests.Classification` (`FilterApiTests` nutzt die Liste)
  - `dotnet test --project tests/NoMorePaper.Architecture.Tests/NoMorePaper.Architecture.Tests.csproj` grün

### Paket P2 – Client-Logik (rein) · Modell: **Sonnet** · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Documents/PageWindow.cs` | neu | `public static class PageWindow`: `int TotalPages(int total, int pageSize)` = `max(1, ceil(total / pageSize))`; `(int From, int To) Range(int page, int pageSize, int total)` (From = `(page−1)·size+1`, To = `min(page·size, total)`; `total == 0` → `(0, 0)`); `IReadOnlyList<int?> Pages(int current, int totalPages)` (`null` = Lücke; `current` auf 1…totalPages begrenzen): `totalPages ≤ 7` → alle; `current ≤ 3` → `1,2,3,4,null,T`; `current ≥ T−2` → `1,null,T−3,T−2,T−1,T`; sonst `1,null,c−1,c,c+1,null,T` | `PageWindowTests` | 50 |
| `Documents/DocumentListView.cs` | neu | `public enum DocumentListView { Table, Tiles }` | – | 8 |
| `Documents/DocumentStatus.cs` | neu | rein: `public static StatusKind KindOf(DocumentProcessingState state)` (Queued→Waiting, Processing→Processing, Done→Done, Failed→Failed, sonst `ArgumentOutOfRangeException`) | `DocumentStatusTests` | 20 |
| `Design/StatusKeys.cs` | neu | rein: `public static string For(StatusKind kind)` – die Schalt-Tabelle aus `StatusText.razor` (`StatusWaiting` …) hierher, damit Ansage und Anzeige dieselben Wörter nutzen | `DocumentStatusTests` | 20 |
| `Design/StatusText.razor` | ändern (Hotspot) | `Key` → `StatusKeys.For(Kind)`; sonst nichts | – | −8 |
| `Documents/DocumentSourceText.cs` | neu | rein: `string? Key(string source)` → `upload`→`NavUpload`, `camera`→`NavCamera`, `scanner-folder`→`InpKindScannerFolder`, `mail`→`InpKindMail`, `icloud`→`InpKindICloud`, sonst `null` (neuerer Server); `string Icon(string source)` → `Icons.Material.Outlined.UploadFile`, `PhotoCamera`, `Scanner`, `Mail`, `Cloud`, sonst `Description` | `DocumentSourceTextTests` | 35 |
| `Classification/DocumentFilterQuery.cs` | ändern | neu `public static DocumentFilterDto? Combine(DocumentFilterDto? bar, Guid? typeId)`: beides leer (`ActiveCount(bar) == 0 && typeId is null`) → `null`, sonst `(bar ?? new DocumentFilterDto()) with { DocumentTypeId = typeId }` | `DocumentFilterQueryTests` | 55 → ~70 |
| `tests/NoMorePaper.Web.Client.Tests/Documents/PageWindowTests.cs` | neu | `TotalPages`: (0,50)→1, (50,50)→1, (51,50)→2. `Range`: (1,50,120)→(1,50), (3,50,120)→(101,120), (1,50,0)→(0,0). `Pages` als `[Theory]`: (1,1)→[1]; (1,7)→1…7; (1,21)→[1,2,3,4,null,21]; (3,21)→dasselbe; (4,21)→[1,null,3,4,5,null,21]; (19,21)→[1,null,18,19,20,21]; (21,21)→dasselbe; (0,21) wie 1; (30,21) wie 21 | selbst | 70 |
| `tests/NoMorePaper.Web.Client.Tests/Documents/DocumentStatusTests.cs` | neu | alle vier Zustände → Art; alle fünf Arten → Schlüssel | selbst | 40 |
| `tests/NoMorePaper.Web.Client.Tests/Documents/DocumentSourceTextTests.cs` | neu | fünf Wörter → Schlüssel und Symbol; unbekanntes Wort → `null` / `Description` | selbst | 40 |
| `tests/NoMorePaper.Web.Client.Tests/Classification/DocumentFilterQueryTests.cs` | neu | `Combine(null, null)` → `null`; `Combine(null, id)` → nur Art; `Combine(bar, null)` behält alle Felder und setzt Art `null`; `Combine(bar mit Art X, Y)` → Y; `Build` mit Art enthält `documentTypeId=` | selbst | 60 |

- **Reihenfolge:** `PageWindow` → `DocumentStatus` + `StatusKeys` → `DocumentSourceText` → `Combine` → Tests.
- **Telemetrie:** – (reine Logik).
- **Akzeptanz:** `dotnet test --project tests/NoMorePaper.Web.Client.Tests/NoMorePaper.Web.Client.Tests.csproj` grün.

### Paket P3 – Seite Dokumente, Filter, Blättern · Modell: **Sonnet** · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Pages/Documents.razor` | ändern | Nur Markup, `@code` wandert nach `.razor.cs`. Reihenfolge: `PageTitle`; `<div class="doc-page-head">` mit `<div>` (`<h1 class="doc-page-title">@L["DocumentsHeading"]</h1>` + bei `_counts` `<span class="doc-count">` „{n} Dokumente“/„1 Dokument“) und (P4) Ansicht-Gruppe; `<div class="doc-filter-card">` mit `<DocumentFilterBar ActiveCount="@DocumentFilterQuery.ActiveCount(_barFilter)" OnApply="ApplyFilterAsync" />` und darunter (nur bei `_counts`) `<DocumentTypeChips Counts="_counts" SelectedTypeId="_typeId" OnSelect="SelectTypeAsync" />`; die zwei `LiveRegion` wie heute; Zustände Laden / Fehler (unverändert: `role="alert"` mit `error-box`, Texte und Knopf „Erneut versuchen“ wie heute) / leer (wie heute, `ActiveCount(Combined)`); Liste: `<div class="doc-list-frame" data-ready>` mit `<DocumentTable Documents="_documents" />` (P4: oder `DocumentTiles`) und `<DocumentPager Page Total=_counts?.Total PageSize Shown=_documents.Length PageChanged PageSizeChanged />`. Kopfkommentar: warum native `h1` (Stufe headline-lg). | UI | 224 → ~90 |
| `Pages/Documents.razor.cs` | neu | `public sealed partial class Documents : IDisposable` mit `[Inject]` `L`, `Api`, `Logger`. Felder: `_documents`, `DocumentCountsDto? _counts`, `DocumentFilterDto? _barFilter`, `Guid? _typeId`, `int _page = 1`, `int _pageSize = 50`, `DocumentListView _view`, `_error`, `_loading`, `_cts`, die zwei `LiveRegion`. `static readonly int[] PageSizes = [10, 25, 50, 100]`. `Combined => DocumentFilterQuery.Combine(_barFilter, _typeId)`. Methoden: `OnInitializedAsync` (Liste, dann Zähler, `_loading=false`, `_ = PollAsync()`); `ReloadAsync` (wie heute + Zähler); `ApplyFilterAsync(DocumentFilterDto bar)` (`_barFilter = ActiveCount(bar)==0 ? null : bar`, dann `ReloadForFilterAsync`); `SelectTypeAsync(Guid? typeId)`; `ReloadForFilterAsync` (`_page=1`, `_lastStates=[]`, Liste + Zähler laden, `Logger.FilterApplied(ActiveCount(Combined), _counts?.Total ?? _documents.Length)`, Ansage wie heute mit `_counts?.Total ?? _documents.Length`, `_ = PollAsync()`); `ChangePageAsync(int page)` (Liste laden, `Logger.ListPageChanged`, Ansage `RangeText()`); `ChangePageSizeAsync(int size)` (nur Werte aus `PageSizes`, sonst ignorieren; `_page=1`; wie `ChangePageAsync`); `SetView(DocumentListView)` (P4); `LoadAsync(bool announceChanges)` wie heute, aber `Api.ListAsync(_page, _pageSize, Combined, ct)`; ist die Liste leer und `_page > 1` (Dokumente verschwunden) → `_page = 1` und einmal neu laden; `LoadCountsAsync` (Erfolg → `_counts`, `Logger.DocumentCountsLoaded(total, byType.Count)`; Fehler → `_counts = null`, **keine** Fehlermeldung: Seite läuft im Modus ohne Gesamtzahl); `RangeText()` = mit `_counts` `L["DocListRange", from, to, total]`, sonst `L["DocListPage", _page]`. | UI | 190 |
| `Pages/Documents.Polling.cs` | neu | `partial class Documents`: `InProgress`, `PollAsync`, `AnnounceChangesAsync`, `Dispose` – unverändert aus der heutigen Datei (Muster `Pages/Camera.Done.cs`), nur `StateText` → `L[StatusKeys.For(DocumentStatus.KindOf(state))]` | `DocumentListTests` | 80 |
| `Documents/DocumentTable.razor` | neu | Parameter `[Parameter, EditorRequired] IReadOnlyList<DocumentSummaryDto> Documents`. `<div class="doc-list-scroll" role="region" aria-labelledby="doc-list-caption" tabindex="0">` (axe `scrollable-region-focusable`) → `<table class="doc-table doc-list-table">` → `<caption id="doc-list-caption">@L["DocumentsCaption"]</caption>`; Köpfe `scope="col"`: `ColTitle`, `ColTypeTags`, `ClsFieldCorrespondent`, `ColDate`, `ClsFieldAmount` (Klasse `doc-col-amount`), `ColStatus`, `ColRecognition`. Je Zeile `<tr id="@DocumentLinks.RowId(d.Id)" @key>`: (1) `<th scope="row"><span class="doc-title-cell"><span class="doc-icon" aria-hidden="true"><MudIcon Icon="@Icons.Material.Outlined.Description" /></span><span><a class="doc-title-link" href=Viewer>Titel</a> @if privat <Badge Text=L["PrivateBadge"] Icon=Lock/> @if Excerpt <span class="doc-excerpt">@Excerpt</span></span></span></th>` (Ausschnitt als Text, nie HTML); (2) Art `<span class="doc-type">` oder `ClsNoValue`; bei Tags `<span class="doc-tags">` mit je `<span aria-hidden="true">#</span>Name` durch Leerzeichen getrennt; (3) Absender oder `ClsNoValue`; (4) `<span class="doc-date">` Dokumentdatum (`FieldFormat.Date`) oder `ClsNoValue`, darunter `<span class="doc-sub"><MudIcon Icon=DocumentSourceText.Icon(..) aria-hidden="true" />` + `DocListReceivedVia` (`<time datetime="o">` mit `ReceivedAt.ToLocalTime()` `"g"`, Weg-Text aus `DocumentSourceText.Key`; unbekannter Weg → `DocListReceived` ohne Weg); (5) `<td class="doc-amount">` `FieldFormat.Amount` oder `ClsNoValue`; (6) `<StatusText Kind="DocumentStatus.KindOf(d.State)" />`; (7) `Automatic` → `<Badge Text=L["ClsRecognitionBadge"] Icon=AutoAwesome />`, `Confirmed` → `<Badge Text=L["DocListConfirmed"] Icon=Verified />`, `None` → `ClsNoValue`. `Tags == null` wie leer. | UI | 120 |
| `Documents/DocumentTypeChips.razor` | neu | Parameter `DocumentCountsDto Counts`, `Guid? SelectedTypeId`, `EventCallback<Guid?> OnSelect`. Nur wenn `Counts.ByType.Count > 0`: `<div class="type-chips" role="group" aria-label="@L["DocListTypeGroup"]">`, erster Knopf „Alle“ (`FilterAll`) mit `Counts.AllTypes`, dann je Art; jeder `<button type="button" class="type-chip" aria-pressed="true/false" @onclick>` mit `<span>Name</span><span class="type-chip-count">n</span>`. Klick auf den schon gedrückten Chip tut nichts. Ist die gewählte Art nicht mehr in `ByType` (z. B. andere Filter), bleibt sie gewählt und bekommt einen Chip mit Anzahl 0 (sonst wäre der aktive Filter unsichtbar). | `DocumentTypeChipsTests` | 60 |
| `Documents/DocumentPager.razor` | neu | Parameter `int Page`, `int PageSize`, `int? Total`, `int Shown`, `IReadOnlyList<int> PageSizes`, `EventCallback<int> PageChanged`, `EventCallback<int> PageSizeChanged`. Nicht angezeigt, wenn `Shown == 0 && Page == 1`. Links: `<label for="doc-page-size">` `DocListPageSize` + `<select id="doc-page-size" @onchange>` mit `PageSizes`, dann `<p class="doc-range">` mit `RangeText` (siehe `Documents.razor.cs`; hier selbst gebildet aus `PageWindow.Range`). Rechts `<nav aria-label="@L["DocListPager"]"><ul class="doc-pager-pages">`: Knopf „Vorherige“ (`aria-label` `DocListPrevious`, Symbol `ChevronLeft` `aria-hidden`, `disabled` auf Seite 1); mit `Total`: je Eintrag aus `PageWindow.Pages` Knopf mit Zahl, `aria-label` `DocListPage`, aktueller mit `aria-current="page"` und `@ref="_current"`, Lücke als `<span aria-hidden="true">…</span>`; „Nächste“ (`DocListNext`, `ChevronRight`, `disabled` auf letzter Seite bzw. ohne `Total` bei `Shown < PageSize`). **Fokus:** merkt sich beim Klick, ob „Vorherige“/„Nächste“ gedrückt wurde; ist dieser Knopf nach dem Rendern `disabled`, `OnAfterRenderAsync` → `_current.FocusAsync()` (mit `Total`) bzw. Fokus auf den anderen Pfeilknopf (ohne `Total`), damit der Fokus nicht auf `body` fällt. | `DocumentPagerTests` | 120 |
| `Classification/DocumentFilterBar.razor` | ändern | Auswahlfeld „Dokumentart“ mit `_type`, `_types` und `EntryKind.DocumentTypes` aus `LoadChoicesAsync` entfernen (F10-A); `ApplyAsync` gibt `DocumentTypeId: null` weiter; `ResetAsync` ohne `_type`. Im Knopf `#filter-toggle` vor dem Text `<MudIcon Icon="@Icons.Material.Outlined.Tune" aria-hidden="true" />`. Kopfkommentar: „Art wählt man über die Chips der Seite; der Knopf zählt nur die Filter hier“. Sonst nichts (Bedienlogik bleibt). | `DocumentFilterTests` | 219 → ~205 |
| `Documents/DocumentApi.cs` | ändern (Hotspot) | `public Task<ApiResult<DocumentCountsDto>> CountAsync(DocumentFilterDto? filter, CancellationToken ct = default)`: `var query = DocumentFilterQuery.Build(filter);` URI `query.Length == 0 ? "api/documents/counts" : "api/documents/counts?" + query[1..]`, Vorgang `"counts"` über das vorhandene `GetAsync<T>` | UI | +8 |
| `Documents/DocumentsLog.cs` | ändern (Hotspot) | `4110` Debug `DocumentCountsLoaded(int total, int typeCount)` „Document counts loaded: {Total} matching, {TypeCount} types“; `4111` Debug `ListPageChanged(int page, int pageSize)` „Document list page {Page} with {PageSize} rows“; `4112` Information `ListViewChanged(string view)` „Document list view switched to {View}“ (`table`/`tiles`) | – | +12 |
| `wwwroot/css/documents.css` | neu | Kopfkommentar wie `design.css` („Stitch Screen 02; keine outline-Regeln“). Regeln aus „Maße“: `.doc-page-head`, `.doc-page-title`, `.doc-count`, `.view-toggle` (+ `button`, `:hover`, `[aria-pressed]`, Symbol), `.doc-filter-card` (+ `#filter-toggle`, `.filter-heading`), `.type-chips`, `.type-chip` (+ Zustände, `.type-chip-count`) | – | 130 |
| `wwwroot/css/document-list.css` | neu | Regeln aus „Maße“: `.doc-list-frame`, `.doc-list-scroll`, `.doc-list-table` (caption, Zellen, `tbody th`, Hover), `.doc-icon`, `.doc-title-cell`, `.doc-title-link`, `.doc-excerpt`, `.doc-type`, `.doc-tags`, `.doc-date`, `.doc-sub`, `.doc-amount`, `th.doc-col-amount`, `.doc-list-table .nmp-status`, `.doc-pager` (+ `label`, `select`, `.doc-range { margin:0 }`, `strong`), `.doc-pager-pages` (+ Knopfzustände, Lücke). Kacheln kommen in P4 dazu. | – | 170 |
| `wwwroot/index.html` | ändern (Hotspot) | zwei Zeilen nach `css/classification.css`: `css/documents.css`, `css/document-list.css` | – | 2 |
| `Localization/Strings.resx`, `Strings.en.resx` | ändern (Hotspot) | neu (DE / EN): `DocListCount` „{0} Dokumente“ / „{0} documents“; `DocListCountOne` „1 Dokument“ / „1 document“; `DocListTypeGroup` „Nach Dokumentart filtern“ / „Filter by document type“; `ColTypeTags` „Art und Tags“ / „Type and tags“; `ColDate` „Datum“ / „Date“; `ColRecognition` „Erkennung“ / „Recognition“; `DocListReceived` „Eingang {0}“ / „Received {0}“; `DocListReceivedVia` „Eingang {0} über {1}“ / „Received {0} via {1}“; `DocListConfirmed` „Bestätigt“ / „Confirmed“; `DocListPager` „Seiten der Dokumentliste“ / „Pages of the document list“; `DocListPageSize` „Zeilen pro Seite“ / „Rows per page“; `DocListPrevious` „Vorherige Seite“ / „Previous page“; `DocListNext` „Nächste Seite“ / „Next page“; `DocListPage` „Seite {0}“ / „Page {0}“; `DocListRange` „Zeige {0}–{1} von {2} Dokumenten“ / „Showing {0}–{1} of {2} documents“. Vorhandene Schlüssel `State*`, `ColReceived`, `ClsFieldType` **nicht** löschen. | – | 2 × 45 |
| `tests/NoMorePaper.UI.Tests/Documents/ApiMocks.cs` | ändern (Hotspot) | `CountsAsync(IPage page, object body, int status = 200)` mit `[GeneratedRegex(@"/api/documents/counts(\?.*)?$")]`; `NoCountsAsync(IPage page)` = `CountsAsync(page, new { }, 404)`. **`ListAsync` ruft zuerst `NoCountsAsync` auf.** Grund: Der UI-Testserver hat eine echte DB; ein nicht gemockter Zähler liefert sonst echte Zahlen zu einer gemockten Liste (falsche Ansage „0 Dokumente gefunden“). Tests, die Zahlen brauchen, rufen danach `CountsAsync` (spätere Route gewinnt in Playwright). | – | +10 |
| `tests/NoMorePaper.UI.Tests/Documents/SampleDocuments.cs` | ändern (Hotspot) | `Rich()` (2 Dokumente mit `Source = "mail"`, `Excerpt`, `Tags = [ClassificationSamples.Strom]`, Art, Absender, Datum, Betrag, `Automatic`/`Confirmed`, eines privat); `Many(int count)` (Titel „Dokument 1…n“, `Done`); `Counts(int total, params (EntryDto Type, int Count)[] byType)` mit `AllTypes = total` | – | +45 |
| `tests/NoMorePaper.UI.Tests/Documents/DocumentListTests.cs` | ändern | Spaltenköpfe → `["Titel", "Art und Tags", "Absender", "Datum", "Betrag", "Status", "Erkennung"]`; Zustände DE „Wartet“, „Wird verarbeitet“, „Fertig“, „Fehlgeschlagen“; EN „Waiting“, „Failed“; Ansagen „Brief: Status Fertig“ bleiben; die drei Tests mit eigenem `page.RouteAsync` rufen im Mock zuerst `ApiMocks.NoCountsAsync(page)` | selbst | ~132 |
| `tests/NoMorePaper.UI.Tests/Classification/DocumentFilterTests.cs`, `ClassificationAccessibilityTests.cs` | ändern (je eine Zeile) | im Mock mit eigenem Listen-Route zuerst `ApiMocks.NoCountsAsync(page)`; Erwartungen bleiben (Ansage aus der Listenlänge) | selbst | +2 |
| `tests/NoMorePaper.UI.Tests/Documents/DocumentRowTests.cs` | neu | mit `Rich()` und Zähler-Mock: Zeile enthält Ausschnitt, Art, „#“ mit `aria-hidden="true"` und Tag-Text, Datum `01.11.2026`, „über Mail“, „84,20 EUR“, „Automatisch erkannt“ bzw. „Bestätigt“, `.nmp-badge` „Privat“; `role=region` mit Namen = Caption und `tabindex="0"`; bei 390 × 844 `document.documentElement.scrollWidth <= window.innerWidth` (Tabelle scrollt nur im Bereich); Titel-Link `href="dokumente/{id}"` | selbst | 120 |
| `tests/NoMorePaper.UI.Tests/Documents/DocumentTypeChipsTests.cs` | neu | Zähler-Mock abhängig von der URL (mit `documentTypeId` → `Total` 2). (1) Gruppe „Nach Dokumentart filtern“, Knöpfe „Alle 3“ (`aria-pressed=true`), „Rechnung 2“, „Vertrag 1“; neben H1 „3 Dokumente“. (2) Klick „Rechnung“ → `aria-pressed=true`, letzte Listen-URL enthält `documentTypeId=` und `page=1`, `.documents-count` „2 Dokumente gefunden.“, Knopf heißt weiter „Filter“. (3) „Alle“ → keine `documentTypeId`. (4) Zähler 500 → keine Chips, kein `.doc-count`, Liste sichtbar. (5) im geöffneten Filter gibt es kein Feld „Dokumentart“ | selbst | 150 |
| `tests/NoMorePaper.UI.Tests/Documents/DocumentPagerTests.cs` | neu | Liste antwortet mit `Many(pageSize)` bzw. Rest, Zähler `Total` 120. (1) „Zeige 1–50 von 120 Dokumenten“, Navigation „Seiten der Dokumentliste“, „Seite 1“ mit `aria-current="page"`, „Vorherige Seite“ `disabled`. (2) „Nächste Seite“ → URL `page=2`, `.documents-count` „Zeige 51–100 von 120 Dokumenten“. (3) zweimal „Nächste Seite“ → `disabled`, Fokus auf „Seite 3“ (`ToBeFocusedAsync`). (4) „Zeilen pro Seite“ 10 → URL `pageSize=10` und `page=1`, „Zeige 1–10 von 120 Dokumenten“. (5) Zähler 500: Text „Seite 1“, „Nächste Seite“ aktiv bei 50 Einträgen, `disabled` bei 20 | selbst | 170 |
| `tests/NoMorePaper.UI.Tests/Documents/DocumentPagesAccessibilityTests.cs` | ändern | neu `[Theory] DE/EN RichDocumentList_WithCountsChipsAndPager_HasNoAxeViolations` (`Rich()` + `Counts`) | selbst | 110 → ~135 |
| `tests/NoMorePaper.UI.Tests/Accounts/PrivacyTests.cs` | ändern (eine Methode) | `List_MarksPrivateDocuments_AsTextWithSymbol`: Selektor `.private-badge` → `.nmp-badge` mit `HasText = "Privat"`, Symbol `svg` weiter 1 | selbst | ±4 |

- **Reihenfolge:** Texte → `DocumentApi`/`DocumentsLog` → `DocumentTable` → `Documents.razor(.cs, .Polling)` →
  CSS + `index.html` → `DocumentFilterBar` → `DocumentTypeChips` → `DocumentPager` → Tests.
- **Telemetrie:** 4110–4112 (oben) im Katalog (P5); Zähler-Fehler über 4103/4104 mit Vorgang `counts`;
  Chip-Wahl und Filter über das vorhandene `FilterApplied` (4512).
- **Akzeptanz:**
  - `dotnet test --project tests/NoMorePaper.UI.Tests/NoMorePaper.UI.Tests.csproj -- --filter-namespace NoMorePaper.UI.Tests.Documents` grün
  - ebenso `--filter-namespace NoMorePaper.UI.Tests.Classification` (`DocumentFilterTests`, `ClassificationAccessibilityTests`) und `--filter-class NoMorePaper.UI.Tests.Accounts.PrivacyTests`
  - Zeilenprüfung: keine Datei > 300, `Documents.razor.cs` < 250

### Paket P4 – Kacheln · Modell: **Sonnet** · Effort: `medium` (nur bei F7-A)

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Documents/DocumentTiles.razor` | neu | Parameter `IReadOnlyList<DocumentSummaryDto> Documents`. `<ul class="doc-tiles">`, je `<li class="doc-tile" id="@DocumentLinks.RowId(d.Id)" @key>`: oben `<div>` mit bei `Done` `<Thumbnail Source="@DocumentLinks.Archive(d.Id)" Title="@d.Title" />`, sonst `.doc-icon` wie in der Tabelle, rechts `Badge` „Privat“; `<h2 class="doc-tile-title"><a class="doc-title-link" href>Titel</a></h2>`; `<p class="doc-tile-meta">Art <span aria-hidden="true">•</span> Datum</p>` (Leerzeichen **außerhalb** des `span`, fehlende Werte weglassen); Fuß `.doc-tile-foot`: `<div><span class="doc-tile-amount-label">Betrag</span> <span class="doc-tile-amount">…</span></div>` (nur bei Betrag), `StatusText`, Erkennungs-`Badge` wie Tabelle. Kein Ausschnitt, keine Tags (wie Entwurfskarte). | `DocumentTilesTests` | 90 |
| `Pages/Documents.razor` | ändern | Ansicht-Gruppe in `.doc-page-head`: `<div class="view-toggle" role="group" aria-label="@L["DocListViewGroup"]">` mit zwei `<button type="button" aria-pressed>` (Symbol `ViewList` / `GridView` `aria-hidden` + Text `DocListViewTable` / `DocListViewTiles`); Liste je nach `_view` `DocumentTable` oder `DocumentTiles` (Pager bei beiden) | – | +15 |
| `Pages/Documents.razor.cs` | ändern | `SetView(DocumentListView view)`: gleiche Ansicht → nichts; sonst setzen und `Logger.ListViewChanged(view == Table ? "table" : "tiles")` | – | +8 |
| `wwwroot/css/document-list.css` | ändern | Kachel-Regeln aus „Maße“: `.doc-tiles`, `.doc-tile` (+ `:hover`), `.doc-tile-title` (+ Link 1 rem), `.doc-tile-meta`, `.doc-tile-foot`, `.doc-tile-amount-label`, `.doc-tile-amount`; die Miniatur behält ihr Maß aus `viewer.css` (80 px, scharf) | – | +45 |
| `Strings*.resx` | ändern (Hotspot) | `DocListViewGroup` „Ansicht“ / „View“; `DocListViewTable` „Tabelle“ / „Table“; `DocListViewTiles` „Kacheln“ / „Tiles“ | – | 2 × 9 |
| `tests/NoMorePaper.UI.Tests/Documents/DocumentTilesTests.cs` | neu | (1) Gruppe „Ansicht“, „Tabelle“ `aria-pressed=true`. (2) „Kacheln“ → gedrückt, keine Tabelle, `main ul.doc-tiles > li` = Anzahl, je `h2` mit Link zum Viewer, Text „Wartet“/„Fertig“, „Privat“. (3) zurück auf „Tabelle“ → Tabelle wieder da. (4) Blättern wirkt auch in Kacheln (URL `page=2`) | selbst | 110 |
| `tests/NoMorePaper.UI.Tests/Viewer/ThumbnailTests.cs` | ändern (eine Methode, F8-A) | `DocumentList_ShowsThumbnailOfDoneDocument_AndLinksTitleToViewer`: vor den Prüfungen Knopf „Kacheln“ drücken | selbst | +2 |
| `tests/NoMorePaper.UI.Tests/Documents/DocumentPagesAccessibilityTests.cs` | ändern | neu `[Theory] DE/EN DocumentTiles_HaveNoAxeViolations` | selbst | +20 |

- **Telemetrie:** 4112 `ListViewChanged`.
- **Akzeptanz:** `--filter-namespace NoMorePaper.UI.Tests.Documents` und `--filter-class NoMorePaper.UI.Tests.Viewer.ThumbnailTests` grün.

### Paket P5 – Doku, Katalog, Häkchen · Modell: **Sonnet** (im selben Subagenten wie P2–P4) · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `docs/kb/telemetrie.md` | ändern (Hotspot) | Zeilen 4110, 4111, 4112 nach 4109; bei 4103/4104 Vorgang `counts` nennen | – | +3 |
| `docs/kb/oberflaeche.md` | ändern (nur ergänzen) | Abschnitt „Dokumentliste“: Spalten, Chips (`aria-pressed`, Gruppe), Blättern (`nav`, `aria-current`, Fokusregel), Tabelle im scrollbaren Bereich, Kacheln `auto-fill`; was aus Screen 02 nicht übernommen wurde | – | +15 |
| `docs/ui/stitch/README.md` | ändern | Status Screen 02 → „umgesetzt“ | – | 1 |
| `docs/plan/m4u-oberflaeche.md` | ändern | nur die drei Checkboxen unter „B. Dokumente“ abhaken (Kacheln nur bei F7-A) | – | 3 |

### Paket P6 – Review und Abnahme · Modell: **Opus** · Effort: `high` (steuernde Sitzung)

- Review-Schleife nach AGENTS.md §8 über die ganze Spur (Diff gegen `feat/m04u-oberflaeche`), danach
  `dotnet build NoMorePaper.slnx`, `dotnet test --solution NoMorePaper.slnx`,
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-file-length.ps1`.
- Prüfliste: Sichtbarkeitsbedingung in `EfDocumentCounts` wörtlich wie in `ListAsync`; Tag-Abfrage nur mit
  Ids der Seite; Ausschnitt nie als HTML; keine Tailwind-Klassen/`material-symbols` übernommen; jedes Symbol
  `aria-hidden`; genau ein `h1`; keine `outline`-Regel; nur `--nmp-*`-Werte (Ausnahme `rgb(0 0 0 / .2)` im
  aktiven Chip, aus dem Entwurf); Bedienlogik der Seite (Ansagen, Nachfragen) unverändert; Dateien aus „Nicht
  ändern“ unberührt.
- Sichtvergleich: Bilder von `/dokumente` bei 1465 × 1600 (Maß von `screen.png`) und 390 × 844 nach
  `TestResults/m4u-b/` neben `screen.png`; Sichtprüfung durch den Nutzer, NVDA-Prüfung (Tabelle senkrecht
  lesen: Länge des Zeilenkopfs mit Ausschnitt) durch den Nutzer.
- Steuernde Sitzung trägt in `m4b-scan-bearbeitung-detail.md` (P0, Zeile „Rohe Abfragen“) `EfDocumentCounts`
  und die Tag-Abfrage in `EfDocumentRepository.ListAsync` als Stellen für den Papierkorb-Filter ein (F12-A).
- Push auf `feat/m04u-b-dokumente`; Merge in den Integrationsbranch nach A und D.

### Eskalation (alle Pakete)

- Zwei rote Läufe am selben Paket (Build oder Tests) → stoppen und melden.
- `SqlQueryRaw` kann eine Zeilenklasse oder `int` nicht lesen (Spaltenalias, Typ) → stoppen und melden, keine
  andere Abfrageform erfinden.
- axe meldet `color-contrast`, `target-size` oder einen anderen Verstoß, den der Plan nicht abdeckt → stoppen
  und melden, keine eigene Farbe oder Größe wählen.
- Ein bestehender Test außerhalb der hier genannten wird rot: nur beheben, wenn es ein Selektor oder eine
  Wartebedingung ist; prüft er Verhalten, das der Plan nicht ändert → stoppen und melden.
- Eine Datei aus „Nicht ändern“ müsste geändert werden, oder eine Datei überschreitet 250 Zeilen ohne
  geplante Aufteilung → stoppen und melden.
- Eine offene Frage (F0–F12) ist anders beantwortet als die Empfehlung und die Spalte „Bei der anderen
  Antwort“ reicht nicht → stoppen und melden.

### Risiken

- **Datenschutz in den Zählern:** Eine falsche Bedingung würde verraten, wie viele private Dokumente andere
  Konten haben. Darum Bedingung wörtlich vorgegeben und Pflichttest (P1, `DocumentCountsTests` Fall 1).
- **Konflikt mit M4b Schritt 0** (läuft gleichzeitig): `EfDocumentRepository`, `DocumentMapping`,
  `DocumentEndpoints`, `DocumentSummaryDto` werden von beiden ergänzt. Beim späteren Merge nach `develop` muss
  der Papierkorb-Filter auch in `EfDocumentCounts` und in die Tag-Abfrage – sonst zählen gelöschte Dokumente.
- **Spur C** ergänzt vermutlich `DocumentApi.cs` (Erkannter Text) – reine Ergänzung, kleiner Konflikt.
- **Spur A** stellt Feldstile um; die Filterfelder ändern ihr Aussehen erst nach dem Merge von A. Darum B nach A.
- **Sichtbare Wortänderung:** „In Warteschlange“ wird „Wartet“ (auch in der Ansage), damit Liste, Viewer und
  Bausteine dieselben Wörter nutzen.
- **NVDA und Zeilenkopf:** Titel, „Privat“ und Ausschnitt stehen im `th`; beim senkrechten Lesen einer Spalte
  wird das jedes Mal mitgelesen. Begrenzt auf 160 Zeichen; Nutzerprüfung entscheidet, ob der Ausschnitt aus
  dem Zeilenkopf heraus muss.
- **UI-Tests mit echter DB:** Der Testserver hat Postgres; jeder Test, der die Liste mockt, muss auch den
  Zähler mocken (`ApiMocks.NoCountsAsync`/`CountsAsync`), sonst mischen sich echte Zahlen mit gemockten Zeilen.
- **Zahl und Liste aus zwei Abfragen:** Kommt zwischen beiden ein Dokument dazu, passen „von N“ und die Liste
  kurz nicht zusammen; beim nächsten Laden stimmt es wieder.
- **Kleine Schrift:** Spaltenköpfe, Tags, Chips und Blättern mit 10–11 px Mono sind die kleinsten Texte der
  Seite (vom Nutzer freigegeben, ADR-0026); Sichtprüfung mit Lupe.

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|
| P1 | 2 | | |
| P2 | 2 | | |
| P3 | 2 | | |
| P4 | 2 | | |
| P5 | 1 (im Sonnet-Lauf) | | |
| P6 | 3 | | |
