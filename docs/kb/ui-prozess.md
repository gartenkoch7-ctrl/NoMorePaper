# UI-Prozess: vom Stitch-Entwurf zur barrierefreien Seite

Stand 2026-11-22. Gilt für jede neue oder überarbeitete Seite. Ziel: Die Optik kommt unverändert aus dem
Stitch-Design-System; was für Tastatur, Zoom und Screenreader fehlt (AGENTS.md §2.5), ist vor der
Umsetzung bekannt und wird einmal nach festen Regeln ergänzt, statt in jeder Spur neu gesucht.

## Erkenntnisse aus den ersten sieben Stitch-Screens (2026-11-19)

**Optik:** Das Design-System „Obsidian Slate“ im Stitch-Projekt (dunkel, monochrom, Geist + JetBrains
Mono, Schriftstufen und Abstände wie in Stitch) ist **vom Nutzer freigegeben und bleibt so**
(Entscheidung 2026-11-22). Schriftgrößen, Farben und Akzent werden **nicht** angehoben oder
umgefärbt – auch nicht auf die älteren Werte aus [prompts.md](../ui/stitch/prompts.md) §1
(20 px, Gelb `#FFD600`); für die Optik gilt das Stitch-Design-System.

**Bedienung:** Was man nicht sieht, aber für Screenreader, Tastatur und Zoom nötig ist, macht Stitch
falsch und wird bei der Umsetzung ergänzt, ohne die Optik zu ändern. In allen Screens wiederkehrend:

- `outline-none` / `focus:outline-none` ohne Ersatz, `user-scalable=no` im Viewport.
- Status als farbiger oder pulsierender Punkt ohne Text; Knöpfe nur mit Symbol und `title`.
- Klickbare `div`/`span`/`tr`, Reiter und Schalter ohne Rolle, Labels ohne `for`, Tabellen aus `div`.
- Material-Symbols ohne `aria-hidden` (werden als „search“, „more_vert“ vorgelesen).
- Erfundene Firmen-Funktionen: GoBD, WORM, QES, DATEV/SKR, ZUGFeRD, S3, Office 365, OAuth/SSO,
  Registrierung, Workflows, „Konfidenz %“, GPU, Arbeitsbereich „Geschäftlich“.
- Externe Ressourcen: Tailwind-CDN, Google Fonts, Bilder von Google-Servern.

Folgerung: Das Design-System in Stitch bleibt unverändert. Neue Screens entstehen **mit diesem
Design-System** und mit einer **Negativliste** im Prompt; geprüft wird jeder Entwurf, bevor er
Vorlage wird.

## Prompt-Zusatz für neue Screens

An jeden Seiten-Prompt anhängen; das Design-System des Projekts mitgeben (`designSystem`
`assets/b3b93e10fb5c43cabf20e90f9bc6508b`), damit die Optik gleich bleibt:

```text
Keep the project's design system exactly (colors, fonts, sizes, spacing, radius).
Household app, runs only on a home server. No business, compliance, cloud or AI-score terms
(no GoBD, WORM, DATEV, ZUGFeRD, S3, Office 365, SSO, sign-up, workflows, confidence %).
Never remove focus outlines; never disable zoom. Every status = icon + word, no animation.
Native elements only: a, button, input with label, select, table. Icons aria-hidden.
```

## Feste Orte

| Was | Wo |
|---|---|
| Stitch-Projekt (einziges) | „NoMorePaper“, ID `17713397687229704853` |
| Design-Werte (Quelle der Wahrheit) | Stitch-Design-System „Obsidian Slate“ (`assets/b3b93e10fb5c43cabf20e90f9bc6508b`); Abschrift in KB `oberflaeche.md` (entsteht in M4u Schritt 0) |
| Seitenbeschreibungen und Prompts | [docs/ui/stitch/prompts.md](../ui/stitch/prompts.md) |
| Abgeholte Entwürfe | `docs/ui/stitch/NN-seitenname/` (`code.html`, `screen.png`, `notizen.md`) |
| Theme im Code | `Theme/ContrastTheme.cs`, `wwwroot/css/` |

Die Design-Werte kommen aus dem Stitch-Design-System (abrufbar per `list_design_systems`). Sie werden
nach `oberflaeche.md` abgeschrieben und von dort in `ContrastTheme.cs` und `wwwroot/css/` übertragen.
Das Design-System in Stitch wird nur geändert, wenn der Nutzer es ausdrücklich will.

## Ablauf je Seite

| Schritt | Wer | Ergebnis |
|---|---|---|
| 1. Beschreiben | Agent (Opus) | Abschnitt in `prompts.md`: Zweck, Inhalt, Aktionen, Zustände (leer, lädt, Fehler, Erfolg), **„Gibt es nicht“** – aus dem echten Code-Stand, nicht aus der Wunschliste |
| 2. Erzeugen | Nutzer im Browser oder Agent per MCP (`generate_screen_from_text`, mit Design-System des Projekts) | Screen in Stitch, Desktop und Mobil; bei Bedarf `generate_variants`, der Nutzer wählt |
| 3. Abholen | Agent (Haiku) per `download_file` | `code.html` unverändert, `screen.png`, Stitch-IDs vollständig in `notizen.md` |
| 4. Prüfen | Agent (Opus) | `notizen.md` mit **Übernehmen / Abgleich mit der App / Nicht mitnehmen (§2.5)**; Prüfliste unten |
| 5. Nachbessern | Agent per `edit_screens` | Grobe Verstöße (falsche Funktionen, Aufbau) werden **in Stitch** behoben, nicht erst im Code. Unsichtbares (ARIA, Fokus, Zoom, Elementarten) darf in „Nicht mitnehmen“ bleiben |
| 6. Freigeben | **Nutzer** | Sichtprüfung des Entwurfs (Bild groß, Zoom); Status in der Tabelle von [docs/ui/stitch/README.md](../ui/stitch/README.md) auf „freigegeben“ |
| 7. Umsetzen | nach Detailplan (§12), meist Sonnet | Blazor-Seite mit MudBlazor-Komponenten und Theme-Werten; vorhandene Bausteine zuerst |
| 8. Abnehmen | Agent (Opus) + Nutzer | axe DE/EN 0 Verstöße, Tastaturweg, Zoom 200 %/400 %, Playwright-Bild neben `screen.png` in `TestResults/`; NVDA/VoiceOver durch den Nutzer |

Status je Screen in der README-Tabelle: **Entwurf → geprüft → freigegeben → umgesetzt**. Umgesetzt wird
nur, was „freigegeben“ ist.

## Prüfliste für Schritt 4

Automatisch per Textsuche im `code.html` (Treffer = Eintrag in „Nicht mitnehmen“):
- `user-scalable=no`, `maximum-scale` – Zoom gesperrt
- `outline-none` – Fokusrahmen entfernt
- `animate-ping`, `animate-pulse` – Bewegung als Status
- `onclick=` an `div`/`span`, `cursor-pointer` an `tr`/`div`
- `title=` ohne `aria-label` an Knöpfen mit nur Symbol; `material-symbols` ohne `aria-hidden`
- `truncate` – schneidet Titel und Adressen ab
- `cdn.`, `fonts.googleapis`, `googleusercontent` – externe Ressourcen (ADR-0002)

Von Hand:
- Genau eine H1, H2-Abschnitte, „Zum Inhalt springen“
- Status immer Symbol **und** Text; keine Modaldialoge (Rückfrage in der Seite)
- Jede Funktion im Entwurf gibt es in der App oder sie steht im Plan – sonst „Abgleich“
- Farben, Schrift und Abstände entsprechen dem Design-System „Obsidian Slate“ (keine Abweichung
  nach oben oder unten)

## Regeln

- Stitch-HTML ist **Vorlage, nie Produktcode**: übernommen werden Aufbau, Abstände, Radien,
  Knopf-Hierarchie. Tailwind, CDN, Google Fonts und externe Bilder kommen nie in den Build.
- **Keine neuen Funktionen aus Entwürfen**, außer sie stehen in `notizen.md` unter „Übernehmen“ **und**
  im Meilensteinplan.
- Bedienlogik (Live-Regionen, Fokusführung, ARIA, Kürzel „/“) kommt aus dem bestehenden Code, nie aus
  dem Entwurf.
- Ein Entwurf, der den Nutzer nicht überzeugt, wird **neu erzeugt**, nicht im Code „schöngebaut“.
- Stitch-Projekte außer „NoMorePaper“ werden nicht angelegt; Versuche laufen als Varianten im Projekt.
