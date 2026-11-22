# M4u Spur C – Dokument ansehen: Detailplan

Aufgabe C aus [m4u-oberflaeche.md](../m4u-oberflaeche.md): `/dokumente/{id}` nach Stitch-Screen 03 – Zurück-Leiste
mit Pfad, Kopf mit Titel und Aktionen, zwei Spalten (Viewer 7/12, Reiter 5/12), Werkzeugleiste mit zugänglichen
Namen, Reiter „Angaben“, „Erkannter Text“, „Verlauf“. Regeln: [AGENTS.md §12](../../../AGENTS.md),
[Vorlage](../detailplan-vorlage.md).

Grundlagen: [notizen.md 03](../../ui/stitch/03-dokument-ansehen/notizen.md) (nur „Übernehmen“),
[code.html](../../ui/stitch/03-dokument-ansehen/code.html) (nur abgelesen, nie kopiert),
[KB oberflaeche.md](../../kb/oberflaeche.md), [KB ui-prozess.md](../../kb/ui-prozess.md),
[ADR-0026](../../adr/0026-optik-nach-stitch-design-system.md), [ADR-0011](../../adr/0011-pdf-darstellung-intelligent-invertiert.md),
[KB darstellung.md](../../kb/darstellung.md), [M4b-Detailplan](m4b-scan-bearbeitung-detail.md).

## Was die API heute liefert (geprüft 2026-11-22)

| Reiter/Teil | Heute | Folge |
|---|---|---|
| Downloads Original/Archiv getrennt | **ja:** `GET /api/documents/{id}/files/original` und `…/files/archive` (`DocumentLinks.Original/Archive`); `DocumentDetailDto.Archive` ist `null`, bis die Verarbeitung fertig ist | nur Oberfläche |
| „Erkannter Text“ | **nein:** der OCR-Text steht in `documents.content_text` (`Document.ContentText`), wird aber nur für Suche und Suchtreffer-Ausschnitt benutzt; kein Endpunkt, kein Feld im DTO | Frage F3 |
| „Verlauf“ | **nur teilweise:** `ReceivedAt` (Zeit), `Source` (aus `/fields`), `State` und `Recognition` (ohne Zeit). Zeitpunkte für „erkannt“, „bestätigt“, „bearbeitet“ und wer es war, speichert die App **nicht** | Frage F4 |
| Löschen (Kopf im Entwurf) | nur „mögliches Duplikat löschen“; allgemeines Löschen kommt mit M4b Spur C (Papierkorb, `DeleteDocumentButton`) | kommt nicht in Spur C, Platz in der Aktionsreihe |

## Offene Fragen an den Nutzer

> **Beantwortet 2026-11-22:** Der Nutzer hat alle Fragen wie empfohlen entschieden. Der Plan gilt so.

Diese Fragen müssen vor der Umsetzung beantwortet sein. **Der Plan unten ist für die jeweils empfohlene Option
geschrieben**; die letzte Spalte sagt, was sich bei der anderen Antwort im Plan ändert.

| Nr. | Frage | Optionen | Empfehlung | Bei der anderen Antwort |
|---|---|---|---|---|
| F1 | Screen 03 steht in [docs/ui/stitch/README.md](../../ui/stitch/README.md) auf „geprüft (`screen.png` fehlt)“. Nach dem UI-Prozess wird nur umgesetzt, was du freigegeben hast. Gibst du den Entwurf mit den Abweichungen aus `notizen.md` frei, und soll das fehlende Bild vorher abgeholt werden? | **A:** Ja, freigegeben; die steuernde Sitzung holt `screen.png` vor dem Start per Stitch-MCP (`get_screen` für Screen `a6e38e56a8a543ce9d57fea24bbea7eb`) als Vergleichsbild. **B:** Nein, erst in Stitch nachbessern. | A | B: Spur C wartet, bis der neue Entwurf abgelegt und freigegeben ist. |
| F2 | In welcher Reihenfolge sollen Screenreader und Tab-Taste die Seite durchgehen? Im Entwurf steht der Viewer links und die Reiter rechts. Steht der Viewer auch im Seitenaufbau zuerst, liest NVDA im Lesemodus den **ganzen Text des Dokuments**, bevor es zu Zusammenfassung und Angaben kommt. Heute stehen die Angaben vor dem Viewer. | **A:** Seitenaufbau wie der Entwurf: Viewer zuerst, dann Reiter. Zu den Angaben springst du mit der Überschriften-Taste (unsichtbare Überschrift „Angaben“). **B:** Optik wie der Entwurf (Viewer links, Reiter rechts), aber im Seitenaufbau kommen die Reiter zuerst; die Tab-Taste springt nach dem Kopf in die rechte Spalte und danach in die linke. Auf schmalen Bildschirmen stehen die Reiter dann über dem Viewer. | B | A: In `DocumentViewer.razor` stehen `doc-view-viewer` und `doc-view-side` in umgekehrter Reihenfolge; in `document-view.css` entfallen `grid-column`/`grid-row`; in `ViewerLayoutTests` dreht sich der Fall „Reihenfolge“ um; `ViewerKeyboardTests` braucht die Schleifenänderung nicht. |
| F3 | Der Reiter „Erkannter Text“ braucht den OCR-Text, den die API heute nicht liefert. Wie soll er kommen? | **A:** Spur C baut einen kleinen neuen Endpunkt `GET /api/documents/{id}/text` (nur neue Dateien, Sichtbarkeit wie beim Dokument selbst; berührt keine Datei von M4b Schritt 0). **B:** Spur C lässt den Reiter weg; er kommt später als eigene Aufgabe. **C:** Text im Browser aus der Textebene der PDF lesen – kein Server nötig, aber Änderung am Viewer-Code in `wwwroot/js/viewer/` und nur für Seiten mit Textebene; widerspricht „Viewer unverändert“. | A | B: P1 und `RecognizedTextPanel` entfallen, `TabList` hat zwei Reiter, Schlüssel `Text*`, Logs 4213–4215 und `DocumentApi.GetTextAsync` entfallen; Tests „Erkannter Text“ entfallen. C: nicht geplant – braucht einen neuen Plan (Stufe 3). |
| F4 | Der Reiter „Verlauf“ soll eine Zeitleiste zeigen (Eingang, erkannt, bestätigt, bearbeitet). Die App speichert heute nur den Zeitpunkt des Eingangs. Was soll Spur C bauen? | **A:** Verlauf aus dem, was es gibt: „Eingegangen“ mit Zeit und Eingangsweg, dazu der Stand der Verarbeitung („Text erkannt“, „Wird verarbeitet“ …) und der Angaben („automatisch erkannt“ oder „geprüft und bestätigt“) **ohne** Zeit, darunter der Satz „Zeitpunkte gibt es bisher nur für den Eingang.“ Echte Zeitpunkte kommen später als eigene Aufgabe nach M4b Schritt 0. **B:** Spur C speichert ab jetzt Zeitpunkte und Personen (neue Spalten oder Tabelle, Migration, Änderungen an Verarbeitung, Erkennung, Bestätigen, Bearbeiten). Kollidiert mit M4b Schritt 0 (gleiche Tabelle, Migration, `JobQueue`), ist Stufe 3 und alte Dokumente haben trotzdem keine Zeiten. **C:** Reiter „Verlauf“ weglassen, bis M4b „Fassungen“ bringt. | A | B: nicht in diesem Plan – eigener Plan nach dem Merge von M4b Schritt 0 (Opus). C: `DocumentHistory`, `HistoryTimeline*` und ihre Tests entfallen, `TabList` hat zwei Reiter. |
| F5 | Die Reiter sehen in den Entwürfen verschieden aus: in 03 (Dokument ansehen) als dunkle Leiste mit hell hinterlegtem gewählten Reiter, in 04 (Eingänge) als Wörter mit weißer Linie unter dem gewählten. Spur D fragt dich (dort F4), ob das Aussehen aus 04 **überall** gelten soll. Wie sollen die Reiter im Viewer aussehen? | **A:** Wie Entwurf 03 – eigene Regeln nur für den Viewer. **B:** Wie alle anderen Reiter der App (also so, wie du bei Spur D F4 entscheidest) – gleiche Bedienelemente sehen überall gleich aus. | B | A: In `document-view.css` kommen die Regeln aus „Maße“, Zeile „Reiterleiste (nur F5-A)“, für `.doc-view-side .nmp-tabs`, `.nmp-tab`, `.nmp-tab:hover`, `.nmp-tab-selected` dazu (~30 Zeilen). |

Durch Vorgaben oder Code schon entschieden (keine Frage; Widerspruch genügt):

- **Optik** nur über Tokens aus `base.css`; Farben des Entwurfs ohne Token werden auf den nächsten Token gelegt
  (Tabelle „Maße“). Kein eigener Fokusrahmen, keine `outline`-Regeln (ADR-0026). Viewer-Shader, `wwwroot/js/viewer/*`
  und die Inversion bleiben **unverändert** (ADR-0011); geändert wird nur der Rahmen um die Bühne.
- **H1** in der Stufe des Entwurfs (`headline-sm` 16 px, 600) – keine größere Schrift. **Keine Großschreibung per
  CSS** (`uppercase` im Entwurf): Screenreader buchstabieren großgeschriebene Wörter teils; sonst gleiche Werte.
- **Bearbeiten bleibt ein eigener Schritt** („Bearbeiten“ → Formular → „Speichern“), kein Formular im Lesemodus
  und kein Text „Änderungen übernehmen“: der Editor wird auch auf „Zu prüfen“ benutzt; „Automatisch gespeichert“
  gibt es nicht (Notizen „Abgleich“).
- **Privat** wandert vom Seitenkopf in den Reiter „Angaben“ (Meilensteinplan Aufgabe C), nur für den Besitzer.
- **Downloads** als zwei Knöpfe „Original“ und „Archiv-PDF“ mit Symbol in der Aktionsreihe des Kopfs; zugänglicher
  Name bleibt „Original herunterladen“ / „Archiv-PDF (PDF/A) herunterladen“ (sichtbarer Text ist darin enthalten).
- **Werkzeugleiste:** Symbolknöpfe wie im Entwurf mit `aria-label` (die bisherigen Wörter); die App-Ergänzungen
  „100 %“ (Zoom zurücksetzen) und „Originalfarben“ bleiben als Textknöpfe; „Zoom 150 %“ und „Seite 1 von 2“ bleiben
  als Text. **Vollbild** (Notizen „Übernehmen“) nur, wo der Browser es für Elemente kann – auf dem iPhone fehlt
  der Knopf (Safari kann dort nur Videos im Vollbild).
- **Zwei Spalten erst ab 80 rem (1280 px) Fensterbreite**, nicht ab 1024 px wie im Entwurf: die Seitenleiste
  (18 rem) nimmt Platz, bei 1024 px bliebe dem Viewer kaum 400 px. Darunter eine Spalte.
- **Pfad** mit dem Baustein `Design/Breadcrumb` aus Spur D (Hinweis in deren Plan), Trenner und Aussehen von dort:
  „Dokumente“ › Titel. Die **Art** des Entwurfs („Dokumente / Rechnungen / …“) fehlt vorerst: die Dokumentliste
  kennt keinen Art-Filter in der Adresse, und im Baustein ist ein Eintrag ohne Link immer die aktuelle Seite.
- **Reiter** sind alle drei immer im DOM (inaktive mit `hidden`), damit ein offenes Formular in „Angaben“ beim
  Wechsel erhalten bleibt; „Erkannter Text“ lädt erst beim ersten Öffnen.
- **Löschen** kommt mit M4b Spur C in die Aktionsreihe (siehe „Übergabe an M4b“).

## Detailplan

**Erstellt von:** Opus (Stufe 3) · **Datum:** 2026-11-22 · **Offene Designfragen:** F1–F5 (oben), vor dem Start
zu beantworten

Branch `feat/m04u-c-viewer` von `feat/m04u-oberflaeche`. Pfade unter `src/NoMorePaper.Web.Client/`, sofern nicht
anders angegeben.

**Exklusiv (Spur C):** `Pages/DocumentViewer.razor(.cs)`, `Classification/DocumentFields*.razor(.cs)`, `Viewer/*`,
`wwwroot/css/viewer.css`, neu `wwwroot/css/document-view.css` und `document-side.css`, neu `wwwroot/js/fullscreen.js`,
Tests `tests/NoMorePaper.UI.Tests/Viewer/*`, `Classification/FieldsPanelTests.cs`, neue Server-Dateien aus P1.
**Zusätzlich beansprucht (steuernde Sitzung bitte in m4u-oberflaeche.md „Exklusiv-Dateien“ nachtragen):**
`Classification/TagPicker.razor` und die Regeln `.tag-list*` in `wwwroot/css/classification.css` (Tag-Chips; wirkt auch
in der Filterleiste von Spur B – Spur B ändert beides nicht).
**Hotspots, nur ergänzen:** `Strings*.resx`, `wwwroot/css/design.css` (eine Hilfsklasse), `wwwroot/index.html`
(drei Zeilen), `Documents/DocumentApi.cs` (eine Methode), `src/NoMorePaper.Server/Program.cs`,
`…Application/Composition/ApplicationServiceCollectionExtensions.cs`,
`…Infrastructure/Composition/InfrastructureServiceCollectionExtensions.cs` (je eine Zeile),
`src/NoMorePaper.Server/Documents/DocumentsLog.cs` (ein Eintrag), `tests/NoMorePaper.UI.Tests/Accounts/PrivacyTests.cs`
(nur die drei `ViewerSwitch_*`-Fälle), `docs/kb/telemetrie.md`, `docs/kb/oberflaeche.md`, `docs/kb/verarbeitung.md`.
**Nicht ändern:** `Layout/MainLayout.razor`, `Theme/ContrastTheme.cs` (gesperrt), `css/base.css`, `css/layout.css`,
`css/components.css`, `css/accounts.css` (Feldstil kommt mit Spur A), `Design/*`, `wwwroot/js/viewer/*`,
`Viewer/ViewerInterop.cs`, `Documents/DocumentLinks.cs`, `Pages/Review.razor(.cs)`, `Pages/InputsEntry.razor`,
`Contracts/Documents/DocumentDetailDto.cs`, `Server/Documents/DocumentEndpoints.cs`, `DocumentMapping.cs`,
`Infrastructure/Documents/EfDocumentRepository.cs`, `DocumentConfiguration.cs` (alle von M4b Schritt 0 geändert).

### Einschätzung

| Aufgabenpunkt | Umfang | Risiko | Neuheit | Klarheit | Prüfbarkeit | Stufe |
|---|---|---|---|---|---|---|
| Endpunkt „Erkannter Text“ (F3-A) | 7 neue Dateien, 4 Zeilen in Hotspots, ~350 Zeilen mit Tests | Datenschutz: privater Text darf nie an Fremde – Muster `VisibleTo` vorhanden | Muster `GetDocument`, `DocumentEndpoints` | entschieden | Server-Tests mit Postgres | 2 |
| Angaben: zerlegen (252/235 Zeilen), Aussehen, Privat verschieben, Tag-Chips | ~10 Dateien, ~700 Zeilen (davon ~400 verschoben) | Fokusführung und Ansagen dürfen sich nicht ändern | Muster | entschieden | bestehende FieldsPanel-/Combobox-/Privacy-Tests | 2 |
| Reiterinhalte „Erkannter Text“, „Verlauf“ | 6 Dateien, ~400 Zeilen | gering | Muster `CopyField`, `InputTexts` | nach F3/F4 entschieden | Unit- + UI-Tests | 2 |
| Seitenrahmen, Werkzeugleiste, Reiter, Vollbild, CSS | ~9 Dateien, ~800 Zeilen | Vollbild = JS-Interop (klein, Muster `ViewerInterop`); bestehende Viewer-Tests | teils neu (Fullscreen-API) | entschieden | UI-Tests + Sichtvergleich | 2 (Vollbild am Rand zu 3) |
| UI-Tests neu/angepasst | 6 Dateien, ~600 Zeilen | Playwright-Zeitverhalten | Muster `ViewerPageTests`, `NavigationTests` | entschieden | selbst | 2 |
| Texte, Katalog, KB, Häkchen | 7 Dateien | keins | Muster | entschieden | Build | 1 (im Sonnet-Lauf) |
| Review §8, Abnahme | – | – | – | – | Review | 3 |

**Läufe:** C0 (steuernde Sitzung) → **S1** Sonnet: P1 im eigenen Worktree ∥ **S2** Sonnet: P2 → P3 auf dem
Spurbranch → **S3** Sonnet: P4 → P5 → P6 auf dem Spurbranch → P1 in den Spurbranch mergen → **P7** Opus
(steuernde Sitzung). P2/P3 und P4 sind getrennte Läufe, weil P4 das Ergebnis von P2/P3 einbaut und ein Lauf über
~35 Dateien zu viel Kontext trägt. **S3 startet erst, wenn Spur D in `feat/m04u-oberflaeche` gemergt ist** (P4 nutzt
`Design/Breadcrumb`, die neuen `TabItem`-Parameter und das Reiter-Aussehen aus D); vorher mergt die steuernde
Sitzung `feat/m04u-oberflaeche` in `feat/m04u-c-viewer`. P1–P3 hängen nicht an D.

```
C0 ─┬─► S1: P1 (Server, Worktree) ──────────────────────────────────┐
    └─► S2: P2 → P3 ─► [Merge von A, D in den Integrationsbranch] ─► S3: P4 → P5 → P6 ─► Merge P1 ┴─► P7 ─► Push
```

### Maße aus dem Entwurf → Umsetzung

Links Tailwind-Klasse im Entwurf, rechts der Wert im Code. Token-Zuordnung für Farben ohne eigenes Token:
`surface-container-lowest #0e0e0e` → `--nmp-canvas-lowest`; `surface-container-highest #353534` → Flächen
`--nmp-elevated-high`, Linien/Punkte `--nmp-border-strong`; `outline-variant/30` → `--nmp-border`. `rounded` =
`--nmp-radius-sm`, `rounded-lg` = `--nmp-radius`. Schriftstufen: `label-sm` Mono `0.625rem/0.875rem`, `.05em`, 400;
`label-md` Mono `0.6875rem/1rem`, `.04em`, 500; `body-sm` `0.75rem/1.125rem`; `headline-sm` `1rem/1.5rem`, `-0.005em`.

| Element | Entwurf | Umsetzung |
|---|---|---|
| Zurück-Leiste | `bg-surface-container-lowest px-space-lg py-space-sm`, Flex, `md:` eine Zeile | `.doc-view-back`: `--nmp-canvas-lowest`, `padding: var(--nmp-space-sm) var(--nmp-space-lg)`, Flex mit Umbruch, `justify-content: space-between`, `gap: var(--nmp-space-sm)`, `border-radius: var(--nmp-radius)` (keine negativen Ränder, Band bleibt im Inhaltsbereich) |
| „Zurück zur Liste“ | `h-8 px-space-sm rounded bg-surface-container label-md`, Pfeil 16 px | `.doc-view-back-link`: `inline-flex`, `min-height: 2rem`, `padding: 0 var(--nmp-space-sm)`, `--nmp-radius-sm`, `--nmp-surface-container`, `--nmp-text`, `label-md`, keine Unterstreichung; Hover `--nmp-elevated-high`/`--nmp-primary`; Symbol `1rem` |
| Pfad | `label-md text-outline`, Trenner „/“, letzter Teil `text-primary font-medium truncate` | Baustein `Design/Breadcrumb` (Spur D, `.nmp-breadcrumb` in `design.css`: Mono 11 px, `--nmp-outline`, aktueller Teil `--nmp-primary` 500); hier nur `.doc-view-back .nmp-breadcrumb { min-width: 0; overflow-wrap: anywhere; }` – **kein** Abschneiden |
| Eingang | `label-sm px-space-xs py-0.5 rounded bg-surface-container text-on-surface-variant uppercase` | `.doc-view-received`: `label-sm`, `padding: .125rem var(--nmp-space-xs)`, `--nmp-radius-sm`, `--nmp-surface-container`, `--nmp-text-variant` (ohne `uppercase`) |
| Kopf | `bg-surface-container-low px-space-lg py-space-md`, `xl:` Titel und Aktionen in einer Zeile | `.doc-view-head`: `--nmp-paper-low`, `padding: var(--nmp-space-md) var(--nmp-space-lg)`, `--nmp-radius`, Flex-Spalte `gap: var(--nmp-space-md)`; ab `80rem` Zeile mit `justify-content: space-between` |
| Symbolkachel | `w-7 h-7 rounded bg-surface-container-highest` | `.doc-view-icon`: `1.75rem` Quadrat, `--nmp-radius-sm`, `--nmp-elevated-high`, `--nmp-primary`, Symbol `1.125rem` |
| H1 | `headline-sm text-primary tracking-tight font-semibold` | `.doc-view-title`: `1rem/1.5rem`, `-0.005em`, 600, `--nmp-primary`, `margin: 0`, `overflow-wrap: anywhere` |
| Abzeichen | Chips `label-sm` | Baustein `Badge` unverändert (Status, Privat) |
| Aktionsknopf | `h-9 px-space-sm rounded bg-surface-container text-on-surface label-md`, Hover `surface-container-high`/`primary` | `.doc-view-action` (auf `<a>`): `inline-flex`, `gap: var(--nmp-space-xs)`, `min-height: 2.25rem`, `padding: 0 var(--nmp-space-sm)`, `--nmp-radius-sm`, `--nmp-surface-container`, `--nmp-text`, `label-md`, keine Unterstreichung; Hover `--nmp-elevated-high`/`--nmp-primary`; Symbol `1.125rem` |
| Spalten | `p-space-md lg:p-space-lg grid lg:grid-cols-12 gap-space-lg`, 7/5 | `.doc-view-columns`: Grid, `gap: var(--nmp-space-lg)`, `padding-block: var(--nmp-space-md)`; ab `80rem` `grid-template-columns: minmax(0, 7fr) minmax(0, 5fr)` |
| Werkzeugleiste | `bg-surface-container-low rounded-lg p-space-xs px-space-sm`, Flex `justify-between` | `.viewer-bar`: `--nmp-paper-low`, `--nmp-radius`, `padding: var(--nmp-space-xs) var(--nmp-space-sm)`, Flex mit Umbruch, `align-items: center`, `gap: var(--nmp-space-xs)` |
| Symbolknopf | `w-8 h-8 rounded bg-surface-container text-on-surface`, Hover `surface-container-high`/`primary`, Symbol 18 px | `.viewer-icon-button`: `2rem` Quadrat, `--nmp-radius-sm`, `--nmp-surface-container`, `--nmp-text`, `border: 0`; Hover `--nmp-elevated-high`/`--nmp-primary`; Symbol `1.125rem` |
| Textknopf (100 %, Originalfarben, Vollbild) | `h-8 px-space-sm rounded bg-surface-container label-sm font-medium` | `.viewer-text-button`: wie Symbolknopf, `min-width: auto`, `padding: 0 var(--nmp-space-sm)`, `label-sm` 500, `gap: .25rem`; gedrückt (`aria-pressed="true"`): `--nmp-primary`/`--nmp-on-primary` **und** Haken davor (bestehende Regel `content: "\2713\00a0" / ""` bleibt) |
| Seite / Zoom | `label-sm`, „Seite 1“ `text-primary font-medium`, „von 3“ `text-outline` | `.viewer-page`, `.viewer-zoom`: `label-sm`, `--nmp-primary`, 500, `padding: 0 var(--nmp-space-xs)`, `font-variant-numeric: tabular-nums` (Text bleibt ein String) |
| Trenner | `w-px h-4 bg-surface-container-highest mx-space-xs` | `.viewer-bar-separator`: `1px × 1rem`, `--nmp-border-strong`, `margin: 0 var(--nmp-space-xs)`, `aria-hidden` |
| Bühne | `bg-surface-container-lowest rounded-lg p-space-md min-h-[780px]` | `.viewer-stage`: Höhe bleibt `75vh`/`min-height: 20rem`, Hintergrund bleibt `#000000` (Shader zeichnet auf Schwarz), Rand `1px solid var(--nmp-border)` statt `2px`, `--nmp-radius`, `overflow: hidden` |
| Reiterleiste | F5-B: keine eigenen Regeln, Aussehen aus `design.css` (Spur D). **Nur F5-A:** `bg-surface-container-lowest p-1 rounded-lg gap-1`; Reiter `flex-1 py-1.5 px-space-xs rounded label-md`, aktiv `bg-surface-container-high text-primary font-medium`, sonst `text-on-surface-variant` | nur in `.doc-view-side .nmp-tabs`: `--nmp-canvas-lowest`, `padding: .25rem`, `--nmp-radius`, `gap: .25rem`, `border-bottom: 0`; `.nmp-tab`: `flex: 1 1 0`, `min-height: 2rem`, `padding: .375rem var(--nmp-space-xs)`, `--nmp-radius-sm`, `label-md`, `--nmp-text-variant`, `border-bottom: 0`; Hover `--nmp-surface-container`/`--nmp-primary`; `.nmp-tab-selected`: `--nmp-elevated-high`, `--nmp-primary`, 500 |
| Reiterkarte | `bg-surface-container-low rounded-lg p-space-md gap-space-md` | `.doc-view-card`: `--nmp-paper-low`, `--nmp-radius`, `padding: var(--nmp-space-md)`, Flex-Spalte `gap: var(--nmp-space-md)` |
| Hinweis „automatisch erkannt“ | `bg-surface-container p-space-sm rounded`, Symbol, `body-sm text-primary font-medium` | `.doc-view-side .fields-recognition`: `--nmp-surface-container`, `padding: var(--nmp-space-sm)`, `--nmp-radius-sm`, Flex `gap: var(--nmp-space-xs)`, `body-sm` 500, `--nmp-primary`, `margin: 0`; Symbol `1.125rem`. Gleich für `.fields-duplicate` |
| Felder (Lesen) | Grid 2 Spalten `gap-space-sm`; Beschriftung `label-sm text-outline`; Wert `h-9 px-space-sm bg-surface-container-lowest rounded body-sm text-primary font-medium` (Datum Mono) | `dl.fields-grid`: Grid `repeat(2, minmax(0, 1fr))`, `gap: var(--nmp-space-sm)`, unter `30rem` eine Spalte; `.fields-pair` Flex-Spalte `gap: .25rem`; `dt` `label-sm`, `--nmp-outline`; `dd` `margin: 0`, `min-height: 2.25rem`, Flex `align-items: center`, `padding: 0 var(--nmp-space-sm)`, `--nmp-canvas-lowest`, `--nmp-radius-sm`, `body-sm` 500, `--nmp-primary`; `.fields-mono` `--nmp-font-mono`; `.fields-pair-wide` `grid-column: 1 / -1` |
| Tag-Chip | `px-space-xs py-0.5 rounded bg-surface-container text-primary label-sm`, Kreuz 13 px `text-outline` | `.fields-tags li`, `.tag-list li`: `inline-flex`, `gap: .25rem`, `padding: .125rem var(--nmp-space-xs)`, `--nmp-radius-sm`, `--nmp-surface-container`, `--nmp-primary`, `label-sm`; Entfernen-Knopf `.tag-remove`: `1.5rem` Quadrat (Zielgröße 24 px, WCAG 2.5.8), transparent, `border: 0`, `--nmp-outline`, Hover `--nmp-primary`, Symbol `0.8125rem` |
| Erkannter Text | Kopf `label-sm text-outline uppercase` + „Kopieren“ `label-sm text-primary`; Fläche `h-96 bg-surface-container-lowest p-space-sm rounded font-mono text-label-sm leading-relaxed` | `.doc-label` (h2): `label-sm`, `--nmp-outline`, `margin: 0`; `.doc-text-copy`: `label-sm`, `--nmp-primary`, transparent, `border: 0`, Flex `gap: .25rem`, Hover unterstrichen; `.doc-text`: `height: 24rem`, `overflow-y: auto`, `--nmp-canvas-lowest`, `padding: var(--nmp-space-sm)`, `--nmp-radius-sm`, `--nmp-font-mono`, `0.625rem/1.625`, `--nmp-text`, `white-space: pre-wrap`, `overflow-wrap: anywhere` |
| Zeitleiste | `pl-2`; Eintrag `relative pl-6 pb-space-sm`; Punkt `w-2.5 h-2.5 rounded-full`, erster `bg-primary`, sonst `bg-outline`; Linie `w-px bg-surface-container-highest`; Titel `label-md`; Zeit `label-sm text-outline font-mono`; Text `body-sm text-on-surface-variant` | `ol.doc-history`: `list-style: none`, `padding-left: var(--nmp-space-sm)`, Flex-Spalte `gap: var(--nmp-space-sm)`; `li` `position: relative`, `padding: 0 0 var(--nmp-space-sm) 1.5rem`; `li::before` Punkt `.625rem`, rund, `--nmp-outline`, `left: -5px; top: .25rem`, `content: ""`; `li:first-child::before` `--nmp-primary`; `li::after` Linie `1px`, `--nmp-border-strong`, `left: 0; top: .75rem; bottom: 0`; `.doc-history-row` Flex `justify-content: space-between`; Titel `label-md` 500 `--nmp-text` (erster `--nmp-primary`); `time` `label-sm` Mono `--nmp-outline`; `.doc-history-detail` `body-sm`, `--nmp-text-variant`, `margin: 0` |

Weggelassen (Notizen „Abgleich“ und „Nicht mitnehmen“): GoBD/WORM/S3/SHA-Leiste, QES, Freigeben, DATEV,
ZUGFeRD-Karte, Workflow-Status, interne Notiz, Netto/USt, USt-IdNr., IBAN, Rechnungsnummer, Kostenstelle,
Ordnerpfad, Fälligkeit, Konfidenz, „Tesseract / Cloud OCR“, „Automatisch gespeichert“, OCR-Layer-Schalter, Text
auswählen, Drehen, Beleg-ID, Dateigröße, zweiter „Vollbild anzeigen“-Knopf in der Bühne, nachgebautes HTML-Dokument,
`truncate`, `title` statt Namen, `onclick` an Reitern, `outline-none`.

### Schritt C0 – Vorbereitung · steuernde Sitzung (Opus)

- Antworten F1–F5 in diese Datei eintragen; bei F1-A `screen.png` nach `docs/ui/stitch/03-dokument-ansehen/` holen.
- Branch `feat/m04u-c-viewer` von `feat/m04u-oberflaeche`.
- Bei F3-A: `src/NoMorePaper.Contracts/Documents/DocumentTextDto.cs` anlegen und committen, damit S1 und S2/S3
  denselben Vertrag haben: `public sealed record DocumentTextDto(string? Text);` mit XML-Doku
  „`Text` ist `null`, solange das Dokument nicht fertig verarbeitet ist; leer, wenn die OCR nichts erkannt hat.“

### Paket P1 – Endpunkt „Erkannter Text“ (nur F3-A) · Modell: **Sonnet** · Effort: `medium` · Lauf S1, eigener Worktree

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Application/Documents/DocumentText.cs` | neu | `public sealed record DocumentText(string? Text);` – Doku wie `DocumentTextDto` | – | 10 |
| `src/NoMorePaper.Application/Documents/IDocumentTextReader.cs` | neu | `Task<DocumentText?> GetAsync(Guid id, Guid viewerId, CancellationToken cancellationToken);` – `null`, wenn das Dokument fehlt oder privat eines anderen Kontos ist (Regel wie `IDocumentRepository.GetAsync`) | – | 15 |
| `src/NoMorePaper.Application/Documents/GetDocumentText.cs` | neu | `public sealed class GetDocumentText(IDocumentTextReader reader, ICurrentAccount current)` mit `Task<DocumentText?> GetAsync(Guid id, CancellationToken cancellationToken) => reader.GetAsync(id, current.Id, cancellationToken);` – Muster `GetDocument.cs` | Server-Test | 15 |
| `src/NoMorePaper.Infrastructure/Documents/EfDocumentTextReader.cs` | neu | `internal sealed class EfDocumentTextReader(AppDbContext db) : IDocumentTextReader`: `db.Documents.AsNoTracking().Where(d => d.Id == id && (d.OwnerId == viewerId \|\| !d.IsPrivate)).Select(d => new { d.State, d.ContentText }).SingleOrDefaultAsync(ct).ConfigureAwait(false)`; kein Treffer → `null`; `State != ProcessingState.Done` → `new DocumentText(null)`; sonst `new DocumentText(row.ContentText ?? string.Empty)`. Kommentar (Warum): Sichtbarkeit wie `EfDocumentRepository.VisibleTo`; LINQ statt SQL, damit der Papierkorb-Filter aus M4b (globaler Abfragefilter) von selbst greift | Server-Test | 40 |
| `src/NoMorePaper.Server/Documents/DocumentTextEndpoints.cs` | neu | `internal static class DocumentTextEndpoints`; `public static RouteGroupBuilder MapDocumentTextEndpoints(this RouteGroupBuilder api)` → `api.MapGroup("/documents/{id:guid}").MapGet("/text", GetAsync)`; `GetAsync(Guid id, GetDocumentText get, ILogger<GetDocumentText> logger, CancellationToken ct)`: `null` → `Results.Problem(title: "Not found.", statusCode: 404)` (wie `DocumentEndpoints.NotFound`), sonst `logger.DocumentTextServed(id, text.Text?.Length ?? 0)` und `Results.Ok(new DocumentTextDto(text.Text))`. Kopfkommentar: angemeldet (Gruppe `/api`), fremde private Dokumente 404 wie überall | Server-Test | 35 |
| `src/NoMorePaper.Server/Documents/DocumentsLog.cs` | ändern (Hotspot) | `[LoggerMessage(EventId = 1102, Level = LogLevel.Debug, Message = "Recognized text of document {DocumentId} served ({Length} characters)")] DocumentTextServed(this ILogger logger, Guid documentId, int length)` – nie Text | – | +3 |
| `src/NoMorePaper.Server/Program.cs` | ändern (Hotspot) | `api.MapDocumentTextEndpoints();` direkt nach `api.MapDocumentEndpoints();` | – | +1 |
| `…Application/Composition/ApplicationServiceCollectionExtensions.cs` | ändern (Hotspot) | `services.AddScoped<GetDocumentText>();` nach `AddScoped<GetDocument>()` | – | +1 |
| `…Infrastructure/Composition/InfrastructureServiceCollectionExtensions.cs` | ändern (Hotspot) | `services.AddScoped<IDocumentTextReader, EfDocumentTextReader>();` nach `IDocumentRepository` | – | +1 |
| `tests/NoMorePaper.Server.Tests/Documents/DocumentTextTests.cs` | neu | `public sealed class DocumentTextTests(ApiFixture api)`, Hilfe `CompleteAsync(id, text)` wie in `PrivacyTests` (Zeile 118). Fälle: **(1)** Besitzer bekommt den Text mit Zeilenumbrüchen unverändert; **(2)** geteiltes Dokument: ein anderes Mitglied bekommt den Text; **(3)** privates Dokument: anderes Mitglied **und** Admin → 404, Besitzer → 200; **(4)** hochgeladen, nicht verarbeitet → 200 mit `text = null`; **(5)** fertig mit leerem Text → `""`; **(6)** unbekannte Id → 404; **(7)** ohne Anmeldung → 401. Dateinamen/Längen eigener Art (`SampleFiles.WithHead(…, 7001…)`), damit keine Duplikate entstehen | selbst | 130 |

- **Reihenfolge:** Application → Infrastructure → Server → Registrierungen → Tests.
- **Telemetrie:** `1102 DocumentTextServed` (Debug, Dokument-Id und Zeichenzahl). Katalog-Eintrag in P6.
- **Akzeptanz:** `dotnet build NoMorePaper.slnx` ohne Warnungen;
  `dotnet test --project tests/NoMorePaper.Server.Tests/NoMorePaper.Server.Tests.csproj -- --filter-class NoMorePaper.Server.Tests.Documents.DocumentTextTests`
  grün; `dotnet test --project tests/NoMorePaper.Architecture.Tests/NoMorePaper.Architecture.Tests.csproj` grün;
  Zeilenprüfung grün. Commit + Push auf `feat/m04u-c-viewer-server` (vom Spurbranch), Merge durch die steuernde Sitzung.

### Paket P2 – Angaben: zerlegen, Aussehen, Privat, Tag-Chips · Modell: **Sonnet** · Effort: `medium` · Lauf S2

Zuerst **nur zerlegen** (Schritt 1), dann bauen (Schritt 2). Nach Schritt 1 müssen alle Tests in
`Classification` und `Accounts.PrivacyTests` **ohne** Teständerung grün sein – das beweist, dass sich nichts geändert hat.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Classification/DocumentFieldsPanel.razor` | ändern | **Schritt 1:** `@code` vollständig nach `.razor.cs`; `@inject` werden `[Inject]`-Eigenschaften dort (Muster `Pages/DocumentViewer.razor.cs`); `@using System.Globalization` wandert mit. **Schritt 2:** Markup: `<section class="fields-panel" aria-labelledby="fields-heading">`; `MudText Typo.h2 id="fields-heading" Class="nmp-visually-hidden"` (der Reiter zeigt das Wort schon, Überschriften-Sprung bleibt); `LiveRegion @ref="_status"` bleibt **als erstes** Live-Element; Ladefehler: `<NoticeBox Kind="NoticeKind.Error" Announce="true" Message="@L["ClsFieldsLoadError", _loadError]" />` + Knopf `#fields-retry` (statt `div.error-box`); „weitergegeben“ und „lädt“ unverändert; geladen: `<DocumentFieldsSummary Fields="_fields" />` statt Absatz/`MailOriginSection`/Hinweise/`dl`; danach `OwnerCheckSection`, Editor bzw. Knöpfe „Bearbeiten“/„Bestätigen“ unverändert. **Nach** dem bedingten Block: `@if (IsMine && _handedOver is null) { <DocumentPrivacy DocumentId="DocumentId" IsPrivate="IsPrivate" OnChanged="PrivacyChangedAsync" /> }`, dann `@if (IsMine && _fields is not null && _handedOver is null && _loadError is null) { <EditorsPicker … /> }` (Privat erscheint auch, wenn die Angaben nicht laden) | `FieldsPanelTests`, `EntryComboboxTests` | 252 → ~80 |
| `Classification/DocumentFieldsPanel.razor.cs` | neu | `public sealed partial class DocumentFieldsPanel`; Inhalt des bisherigen `@code` unverändert, dazu: `[Parameter] public bool IsPrivate`, `[Parameter] public EventCallback<bool> OnPrivacyChanged`, `[Parameter] public EventCallback<DocumentFieldsDto> OnFieldsLoaded` (Doku: „nach jedem erfolgreichen Laden – die Seite braucht Art, Quelle und Erkennung für Pfad und Verlauf“); in `LoadAsync` nach `Logger.FieldsLoaded(…)`: `await OnFieldsLoaded.InvokeAsync(_fields);`; `private Task PrivacyChangedAsync(bool isPrivate) => OnPrivacyChanged.InvokeAsync(isPrivate);`. `RecognitionIcon` wandert nach `DocumentFieldsSummary` | wie oben | ~185 |
| `Classification/DocumentFieldsSummary.razor` | neu | Lesemodus der Angaben, `[Parameter, EditorRequired] DocumentFieldsDto Fields`. Reihenfolge wie bisher: `<p class="fields-summary">` (Begriff + Text), `<MailOriginSection Mail="Fields.Mail" />`, `<p class="fields-recognition"><MudIcon Icon="@RecognitionIcon" aria-hidden="true" /><span>@L["ClsRecognition" + Fields.Recognition]</span></p>`, Duplikat-Hinweis wie bisher (Symbol `aria-hidden`), dann `<dl class="fields-grid">` mit je `<div class="fields-pair">dt+dd</div>`: Absender, Dokumentart, Dokumentdatum (`dd class="fields-mono"`), Betrag (`fields-mono`), Tags (`fields-pair fields-pair-wide`; keine → `ClsNoValue`, sonst `<ul class="fields-tags">` mit `<li @key="tag.Id">@tag.Name</li>`). Kommentar oben: Warum Zusammenfassung zuerst (aus dem Panel übernommen) | `FieldsPanelTests`, neu `FieldsSummaryTests` | 75 |
| `Classification/DocumentFieldsEditor.razor` | ändern | **nur zerlegen:** `@code` nach `.razor.cs`, `@inject` → `[Inject]`; Markup unverändert (Speichern bleibt „Speichern“, wird auch auf „Zu prüfen“ benutzt) | `FieldsPanelTests`, `ReviewPageTests`, `EntryComboboxTests` | 235 → ~70 |
| `Classification/DocumentFieldsEditor.razor.cs` | neu | `public sealed partial class DocumentFieldsEditor`, Inhalt des bisherigen `@code` unverändert | wie oben | ~175 |
| `Viewer/DocumentPrivacy.razor` | neu | Schalter „Privat“ aus `DocumentViewer` hierher, Verhalten **unverändert**: `[Parameter, EditorRequired] Guid DocumentId`, `[Parameter] bool IsPrivate`, `[Parameter] EventCallback<bool> OnChanged`; `@inject DocumentApi Api`, `L`. Markup `<div class="doc-privacy">` → `div.form-check` mit `<input @key="_version" @ref="_box" id="viewer-private" type="checkbox" checked="@IsPrivate" @onchange="ChangedAsync" aria-describedby="viewer-private-hint" />` + `label for="viewer-private"` (`PrivateLabel`), `<p id="viewer-private-hint" class="form-hint">@L["ViewerPrivateHint"]</p>`, `<LiveRegion @ref="_status" />`. `ChangedAsync` = bisheriges `OnPrivateChangedAsync` (Erfolg: `await OnChanged.InvokeAsync(wanted)`, Ansage `ViewerNowPrivate`/`ViewerNowShared`; Fehler: `_version++`, `_focus = true`, Ansage `ErrorPrefix`); `OnAfterRenderAsync` setzt Fokus auf `_box`, wenn `_focus` (wie bisher in der Seite) | `PrivacyTests.ViewerSwitch_*` | 75 |
| `Pages/DocumentViewer.razor` + `.razor.cs` | ändern | Schalter-Markup, `_privateBox`, `_privacyVersion`, `_focusPrivateBox`, `OnPrivateChangedAsync` und der Fokus-Teil in `OnAfterRenderAsync` entfallen; `<DocumentFieldsPanel … IsPrivate="_detail.IsPrivate" OnPrivacyChanged="OnPrivacyChanged" OnFieldsLoaded="OnFieldsLoaded" />`; neu `private void OnPrivacyChanged(bool isPrivate) => _detail = _detail is null ? null : _detail with { IsPrivate = isPrivate };`, `private DocumentFieldsDto? _fields;` (in `OnParametersSetAsync` mit zurücksetzen), `private void OnFieldsLoaded(DocumentFieldsDto fields) => _fields = fields;` (Rest der Seite bleibt bis P4) | `PrivacyTests` | 214 → ~185 |
| `Classification/TagPicker.razor` | ändern | Entfernen-Knopf: `<button type="button" class="tag-remove" aria-label="@L["ClsRemoveTagFor", tag.Name]" @onclick="…"><MudIcon Icon="@Icons.Material.Filled.Close" aria-hidden="true" /></button>` statt Textknopf „Entfernen“; Name, Fokus nach dem Entfernen und Ansage bleiben. `ClsRemoveTag` danach per Suche prüfen: ungenutzt → im Gesamt-Review entfernen (nicht hier) | `EntryComboboxTests.Tags_…` (unverändert grün), `DocumentFilterTests` | 70 → 70 |
| `wwwroot/css/classification.css` | ändern (beansprucht) | nur `.tag-list` und `.tag-list li` auf die Chip-Werte („Maße“, Tag-Chip), neu `.tag-remove` + `:hover` + `.tag-remove .mud-icon-root`; sonst nichts | – | 205 → ~220 |
| `wwwroot/css/design.css` | ändern (Hotspot, nur ergänzen) | am Ende `.nmp-visually-hidden { position: absolute !important; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0 0 0 0); clip-path: inset(50%); white-space: nowrap; border: 0; }` mit Kommentar (für Überschriften, die nur Screenreader brauchen) | – | +14 |
| `wwwroot/css/document-side.css` | neu | Kopfkommentar wie `design.css` (Tokens, keine `outline`). Hier in P2: `.doc-view-side .fields-summary`, `.fields-term`, `.doc-view-side .fields-recognition`, `.doc-view-side .fields-duplicate` (+ Symbolgröße), `.fields-grid`, `.fields-pair`, `.fields-pair dt`, `.fields-pair dd`, `.fields-mono`, `.fields-pair-wide`, `@media (max-width: 29.98rem)` eine Spalte, `.fields-tags` (Flex, `list-style: none`, `padding: 0`, `margin: 0`) + `li`, `.doc-privacy`. Werte aus „Maße“. Bis P4 sind die `.doc-view-side`-Regeln ohne Wirkung (Klasse kommt in P4) | – | ~90 |
| `wwwroot/index.html` | ändern (Hotspot) | `<link rel="stylesheet" href="css/document-side.css" />` nach `css/classification.css` | – | +1 |
| `tests/NoMorePaper.UI.Tests/Accounts/PrivacyTests.cs` | ändern (nur `ViewerSwitch_*`) | Ansage-Locator `main .viewer-announcer` → `main .doc-privacy .live-region` (3 Stellen); sonst nichts | selbst | 0 |
| `tests/NoMorePaper.UI.Tests/Classification/FieldsSummaryTests.cs` | neu | Muster `FieldsPanelTests` (`FakeFields`). Fälle: **(1)** Tags als Liste von Chips (`.fields-tags li` = „Strom“), Werte paarweise (`GetByRole(Term)` und `Definition` wie bisher); **(2)** Hinweis „Automatisch erkannt …“ steht vor der Liste, Symbol `svg` hat `aria-hidden="true"`; **(3)** Überschrift „Angaben“ ist als `h2` per Rolle erreichbar (`GetByRole(Heading, Name="Angaben", Level=2)`), aber unsichtbar (Breite ≤ 1 px); **(4)** Ladefehler (`/fields` → 500): `role=alert` mit „…konnten nicht geladen werden“, Knopf „Erneut versuchen“ lädt neu; **(5)** Besitzer mit Ladefehler sieht trotzdem den Schalter „Privat“ | selbst | 120 |

- **Reihenfolge:** Schritt 1 (zwei Zerlegungen) → `dotnet test … --filter-namespace NoMorePaper.UI.Tests.Classification`
  grün → `DocumentPrivacy` + Seite → `PrivacyTests` anpassen → `DocumentFieldsSummary` + Panel → `TagPicker` + CSS →
  `FieldsSummaryTests`.
- **Telemetrie:** keine neuen Logs; `FieldsLoaded`, `FieldsConfirmed`, `FieldsSaved`, `FieldsChangedMeanwhile`
  bleiben an derselben Stelle (nur in die `.razor.cs` verschoben).
- **Akzeptanz:** Build ohne Warnungen; Namespaces `Classification`, `Accounts`, `Inputs`, `Viewer` der UI-Tests grün
  (`dotnet test --project tests/NoMorePaper.UI.Tests/NoMorePaper.UI.Tests.csproj -- --filter-namespace …` je
  Namespace); keine Datei über 250 Zeilen; Zeilenprüfung grün.

### Paket P3 – Reiterinhalte „Erkannter Text“ und „Verlauf“ · Modell: **Sonnet** · Effort: `medium` · Lauf S2 (nach P2)

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Documents/DocumentApi.cs` | ändern (Hotspot, nur ergänzen) | nach `GetAsync(Guid id, …)`: `public Task<ApiResult<DocumentTextDto>> GetTextAsync(Guid id, CancellationToken ct = default) => GetAsync<DocumentTextDto>($"api/documents/{id}/text", "text", ct);` | UI-Tests P5 | +3 |
| `Viewer/RecognizedTextPanel.razor` | neu | Inhalt des Reiters. Parameter: `[Parameter, EditorRequired] Guid DocumentId`, `[Parameter, EditorRequired] DocumentProcessingState State`, `[Parameter] bool Active`. `@inject DocumentApi Api`, `IJSRuntime Js`, `L`, `ILogger<RecognizedTextPanel> Logger`. Felder `_loadedId`, `_requested`, `_loading`, `_text` (`string?`), `_error`, `ElementReference _box`, `LiveRegion? _status`. `OnParametersSetAsync`: andere Id → alles zurücksetzen; `Active && !_requested && State == Done` → `LoadAsync()`. `LoadAsync`: `_requested = _loading = true`, `_error = null`, Aufruf, Fehler → `_error = result.Error`; sonst `_text = result.Value!.Text`, `Logger.RecognizedTextLoaded(_text?.Length ?? 0)`. Markup: `<div class="doc-text-head"><h2 class="doc-label" id="doc-text-heading">@L["ViewerTabText"]</h2>` + (nur mit Text) Knopf `<button type="button" class="doc-text-copy" aria-label="@L["TextCopyLabel"]" @onclick="CopyAsync"><MudIcon Icon="@Icons.Material.Filled.ContentCopy" aria-hidden="true" /><span>@L["Copy"]</span></button></div>`; Zustände: `Queued`/`Processing` → `<p>@L["TextNotYet"]</p>`; `Failed` → `TextFailed`; `_error` → `<NoticeBox Kind="NoticeKind.Error" Announce="true" Message="@L["TextLoadError", _error]"><button type="button" class="nmp-button" @onclick="LoadAsync">@L["Retry"]</button></NoticeBox>`; `_loading` oder noch nicht angefragt → `<p role="status">@L["TextLoading"]</p>`; `_text is null` → `TextNotYet`; leer → `TextEmpty`; sonst `<section @ref="_box" class="doc-text" tabindex="0" aria-labelledby="doc-text-heading">@_text</section>` (Zeilenumbrüche über CSS `pre-wrap`); am Ende `<LiveRegion @ref="_status" />`. `CopyAsync` nach Muster `Accounts/CopyField.razor`: `navigator.clipboard.writeText` → Ansage `TextCopied` + `Logger.RecognizedTextCopied()`; `catch (Exception ex) when (ex is JSException or InvalidOperationException)` → `Logger.RecognizedTextCopyFailed(ex.GetType().Name)`, Fokus auf `_box`, Ansage `TextCopyFailed`. Kopfkommentar (Warum: lädt erst beim Öffnen; Text lesbar und kopierbar für Screenreader) | P5 `ViewerTabsTests` | 130 |
| `Viewer/HistoryEntryKind.cs` | neu | `public enum HistoryEntryKind { Received, Queued, Processing, TextRecognized, Failed, FieldsRecognized, FieldsConfirmed }` mit Doku je Wert | – | 25 |
| `Viewer/HistoryEntry.cs` | neu | `public sealed record HistoryEntry(HistoryEntryKind Kind, DateTimeOffset? At, string? SourceKey);` – `At` nur für `Received`, `SourceKey` nur für `Received` mit bekannter Quelle | – | 10 |
| `Viewer/HistoryTimeline.cs` | neu | `public static class HistoryTimeline` (rein, ohne Uhr): `IReadOnlyList<HistoryEntry> Build(DateTimeOffset receivedAt, DocumentProcessingState state, DocumentRecognitionState? recognition, string? source)` → **neueste zuerst**: Angaben-Eintrag (`Automatic` → `FieldsRecognized`, `Confirmed` → `FieldsConfirmed`, `None`/`null` → keiner; unabhängig vom Verarbeitungsstand, weil Bestätigen während der OCR möglich ist), dann Stand (`Queued`/`Processing`/`Done` → `TextRecognized`/`Failed`), zuletzt `Received` mit Zeit und `SourceKey(source)`. `string TitleKey(HistoryEntryKind kind)` → `History` + Name (z. B. `HistoryTextRecognized`). `string? SourceKey(string? source)`: `upload` → `HistorySourceUpload`, `camera` → `HistorySourceCamera`, `scanner-folder` → `HistorySourceScannerFolder`, `mail` → `HistorySourceMail`, `icloud` → `HistorySourceICloud`, sonst `null` (Werte aus `DocumentFieldsDto.Source`) | `HistoryTimelineTests` | 70 |
| `Viewer/DocumentHistory.razor` | neu | Parameter `[Parameter, EditorRequired] DocumentDetailDto Detail`, `[Parameter] DocumentFieldsDto? Fields`. Markup: `<h2 class="nmp-visually-hidden" id="doc-history-heading">@L["ViewerTabHistory"]</h2>`, `<ol class="doc-history" aria-labelledby="doc-history-heading">` je Eintrag aus `HistoryTimeline.Build(Detail.ReceivedAt, Detail.State, Fields?.Recognition, Fields?.Source)`: `<li><div class="doc-history-row"><span class="doc-history-title">@L[TitleKey]</span>` + bei `At` `<time class="doc-history-time" datetime="@at.ToString("o", CultureInfo.InvariantCulture)">@at.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)</time>` (Muster `Documents.razor` Zeile 78)`</div>` + bei `SourceKey` `<p class="doc-history-detail">@L["HistoryVia", L[key]]</p></li>`; darunter `<p class="doc-history-hint">@L["HistoryHint"]</p>`. Punkte/Linie nur CSS (`content: ""`, nichts wird vorgelesen) | P5 | 50 |
| `Viewer/ViewerLog.cs` | ändern | neu (Bereich Viewer 4200–4299): `4212 ViewerTabSelected(string tab)` Debug, feste Wörter `fields`/`text`/`history`; `4213 RecognizedTextLoaded(int length)` Information; `4214 RecognizedTextCopied()` Information; `4215 RecognizedTextCopyFailed(string exceptionType)` Warning; `4216 FullscreenChanged(string state)` Information (`on`/`off`); `4217 FullscreenFailed(string errorType)` Warning. Nie Text oder Titel | – | 60 → ~95 |
| `wwwroot/css/document-side.css` | ändern | ergänzen: `.doc-text-head` (Flex, `justify-content: space-between`, `align-items: center`), `.doc-label`, `.doc-text-copy` + Hover + Symbol, `.doc-text`, `.doc-history` samt `li`, `::before`, `::after`, `:first-child`, `.doc-history-row`, `.doc-history-title`, `.doc-history-time`, `.doc-history-detail`, `.doc-history-hint` (`body-sm`, `--nmp-text-secondary`) – Werte aus „Maße“ | – | ~90 → ~190 |
| `Localization/Strings.resx`, `Strings.en.resx` | ändern (Hotspot, nur ergänzen) | **DE / EN:** `ViewerTabText` Erkannter Text / Recognized text; `ViewerTabHistory` Verlauf / History; `TextLoading` Text wird geladen … / Loading the text …; `TextLoadError` Der Text konnte nicht geladen werden: {0} / The text could not be loaded: {0}; `TextNotYet` Der Text steht bereit, sobald das Dokument verarbeitet ist. / The text is ready once the document has been processed.; `TextFailed` Die Verarbeitung ist fehlgeschlagen, es gibt keinen erkannten Text. / Processing failed, there is no recognized text.; `TextEmpty` Auf diesem Dokument wurde kein Text erkannt. / No text was recognized on this document.; `TextCopyLabel` Erkannten Text kopieren / Copy recognized text; `TextCopied` Der erkannte Text wurde kopiert. / The recognized text was copied.; `TextCopyFailed` Kopieren ging nicht. Markiere den Text in der Textfläche von Hand. / Copying did not work. Select the text in the text area by hand.; `HistoryReceived` Eingegangen / Received; `HistoryVia` über {0} / via {0}; `HistorySourceUpload` Hochladen / upload; `HistorySourceCamera` Kamera / camera; `HistorySourceScannerFolder` Scanner-Ordner / scanner folder; `HistorySourceMail` Mail / mail; `HistorySourceICloud` iCloud Drive / iCloud Drive; `HistoryQueued` Wartet auf die Verarbeitung / Waiting to be processed; `HistoryProcessing` Wird verarbeitet / Being processed; `HistoryTextRecognized` Text erkannt / Text recognized; `HistoryFailed` Verarbeitung fehlgeschlagen / Processing failed; `HistoryFieldsRecognized` Angaben automatisch erkannt / Details recognized automatically; `HistoryFieldsConfirmed` Angaben geprüft und bestätigt / Details checked and confirmed; `HistoryHint` Zeitpunkte gibt es bisher nur für den Eingang. / So far only the time of arrival is recorded. | Build | +72 je Datei |
| `tests/NoMorePaper.Web.Client.Tests/Viewer/HistoryTimelineTests.cs` | neu | Muster `Design/TabNavigationTests.cs`. Fälle: `Received` ist immer letzter Eintrag mit Zeit und Quelle; `[Theory]` Stand → Art (4 Fälle); `[Theory]` Erkennung `Automatic`/`Confirmed`/`None`/`null` → erster Eintrag bzw. keiner; Bestätigt während `Processing` → beide Einträge; unbekannte Quelle und `null` → `SourceKey` `null`; `TitleKey` aller Arten verschieden und beginnt mit `History`; nur `Received` hat `At` | selbst | 110 |

- **Reihenfolge:** `DocumentApi` → `HistoryEntryKind`/`HistoryEntry`/`HistoryTimeline` + Tests → `DocumentHistory` →
  `RecognizedTextPanel` → `ViewerLog` → Texte → CSS → Build.
- **Telemetrie:** 4212–4217 (siehe `ViewerLog`); 4212, 4216 und 4217 werden erst in P4 aufgerufen. Katalog in P6.
- **Akzeptanz:** Build ohne Warnungen;
  `dotnet test --project tests/NoMorePaper.Web.Client.Tests/NoMorePaper.Web.Client.Tests.csproj` grün; Zeilenprüfung
  grün. Commit auf `feat/m04u-c-viewer` erst nach P7 (S2 übergibt den Arbeitsstand an S3 auf demselben Branch).

### Paket P4 – Seitenrahmen, Werkzeugleiste, Reiter, Vollbild · Modell: **Sonnet** · Effort: `medium` · Lauf S3

Aufbau von `DocumentViewer.razor` (Option F2-B; die Reihenfolge der zwei Spalten ist die einzige Stelle, die F2 ändert):

```
<div class="viewer doc-view">
  <DocumentBackBar Detail TypeName />                       Zurück, Pfad, Eingang
  <header class="doc-view-head">                            Symbolkachel, H1, Abzeichen | Aktionen (Downloads)
  <div class="doc-view-columns">
    <div class="doc-view-side">  TabList + 3 Panels in .doc-view-card     (F2-B: zuerst im DOM, optisch rechts)
    <div class="doc-view-viewer" id="doc-view-viewer">  Leiste, Hinweise, Bühne, Tastenhilfe, Ansage
```

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Viewer/DocumentBackBar.razor` | neu | Parameter `[Parameter, EditorRequired] DocumentDetailDto Detail`. `<div class="doc-view-back">`: `<div class="doc-view-back-start">` mit Link `<a class="doc-view-back-link" href="dokumente"><MudIcon Icon="@Icons.Material.Filled.ArrowBack" aria-hidden="true" /><span>@L["ViewerBackToList"]</span></a>` und `<Breadcrumb Items="_path" />` (Spur D; `_path` in `OnParametersSet`: `[new(L["NavDocuments"], "dokumente"), new(Detail.Title, null)]` – `NavDocuments` ist der Schlüssel der Seitenleiste); danach `<span class="doc-view-received">@L["ViewerReceived", …]</span>` mit `<time datetime="@Detail.ReceivedAt.ToString("o", CultureInfo.InvariantCulture)">` und `ToLocalTime().ToString("g", CultureInfo.CurrentCulture)` | `ViewerLayoutTests` | 45 |
| `Viewer/FullscreenButton.razor` | neu | `@implements IAsyncDisposable`, `@inject IJSRuntime Js`, `L`, `ILogger<FullscreenButton> Logger`. Parameter `[Parameter, EditorRequired] string TargetId`, `[Parameter] EventCallback<bool> OnChanged`, `[Parameter] EventCallback OnFailed`. `OnAfterRenderAsync(firstRender)`: `_self = DotNetObjectReference.Create(this)`; `_supported = await Js.InvokeAsync<bool>("nmpFullscreen.watch", TargetId, _self)`; bei `true` `StateHasChanged()`. Markup nur bei `_supported`: `<button type="button" class="viewer-text-button" aria-pressed="@(_active ? "true" : "false")" @onclick="ToggleAsync"><MudIcon Icon="@(_active ? Icons.Material.Filled.FullscreenExit : Icons.Material.Filled.Fullscreen)" aria-hidden="true" /><span>@L["ViewerFullscreen"]</span></button>`. `ToggleAsync`: `Js.InvokeVoidAsync("nmpFullscreen.toggle", TargetId)`; `catch (JSException ex)` → `Logger.FullscreenFailed(ex.GetType().Name)`, `OnFailed`. `[JSInvokable] public async Task OnFullscreenChanged(bool active)`: `_active = active`, `Logger.FullscreenChanged(active ? "on" : "off")`, `StateHasChanged()`, `OnChanged.InvokeAsync(active)`. `DisposeAsync`: `nmpFullscreen.unwatch` in `try/catch (JSDisconnectedException)`, `_self?.Dispose()`. Muster `DotNetObjectReference` in `Viewer/ViewerInterop.cs` | `ViewerLayoutTests` | 75 |
| `wwwroot/js/fullscreen.js` | neu | Kommentar oben (Warum: eigener kleiner Baustein, der Viewer-Code bleibt unberührt; iPhone kann Elemente nicht ins Vollbild). `window.nmpFullscreen = (function () { … })()` mit `Map` der Beobachter: `supported()` = `document.fullscreenEnabled === true && typeof Element.prototype.requestFullscreen === 'function'`; `watch(id, dotnet)` → bei fehlender Unterstützung `false`, sonst `fullscreenchange`-Handler (`dotnet.invokeMethodAsync('OnFullscreenChanged', document.fullscreenElement === document.getElementById(id))`) registrieren, `true`; `unwatch(id)` → Handler entfernen, ist das Element noch im Vollbild, `document.exitFullscreen()`; `toggle(id)` (async) → im Vollbild `exitFullscreen()`, sonst `requestFullscreen()`; fehlt das Element, nichts tun | `ViewerLayoutTests` | 45 |
| `Pages/DocumentViewer.razor` | ändern | Laden/Fehler wie bisher, Fehlerbox → `<div data-ready><NoticeBox Kind="NoticeKind.Error" Announce="true" Message="@L["ViewerLoadError", _error]" /></div>`. Geladen: `<div class="viewer doc-view">` → `<DocumentBackBar Detail="_detail" />` → `<header class="doc-view-head"><div class="doc-view-heading"><div class="doc-view-title-row"><span class="doc-view-icon" aria-hidden="true"><MudIcon Icon="@Icons.Material.Outlined.Description" /></span><h1 class="doc-view-title">@_detail.Title</h1></div><div class="doc-view-badges"><span class="viewer-state"><Badge Text="@L["ViewerStatus", L["State" + _detail.State]]" Icon="@StateIcon(_detail.State)" /></span>` + bei `IsPrivate` `<Badge Text="@L["PrivateBadge"]" Icon="@Icons.Material.Filled.Lock" />`</div></div>`<div class="doc-view-actions"><ul class="doc-view-downloads" aria-label="@L["ViewerDownloads"]"><li><a class="doc-view-action" href="@DocumentLinks.Original(Id)" download aria-label="@L["DownloadOriginal"]"><MudIcon Icon="@Icons.Material.Filled.Download" aria-hidden="true" /><span>@L["DownloadOriginalShort"]</span></a></li>` + bei Archiv dasselbe mit `DownloadArchive`/`DownloadArchiveShort`</ul></div></header>` → `<div class="doc-view-columns">` → **Seite:** `<div class="doc-view-side"><div class="doc-view-card"><TabList Items="Tabs" SelectedId="@_tab" SelectedIdChanged="OnTabChanged" AriaLabel="@L["ViewerTabs"]" />` + drei Panels `<div role="tabpanel" id="doc-fields-panel" aria-labelledby="doc-fields-tab" tabindex="0" hidden="@(_tab != FieldsTab)">` (`<DocumentFieldsPanel …/>` wie nach P2), `doc-text-panel` (`<RecognizedTextPanel DocumentId="Id" State="_detail.State" Active="@(_tab == TextTab)" />`), `doc-history-panel` (`<DocumentHistory Detail="_detail" Fields="_fields" />`)`</div></div>` → **Viewer:** `<div class="doc-view-viewer" id="doc-view-viewer">`: ohne Quelle `<p data-ready class="viewer-message">…</p>` (Texte wie bisher); `_showFailed` → `<div data-ready><NoticeBox … Announce="true" Message="@L["ViewerShowError"]" /></div>`; sonst `<div class="viewer-bar">` mit `<div class="viewer-bar-group" role="group" aria-label="@L["ViewerPages"]">` [Symbolknopf `PreviousPage` `ChevronLeft`, `<span class="viewer-page">@PageText</span>`, Symbolknopf `NextPage` `ChevronRight`], `<span class="viewer-bar-separator" aria-hidden="true"></span>`, `<div class="viewer-bar-group viewer-bar-view" role="group" aria-label="@L["ViewerToolbar"]">` [Symbolknopf `ZoomOut` `Icons.Material.Filled.ZoomOut`, `<span class="viewer-zoom">@ZoomText</span>`, Symbolknopf `ZoomIn` `ZoomIn`, Textknopf `ZoomReset`, Symbolknopf `FitWidth` `FitScreen`, `<span class="viewer-bar-end">` Textknopf „Originalfarben“ mit `aria-pressed` (wie bisher) + `<FullscreenButton TargetId="doc-view-viewer" OnChanged="OnFullscreenChangedAsync" OnFailed="OnFullscreenFailedAsync" />`</span>]`</div>`; Symbolknopf = `<button type="button" class="viewer-icon-button" aria-label="@L[…]" @onclick="…"><MudIcon Icon="…" aria-hidden="true" /></button>`; danach `.viewer-no-text` (wie bisher), Bühne **unverändert** (`.viewer-stage`, `.viewer-canvas`, `section.viewer-scroller` mit allen Attributen), `<p id="viewer-keys" class="viewer-keys">` (nach der Bühne), `<LiveRegion @ref="_announcer" Class="viewer-announcer" />` (im Viewer-Block, damit Ansagen auch im Vollbild gehört werden) | alle Viewer-Tests | 88 → ~140 |
| `Pages/DocumentViewer.razor.cs` | ändern | Konstanten `FieldsTab = "doc-fields"`, `TextTab = "doc-text"`, `HistoryTab = "doc-history"`; `private string _tab = FieldsTab;` (in `OnParametersSetAsync` bei neuer Id zurück auf `FieldsTab`); `private IReadOnlyList<TabItem> Tabs => [new(FieldsTab, L["ClsFieldsHeading"]), new(TextTab, L["ViewerTabText"]), new(HistoryTab, L["ViewerTabHistory"])];`; `private void OnTabChanged(string id) { _tab = id; Logger.ViewerTabSelected(id switch { TextTab => "text", HistoryTab => "history", _ => "fields" }); }`; `private Task OnFullscreenChangedAsync(bool active) => AnnounceAsync(L[active ? "ViewerFullscreenOn" : "ViewerFullscreenOff"]);`, `OnFullscreenFailedAsync` → `ViewerFullscreenFailed`; kleine Hilfe `AnnounceAsync(string)` (auch in `OnRendererChangedAsync` nutzen); `private static string StateIcon(DocumentProcessingState state)` → `Queued` `Schedule`, `Processing` `Sync`, `Done` `CheckCircle`, `Failed` `Error` (wie `Design/StatusText`). Kommentar am Seitenaufbau (Warum F2-B: Screenreader hört Zusammenfassung vor dem Dokumenttext) | – | ~185 → ~230 |
| `wwwroot/css/viewer.css` | ändern | `.viewer-toolbar`-Regeln (Zeilen 24–64) ersetzen durch `.viewer-bar`, `.viewer-bar-group` (`inline-flex`, `align-items: center`, `gap: var(--nmp-space-xs)`), `.viewer-bar-view` (`flex: 1 1 auto`), `.viewer-bar-end` (`margin-inline-start: auto`, `inline-flex`, `gap`), `.viewer-bar-separator`, `.viewer-icon-button` (+ `:hover`, `.mud-icon-root`), `.viewer-text-button` (+ `:hover`, `[aria-pressed="true"]`, `[aria-pressed="true"]::before` mit dem bisherigen Haken), `.viewer-zoom`/`.viewer-page` neu; `.viewer-keys`, `.viewer-no-text`, `.viewer-message` → `body-sm`, `--nmp-text-secondary`, `margin: var(--nmp-space-sm) 0`; `.viewer-state` (Zeile 11) entfällt (Abzeichen); `.viewer-downloads` (Zeile 15) entfällt; `.viewer-stage`: nur Rand/Radius/`overflow` laut „Maße“. Ab `.viewer-scroller` bis Dateiende (Textebene, Miniaturen, Suchtreffer) **nichts ändern** | `ShaderPixelTests`, `ViewerKeyboardTests` | 200 → ~210 |
| `wwwroot/css/document-view.css` | neu | Kopfkommentar wie `design.css`. `.app-main:has(.doc-view)` nichts Neues (Regel in `viewer.css` bleibt); `.doc-view-back`, `.doc-view-back-start` (Flex mit Umbruch, `align-items: center`, `gap: var(--nmp-space-sm)`, `min-width: 0`), `-back-link` (+ Hover, Symbol), `.doc-view-back .nmp-breadcrumb`, `.doc-view-received`; `.doc-view-head`, `-heading`, `-title-row`, `-icon`, `.doc-view-title`, `-badges`, `-actions`, `.doc-view-downloads` (Flex, `list-style: none`, `padding: 0`, `margin: 0`, `gap`), `.doc-view-action` (+ Hover, Symbol); `.doc-view-columns`, `.doc-view-viewer` (Flex-Spalte, `gap: var(--nmp-space-sm)`, `min-width: 0`), `.doc-view-side` (Flex-Spalte, `gap: var(--nmp-space-md)`, `min-width: 0`), `.doc-view-card` (bei F5-B **keine** Reiter-Regeln; bei F5-A die Überschreibungen aus „Maße“); `@media (min-width: 80rem)`: Spalten 7/5, `.doc-view-side { grid-column: 2; grid-row: 1 }`, `.doc-view-viewer { grid-column: 1; grid-row: 1 }`, Kopf in einer Zeile; Vollbild: `.doc-view-viewer:fullscreen { height: 100vh; box-sizing: border-box; padding: var(--nmp-space-md); background: var(--nmp-background); }`, `.doc-view-viewer:fullscreen .viewer-stage { flex: 1 1 auto; height: auto; min-height: 0; }`, `.doc-view-viewer::backdrop { background: var(--nmp-background); }`. Werte aus „Maße“, keine `outline` | `ViewerLayoutTests` | 200 |
| `wwwroot/index.html` | ändern (Hotspot) | `<link rel="stylesheet" href="css/document-view.css" />` nach `css/viewer.css`; `<script src="js/fullscreen.js"></script>` nach `js/tabs.js` | – | +2 |
| `Localization/Strings.resx`, `Strings.en.resx` | ändern (Hotspot, nur ergänzen) | **DE / EN:** `ViewerBackToList` Zurück zur Liste / Back to the list; (Name des Pfads: `PathLabel` aus Spur D); `ViewerReceived` Eingang: {0} / Received: {0}; `ViewerTabs` Bereiche des Dokuments / Document sections; `DownloadOriginalShort` Original / Original; `DownloadArchiveShort` Archiv-PDF / Archive PDF; `ViewerFullscreen` Vollbild / Full screen; `ViewerFullscreenOn` Vollbild an. Escape beendet es. / Full screen on. Escape leaves it.; `ViewerFullscreenOff` Vollbild aus. / Full screen off.; `ViewerFullscreenFailed` Vollbild ist hier nicht möglich. / Full screen is not possible here. `ViewerPrivateHint`, `ViewerStatus` usw. bleiben | Build | +30 je Datei |

- **Reihenfolge:** Texte → `fullscreen.js` + `FullscreenButton` → `DocumentBackBar` → Seite (`.razor`, `.razor.cs`) →
  `viewer.css` → `document-view.css` → `index.html` → Build → bestehende Viewer-/Classification-/Accounts-Tests.
- **Telemetrie:** ruft 4212, 4216, 4217 auf; keine weiteren.
- **Akzeptanz:** Build ohne Warnungen; Namespaces `Viewer`, `Classification`, `Accounts`, `Inputs` grün (bis auf die in
  P5 genannten Anpassungen); keine Änderung in `wwwroot/js/viewer/*` (`git diff --stat` leer für den Ordner).

### Paket P5 – UI-Tests · Modell: **Sonnet** · Effort: `medium` · Lauf S3 (nach P4)

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `tests/NoMorePaper.UI.Tests/Viewer/ViewerPages.cs` | ändern | `OpenAsync` bekommt `string? text = ViewerPages.SampleText` (Konstante mit zwei Zeilen „Erste Zeile\nZweite Zeile“) und `int textStatus = 200`; Route `GET /api/documents/{id}/text$` → `DocumentTextDto(text)` bzw. Status; Zähler `TextRequests` in `ViewerSession` (oder Rückgabe-Hilfe), damit Tests „nur einmal geladen“ prüfen können; neue Hilfe `public static Task SelectTabAsync(IPage page, string name) => page.GetByRole(AriaRole.Tab, new() { Name = name, Exact = true }).ClickAsync();` | – | 70 → ~95 |
| `tests/NoMorePaper.UI.Tests/Viewer/ViewerSession.cs` | ändern | falls nötig Eigenschaft `TextRequests` (siehe oben) | – | 14 → ~20 |
| `tests/NoMorePaper.UI.Tests/Viewer/ViewerTabsTests.cs` | neu | **(1)** Drei Reiter „Angaben“, „Erkannter Text“, „Verlauf“ in `tablist` „Bereiche des Dokuments“, erster `aria-selected="true"`, nur sein Panel sichtbar; **(2)** Pfeil rechts auf dem Reiter wählt „Erkannter Text“, Fokus wandert mit, Ende/Pos1; **(3)** `[Theory]` DE/EN axe 0 Verstöße mit jedem Reiter gewählt (`AxeRules.Strict()`, Muster `ViewerPageTests.AssertNoViolationsAsync`); **(4)** Erkannter Text lädt erst beim Öffnen (vorher 0 Anfragen), zweites Öffnen keine neue Anfrage; Textfläche enthält beide Zeilen, ist per Tab erreichbar (`tabindex=0`) und heißt „Erkannter Text“; **(5)** Kopieren: Berechtigung `clipboard-read/-write` im Kontext erteilen, Klick auf „Erkannten Text kopieren“, `navigator.clipboard.readText()` = Text, Ansage „Der erkannte Text wurde kopiert.“; **(6)** `state: Queued` → „Der Text steht bereit …“ und **keine** Anfrage; **(7)** leerer Text → „Auf diesem Dokument wurde kein Text erkannt.“; **(8)** `textStatus: 500` → `role=alert` + „Erneut versuchen“ lädt neu; **(9)** Verlauf mit `ClassificationSamples.Automatic` und Quelle `mail`: Einträge in der Reihenfolge „Angaben automatisch erkannt“, „Text erkannt“, „Eingegangen“, `time` nur beim letzten, „über Mail“, Hinweis-Satz; **(10)** offenes Formular bleibt beim Reiterwechsel: „Bearbeiten“, Titel ändern, zu „Verlauf“ und zurück → Feld hat den geänderten Wert | selbst | 240 |
| `tests/NoMorePaper.UI.Tests/Viewer/ViewerLayoutTests.cs` | neu | **(1)** Zurück-Link `href="dokumente"`, `navigation` „Pfad“ mit Link „Dokumente“ und Titel mit `aria-current="page"`; Eingang mit `time[datetime]`; **(2)** genau ein `h1` = Titel; Abzeichen „Status: Fertig“ und bei privatem Dokument „Privat“; **(3)** Downloads: sichtbarer Text „Original“/„Archiv-PDF“, Namen wie bisher, ohne Archiv nur „Original“; **(4)** alle Knöpfe der Leiste haben einen Namen (`GetByRole(Button)` je Name aus `ViewerKeyboardTests`), jedes `svg` in `.viewer-bar` hat `aria-hidden="true"`; **(5)** Vollbild: Klick → `document.fullscreenElement.id == "doc-view-viewer"`, `aria-pressed="true"`, Ansage „Vollbild an …“; `document.exitFullscreen()` → `aria-pressed="false"`; **(6)** Vollbild nicht unterstützt (`prepare`: `Object.defineProperty(document, 'fullscreenEnabled', { value: false })`) → kein Knopf „Vollbild“; **(7)** Reihenfolge (F2-B): im DOM steht `.doc-view-side` vor `.doc-view-viewer`; bei 1440 × 900 liegt die Reiterkarte rechts vom Viewer (`BoundingBox.X`); **(8)** `[Theory]` (390, 844) und (320, 256): `scrollWidth <= innerWidth` (Muster `Layout/NavigationTests`); **(9)** keine Anfrage an fremde Hosts (Muster `PrivacyTests`) | selbst | 200 |
| `tests/NoMorePaper.UI.Tests/Viewer/ViewerKeyboardTests.cs` | ändern | `Scroller_IsReachableByTab_…`: Schleifengrenze `30` → `60` (bei F2-B liegen Reiter und Angaben vor dem Viewer); sonst nichts | selbst | 0 |
| `tests/NoMorePaper.UI.Tests/Viewer/ViewerPageTests.cs` | ändern | nur falls rot: Wartebedingungen/Selektoren an `NoticeBox` (`main [data-ready]`, `main [role=alert]` bleiben gültig) | selbst | 0–5 |

- **Reihenfolge:** `ViewerPages` → `ViewerTabsTests` → `ViewerLayoutTests` → ganze Suite.
- **Telemetrie:** –
- **Akzeptanz:**
  - `dotnet test --project tests/NoMorePaper.UI.Tests/NoMorePaper.UI.Tests.csproj -- --filter-namespace NoMorePaper.UI.Tests.Viewer` grün
  - danach `dotnet test --solution NoMorePaper.slnx` grün
  - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-file-length.ps1` grün

### Paket P6 – Katalog, KB, Häkchen · Modell: **Sonnet** (im Lauf S3, eigener Start lohnt nicht) · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `docs/kb/telemetrie.md` | ändern (Hotspot) | Zeilen 1102 (`Server/Documents/DocumentsLog`), 4212–4217 (`Web.Client/Viewer/ViewerLog`) nach dem Muster der Tabelle, jeweils mit „nie Text“ | – | +7 |
| `docs/kb/oberflaeche.md` | ändern (nur ergänzen) | Abschnitt „Seite Dokument ansehen“: Aufbau (Zurück-Leiste, Kopf, Spalten ab 80 rem, Reihenfolge F2), Reiter immer im DOM, Erkannter Text lädt beim Öffnen, Verlauf ohne Zeiten außer Eingang (F4-A), Vollbild nur mit Fullscreen-API (iPhone ohne), `.nmp-visually-hidden` in der Bausteintabelle | – | +20 |
| `docs/kb/verarbeitung.md` | ändern | Satz unter „Ablauf“: der erkannte Text ist über `GET /api/documents/{id}/text` lesbar (`null` vor Done, leer ohne Text; Sichtbarkeit wie das Dokument) | – | +2 |
| `docs/ui/stitch/README.md` | ändern | Status Screen 03 → „umgesetzt“ | – | 1 |
| `docs/plan/m4u-oberflaeche.md` | ändern | nur die drei Checkboxen unter „C. Dokument ansehen (Screen 03)“ abhaken | – | 3 |

- **Akzeptanz:** Commit auf `feat/m04u-c-viewer` erst nach P7.

### Paket P7 – Review und Abnahme · Modell: **Opus** · Effort: `high` (steuernde Sitzung)

- `feat/m04u-c-viewer-server` (P1) in `feat/m04u-c-viewer` mergen, Build + ganze Suite.
- Review-Schleife nach AGENTS.md §8 über die ganze Spur (Diff gegen `feat/m04u-oberflaeche`).
- Prüfliste: kein Tailwind-Klassenname, kein `material-symbols`, kein `title` an Knöpfen; jedes Symbol `aria-hidden`;
  genau ein `h1`; keine `outline`-Regel; Farben nur `--nmp-*`; `wwwroot/js/viewer/*` und `ViewerInterop.cs`
  unverändert; `@code` von Panel und Editor bis auf die genannten Parameter unverändert (Diff der `.razor.cs` gegen
  das alte `@code`); P1: kein Text in Logs, 404 für fremde private Dokumente; Dateien anderer Spuren unberührt.
- Sichtvergleich: Playwright-Bilder von `/dokumente/{id}` bei 1440 × 900 und 390 × 844, Reiter je einmal, nach
  `TestResults/m4u-c/` neben `screen.png`; Zoom 200 %/400 %; danach Sichtprüfung, NVDA und VoiceOver durch den Nutzer.
- Push auf `feat/m04u-c-viewer`; Merge in den Integrationsbranch durch die steuernde Sitzung (Reihenfolge A → D → B → C).

### Eskalation (alle Pakete)

- Zwei rote Läufe am selben Paket (Build oder Tests) → stoppen und melden.
- Schritt 1 von P2 (nur zerlegen) macht einen bestehenden Test rot → nicht den Test ändern, stoppen und melden.
- Ein bestehender Viewer-Test (`ShaderPixelTests`, `ContextLossTests`, `LargePageTests`, `ThumbnailTests`) wird rot →
  stoppen und melden; kein Eingriff in `wwwroot/js/viewer/*`.
- Vollbild-Test ist in Headless-Chromium nicht stabil (zweimal rot) → Fall (5) von `ViewerLayoutTests` melden, nicht
  umgehen; die steuernde Sitzung entscheidet (Stufe 3).
- axe meldet `color-contrast`, `target-size` oder einen Verstoß, den der Plan nicht abdeckt → stoppen und melden,
  keine eigene Farbe oder Größe wählen.
- Eine Datei aus „Nicht ändern“ müsste geändert werden, oder ein Text/Wert fehlt im Plan → stoppen und melden.

### Übergabe an M4b (Spur B „Seiten bearbeiten/Fassungen“, Spur C „Papierkorb“)

- `VersionsSection` gehört in das Panel `doc-history-panel` (unter die Zeitleiste) – dort sucht man Fassungen;
  „Seiten bearbeiten“ und `DeleteDocumentButton` kommen in `div.doc-view-actions` nach `ul.doc-view-downloads`
  (Knopfstil `.doc-view-action`).
- `DocumentDetailDto` (M4b: `Edited`, `PageCount` …) ändert Spur C nicht; der Download „bearbeitete Fassung“ wird ein
  drittes `li` in `ul.doc-view-downloads`.
- Echte Zeitpunkte für den Verlauf (F4) passen als eigene Aufgabe nach M4b Schritt 0; `HistoryTimeline.Build` bekommt
  dann Zeiten statt `null`.

### Risiken

- **Reihenfolge Screenreader (F2):** Die empfohlene Variante weicht im Seitenaufbau, nicht in der Optik vom Entwurf ab;
  Tastaturnutzer springen von rechts nach links. Abnahme mit NVDA durch den Nutzer entscheidet.
- **Kleine Schrift:** Werkzeugleiste, Pfad und erkannter Text in 10–11 px Mono wie im Entwurf (ADR-0026); der
  erkannte Text ist der längste Text in dieser Größe. Sichtprüfung mit Lupe durch den Nutzer.
- **Ausgeblendete Panels:** Live-Regionen in einem ausgeblendeten Reiter sagen nichts an; das betrifft nur Aktionen im
  sichtbaren Reiter, ist also unkritisch – beim Review prüfen, dass keine Ansage aus einem fremden Reiter kommt.
- **Merge mit Spur A:** A stellt die Feldbausteine (`accounts.css`) um; Editor und Privat-Schalter bekommen das neue
  Aussehen erst nach A. Nach dem Merge von A in den Integrationsbranch den Spurbranch neu aufsetzen und die Suite laufen
  lassen.
- **Abhängigkeit von Spur D:** P4 braucht `Design/Breadcrumb`, `PathLabel` und das Reiter-Aussehen aus D. Verzögert
  sich D, wartet S3; P1–P3 laufen trotzdem. Kein eigener Pfad-Baustein in Spur C (sonst zwei Bausteine für dasselbe).
- **Merge mit Spur B:** `TagPicker` (Chips) wirkt in der Filterleiste; `DocumentApi.cs` bekommt in beiden Spuren nur
  Ergänzungen.
- **M4b Schritt 0** läuft gleichzeitig; P1 berührt nur neue Dateien und drei Registrierungszeilen.

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|
| P1 | 2 | | |
| P2 | 2 | | |
| P3 | 2 | | |
| P4 | 2 (Vollbild am Rand zu 3) | | |
| P5 | 2 | | |
| P6 | 1 (im Sonnet-Lauf) | | |
| P7 | 3 | | |
