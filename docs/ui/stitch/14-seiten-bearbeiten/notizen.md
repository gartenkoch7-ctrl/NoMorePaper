# 14 – Seiten bearbeiten (Stitch-Entwurf, Desktop, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `792d42ff522f4ac1b9ae2131a47ee434`,
  HTML-Datei `projects/17713397687229704853/files/04d6ffdcf6c74d72a00c826056dbe1e2`
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** geplante Seite `/dokumente/{id}/seiten` (M4b Spur B, noch nicht gebaut)
- **Grundlage:** [M4b-Plan](../../../plan/m4b-scan-bearbeitung.md), [ADR-0013](../../../adr/0013-scan-bearbeitung-versionen-papierkorb.md),
  [ADR-0025](../../../adr/0025-scan-bearbeitung-umsetzung.md)
- **Prompt:** vom Agenten am 2026-11-23 aus [prompts.md](../prompts.md) §4.1 erzeugt

## Übernehmen (passt zum Plan)

- Rahmen wie die übrigen Desktop-Screens (Seitenleiste, Kopf); Brotkrumen
  „Dokumente › Titel › Seiten bearbeiten“ (Baustein `Design/Breadcrumb.razor` vorhanden).
- **H1** „Seiten bearbeiten – <Titel>“, darunter Knopfleiste **„Speichern“** (weiß, primär), **„Alles
  verwerfen“**, **„Dokument anhängen“** und der Satz „Erst „Speichern“ legt eine neue Fassung an. Das
  Original bleibt erhalten.“ – genau ADR-0025 (eine Fassung je Speichern).
- **Senkrechte Seitenliste** (kein Raster), je Seite ein `article`: große Vorschau links, rechts **H2
  „Seite 1 von 3“**, „Beginnt mit: …“, Abzeichen „Vermutlich leer“ (Symbol + Text), Knopfgruppe
  „Nach links drehen“, „Nach rechts drehen“, „Nach oben“, „Nach unten“, „Seite löschen“, „Ränder und
  Schräglage automatisch“, „Hier teilen“ – alle Knöpfe mit sichtbarem Text, Symbole `aria-hidden`.
- **Ergebnis der Automatik** als Zeile mit Symbol + Text „Um 3 Grad gedreht, Rand oben entfernt“ (Demo M4b).
- **Zuschnitt-Zustand:** Karte hervorgehoben, Abzeichen „Zuschnitt-Modus aktiv“, vier große Eckgriffe,
  Hinweis „Ecke mit Pfeiltasten verschieben, Tab wechselt die Ecke“, Knöpfe „Zuschnitt übernehmen“ /
  „Abbrechen“.

## Abgleich mit dem Plan M4b

| Im Entwurf | Im Plan |
|---|---|
| Kein Knopf, um den Zuschnitt zu starten | Plan: Knöpfe je Seite inkl. „zuschneiden“ (Ecken per Pfeiltasten/Maus) – Knopf „Zuschneiden“ fehlt |
| Text Karte 2: „… Die Vorlage wird dabei verlustfrei skaliert.“ | **Falsch:** Zuschnitt ersetzt die Seite durch ein Bild (200 dpi), eine Textebene geht verloren, Seite wird neu erkannt (ADR-0025 Folgen, Plan-Risiko „Hinweis in der Ansage“) |
| Text Karte 3: „weder maschinelle Schrift noch verwertbare handschriftliche Notizen … empfiehlt sich das Löschen“ | nur „vermutlich leer“ (`document_pages`); Handschrifterkennung und Empfehlung gibt es nicht |
| Vorschau Karte 3: „Kein Textinhalt erfasst“, „Rückseite / Leerseite“, „0 Bytes Text“ | Vorschau zeigt immer die echte Seite mit Shader (ADR-0011); keine solchen Angaben |
| „Beginnt mit: [Kein erkennbarer Text]“ (kursiv) | Textanfang ≤ 300 Zeichen; bei leerer Seite reicht „Vermutlich leer“ |
| Vorschau als gezeichnetes Schreiben (Name, Adresse, Vertragsnummer, `table`) | echte Seite als Bild/Canvas, invertiert; Inhalt nur über die Ansage |
| „Hier teilen“ auch auf der letzten Seite | Teilen nach der letzten Seite ist sinnlos; gesetzte Trennstellen in der Liste („Trennstellen in der Seitenliste“) sind nicht gezeigt |
| fehlt | Zustand einer gelöschten/gedrehten Seite vor dem Speichern (Markierung, rückgängig) – im Plan nicht entschieden, **offene Frage für den Detailplan** |
| „Dokument anhängen“ ohne Folgezustand | Auswahl per Suche, Privat-Hinweis („Ergebnis wird privat“) – eigener Zustand fehlt |
| fehlt | Zustände Speichern läuft / „wird bearbeitet“, Fehler, 409 „noch nicht fertig verarbeitet“ |
| „Alles verwerfen“ ohne Rückfrage | Plan: verwerfen macht alles rückgängig; bei ungespeicherten Änderungen Rückfrage in der Seite (keine Modale) |
| Kopf: „Status / Angemeldet als Anna“, DE|EN, „Abmelden“ | wie 02/07; Kopf kommt aus `MainLayout` |

## Beim Übernehmen nicht mitnehmen (§2.5)

- **Farben außerhalb des Design-Systems:** `text-emerald-400`/`bg-emerald-950` (Automatik-Ergebnis),
  `amber-*` („Vermutlich leer“), `text-red-400`/`red-950` („Seite löschen“), `neutral-200/300/400`,
  `bg-black/80` und feste Hexwerte `#262626`, `#333333`, `#393939`, `#2e2e2e`, `#27272a`, `#3f3f46`,
  `#0a0a0a` statt Tokens. Obsidian Slate ist monochrom; Status über Symbol + Wort (Fehlerton nur `error`).
- Eckgriffe als `div` mit `cursor-*-resize`, nicht fokussierbar – in der App fokussierbare Elemente mit
  Namen („Ecke oben links“ …) und Ansage der Position; Logik aus dem Plan, nicht aus dem Entwurf.
- `table aria-label="Abrechnungsübersicht"` in der Vorschau – würde vorgelesen; Vorschau ist für
  Screenreader nur die Ansage.
- `focus:outline-none` am Suchfeld (Ersatz nur Randfarbe) – Fokus wie im Design-System (ADR-0026).
- Zweites `<header>` im Inhalt um H1 und Knöpfe.
- Sprachumschalter „DE/EN“ ohne `aria-pressed`; „Hochladen“ als `button` statt Link; zwei Navigationen
  in der Seitenleiste ohne `aria-label`.
- Kein „Zum Inhalt springen“ (kommt aus `MainLayout`).
- `::-webkit-scrollbar{display:none}`; Tailwind-CDN, Google Fonts (ADR-0002).
- Nur Desktop entworfen; schmal stapelt `md:flex-row` Vorschau und Knöpfe – beim Umsetzen mobil prüfen.

## Schrift

`label-sm/-md/-lg` sind `["JetBrains Mono"]` (Google Fonts); im `screen.png` korrekt Mono, kein
Serifen-Artefakt. Vorschau-Inhalt in Mono ist nur Platzhalter.

## Nachbessern in Stitch?

Ja (als neuer Screen „Seiten bearbeiten v2“):
- Farben auf Tokens: Automatik-Ergebnis, „Vermutlich leer“ und „Seite löschen“ monochrom (Symbol + Wort),
  keine Grün-/Gelb-/Rottöne außer Token `error`.
- Knopf „Zuschneiden“ in jede Knopfgruppe; „Hier teilen“ nicht auf der letzten Seite.
- Erklärtexte Karte 2 und 3 entfernen; bei Karte 2 höchstens „Die Seite wird als Bild ersetzt und neu
  erkannt.“
- Vorschau Karte 3 als echte (fast leere) Seite, ohne „Kein Textinhalt erfasst“ / „0 Bytes Text“;
  „Beginnt mit“ bei leerer Seite weglassen.
- Optional: gesetzte Trennstelle zwischen zwei Seiten zeigen.

Vor der Umsetzung im Detailplan entscheiden: Darstellung gelöschter/geänderter Seiten vor dem Speichern,
Zustand „Dokument anhängen“.

Status: Vorschlag – Freigabe durch den Nutzer offen
