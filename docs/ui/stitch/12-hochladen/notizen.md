# 12 – Hochladen (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `29be59d496ac4420b1b8c873f7b9962e`,
  HTML-Datei `projects/17713397687229704853/files/782847f147324b07ad44a141e58a19ec`
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/Upload.razor` (`/hochladen`) mit
  `Documents/PrivateSwitch.razor`
- **Prompt:** vom Agenten erzeugt am 2026-11-23 aus [prompts.md](../prompts.md) §3.2

## Übernehmen (passt zur App)

- **Reihenfolge:** Privat-Schalter **vor** der Dateiauswahl (wie `PrivateSwitch` vor `InputFile`).
- **Privat-Karte:** native Checkbox mit sichtbarem `label for` und Hinweiszeile darunter, Fläche
  `surface-container-low`, `rounded-xl`, `p-5`.
- **Ablagefeld:** große Fläche, Upload-Symbol im Kreis, weißer Hauptknopf, Hinweiszeile darunter;
  `input type=file` mit `multiple` und `accept`.
- **H2 „Ergebnis“** (`UploadResultsHeading`) und Ergebnisliste: je Zeile Symbol **und** Wort
  („Erfolg“/„Hinweis“/„Fehler“), Satz, Link rechts; Trennung durch Flächenstufe statt Linie.
- **Warnbox „übersprungen“** mit Warnsymbol, Dateinamen in Mono, Fläche `surface-container-high`.
- Navigation ohne aktiven Punkt; Breadcrumb „Hinzufügen / Hochladen“ zeigt die Einordnung unter
  dem Hauptpunkt (prompts.md §2).

## Abgleich mit der App

| Im Entwurf | In NoMorePaper |
|---|---|
| H1 „Dokumente hochladen“ | `UploadHeading` „Dokument hochladen“ (Einzahl) – Text angleichen, Entscheidung offen |
| Checkbox-Label „Privat – nur ich sehe diese Dokumente“ | `PrivateLabel` „Privat“ |
| Hinweis „Gilt für alle Dateien, die du jetzt auswählst.“ | `UploadPrivateHint` „Private Dokumente sieht nur du. Das gilt für die Dateien, die du als Nächstes auswählst.“ |
| fehlt | `PrivateUnknown`: Hinweis, wenn die Konto-Einstellung nicht ladbar ist |
| Knopf „Dateien auswählen“ (`button` + `onclick` auf verstecktes `input`) | `label for="upload-input"` „Datei auswählen“ (`UploadFileLabel`) + echtes `InputFile` |
| Hinweis „PDF, JPG, PNG oder HEIC, mehrere möglich. Du kannst Dateien auch hierher ziehen.“ | `UploadHint` nennt zusätzlich **bis 100 MB** und **„wird sofort hochgeladen“** – beides bleibt |
| Warnbox **unter** H2 „Ergebnis“, immer sichtbar | Warnbox nur, wenn Dateien übersprungen; steht **vor** der Überschrift |
| „2 Dateien übersprungen: Notiz.txt, Video.mov“ | Grund in der App: mehr als 20 auf einmal (`UploadSkipped`/`UploadSkippedOne`), Namen als Liste (`UploadSkippedListLabel`); falsche Dateitypen meldet der Server je Datei als Fehler |
| Zeile „Rechnung.pdf hochgeladen“ | `UploadSuccessPrivate`/`UploadSuccessShared`: sagt **privat/geteilt** und „wartet auf die Verarbeitung“ |
| Link „In der Liste zeigen“ | `UploadShowInList` „{0} in der Dokumentenliste zeigen“ – Dateiname im Linktext (eindeutig für Screenreader) |
| „Brief.pdf ist schon vorhanden“ + „Vorhandenes öffnen“ | `UploadDuplicateNamed` + `UploadShowExisting` „Vorhandenes Dokument zu „{0}“ ansehen“ |
| „Foto.heic: Datei zu groß“ | `UploadFailed` „{0} konnte nicht hochgeladen werden. {1}“ |
| Status als Wort-Label „ERFOLG/HINWEIS/FEHLER“ | App: nur Symbol (`MudIcon`) + Satz; das Wort-Label wäre neu (gut für §2.5 „Symbol und Text“) – mit Nutzer klären |
| Ergebnisliste aus `div` | `ul.upload-results` > `li` bleibt |
| fehlt | `LiveRegion` mit Ansage „{0} wird hochgeladen …“ und Ergebnis je Datei |
| Breadcrumb `nav` | gibt es in der App nicht; neu, nur mit Freigabe |

## Beim Übernehmen nicht mitnehmen (§2.5)

- `onclick` am Knopf, der ein `sr-only`-Dateifeld auslöst – in der App bleibt `label` + echtes Feld.
- Checkbox `focus:ring-0 focus:outline-none` – Fokus unsichtbar; `focus:outline-none` auch an
  Links, Knöpfen, Suchfeld, Sprachumschalter (dort ohne Ersatz). Fokus wie im Design-System,
  kein eigener Rahmen (ADR-0026).
- `truncate` an Dateinamen und Fehlertext – schneidet ab.
- `uppercase tracking-wider` an den Status-Wörtern – Screenreader buchstabieren evtl.; Text in
  normaler Schreibweise.
- Ergebnisliste und Warnbox als `div`, kein `role`.
- `aria-label="Datei-Uploadbereich"` an der `section` – überflüssig, Feld hat eigenes Label.
- `cursor-pointer`/`select-none` am Label.
- Sprachumschalter ohne Zustand (`aria-pressed` fehlt); „Hochladen“ im Kopf als `button` statt Link.
- Titel `<title>` lautet noch „Hinzufügen (Desktop)“ (Kopie aus Screen 11) – App: `UploadTitle`.
- Material-Symbols-Font geladen, aber nicht benutzt; Tailwind-CDN, Google Fonts – ADR-0002.
- Kein „Zum Inhalt springen“ (kommt aus `MainLayout`).

## Schrift und Größen

- `mono` = `['"JetBrains Mono"', 'monospace']`, per Google Fonts geladen; im `screen.png` korrekt
  (Dateinamen, „Deutsch | English“). Keine Abweichung.
- **Echte Abweichung:** Die Inhaltsklassen `font-headline-lg`, `text-headline-lg`, `text-body-md`,
  `text-on-surface-variant`, `text-on-surface`, `text-error`, `text-secondary` u. a. sind im
  Tailwind-Config **nicht definiert** (nur `surface.*`, daher greift `bg-surface-container-low`,
  die Typo- und Text-Token nicht). Folge im Bild:
  H1 „Dokumente hochladen“ ist **so klein wie Fließtext**, kleiner als H2-Ebene anderer Screens;
  „Fehler“-Zeile nicht farbig (hier unkritisch, Wort trägt die Info). In der App gelten die
  Typo-Stufen des Design-Systems (H1 wie „Hinzufügen“ in Screen 11).

## Nachbessern in Stitch?

Ja:
- H1 in Design-System-Größe (wie Screen 11, `text-3xl font-bold`).
- Warnbox über die Überschrift „Ergebnis“ und Grund nennen („… höchstens 20 auf einmal“).
- Erfolgszeile mit „privat“/„geteilt“, Linktexte mit Dateinamen.
- Hinweis im Ablagefeld um „bis 100 MB“ und „wird sofort hochgeladen“ ergänzen.
- Navigationspunkt „Startseite“ → „Start“, Sprachumschalter „DE | EN“ (wie Screen 11).
- Status-Wörter nicht in Großbuchstaben; `truncate` weg.

Design-System (Farben, Geist/JetBrains Mono, Flächenstufen) sonst eingehalten.

Status: Vorschlag – Freigabe durch den Nutzer offen
