# 11 – Hinzufügen (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `905bedf6ec704a4b99492611314c1ad4`,
  HTML-Datei `projects/17713397687229704853/files/323214722bd34222969a9d94c861b76d`
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/Add.razor` (`/hinzufuegen`)
- **Prompt:** vom Agenten erzeugt am 2026-11-23 aus [prompts.md](../prompts.md) §2 (Hauptpunkt
  „Hinzufügen“)

## Übernehmen (passt zur App)

- **H1 „Hinzufügen“ + Einleitung** wortgleich mit `AddTitle` / `AddIntro`.
- **Zwei große Kacheln nebeneinander** (`md:grid-cols-2`, auf Mobil untereinander): Symbol oben in
  eigener Fläche, H2 mit Link „Hochladen →“ / „Kamera →“, Beschreibung darunter. Texte wortgleich
  mit `NavUpload`/`AddUploadText` und `NavCamera`/`AddCameraText`.
- **Flächen, Radien, Abstände** der Kacheln (`#1c1b1b`, Rand `#2e2e2e`, `rounded-xl`, `p-8`,
  Hover-Rand heller).
- Navigation: „Hinzufügen“ als aktuelle Seite (`aria-current="page"`, hellere Fläche + Rand) –
  passt, weil `/hinzufuegen` ein Hauptpunkt ist.
- Pfeil- und Symbol-SVGs haben `aria-hidden`; nur ein `main`, `nav aria-label="Hauptnavigation"`.

## Abgleich mit der App

| Im Entwurf | In NoMorePaper |
|---|---|
| Kacheln als `div` + `h2` > `a` | `ul.link-tiles` > `li` mit Link und `aria-describedby` auf den Beschreibungstext – Liste und Verknüpfung bleiben |
| Linktext „Hochladen“ / „Kamera“ mit Pfeil | gleiche Texte (`NavUpload`, `NavCamera`); Pfeil nur Optik, `aria-hidden` |
| Symbol neben Linktext im Link | Symbol (`MudIcon`) heute im Link vor dem Text; im Entwurf als eigene Fläche über der Überschrift |
| H2 je Kachel | heute keine Überschrift in der Kachel (nur Link); H2 wäre neu, Navigation per Überschrift nützlich – mit Nutzer klären |
| Navigationspunkt „Startseite“ | Hauptpunkt heißt „Start“ (prompts.md §2) |
| Sprachumschalter „Deutsch | English“ | App: „DE | EN“ nach prompts.md §2 |
| Kopf-Knopf „Hochladen“ (`button`) | in der App ein Link nach `hochladen` |

Keine erfundenen Funktionen; Inhalt deckt sich mit der App.

## Beim Übernehmen nicht mitnehmen (§2.5)

- `focus:outline-none` überall; Ersatz `focus:ring-1 focus:ring-neutral-400` bzw. am Kachel-Link
  nur `focus:underline`, an den Sprachknöpfen **gar keiner**. In der App Fokus wie im
  Design-System, kein eigener Rahmen (ADR-0026).
- Sprachumschalter als Knöpfe ohne Zustand (`aria-pressed`/`aria-current` fehlt).
- Kein „Zum Inhalt springen“ (kommt aus `MainLayout`).
- Hover-Effekt an der ganzen Kachel, aber nur der Titel ist klickbar – in der App entweder ganze
  Kachel als Link oder kein Kachel-Hover, sonst wirkt die Fläche klickbar.
- `select-none` an der Seitenleiste.
- Tailwind-CDN und Google Fonts (`cdn.tailwindcss.com`, `fonts.googleapis`) – ADR-0002.

## Schrift

`mono` ist im Tailwind-Config als `['"JetBrains Mono"', 'monospace']` mit Ausweichschrift
definiert, Geist/JetBrains Mono per Google Fonts geladen. Im `screen.png` rendert Mono korrekt
(„Deutsch | English“, Zähler „3“) – keine Abweichung.

## Nachbessern in Stitch?

Kleine Punkte (Regel: jede UI-Änderung zuerst in Stitch):
- Navigationspunkt „Startseite“ → „Start“.
- Sprachumschalter „Deutsch | English“ → „DE | EN“.

Design-System (Farben, Geist/JetBrains Mono, Flächenstufen) eingehalten.

Status: Vorschlag – Freigabe durch den Nutzer offen
