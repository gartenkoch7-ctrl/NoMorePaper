# M4u – Neue Oberfläche nach den Stitch-Entwürfen

**Ziel:** Die App bekommt ein einheitliches, ruhigeres Erscheinungsbild nach den Stitch-Entwürfen in
[docs/ui/stitch/](../ui/stitch/README.md): Seitenleiste statt zehn Kopf-Links, neues Theme (Farben,
Abstände, Radien, Typografie), überarbeitete Seiten Anmelden, Dokumente, Dokument ansehen und Eingänge.
Barrierefreiheit (AGENTS.md §2.5) geht immer vor dem Entwurf.
**Demo:** Anmelden im neuen Design → Seitenleiste mit fünf Hauptpunkten, „Zu prüfen (n)“ mit Zähler →
Dokumente mit Art-Chips, Tabelle mit Status als Symbol + Text, Blättern → Dokument öffnen: Viewer links,
Reiter „Angaben“ / „Erkannter Text“ / „Verlauf“ rechts → Eingänge mit Reitern je Quelle. Alles per Tastatur
und NVDA bedienbar, axe 0 Verstöße.
**Voraussetzungen:** M4 (Seite „Eingänge“). **Größe:** M.
**Grundlage:** [Stitch-Notizen](../ui/stitch/README.md), [prompts.md](../ui/stitch/prompts.md),
ADR-0010, ADR-0011.

Grundsätze (aus den Notizen je Screen):
- Übernommen werden **Aufbau, Farben, Abstände, Radien, Typografie** – nie das Stitch-HTML selbst
  (kein Tailwind-CDN, keine Google Fonts, keine externen Bilder; ADR-0002)
- Was die App nicht hat (GoBD, WORM, QES, DATEV, Cloud-Speicher, OAuth, Workflows …), fällt weg – Liste
  „Abgleich mit der App“ in jeder `notizen.md`
- Bedienlogik (Live-Regionen, Fokusführung, ARIA, Tastenkürzel „/“) bleibt wie im bestehenden Code
- Keine neuen Funktionen aus den Entwürfen, außer den in den Notizen als „Übernehmen“ markierten
  (z. B. Reiter „Erkannter Text“, Art-Chips mit Anzahl, Blättern)

## Aufgaben

### 0. Gemeinsame Bausteine (Schritt 0)
- [x] Design-Werte aus den vier Entwürfen sammeln (Farben, Abstände, Radien, Schriftstufen) sammeln und
      unverändert übernehmen – Design-System „Obsidian Slate“ ist vom Nutzer freigegeben (2026-11-22), keine
      Anhebung auf 20 px / Gelb; nur Fokusrahmen sichtbar und Zoom erlaubt; Ergebnis in KB `oberflaeche.md`
- [x] `Theme/ContrastTheme.cs` auf die neuen Werte; `wwwroot/css/app.css` (247 Zeilen) **jetzt** nach
      Verantwortung aufteilen (Basis/Fokus, Layout, Bausteine, Viewer)
- [x] Bausteine aus prompts.md §5 als Komponenten: Statuszeile, Abzeichen, Fehler-/Erfolgs-/Rückfrage-Box,
      Reiter mit `role="tab"` + Pfeiltasten, Kopierfeld – vorhandene wiederverwenden, nur fehlende neu
- [x] Navigation nach prompts.md §2: Seitenleiste mit Start, Hinzufügen, Dokumente, Zu prüfen (n),
      Einstellungen; mobil Knopf „Menü“ mit Text; Kopf mit Suche, Hochladen, Konto (`MainLayout.razor`,
      `HeaderSearch.razor`, `AccountMenu.razor`)
- [x] Unterseite „Einstellungen“ bündelt Konto, Eingänge, Listen, Mitglieder (Admin); Routen bleiben gleich

### A. Anmelden (Screen 01)
- [x] `SignIn.razor` nach Entwurf: Logo lokal, Karte, Benutzername statt E-Mail, Link „Mit Notfall-Code
      anmelden“; dieselbe Gestaltung für Ersteinrichtung, Einladung, Passwort zurücksetzen, Notfall

### B. Dokumente (Screen 02)
- [x] `Documents.razor`: Überschrift mit Anzahl, „Zuletzt bearbeitet“ als Karten (entfallen, F6-A), Art-Chips mit Anzahl,
      Tabelle (Titel + Textausschnitt, Art/Tags, Absender, Datum + Eingangsweg, Betrag, Status, Erkennung),
      Abzeichen „Privat“, Blättern mit „Zeilen pro Seite“
- [x] `DocumentFilterBar.razor`: nur eine Suche (im Kopf); Filter Datum, Betrag, Sichtbarkeit, Absender
- [x] Umschalter Tabelle / Kacheln (Kacheln mit invertierter Miniatur) – ⚠️ nur, wenn API Miniaturen liefert,
      sonst nach M4b verschieben

### C. Dokument ansehen (Screen 03)
- [x] `DocumentViewer.razor`: Zurück-Leiste mit Pfad, Kopf mit Titel und Aktionen, zwei Spalten (7/12 · 5/12),
      Viewer-Werkzeugleiste mit Text oder zugänglichem Namen je Knopf
- [x] Reiter „Angaben“, **„Erkannter Text“** (OCR-Text lesbar, kopierbar), „Verlauf“ (Zeitleiste)
- [x] `DocumentFieldsPanel/Editor.razor`: Zusammenfassung zuerst, Hinweis „Automatisch erkannt“, Tags als
      Chips mit Knopf „entfernen“, Bestätigen, Privat, Bearbeiter, Downloads Original/Archiv getrennt

### D. Eingänge (Screen 04)
- [x] `Inputs.razor`/`InputsEntry.razor`: Reiter je Eingang mit Anzahl, Karte je Quelle (Status als
      Abzeichen mit Text, „Zuletzt gelaufen“ / „Zuletzt erfolgreich“, Knöpfe)
- [x] `MailSettings.razor`: Postfach-Karte, Regelliste (Spam-Prüfung, Spam-Ordner, Absender je Konto);
      Platz für iCloud-Abschnitt (kommt mit M4 Spur D)

### E. Restliche Seiten angleichen
- [ ] Neue Stitch-Entwürfe für Start, Hinzufügen (Hochladen + Kamera), Zu prüfen, Suche, Listen, Konto,
      Mitglieder (Prompts in prompts.md §3) – **Nutzer** erzeugt sie in Stitch, Ablage wie 01–04
- [ ] Diese Seiten aufs neue Theme und die Bausteine umstellen (ohne Neuaufbau, wo kein Entwurf da ist)
- [ ] Entwürfe für M4b vorziehen: „Seiten bearbeiten“, „Papierkorb“, „Fassungen“ (prompts.md §4.1–4.3)
- [ ] Rückfragen (Review, Members, ListSection) auf `Design/ConfirmBox` umstellen, mit UI-Test

### F. Tests und Doku
- [x] UI-Tests: axe DE/EN auf allen geänderten Seiten, Tastatur (Seitenleiste, Reiter, Menü mobil),
      bestehende Playwright-Tests an neue Struktur anpassen
- [x] KB `oberflaeche.md` (Design-Werte, Bausteine, was aus Stitch nicht übernommen wird); Katalog: eigene Logs der Spuren im Katalog
      eingetragen (Abläufe und Meldungen der Spuren)

## Akzeptanzkriterien
- [ ] Vier Seiten (01–04) entsprechen im Aufbau dem Entwurf, ohne die Punkte aus „nicht mitnehmen“
- [ ] Farben, Schrift und Abstände wie im Stitch-Design-System; Fokusrahmen sichtbar, Zoom erlaubt
      (kein `user-scalable=no`)
- [x] Keine externen Ressourcen (Fonts, CDN, Bilder) im Build
- [ ] axe 0 Verstöße DE/EN; NVDA-Prüfung Navigation, Dokumente, Viewer, Eingänge durch den Nutzer
- [ ] iPhone (PWA): Menü, Viewer und Eingänge bedienbar mit VoiceOver
- [ ] CI grün

## Parallelisierung

Integrationsbranch `feat/m04u-oberflaeche` von `develop` (die Stitch-Ablage aus `feat/ui-stitch` zuerst
nach `develop` mergen – nur Doku).

| Spur | Inhalt | Branch | Start | wartet auf |
|---|---|---|---|---|
| 0 | Theme, CSS-Aufteilung, Bausteine, Navigation | `feat/m04u-oberflaeche` | sofort | – |
| A | Anmelden + Konto-Seiten | `feat/m04u-a-anmelden` | nach 0 | – |
| B | Dokumente | `feat/m04u-b-dokumente` | nach 0 | – |
| C | Dokument ansehen | `feat/m04u-c-viewer` | nach 0 | – |
| D | Eingänge | `feat/m04u-d-eingaenge` | nach 0 | – |
| E | Restliche Seiten | `feat/m04u-e-rest` | nach 0 + neue Entwürfe | Nutzer (Stitch) |

```
0 ──┬─► A ─┐
    ├─► B ─┤
    ├─► C ─┼─► Merge A, D, B, C, E ─► Gesamt-Review
    ├─► D ─┤
    └─► E ─┘ (sobald Entwürfe da sind)
```

- **Exklusiv-Dateien:** A `SignIn/Setup/AcceptInvitation/ResetPassword/EmergencyReset.razor`; B
  `Documents.razor`, `DocumentFilterBar.razor`; C `DocumentViewer.razor(.cs)`, `DocumentFields*.razor`,
  `Viewer/`, `TagPicker.razor` und `.tag-list*` in `classification.css`; D `Inputs*`, `MailSettings*`; E übrige Seiten
- **Hotspots** (nur ergänzen): `Strings*.resx`, CSS-Bausteindatei, UI-Tests-Hilfen
- **Nach Schritt 0 gesperrt:** `ContrastTheme.cs`, `MainLayout.razor` – Änderungen nur noch in Spur 0
- **Merge-Reihenfolge:** A → D → B → C → E

### Verhältnis zu M4b
- M4b **Schritt 0, Spur A, D** und der Server-Teil von **C** sind reine Backend-Arbeit und laufen
  **gleichzeitig** mit M4u (keine gemeinsamen Dateien)
- M4b **Spur B** (Seiten bearbeiten, Fassungen) und die **Seite `/papierkorb`** starten erst nach dem
  Merge von M4u Spur C nach `develop` – sie teilen `DocumentViewer.razor` und `MainLayout.razor` und sollen
  gleich im neuen Design entstehen

## Detailplan

Spuren: [A](features/m4u-a-anmelden-detail.md) · [B](features/m4u-b-dokumente-detail.md) ·
[C](features/m4u-c-viewer-detail.md) · [D](features/m4u-d-eingaenge-detail.md). Offene Fragen aller vier Pläne
vom Nutzer am 2026-11-22 **alle wie empfohlen** beantwortet; Entwürfe 01–04 freigegeben.

Nach [Vorlage](detailplan-vorlage.md). **Erstellt von:** Opus · **Datum:** 2026-11-22. Spuren A–E bekommen
ihren Detailplan erst nach Schritt 0 (sie hängen an den Bausteinen).

Entschieden (Nutzer, 2026-11-22):
- Optik **unverändert** aus dem Stitch-Design-System „Obsidian Slate“ (Farben, Schriftstufen, Abstände,
  Radien) – keine Anhebung auf 20 px, kein Gelb
- **Kein eigener Fokusrahmen**: Fokus sieht aus wie im Entwurf (Feld: Rand wird weiß); die Bildschirmlupe des
  Nutzers zeichnet Fokus selbst. Der globale gelbe `:focus-visible`-Rahmen entfällt
- Schriften **Geist** und **JetBrains Mono** lokal als WOFF2 (OFL 1.1) in `wwwroot/fonts/` mit Lizenz

### Einschätzung Schritt 0

| Punkt | Umfang | Risiko | Neuheit | Klarheit | Prüfbarkeit | Stufe |
|---|---|---|---|---|---|---|
| Design-Werte → KB | 1 Datei | – | – | entschieden | Lesen | 1 |
| Theme + CSS-Aufteilung | ~8 Dateien, ~600 Zeilen | Optik aller Seiten ändert sich | Muster | entschieden | UI-Tests + Sichtprüfung | 2 |
| Bausteine | ~10 Dateien, ~500 Zeilen | ARIA (Reiter) | teils Muster | entschieden | Unit + UI-Tests | 2 |
| Navigation + Seiten Hinzufügen/Einstellungen | ~10 Dateien, ~500 Zeilen | bestehende UI-Tests | Muster | entschieden | UI-Tests | 2 |

### P0.1 Theme und CSS · Opus (steuernde Sitzung)

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `docs/kb/oberflaeche.md` | neu | Abschrift des Design-Systems: Farben, Schriftstufen, Abstände, Radien, Bausteine; was aus Stitch nicht übernommen wird | – | 120 |
| `wwwroot/fonts/{geist,jetbrains-mono}/*` | neu | WOFF2 + `LICENSE.txt` (OFL 1.1) | – | – |
| `Theme/ContrastTheme.cs` | ändern | Palette aus „Obsidian Slate“ (Hintergrund `#131313`, Fläche `#121212`/`#1c1b1b`, Text `#e5e2e1`, Primär `#ffffff` mit Text `#0a0a0a`, Linien `#27272a`/`#3f3f46`, Fehler `#ffb4ab`); Typografie Geist, `body-md` 0,875 rem, `body-lg` 1 rem, H1 2,5 rem/600, H2 1,75 rem/600, H3 1,25 rem/500; Radius 8 px | Build | 90 |
| `wwwroot/css/base.css` | neu (aus `app.css`) | `@font-face`, Tokens `--nmp-*` aus dem Design-System, `html/body`, Links, Sprunglink, `#blazor-error-ui`; **kein** globaler Fokusrahmen | – | 110 |
| `wwwroot/css/layout.css` | neu (aus `app.css`) | Seitenleiste, Kopf, Inhalt, Menü mobil (< 1024 px) | UI-Tests | 160 |
| `wwwroot/css/components.css` | neu (aus `app.css`) | Felder, Ablagefläche, Live-Region, Fehlerbox, Tabelle, Suchtreffer | – | 150 |
| `wwwroot/css/app.css` | löschen | Inhalt aufgeteilt | – | – |
| `wwwroot/css/viewer.css` | ändern | nur Kommentare (der Viewer hatte keine eigene Fokusfarbe, nur einen Versatz) | – | 2 |
| `wwwroot/index.html` | ändern | CSS-Dateien in Reihenfolge base → layout → components → Feature-CSS; Hintergrund vor dem Laden `#131313` | – | 5 |

### P0.2 Bausteine · Sonnet `medium` (Subagent)

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Design/StatusText.razor`, `Design/StatusKind.cs` | neu | Symbol + Wort; `StatusKind { Waiting, Processing, Done, Failed, Recognized }`; Symbol `aria-hidden`, Wort als Text | UI-Test in der Spur, die ihn zuerst nutzt | 40 + 15 |
| `Design/Badge.razor` | neu | Chip nach Design-System (Fläche `#18181b`, Rand `#27272a`, `label-sm`), Symbol optional, Text Pflicht | – | 30 |
| `Design/NoticeBox.razor`, `Design/NoticeKind.cs` | neu | `Error`/`Success`; Rahmen, Symbol + Satz, `ChildContent` für Knopf/Link; Fehler `role="alert"` nur wenn `Announce` | – | 50 |
| `Design/ConfirmBox.razor` | neu | in der Seite, Überschrift (Ebene per `HeadingLevel`, Standard H2), Erklärung, **„Abbrechen“ zuerst**, dann Aktion; Fokus beim Öffnen auf die Überschrift (`tabindex=-1`) | – | 60 |
| `Design/TabList.razor`, `Design/TabNavigation.cs` | neu | `role="tablist"`, Knöpfe `role="tab"`, `aria-selected`, `aria-controls`, roving `tabindex`; Pfeil links/rechts, Pos1/Ende über reine Logik `TabNavigation.Next(int current, int count, string key) → int` | `tests/NoMorePaper.Web.Client.Tests/Design/TabNavigationTests.cs` | 90 + 30 + 60 |
| `wwwroot/css/design.css` | neu | Stile der Bausteine nach Design-System | – | 120 |
| `Strings.resx`, `Strings.en.resx` | ändern (Hotspot) | Wörter der Statusarten, „Abbrechen“ (vorhandene Schlüssel wiederverwenden) | – | 20 |

### P0.3 Navigation · Opus (steuernde Sitzung, nach P0.2)

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Layout/NavSection.cs`, `Layout/NavItem.cs` | neu | rein: `enum NavItem { Start, Add, Documents, Review, Settings }` (eigene Datei); `static NavItem? For(string relativePath)` (hochladen/kamera/hinzufuegen → Add; dokumente*/suche → Documents; konto/eingaenge*/listen/mitglieder/einstellungen → Settings) | `Web.Client.Tests/Layout/NavSectionTests.cs` | 50 + 60 |
| `Layout/MainLayout.razor` | ändern | Seitenleiste (`<nav>` mit 5 Links, Symbol `aria-hidden` + Text, `aria-current`), Kopf mit Suche, Knopf „Hochladen“, Konto, Sprache; mobil Knopf „Menü“ mit Text, `aria-expanded`/`aria-controls` | UI-Tests | 140 |
| `Layout/SideNav.razor` | neu | Seitenleiste aus `MainLayout` ausgelagert | – | 80 |
| `Pages/Add.razor` | neu | `/hinzufuegen`: H1, zwei große Links „Hochladen“, „Kamera“ mit Erklärung | UI-Test | 40 |
| `Pages/Settings.razor` | neu | `/einstellungen`: H1, Links Konto, Eingänge, Listen, Mitglieder (Admin) mit Erklärung | UI-Test | 50 |
| `Strings*.resx` | ändern | `NavAdd`, `NavSettings`, `Menu`, Texte der zwei Seiten | – | 40 |
| UI-Tests | ändern | `InputsPageTests` (aktuell = „Einstellungen“), `MembersTests`, `RealApiAccountTests` (Mitglieder über `/einstellungen`), `LanguageSwitchTests`; neu `Layout/NavigationTests.cs` (aktueller Punkt, Menü mobil, axe DE/EN) | selbst | 150 |

- **Reihenfolge:** P0.1 → (P0.2 ∥ KB-Datei) → P0.3 → Build, alle Tests, Zeilenprüfung → Review-Schleife §8.
- **Telemetrie:** keine neuen Abläufe (nur Darstellung); Seitenaufrufe der zwei neuen Seiten wie bestehende
  Seiten (keine eigenen Logs).
- **Akzeptanz:** Build ohne Warnungen; alle Tests grün; keine externen Ressourcen; Sichtprüfung durch den
  Nutzer nach Schritt 0. Die Bausteine aus P0.2 werden in Schritt 0 noch nicht benutzt; ihre UI-Tests
  (axe, Tastatur, Ansage) kommen mit der ersten Spur, die sie einbaut (A–D).

## Offene Fragen / Risiken
- **Screen 03:** `screen.png` und Stitch-Screen-ID fehlen noch (siehe Notizen)
- **Stitch-Werte:** Schriftgrößen und Farben bleiben wie im Entwurf (Nutzerentscheidung 2026-11-22);
  kein eigener Fokusrahmen (ADR-0026); der Browser-Standard bleibt; Sichtprüfung durch den Nutzer nach
  Schritt 0
- [x] **MudBlazor-Grenzen (erledigt):** Seitenleiste und Reiter möglichst mit MudBlazor-Komponenten, ARIA prüfen;
  wo MudBlazor nicht reicht, eigene schlanke Komponente
- [x] **Miniaturen (erledigt)** (Kacheln, Zuletzt bearbeitet) brauchen Vorschaubilder – kommen sonst erst mit M4b

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|
