# Oberfläche: Design-Werte

Abschrift des Stitch-Design-Systems **„Obsidian Slate“** (Projekt „NoMorePaper“ `17713397687229704853`,
Asset `assets/b3b93e10fb5c43cabf20e90f9bc6508b`), vom Nutzer so freigegeben ([ADR-0026](../adr/0026-optik-nach-stitch-design-system.md)).
Im Code: `Theme/ContrastTheme.cs` (MudBlazor) und `wwwroot/css/base.css` (CSS-Variablen `--nmp-*`).
Ablauf für neue Seiten: [ui-prozess.md](ui-prozess.md). Stand 2026-11-22 (M4u Schritt 0).

## Farben

| Rolle | Wert | Variable | Kontrast auf `#131313` |
|---|---|---|---|
| Hintergrund (Seite) | `#131313` | `--nmp-background` | – |
| Tiefste Fläche (Felder) | `#0a0a0a` | `--nmp-canvas-lowest` | – |
| Karte/Box (Paper) | `#121212` | `--nmp-paper` | – |
| Seitenleiste | `#1c1b1b` | `--nmp-paper-low` | – |
| Erhöht (Hover, Chips) | `#18181b` | `--nmp-elevated` | – |
| Erhöht stark (aktueller Menüpunkt) | `#2a2a2a` | `--nmp-elevated-high` | – |
| Tabellenkopf | `#201f1f` | `--nmp-surface-container` | – |
| Text | `#e5e2e1` | `--nmp-text` | 14,4 : 1 |
| Text Menü, Kacheln (on-surface-variant) | `#c4c7c8` | `--nmp-text-variant` | 10,1 : 1 auf `#1c1b1b` |
| Text zweitrangig | `#a1a1aa` | `--nmp-text-secondary` | 7,2 : 1 |
| Umriss (Platzhalter, Tabellenkopf) | `#8e9192` | `--nmp-outline` | 5,9 : 1 (5,2 auf `#201f1f`) |
| Text gedämpft (nur deaktiviert) | `#71717a` | `--nmp-text-muted` | 3,8 : 1 |
| Primär (Überschriften, Hauptknopf) | `#ffffff` | `--nmp-primary` | – |
| Text auf Primär | `#0a0a0a` | `--nmp-on-primary` | – |
| Linie fein | `#27272a` | `--nmp-border` | – |
| Linie deutlich (Hover, aktiv) | `#3f3f46` | `--nmp-border-strong` | – |
| Fehler | `#ffb4ab` | `--nmp-error` | 10,9 : 1 |

Kontrast mit der WCAG-Formel gerechnet (Node, 2026-11-22).

**Kontrast:** Die UI-Tests prüfen mit axe WCAG AA (4,5 : 1). AAA (7 : 1) wird seit M4u nicht mehr
geprüft, weil einige Grautöne des freigegebenen Designs (Umriss, Tabellenkopf) darunter liegen
([ADR-0026](../adr/0026-optik-nach-stitch-design-system.md)). Welche Grautöne wo stehen, steht in den
Stitch-Screens: Menülinks `text-on-surface-variant`, Platzhalter und Tabellenkopf `outline`. `--nmp-accent` gibt es nur noch für alte
Feature-CSS und ist weiß.

## Schrift

Geist (Text, Überschriften) und JetBrains Mono (Etiketten, Tabellenkopf, Daten), beide lokal in
`wwwroot/fonts/` (OFL 1.1). Größen in rem, damit Browser- und Lupen-Zoom wirken (1 rem = 16 px).

| Stufe | Größe / Zeile | Gewicht | Schrift | MudBlazor |
|---|---|---|---|---|
| headline-xl | 40 / 48 px, −0,03 em | 600 | Geist | `H1` |
| headline-lg | 28 / 36 px, −0,02 em | 600 | Geist | `H2` |
| headline-md | 20 / 28 px, −0,01 em | 500 | Geist | `H3` |
| headline-sm | 16 / 24 px | 500 | Geist | `H4` |
| body-lg | 16 / 26 px | 400 | Geist | `Body1` |
| body-md (Grundschrift) | 14 / 22 px | 400 | Geist | `Default`, `Body2`, `Button` (500) |
| body-sm | 12 / 18 px | 400 | Geist | `Caption` |
| label-lg / md / sm | 13 / 11 / 10 px, +0,02–0,05 em | 500/500/400 | JetBrains Mono | `.doc-table th`, `.nmp-badge` |

## Abstände und Radien

Abstände `xs 0,25 · sm 0,5 · md 1 · lg 1,5 · xl 2,5 rem` (`--nmp-space-*`); Rand außen 1 rem mobil,
2 rem Desktop. Radien: Bedienelemente 0,5 rem (`--nmp-radius`), Karten 1 rem (`--nmp-radius-lg`),
innen 0,25 rem. Knöpfe 38 px hoch.

## Rahmen der Seite

- Seitenleiste 18 rem links, darin Start, Hinzufügen, Dokumente, Zu prüfen (n); unten abgesetzt
  Einstellungen. Aktueller Punkt: hellere Fläche, weißer Text, Rand und `aria-current="page"`.
- Kopf 4 rem: Suche, Knopf „Hochladen“ (weiß), Konto, Sprache. Unter 1024 px: Knopf „Menü“ (mit Text,
  `aria-expanded`), die Navigation liegt dann unter dem Kopf.
- Unterseiten: `/hinzufuegen` (Hochladen, Kamera), `/einstellungen` (Konto, Eingänge, Listen,
  Mitglieder für Admins). Alte Adressen bleiben.

## Bausteine (`Design/`)

| Baustein | Datei | Verhalten |
|---|---|---|
| Statuszeile | `StatusText.razor` | Symbol (`aria-hidden`) + Wort: Wartet, Wird verarbeitet, Fertig, Fehlgeschlagen, Erkannt. Symbol und Wort kommen aus `StatusIcons`/`StatusKeys` (auch Viewer-Abzeichen und Ansagen); `DocumentStatus.KindOf` bildet den Verarbeitungsstand ab |
| Abzeichen | `Badge.razor` | Chip `#18181b`, Rand `#27272a`, `label-sm`; Text Pflicht |
| Hinweisbox | `NoticeBox.razor` | Fehler/Erfolg mit Symbol + Satz; `role` nur mit `Announce`; optionales `Icon` ersetzt das Standardsymbol (Fehler/Erfolg) |
| Rückfrage | `ConfirmBox.razor` | benannte Gruppe (`role="group"`, `aria-labelledby` auf die Überschrift, Pflicht-`Id`); Überschrift (Ebene per `HeadingLevel`, Standard H2), optionaler `Text`, `ChildContent` für Felder; „Abbrechen“ zuerst, dann die Aktion; `PrimaryConfirm` (Standard neutral, Rot nie); Escape bricht ab; Fokus beim ersten Rendern auf `InitialFocusId`, sonst auf die Überschrift. Bei einer erneuten Rückfrage `@key` je Eintrag setzen, damit die Box neu entsteht |
| Reiter | `TabList.razor`, `TabItem` | WAI-ARIA-Muster, Pfeile/Pos1/Ende über `TabNavigation`; JetBrains Mono 11 px wie Entwurf 04 (gewählt: weiß mit weißer Linie, `aria-selected`); optional `Icon` (`aria-hidden`) und `Count` (kurze Angabe, Teil des Namens: „Mail 1 Problem“, Name kommt aus dem sichtbaren Text wegen axe `label-content-name-mismatch`) |
| Pfad | `Breadcrumb.razor`, `BreadcrumbItem` | `nav` mit `aria-label` „Pfad“, Liste, aktuelle Seite (`Href` `null`) mit `aria-current="page"`, Trenner-Symbol `aria-hidden`; auch für die Zurück-Leiste anderer Seiten (Viewer) gedacht |
| Nur Screenreader | `.nmp-visually-hidden` | Text ist im Lesebaum, aber optisch weg (z. B. Überschrift „Verlauf“) |
| Knöpfe | `.nmp-button`, `.nmp-button-primary` | Standard umrandet, Hauptaktion weiß; `display:inline-flex` – zum Ausblenden braucht es eine Regel mit höherer Spezifität, das `hidden`-Attribut allein wirkt nicht |
| Kacheln | `.link-tiles`, `.link-tile` | große Links mit Titel und Erklärung |
| Anmelderahmen | `Accounts/AccountFrame.razor` | Rahmen der Seiten für Abgemeldete: Logo-Kachel (`aria-hidden`), Überzeile, einzige H1, Unterzeile, Karte |

**Felder** (`FormField`, `form-fields.css`, vor `accounts.css` geladen): Beschriftung Mono 11 px, Feld 44 px hoch auf `#0a0a0a`, optionales Symbol
links (`aria-hidden`), bei Passwörtern Augen-Knopf rechts im Feld (Name „Passwort anzeigen: …“, Zustand in
`aria-pressed`). Im Fokus weißer Rand; ein ungültiges Feld hat einen gestrichelten Rand in `--nmp-error`
(nicht nur Farbe).

## Fokus

Kein eigener Fokusrahmen ([ADR-0026](../adr/0026-optik-nach-stitch-design-system.md)): Felder bekommen
im Fokus einen weißen Rand wie im Entwurf, sonst zeichnet der Browser. MudBlazor entfernt den
Browser-Rahmen an Links und Knöpfen (`a:focus-visible`, `button:focus`); `base.css` holt ihn mit
`outline: revert` zurück. Unter 1024 px ist der Kopf nicht fest (er bricht um und würde den Fokus
verdecken); „Menü“ setzt den Fokus auf den ersten Link, Escape schließt und kehrt zum Knopf zurück. Die Bildschirmlupe des Nutzers
hebt Fokus selbst hervor.

## Dokumentliste (`/dokumente`, Screen 02)

- **Spalten:** Titel (mit Privat-Abzeichen und Textausschnitt, 160 Zeichen plus Auslassungszeichen, nur als Text), Art und Tags
  (`#` ist `aria-hidden`), Absender, Datum (Dokumentdatum; darunter Eingang mit Weg), Betrag, Status, Erkennung.
  Der Titel ist der Link, die Zeile ist nicht klickbar.
- **Art-Chips:** Gruppe „Nach Dokumentart filtern“, jeder Chip ein Knopf mit `aria-pressed` und Anzahl, nur Arten mit
  Dokumenten; die Zahlen beachten die übrigen Filter. Die gewählte Art bleibt auch mit Anzahl 0 sichtbar.
  Das Auswahlfeld „Dokumentart“ im Filterbereich entfällt; der Tag-Filter bleibt dort.
- **Blättern:** `nav` „Seiten der Dokumentliste“, aktuelle Seite mit `aria-current="page"`, „Zeilen pro Seite“
  (10/25/50/100, Standard 50). Wird „Vorherige/Nächste Seite“ durch das Blättern abgeschaltet, springt der Fokus auf die
  aktuelle Seite (ohne Gesamtzahl auf den anderen Pfeil), sonst fiele er auf `body`. Ohne Gesamtzahl (Zähler-Aufruf
  schlägt fehl) gibt es nur „Seite n“, „Vorherige“ und „Nächste“ (aktiv, solange eine volle Seite kam).
- **Tabelle** liegt in einem scrollbaren, benannten Bereich (`role="region"`, `tabindex="0"`), damit die Seite
  auf dem Handy nicht seitlich scrollt. **Kacheln:** `auto-fill`/`minmax(15rem, 1fr)`, `min-height` statt fester Höhe
  (Zoom); sie zeigen die Miniatur der ersten Seite, die Tabelle nur einen Symbolkasten.
- **Nicht übernommen:** „Zuletzt bearbeitet“ (kein Änderungszeitpunkt in der DB), Export, Stapelverarbeitung,
  Auswahl-Kästchen, Aktionsspalte, Spalten-/Sortierwahl, zweites Suchfeld, Brotkrumen, WORM-/SHA-Hinweise.

## Seite „Dokument ansehen“ (`/dokumente/{id}`, Screen 03)

- **Aufbau:** Zurück-Leiste (Link „Zurück zur Liste“, `Breadcrumb` „Dokumente › Titel“, Eingang mit `time`), Kopf (Symbolkachel,
  einzige H1, Abzeichen Status und Privat, Downloads „Original“ und „Archiv-PDF“ mit langem zugänglichem Namen), darunter zwei
  Spalten. Spalten erst ab 80 rem Fensterbreite (7/12 Viewer, 5/12 Reiterkarte), darunter eine Spalte.
- **Reihenfolge (F2-B):** Im Seitenaufbau steht die Reiterkarte vor dem Viewer, damit der Screenreader die Zusammenfassung vor dem
  ganzen Dokumenttext liest. Per CSS (`grid-column`) liegt sie rechts vom Viewer; die Tab-Taste springt daher von rechts nach links.
- **Reiter:** „Angaben“, „Erkannter Text“, „Verlauf“; alle drei Panels bleiben im DOM (inaktive mit `hidden`), ein offenes
  Formular in „Angaben“ überlebt den Wechsel. Das Aussehen kommt von `TabList` (design.css), nicht vom Entwurf 03.
- **Erkannter Text:** lädt erst beim ersten Öffnen des Reiters (`GET /api/documents/{id}/text`), fokussierbarer Bereich
  (`tabindex="0"`) mit echtem Text, Knopf „Kopieren“ mit Ansage. Vor „Fertig“ kommt keine Anfrage.
- **Verlauf:** nur der Eingang hat eine Zeit (F4-A); Verarbeitungsstand und Angaben ohne Zeit, darunter ein Hinweis-Satz.
  Punkte und Linie sind reines CSS (`content: ""`), nichts davon wird vorgelesen.
- **Werkzeugleiste:** Symbolknöpfe mit `aria-label`, Zoom und Seite als Text; „Originalfarben“ und „Vollbild“ als Textknöpfe mit
  `aria-pressed`. Vollbild (`wwwroot/js/fullscreen.js`) nur, wo der Browser Elemente ins Vollbild setzen kann (iPhone: kein Knopf);
  die Spalte `#doc-view-viewer` geht ins Vollbild (alles drumherum ist dabei `inert`, damit Tab nicht hinausläuft), die Ansage „Vollbild an“ kommt aus ihr.
- **Seitenbreite im Test:** Bei 100 % ist eine A4-Seite etwa 800 px breit; Pixeltests des Viewers brauchen daher ein Fenster von
  mindestens 1800 px, sonst ist die Seite im schmaleren Viewer seitlich angeschnitten.
- `.nmp-visually-hidden` (design.css) blendet Überschriften nur optisch aus (Reiter „Verlauf“).

## Startseite (M4u Spur E, Entwurf 05)

- **Karten:** „Zu prüfen“ mit Anzahl (`ReviewCounter`, `api/review/count`), „Zuletzt hinzugefügt“ (`api/documents?page=1&pageSize=5`,
  Tabelle wie in Dokumente, Link „Alle Dokumente“), „Eingänge“ (`api/inputs`, Problem zuerst; keine Pfade und keine Adressen).
  Zwei Knöpfe: „Datei hochladen“, „Mit Kamera aufnehmen“.
- **Laden:** kein Polling. Ein Fehler je Karte zeigt `NoticeBox` mit Knopf „Erneut versuchen“, aber **ohne** `Announce`:
  beim Laden der Startseite gibt es keine Alarme (`role="alert"`), die Seite bleibt in `/`-Tests ruhig.

## Restliche Seiten (M4u Spur E)

Nach den Entwürfen 06–13 umgesetzt; was nicht übernommen wurde, steht in `docs/ui/stitch/NN-…/notizen.md`.
Wo die Umsetzung bewusst von einer Notiz abweicht, gilt diese Liste:

- **Rückfragen (06, 08, 10):** Fokus beim Öffnen auf die Überschrift der Rückfrage (Baustein-Vertrag), nicht
  auf „Abbrechen“; „Abbrechen“ ist einen Tab weiter. Beim Umbenennen (08) Fokus ins Namensfeld. Bestätigen
  neutral, nur „Neuen Namen speichern“ weiß.
- **07 Suche:** markiert in der Navigation weiter „Dokumente“ (Suchergebnisse gehören dazu, prompts.md §2).
- **09 Konto:** Lesereihenfolge Passwort → Abmelden → Eingänge (Nutzer, 2026-11-23); keine Karte „Sprache“.
- **11 Hinzufügen:** H2 je Kachel wie im freigegebenen Entwurf; die ganze Kachel ist klickbar.
- **Überschriften:** Seiten-H1 in `headline-lg` (`Typo.h2` mit `HtmlTag="h1"`), Abschnitts-H2 eine Stufe kleiner.

## Nicht aus Stitch übernommen

Tailwind, CDN, Google Fonts, Material-Symbols-Schrift (MudBlazor-Symbole statt dessen), externe Bilder,
`user-scalable=no`, klickbare `div`s, pulsierende Statuspunkte, Firmen-Funktionen (siehe Notizen je Screen).
