# 07 – Suche (Stitch-Entwurf „Suche v2“, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `cb0de0fd14044036aabc6ced8220501d`
  („Suche v2“), HTML-Datei `projects/17713397687229704853/files/23a574f7ecfd4c9f99238d1e980a1924`
- **Ersetzt:** Screen `4cea95ccb94d46339afc4fe15f79d023` (erste Fassung). Die Nachbesserung per
  `edit_screens` blieb dort nicht gespeichert, daher neu erzeugt.
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/Search.razor` (`/suche?q=…`)

## Übernehmen (passt zur App)

- **H1 „Suche“ + Zusammenfassung** „7 Treffer für „Versicherung““ direkt darunter (heute
  `search-summary`, Text wie `SearchCount`, ohne Zusatz).
- **Karte je Treffer** (`article`): Miniatur links, H2-Titel als Link, Abzeichen „Privat“ mit
  Schloss-Symbol und Text, Textausschnitt, darunter Zeile „Eingegangen am …“.
- **Abstände, Radien, Flächen** der Karten (`surface-container-low`, Hover eine Stufe heller).
- Suchfeld im Kopf mit Suchbegriff gefüllt – passt zu `HeaderSearch`.
- Ergebnisliste als `section aria-label="Suchergebnisse"`, nur ein `main`.

## Abgleich mit der App

| Im Entwurf | In NoMorePaper |
|---|---|
| „Eingegangen am 03.03.2026, 14:32 **über Mail / Scanner / Upload**“ | Herkunft gibt es im Treffer nicht (`SearchHitDto`); nur „Eingegangen am <Datum Uhrzeit>“ (`SearchReceived`) |
| Suchbegriff im Ausschnitt fett und weiß (`strong`) | Ausschnitt ist reiner Text vom Server, keine Hervorhebung |
| Gezeichnete Platzhalter-Miniatur („HUK 01“) | echte Miniatur `Thumbnail` aus dem PDF/A, invertiert |
| Navigation: „Dokumente“ als aktuelle Seite (`aria-current`) | Suche ist kein Hauptpunkt; kein `aria-current` |
| fehlt | Leerzustand „Gib oben einen Suchbegriff ein (Taste /).“ (`SearchPrompt`) |
| fehlt | Fehlerzustand (`role="alert"`, Fehlerbox) und Ansage „Suche läuft …“ per `LiveRegion` |
| fehlt | 0 Treffer: sichtbare Zusammenfassung „0 Treffer für …“ |

Erledigt gegenüber der ersten Fassung: Filter-Chips, Sortierung, Seitenblättern, Knöpfe
„Herunterladen“/„Details“, Tag-Abzeichen, Ordnerpfad, Dateigröße/Seitenzahl, Statuszeilen
„SSD RAID1“/„SQLite FTS5“ und „… im lokalen Archiv“ sind entfernt.

## Beim Übernehmen nicht mitnehmen (§2.5)

- `outline-none` am Suchfeld; Ersatz nur `focus:bg-surface-container` (kaum sichtbar). In der App
  bleibt der Fokus wie im Design-System, kein eigener Rahmen (ADR-0026).
- Zweites `<header>` im Inhalt (um H1 und Zusammenfassung) – in der App H1 direkt.
- `line-clamp-2` schneidet den Ausschnitt ab.
- Platzhalter-Miniatur mit 10-px-Text (`label-sm`) – entfällt durch `Thumbnail`.
- Material-Symbols ohne `aria-hidden` (Navigation, Kopf, Logo, Avatar).
- Sprachumschalter „DE/EN“ als Knöpfe ohne Zustand (`aria-pressed` fehlt); „Hochladen“ als
  `button` statt Link `hochladen`.
- Zwei `nav` ohne `aria-label`.
- Kein „Zum Inhalt springen“ (kommt aus `MainLayout`).
- Tailwind-CDN und Google Fonts (`fonts.googleapis`, `cdn.tailwindcss.com`) – ADR-0002.

## Schrift (Mono wirkt im Bild serifig)

`label-sm`, `label-md`, `label-lg` sind im Tailwind-Config als `["JetBrains Mono"]` definiert, und
JetBrains Mono wird per Google-Fonts-Link (`family=JetBrains+Mono:wght@100..900`) geladen. Die
Serifenschrift im `screen.png` („HOME STORAGE“, „Privat“, „DE/EN“, „Angemeldet als“,
„Eingegangen am …“, Miniatur) ist daher ein **Darstellungsartefakt** des Stitch-Screenshots: Die
Schrift war beim Rendern nicht geladen, und weil die Liste **keine Ausweichschrift** (`monospace`)
hat, fiel der Browser auf seine Standard-Serifenschrift zurück. Keine echte Abweichung vom
Design-System. In der App Ausweichschrift `monospace` mitgeben.

## Nachbessern in Stitch?

Ja, kleine Punkte (Regel: jede UI-Änderung zuerst in Stitch):
- Fußzeile je Treffer auf „Eingegangen am 03.03.2026, 14:32“ kürzen (ohne „über …“).
- Hervorhebung des Suchbegriffs im Ausschnitt entfernen (reiner Text).
- In der Navigation keinen Punkt als aktiv markieren („Dokumente“ nicht aktuell).

Design-System (Farben, Geist/JetBrains Mono, Stufen) eingehalten.

Status: freigegeben durch den Nutzer (2026-11-23); Restpunkte werden beim Umsetzen behoben
