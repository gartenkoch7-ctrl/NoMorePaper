# 12 – Hochladen (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `cc3f869e9d334cc2bb78aed3c0b706bc` („Hochladen v2“),
  HTML-Datei `projects/17713397687229704853/files/d46d4ac04a504350bae7bcba78cdc5ec`
- **Ersetzt:** v1 `29be59d496ac4420b1b8c873f7b9962e` (HTML `…/files/782847f147324b07ad44a141e58a19ec`) – nicht mehr gültig
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/Upload.razor` (`/hochladen`) mit
  `Documents/PrivateSwitch.razor`
- **Prompt:** v1 vom Agenten erzeugt am 2026-11-23 aus [prompts.md](../prompts.md) §3.2; v2 am 2026-11-23 mit den
  App-Texten aus `Strings.resx` und den Befunden aus v1

## Übernehmen (passt zur App)

- **Reihenfolge:** H1, Privat-Schalter **vor** der Dateiauswahl, Ablagefeld, Warnbox „übersprungen“ **vor**
  H2 „Ergebnis“, Ergebnisliste – wie `Upload.razor`.
- **Texte** entsprechen `Strings.resx`: `UploadHeading`, `PrivateLabel`, `UploadPrivateHint`, `UploadFileLabel`,
  `UploadHint`, `UploadSkipped`, `UploadResultsHeading`, `UploadSuccessShared`, `UploadShowInList`,
  `UploadShowExisting`, `UploadFailed`.
- **H1** in Design-System-Größe (`text-3xl font-bold`, wie Screen 11).
- **Privat-Karte:** native Checkbox mit umschließendem `label` und Hinweiszeile darunter, Fläche
  `surface-container-low`, `rounded-xl`, `p-5`.
- **Ablagefeld:** große gestrichelte Fläche, Upload-Symbol im Kreis, weißer Hauptknopf als `label for` + echtes
  `input type=file multiple`, Hinweiszeile darunter.
- **Ergebnisliste** als `ul` > `li`: je Zeile Symbol (`aria-hidden`) + ganzer Satz + Link mit Dateinamen rechts;
  kein Abschneiden, keine Großbuchstaben-Labels – wie die App (Symbol + Satz).
- **Warnbox** mit Warnsymbol, Satz mit Grund („höchstens 20 auf einmal“) und Dateinamen als `ul` in Mono.
- Kein Brotkrumenpfad mehr (gibt es in der App nicht).

## Abgleich mit der App

| Im Entwurf | In NoMorePaper |
|---|---|
| „Brief.pdf ist schon vorhanden.“ | `UploadDuplicateNamed` „Diese Datei ist schon vorhanden: „Brief.pdf“. Sie wurde nicht noch einmal gespeichert.“ |
| Warnbox ohne Zwischenzeile | `UploadSkippedListLabel` „Nicht hochgeladen:“ vor der Namensliste |
| Warnbox Bernstein/Gelb (`#24211a`, `#eab308`, `#fef08a`) | App: `error-box` mit `--nmp-error`; Gelb gibt es im Design-System nicht |
| Symbole grün/blau/rot (`#4ade80`, `#60a5fa`, `#f87171`) | Design-System kennt nur `--nmp-error` (`#ffb4ab`); Info trägt der Satz, Farbe ist Zusatz – Farben angleichen |
| fehlt | `PrivateUnknown`: Hinweis, wenn die Konto-Einstellung nicht ladbar ist (Zustandsvariante) |
| fehlt | `LiveRegion` mit Ansage „{0} wird hochgeladen …“ und Ergebnis je Datei (unsichtbar) |
| `input` ohne `accept` | `accept` mit PDF/JPG/PNG/HEIC bleibt; `aria-describedby="upload-hint"` bleibt |
| Hinweis nicht mit dem Feld verknüpft | Hinweis per `aria-describedby` am Dateifeld |
| Navigation: „Hinzufügen“ aktiv (`aria-current`) | Kopf/Menü aus `MainLayout`; ob „Hinzufügen“ auf `/hochladen` aktiv ist, entscheidet die App |
| Sprachumschalter „Deutsch | English“ | Screen 11 zeigt „DE | EN“ – Kopf kommt aus `MainLayout`, dort einheitlich halten |

## Beim Übernehmen nicht mitnehmen (§2.5)

- Checkbox `focus:ring-0 focus:outline-none`, Suchfeld `focus:outline-none` – Fokus wie im Design-System,
  kein eigener Rahmen (ADR-0026).
- `role="alert"` an der dauerhaft sichtbaren Warnbox – Ansage kommt aus der `LiveRegion` (sonst doppelt).
- Dateifeld `sr-only`: Tastaturfokus landet auf unsichtbarem Feld – App-Lösung (`label` + `InputFile`) bleibt.
- `cursor-pointer`/`select-none` am Label; `hover:bg` an Listenzeilen.
- Sprachumschalter ohne Zustand (`aria-pressed` fehlt); „Hochladen“ im Kopf als `button` statt Link.
- `<title>` „nomorepaper – Hochladen v2 (Desktop)“ – App: `UploadTitle` „Hochladen – NoMorePaper“.
- Tailwind-CDN, Google Fonts – ADR-0002.
- Kein „Zum Inhalt springen“ (kommt aus `MainLayout`).

## Schrift und Größen

- Config: `sans` = `['Geist', 'sans-serif']`, `mono` = `['JetBrains Mono', 'monospace']`, beide per Google Fonts
  geladen; Inhalt nutzt feste Werte (`text-3xl`, `text-xl`, `text-sm`, Hex-Farben) statt nicht definierter
  Token. Im `screen.png` Geist bzw. JetBrains Mono (Dateinamen, Sprachumschalter), kein Serifen-Artefakt.
  Die v1-Abweichung (H1 so klein wie Fließtext) ist behoben.

## Nachbessern in Stitch?

Ja, klein:
- Duplikat-Zeile mit vollem App-Satz (`UploadDuplicateNamed`).
- Warnbox: Zeile „Nicht hochgeladen:“ vor den Namen; Farbe ohne Gelb (Fehlerton `#ffb4ab` bzw. Flächenstufe
  `surface-container-high` wie in v1).
- Status-Symbole in Design-System-Farben (weiß/`#ffb4ab`) statt Grün/Blau/Rot.
- Optional als Variante: Hinweis `PrivateUnknown` unter dem Privat-Schalter.

Behoben gegenüber v1: H1-Größe, Warnbox vor „Ergebnis“ mit Grund, Erfolgszeile mit „geteilt“, Linktexte mit
Dateinamen, Hinweis mit „bis 100 MB“ und „sofort hochgeladen“, Status-Wörter in Großbuchstaben und `truncate`
entfernt, Brotkrumen entfernt, `onclick`-Knopf durch `label for` ersetzt, Material-Symbols-Font entfernt.
Navigationspunkt „Startseite“ ist korrekt (`NavHome`), kein Befund mehr.

Status: freigegeben durch den Nutzer (2026-11-23); Restpunkte werden beim Umsetzen behoben
