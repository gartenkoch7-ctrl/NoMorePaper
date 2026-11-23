# 13 – Kamera (Stitch-Entwurf, mobil, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `da911ff1bb294a289cdf8c56384b81a1`,
  HTML-Datei `projects/17713397687229704853/files/5ec262815018456db5a51438f73c8f06`
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/Camera.razor` (+ `Camera.razor.cs`,
  `Camera.Done.cs`), Route `/kamera`
- **Prompt:** vom Agenten am 2026-11-23 aus [prompts.md](../prompts.md) §3.3 erzeugt

## Übernehmen (passt zur App)

- Reihenfolge: H1, Einleitung, Schalter, Kamerabild, Hinweiszeile, Knöpfe, H2 „Aufgenommene Seiten“,
  Erfolgsbox – wie `Camera.razor`.
- **Schalter** als echte Checkboxen mit umschließendem `label`, gemeinsam auf einer Fläche
  `surface-container-low`.
- **Hinweiszeile** unter dem Bild groß (`headline-sm`, weiß, Symbol + Text) auf `surface-container` –
  passt zu `camera-hint` („Hinweis: …“).
- **Knöpfe** untereinander, volle Breite, mind. 44 px hoch; „Aufnehmen“ weiß (primär), die anderen
  `surface-container`.
- **Erfolgsbox** `surface-container-high` mit Symbol, Satz und unterstrichenem Link darunter.
- Weiße Eckwinkel + Umriss als Anzeige „Blatt erkannt“ (monochrom, kein Akzent – passt zum Design-System).

## Abgleich mit der App

| Im Entwurf | In NoMorePaper |
|---|---|
| H1 „Mit Kamera scannen“ | „Mit der Kamera aufnehmen“ (`CamHeading`) |
| Einleitung, 1 kurzer Satz | `CamIntro` (3 Sätze: dunkle Fläche, Ansagen, Auto-Auslöser, mehrere Seiten = ein Dokument) |
| „Töne bei Hinweisen“, ohne Erklärung | „Töne“ + Hinweis darunter (`CamTonesHint`); „Privat“ mit Hinweis (`PrivateSwitch`, `CamPrivateHint`) |
| fehlt | Knopf „Kamera starten“ (Zustand vor dem Start) und Ladehinweis `CamStarting` |
| Kamerabild ohne Rahmen, Tischfoto als Hintergrund | `video` mit 3 px weißem Rahmen, für Screenreader verborgen |
| Abzeichen „ERKANNT“ im Bild | gibt es nicht; Zustand kommt als Hinweis (`CamHintHold` „Ruhig halten.“ usw.) |
| „Hinweis: Etwas näher heran“ | „Hinweis: Näher ran.“ (`CamHintLabel` + `CamHintCloser`) |
| „Aufnehmen“ / „Verwerfen“ | „Auslösen“ (`CamCapture`) / „Letzte Seite verwerfen“ (`CamDiscard`) |
| Seitenliste als `div`-Karten mit Symbol, „DIN A4 • Scan“, „✓ Bereit“, Zähler „2 SEITEN“ | nummerierte `ol` mit „Seite 1“, „Seite 2“; Format, Status und Zähler gibt es nicht |
| fehlt | Leerzustand „Noch keine Seite aufgenommen.“ (`CamNoPages`) |
| „Dokument mit 2 Seiten gespeichert“ | „Das Dokument wurde geteilt/privat gespeichert und wartet auf die Verarbeitung. Seiten: 2.“ (`CamSavedShared`/`CamSavedPrivate`) |
| Link „In der Liste zeigen“ | „Neues Dokument in der Dokumentenliste zeigen“; bei Duplikat „Vorhandenes Dokument ansehen“ mit Info-Symbol |
| fehlt | Fehlerbox mit Symbol und Link „Stattdessen eine Datei hochladen“ (`CamUploadInstead`); im Prompt verlangt |
| Kopf: Logo + „HEIMSERVER“, Knopf „Menü“, Avatar ohne Namen | Kopf aus `MainLayout` (Knopf `menu-toggle` mit `aria-expanded`, `AccountMenu`) |

## Beim Übernehmen nicht mitnehmen (§2.5)

- `maximum-scale=1.0, user-scalable=no` im Viewport – Zoom gesperrt.
- `lang="en"` bei deutschem Inhalt (Sprache kommt aus der App).
- Material-Symbols im Kopf ohne `aria-hidden` („description“, „menu“, „person“); im Inhalt korrekt.
- Seitenliste aus `div` statt `ol`/`li`.
- Tischfoto von `lh3.googleusercontent.com`, Tailwind-CDN, Google Fonts – ADR-0002.
- Festes Kopfband (`fixed`, `backdrop-blur`) frisst bei 200/400 % Zoom Höhe – Kopf kommt aus `MainLayout`.
- `::-webkit-scrollbar{display:none}` – Bildlaufleiste bleibt sichtbar.
- Kein „Zum Inhalt springen“ (kommt aus `MainLayout`).
- Statusabzeichen „ERKANNT“ in 10 px (`label-sm`) über dem Bild – entfällt (s. Abgleich).

## Schrift

`label-sm/-md/-lg` sind `["JetBrains Mono"]`, geladen per Google Fonts; im `screen.png` korrekt als Mono
dargestellt, kein Serifen-Artefakt. Einige Spans haben zusätzlich `font-mono` (Tailwind-Standard) – ohne
Wirkung auf die Optik, nicht übernehmen.

## Nachbessern in Stitch?

Ja (Regel: jede UI-Änderung zuerst in Stitch; Nachbessern als neuer Screen „Kamera v2“):
- Texte auf App-Stand: H1, Einleitung, „Töne“ + Hinweis, Privat-Hinweis, „Auslösen“, „Letzte Seite
  verwerfen“, „Hinweis: Näher ran.“, Erfolgssatz und Linktext.
- Seitenliste schlicht: „Seite 1“, „Seite 2“ als nummerierte Liste, ohne „DIN A4 • Scan“, „Bereit“, Zähler.
- Abzeichen „ERKANNT“ und Tischfoto entfernen; Kamerabild mit dickem weißem Rahmen.
- Zoom nicht sperren (Viewport).
- Optional als Varianten: Zustand vor dem Start („Kamera starten“) und Fehlerbox „Kein Kamerazugriff –
  Stattdessen eine Datei hochladen“.

Design-System (Farben, Geist/JetBrains Mono, Stufen) eingehalten.

Status: Vorschlag – Freigabe durch den Nutzer offen
