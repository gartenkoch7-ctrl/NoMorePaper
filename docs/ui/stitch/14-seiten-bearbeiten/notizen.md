# 14 – Seiten bearbeiten (Stitch-Entwurf, Desktop, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `3f56dde504c24188b4669841c0de9b72` („v2“),
  HTML-Datei `projects/17713397687229704853/files/c373469cf26340f6abb084a2a347f591`
- **Ersetzt:** v1 `792d42ff522f4ac1b9ae2131a47ee434` (Farben außerhalb der Tokens, kein „Zuschneiden“,
  falsche Erklärtexte)
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** geplante Seite `/dokumente/{id}/seiten` (M4b Spur B, noch nicht gebaut)
- **Grundlage:** [M4b-Plan](../../../plan/m4b-scan-bearbeitung.md), [ADR-0013](../../../adr/0013-scan-bearbeitung-versionen-papierkorb.md),
  [ADR-0025](../../../adr/0025-scan-bearbeitung-umsetzung.md)
- **Prompt:** vom Agenten am 2026-11-23 aus [prompts.md](../prompts.md) §4.1 erzeugt, für v2 nachgeschärft
  (nur Tokens, „Zuschneiden“, Randseiten ohne sinnlose Knöpfe, Zuschnitt-Hinweis, leere Seite 3)

## Übernehmen (passt zum Plan)

- Rahmen wie die übrigen Desktop-Screens (Seitenleiste, Kopf); Brotkrumen
  „Dokumente › Titel › Seiten bearbeiten“ (Baustein `Design/Breadcrumb.razor` vorhanden).
- **H1** „Seiten bearbeiten – <Titel>“, darunter Knopfleiste **„Speichern“** (weiß, primär), **„Alles
  verwerfen“**, **„Dokument anhängen“** und der Satz „Erst „Speichern“ legt eine neue Fassung an. Das
  Original bleibt erhalten.“ – genau ADR-0025 (eine Fassung je Speichern).
- **Senkrechte Seitenliste** (kein Raster), je Seite eine Karte: große Vorschau links, rechts **H2
  „Seite 1 von 3“**, „Beginnt mit: …“, Abzeichen „Vermutlich leer“ (Symbol + Text, monochrom),
  Knopfgruppe „Nach links drehen“, „Nach rechts drehen“, „Nach oben“, „Nach unten“, „Zuschneiden“,
  „Ränder und Schräglage automatisch“, „Seite löschen“, „Hier teilen“ – alle Knöpfe mit sichtbarem Text,
  Symbole `aria-hidden`.
- **Randseiten:** Seite 1 ohne „Nach oben“, letzte Seite ohne „Nach unten“ und ohne „Hier teilen“.
- **Ergebnis der Automatik** als Zeile mit Häkchen + Text „Um 3 Grad gedreht, Rand oben entfernt“
  (Demo M4b), monochrom.
- **Zuschnitt-Zustand:** Karte hervorgehoben, Abzeichen „Zuschnitt aktiv“, vier große Eckgriffe, Satz
  „Die Seite wird als Bild ersetzt und neu erkannt.“ (passt zu ADR-0025), Hinweis „Ecke mit Pfeiltasten
  verschieben, Tab wechselt die Ecke“, Knöpfe „Zuschnitt übernehmen“ / „Abbrechen“.
- Farben nur aus den Design-Tokens (Obsidian Slate, monochrom); Vorschauen ohne Tabellen.

## Abgleich mit dem Plan M4b

| Im Entwurf | Im Plan |
|---|---|
| Vorschau Karte 3: Symbol + „Kein Textinhalt erfasst“ statt fast leerer Seite | Vorschau zeigt immer die echte Seite mit Shader (ADR-0011); keine solche Angabe |
| „Beginnt mit: [Kein erkennbarer Text]“ bei Karte 3 | Textanfang ≤ 300 Zeichen; bei leerer Seite reicht „Vermutlich leer“, Zeile weglassen |
| Fußzeile „Änderungen betreffen nur die lokale OCR- und Seitenstruktur auf diesem Heimserver.“ | Erfunden und irreführend (Speichern legt eine neue Fassung inkl. PDF/A an, ADR-0025); nicht übernehmen. „3 Seiten im Dokument“ doppelt sich mit „Seite x von 3“ |
| „#01 / 03“ rechts in jeder Karte | doppelt zur H2; nicht übernehmen |
| Vorschauen als Platzhalter-Balken | echte Seite als Bild/Canvas, invertiert; Inhalt nur über die Ansage |
| Gesetzte Trennstellen in der Liste nicht gezeigt | Plan: „Trennstellen in der Seitenliste“ – Darstellung im Detailplan festlegen |
| fehlt | Zustand einer gelöschten/gedrehten Seite vor dem Speichern (Markierung, rückgängig) – im Plan nicht entschieden, **offene Frage für den Detailplan** |
| „Dokument anhängen“ ohne Folgezustand | Auswahl per Suche, Privat-Hinweis („Ergebnis wird privat“) – eigener Zustand fehlt |
| fehlt | Zustände Speichern läuft / „wird bearbeitet“, Fehler, 409 „noch nicht fertig verarbeitet“ |
| „Alles verwerfen“ ohne Rückfrage | Plan: verwerfen macht alles rückgängig; bei ungespeicherten Änderungen Rückfrage in der Seite (keine Modale) |
| Kopf: „Status / Angemeldet als Anna“, DE|EN, „Abmelden“ | wie 02/07; Kopf kommt aus `MainLayout` |

## Beim Übernehmen nicht mitnehmen (§2.5)

- Eckgriffe weiter als `span aria-hidden` im `pointer-events-none`-Rahmen, nicht fokussierbar – in der
  App fokussierbare Elemente mit Namen („Ecke oben links“ …) und Ansage der Position; Logik aus dem Plan,
  nicht aus dem Entwurf. Zuschnitt-Rahmen mit Inline-`rgba(255,255,255,0.85)` statt Token `primary`.
- Karten als `section aria-label` zusätzlich zur H2 (doppelte Ansage, viele Regionen) – im Code `article`
  mit H2 oder `aria-labelledby`; Vorschau `role="img"`/`role="region"` nur mit echter Ansage.
- `focus:outline-none` am Suchfeld (Ersatz nur Randfarbe) – Fokus wie im Design-System (ADR-0026).
- Zweites `<header>` im Inhalt um H1 und Knöpfe.
- Sprachumschalter „DE/EN“ ohne `aria-pressed`; „Hochladen“ als `button` statt Link; zwei Navigationen
  in der Seitenleiste ohne `aria-label`.
- Kein „Zum Inhalt springen“ (kommt aus `MainLayout`).
- `::-webkit-scrollbar{display:none}`; Tailwind-CDN, Google Fonts (ADR-0002).
- Nur Desktop entworfen; schmal stapelt `md:flex-row` Vorschau und Knöpfe – beim Umsetzen mobil prüfen.

## Schrift

Konfiguration: `headline-*`/`body-*` = `["Geist"]`, `label-sm/-md/-lg` = `["JetBrains Mono"]` (Google
Fonts). Im `screen.png` korrekt Geist bzw. Mono, kein Serifen-Artefakt.

## Nachbessern in Stitch?

Ja, kleiner Rest (als „Seiten bearbeiten v3“ oder per Edit):
- Vorschau Karte 3 als echte, fast leere Seite (kein Symbol, kein „Kein Textinhalt erfasst“);
  „Beginnt mit“ bei Karte 3 weglassen.
- Fußzeile („lokale OCR- und Seitenstruktur …“, „3 Seiten im Dokument“) und „#01 / 03“ entfernen.
- Optional: gesetzte Trennstelle zwischen zwei Seiten zeigen; Eckgriffe als sichtbar fokussierbare Knöpfe.

Vor der Umsetzung im Detailplan entscheiden: Darstellung gelöschter/geänderter Seiten vor dem Speichern,
Trennstellen, Zustand „Dokument anhängen“.

Status: freigegeben durch den Nutzer (2026-11-23); Restpunkte werden beim Umsetzen behoben
