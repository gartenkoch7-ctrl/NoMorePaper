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
- [ ] Design-Werte aus den vier Entwürfen sammeln (Farben, Abstände, Radien, Schriftstufen) sammeln und
      unverändert übernehmen – Design-System „Obsidian Slate“ ist vom Nutzer freigegeben (2026-11-22), keine
      Anhebung auf 20 px / Gelb; nur Fokusrahmen sichtbar und Zoom erlaubt; Ergebnis in KB `oberflaeche.md`
- [ ] `Theme/ContrastTheme.cs` auf die neuen Werte; `wwwroot/css/app.css` (247 Zeilen) **jetzt** nach
      Verantwortung aufteilen (Basis/Fokus, Layout, Bausteine, Viewer)
- [ ] Bausteine aus prompts.md §5 als Komponenten: Statuszeile, Abzeichen, Fehler-/Erfolgs-/Rückfrage-Box,
      Reiter mit `role="tab"` + Pfeiltasten, Kopierfeld – vorhandene wiederverwenden, nur fehlende neu
- [ ] Navigation nach prompts.md §2: Seitenleiste mit Start, Hinzufügen, Dokumente, Zu prüfen (n),
      Einstellungen; mobil Knopf „Menü“ mit Text; Kopf mit Suche, Hochladen, Konto (`MainLayout.razor`,
      `HeaderSearch.razor`, `AccountMenu.razor`)
- [ ] Unterseite „Einstellungen“ bündelt Konto, Eingänge, Listen, Mitglieder (Admin); Routen bleiben gleich

### A. Anmelden (Screen 01)
- [ ] `SignIn.razor` nach Entwurf: Logo lokal, Karte, Benutzername statt E-Mail, Link „Mit Notfall-Code
      anmelden“; dieselbe Gestaltung für Ersteinrichtung, Einladung, Passwort zurücksetzen, Notfall

### B. Dokumente (Screen 02)
- [ ] `Documents.razor`: Überschrift mit Anzahl, „Zuletzt bearbeitet“ als Karten, Art-Chips mit Anzahl,
      Tabelle (Titel + Textausschnitt, Art/Tags, Absender, Datum + Eingangsweg, Betrag, Status, Erkennung),
      Abzeichen „Privat“, Blättern mit „Zeilen pro Seite“
- [ ] `DocumentFilterBar.razor`: nur eine Suche (im Kopf); Filter Datum, Betrag, Sichtbarkeit, Absender
- [ ] Umschalter Tabelle / Kacheln (Kacheln mit invertierter Miniatur) – ⚠️ nur, wenn API Miniaturen liefert,
      sonst nach M4b verschieben

### C. Dokument ansehen (Screen 03)
- [ ] `DocumentViewer.razor`: Zurück-Leiste mit Pfad, Kopf mit Titel und Aktionen, zwei Spalten (7/12 · 5/12),
      Viewer-Werkzeugleiste mit Text oder zugänglichem Namen je Knopf
- [ ] Reiter „Angaben“, **„Erkannter Text“** (OCR-Text lesbar, kopierbar), „Verlauf“ (Zeitleiste)
- [ ] `DocumentFieldsPanel/Editor.razor`: Zusammenfassung zuerst, Hinweis „Automatisch erkannt“, Tags als
      Chips mit Knopf „entfernen“, Bestätigen, Privat, Bearbeiter, Downloads Original/Archiv getrennt

### D. Eingänge (Screen 04)
- [ ] `Inputs.razor`/`InputsEntry.razor`: Reiter je Eingang mit Anzahl, Karte je Quelle (Status als
      Abzeichen mit Text, „Zuletzt gelaufen“ / „Zuletzt erfolgreich“, Knöpfe)
- [ ] `MailSettings.razor`: Postfach-Karte, Regelliste (Spam-Prüfung, Spam-Ordner, Absender je Konto);
      Platz für iCloud-Abschnitt (kommt mit M4 Spur D)

### E. Restliche Seiten angleichen
- [ ] Neue Stitch-Entwürfe für Start, Hinzufügen (Hochladen + Kamera), Zu prüfen, Suche, Listen, Konto,
      Mitglieder (Prompts in prompts.md §3) – **Nutzer** erzeugt sie in Stitch, Ablage wie 01–04
- [ ] Diese Seiten aufs neue Theme und die Bausteine umstellen (ohne Neuaufbau, wo kein Entwurf da ist)
- [ ] Entwürfe für M4b vorziehen: „Seiten bearbeiten“, „Papierkorb“, „Fassungen“ (prompts.md §4.1–4.3)

### F. Tests und Doku
- [ ] UI-Tests: axe DE/EN auf allen geänderten Seiten, Tastatur (Seitenleiste, Reiter, Menü mobil),
      bestehende Playwright-Tests an neue Struktur anpassen
- [ ] KB `oberflaeche.md` (Design-Werte, Bausteine, was aus Stitch nicht übernommen wird); Katalog: keine
      neuen Abläufe, nur Seitenaufrufe prüfen

## Akzeptanzkriterien
- [ ] Vier Seiten (01–04) entsprechen im Aufbau dem Entwurf, ohne die Punkte aus „nicht mitnehmen“
- [ ] Farben, Schrift und Abstände wie im Stitch-Design-System; Fokusrahmen sichtbar, Zoom erlaubt
      (kein `user-scalable=no`)
- [ ] Keine externen Ressourcen (Fonts, CDN, Bilder) im Build
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
  `Viewer/`; D `Inputs*`, `MailSettings*`; E übrige Seiten
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
Folgt vor der Umsetzung nach [Vorlage](detailplan-vorlage.md) (Einschätzung je Punkt, Modell, Effort).

## Offene Fragen / Risiken
- **Screen 03:** `screen.png` und Stitch-Screen-ID fehlen noch (siehe Notizen)
- **Stitch-Werte:** Schriftgrößen und Farben bleiben wie im Entwurf (Nutzerentscheidung 2026-11-22);
  nur `outline-none` wird durch einen sichtbaren Fokusrahmen ersetzt; Sichtprüfung durch den Nutzer nach
  Schritt 0
- **MudBlazor-Grenzen:** Seitenleiste und Reiter möglichst mit MudBlazor-Komponenten, ARIA prüfen;
  wo MudBlazor nicht reicht, eigene schlanke Komponente
- **Miniaturen** (Kacheln, Zuletzt bearbeitet) brauchen Vorschaubilder – kommen sonst erst mit M4b

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|
