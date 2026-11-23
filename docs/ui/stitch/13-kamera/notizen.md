# 13 – Kamera (Stitch-Entwurf, mobil, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `1f5694c9e9c04c40a6bcff1784cd04be` („Kamera v2“),
  HTML-Datei `projects/17713397687229704853/files/4eedc12ca5534905bd49bbbeedf04aea`
- **Ersetzt:** v1 `da911ff1bb294a289cdf8c56384b81a1` (HTML `…/files/5ec262815018456db5a51438f73c8f06`) – nicht mehr gültig
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/Camera.razor` (+ `Camera.razor.cs`,
  `Camera.Done.cs`), Route `/kamera`
- **Prompt:** v1 vom Agenten am 2026-11-23 aus [prompts.md](../prompts.md) §3.3 erzeugt; v2 am 2026-11-23 mit den
  App-Texten aus `Strings.resx` und den Befunden aus v1

## Übernehmen (passt zur App)

- **Texte** entsprechen `Strings.resx`: `CamHeading`, `CamIntro`, `CamTones` + `CamTonesHint`, `PrivateLabel` +
  `CamPrivateHint`, `CamHintLabel`/`CamHintCloser` („Hinweis: Näher ran.“), `CamCapture`, `CamDiscard`, `CamDone`,
  `CamPagesHeading`, `CamPageItem`, `CamSavedShared`, `CamShowInList`, `CamUploadInstead`.
- **Schalter** als echte Checkboxen mit umschließendem `label` und Hinweis darunter, gemeinsam auf
  `surface-container-low`.
- **Kamerabild** mit 3 px weißem Rahmen, ohne Abzeichen und ohne Fremdfoto (gezeichnetes Blatt) – wie
  `camera-stage`.
- **Hinweiszeile** unter dem Bild groß (Symbol + Text) auf `surface-container-high` – passt zu `camera-hint`.
- **Knöpfe** untereinander, volle Breite, mind. 44 px hoch; „Auslösen“ weiß (primär), die anderen
  `surface-container-high`.
- **Seitenliste** als `ol` „Seite 1“, „Seite 2“, ohne Format/Status/Zähler.
- **Erfolgsbox** und **Fehlerbox** mit Symbol, Satz und unterstrichenem Link darunter.
- `lang="de"`; Viewport `width=device-width, initial-scale=1.0` – **Zoom erlaubt**.

## Abgleich mit der App

| Im Entwurf | In NoMorePaper |
|---|---|
| Fehlertext „Die Kamera ist nicht verfügbar.“ | Kein solcher Text; App nennt den Grund (`CamErrorDenied`, `CamErrorNoCamera`, `CamErrorFailed` …), z. B. „Die Kamera konnte nicht gestartet werden.“ |
| Fehlerbox ganz unten | App: Fehlerbox direkt nach der Einleitung, **vor** den Schaltern |
| Erfolgsbox grün (`success.*`), Fehlerbox rot (`#cf6679`, `#2c1517`) | Design-System: kein Grün; Fehler `--nmp-error` `#ffb4ab`. Farben angleichen (Symbol + Satz tragen die Info) |
| Seitenliste in JetBrains Mono (`font-mono`) | App: Fließtext-Schrift (Geist) |
| fehlt | Knopf „Kamera starten“ (`CamStart`) und Ladehinweis `CamStarting` (Zustand vor dem Start) |
| fehlt | Leerzustand „Noch keine Seite aufgenommen.“ (`CamNoPages`) |
| fehlt | Duplikat: Info-Symbol + „Vorhandenes Dokument ansehen“ (`CamShowExisting`) |
| Hinweis unter den Checkboxen nicht verknüpft | `aria-describedby` (`cam-tones-hint`, `PrivateSwitch`) |
| Kopf: Logo + Knopf „Menü“ | Kopf aus `MainLayout` (Knopf `menu-toggle` mit `aria-expanded`, `AccountMenu`) |

## Beim Übernehmen nicht mitnehmen (§2.5)

- `focus:outline-none` mit eigenem `focus:ring-*` an Logo-Link, Menü-Knopf, Knöpfen und Links sowie
  `focus:ring-2` an den Checkboxen – kein eigener Fokusrahmen, Fokus wie im Design-System (ADR-0026).
- Festes Kopfband (`sticky`) frisst bei 200/400 % Zoom Höhe – Kopf kommt aus `MainLayout`.
- `cursor-pointer`/`select-none` an den Labels.
- Kamerabild-Platzhalter als `div`; in der App `video` mit `aria-hidden`.
- Tailwind-CDN, Google Fonts – ADR-0002.
- Kein „Zum Inhalt springen“ (kommt aus `MainLayout`).
- `<title>` „nomorepaper – Kamera v2 (Mobil)“ – App: `CamTitle` „Kamera – NoMorePaper“.

## Schrift

Config: `sans` = `['Geist', 'sans-serif']`, `mono` = `['"JetBrains Mono"', 'monospace']` (plus `.font-mono`-Regel
mit JetBrains Mono), beide per Google Fonts geladen. Im `screen.png` Geist für Text, JetBrains Mono nur in der
Seitenliste; kein Serifen-Artefakt.

## Nachbessern in Stitch?

Ja, klein:
- Fehlerbox mit App-Text (z. B. `CamErrorFailed` „Die Kamera konnte nicht gestartet werden.“) und nach oben
  direkt unter die Einleitung.
- Erfolgs- und Fehlerbox in Design-System-Farben (kein Grün; Fehler `#ffb4ab`).
- Seitenliste in Geist statt Mono.
- Optional als Variante: Zustand vor dem Start („Kamera starten“, Ladehinweis, „Noch keine Seite aufgenommen.“).

Behoben gegenüber v1: alle Texte auf App-Stand, Seitenliste schlicht als `ol`, Abzeichen „ERKANNT“ und Tischfoto
entfernt, weißer Rahmen ums Kamerabild, Zoom nicht mehr gesperrt, `lang="de"`, Material-Symbols ohne
`aria-hidden` entfernt (SVG mit `aria-hidden`), Scrollbar-Ausblendung entfernt, Fehlerbox mit „Stattdessen eine
Datei hochladen“ ergänzt.

Status: Vorschlag – Freigabe durch den Nutzer offen
