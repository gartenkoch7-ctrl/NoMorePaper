# 14 – Seiten bearbeiten (Stitch-Entwurf, Desktop, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `c46fa1c91408470d95a42a39b744d235` („v3“),
  HTML-Datei `projects/17713397687229704853/files/18f9ffe348634a11835a59e6a2b0f193`
- **Ersetzt:** v2 `3f56dde504c24188b4669841c0de9b72` (Zuschnitt-Zustand, Ergebnis der Automatik; Stand im Commit
  `15c7f89`), davor v1 `792d42ff522f4ac1b9ae2131a47ee434`
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** geplante Seite `/dokumente/{id}/seiten` (M4b Spur B, Pakete P2b/P2c, noch nicht gebaut)
- **Grundlage:** [M4b-Plan](../../../plan/m4b-scan-bearbeitung.md), Detailplan
  [m4b-scan-bearbeitung-detail.md](../../../plan/features/m4b-scan-bearbeitung-detail.md) („Entschieden“, „Stitch-Nachtrag
  (S0)“), [ADR-0013](../../../adr/0013-scan-bearbeitung-versionen-papierkorb.md),
  [ADR-0025](../../../adr/0025-scan-bearbeitung-umsetzung.md)
- **Prompt:** vom Agenten am 2026-11-23 aus [prompts.md](../prompts.md) §4.1 erzeugt; v3 ergänzt die Zustände aus S0

## Übernehmen (passt zum Plan)

- Rahmen wie die übrigen Desktop-Screens (Seitenleiste, Kopf); Brotkrumen
  „Dokumente / Stromrechnung März / Seiten bearbeiten“ (Baustein `Design/Breadcrumb.razor`), „Dokumente“ aktiv.
- **H1** „Seiten bearbeiten – <Titel>“, Knopfleiste **„Speichern“** (weiß, primär), **„Alles verwerfen“**, **„Dokument
  anhängen“**, darunter der Satz „Erst „Speichern“ legt eine neue Fassung an. Das Original bleibt erhalten.“ (ADR-0025).
- **Senkrechte Seitenliste** als `ol`, je Seite eine Karte: große Vorschau links, rechts **H2 „Seite x von y“**, „Beginnt
  mit: …“, Knopfgruppe „Nach links drehen“, „Nach rechts drehen“, „Nach oben“, „Nach unten“, „Zuschneiden“, „Ränder und
  Schräglage automatisch“, „Seite löschen“, „Hier teilen“ – sichtbarer Text, Symbole `aria-hidden`, Knöpfe brechen um.
- **Randseiten:** Seite 1 ohne „Nach oben“, letzte Seite ohne „Nach unten“ und ohne „Hier teilen“. ✔
- **Zustandszeile** bei geänderter Seite: Symbol + Text „Nach rechts gedreht“, monochrom (S0). ✔
- **Trennstelle** als eigener Listeneintrag zwischen zwei Karten: „Trennstelle: Dokument 1 endet nach Seite 2“, Knopf
  „Trennstelle entfernen“ (S0). ✔
- **Gelöschte Seite** als zusammengeklappte Karte ohne Vorschau: „Gelöschte Seite, vorher Seite 3“, Knopf „Löschen
  rückgängig“ (S0). ✔
- **Abschnitt „Dokument anhängen“** unter der Knopfleiste (kein Dialog): H2, Suchfeld „Dokument suchen“, Treffer als
  Knöpfe mit Titel (S0, Grundform). ✔
- **Rückfrage „Alle Änderungen verwerfen?“** in der Seite: „Deine Änderungen an diesem Dokument gehen verloren.“,
  „Abbrechen“ zuerst, „Ja, verwerfen“ **neutral** (kein Rot, `ConfirmBox`) (S0). ✔
- Fußzeile („lokale OCR- und Seitenstruktur …“, „3 Seiten im Dokument“) und „#01 / 03“ entfernt. ✔
- **Zuschnitt-Zustand** (Abzeichen „Zuschnitt aktiv“, Eckgriffe, „Zuschnitt übernehmen“ / „Abbrechen“) und Ergebniszeile
  der Automatik sind in v3 nicht mehr zu sehen; dafür gilt weiter der freigegebene v2-Stand (Commit `15c7f89`) und P2c.
- Farben nur aus den Design-Tokens (Obsidian Slate, monochrom).

## Abgleich mit dem Plan M4b

| Im Entwurf | Im Plan |
|---|---|
| Karte 4: Symbol + Abzeichen „Vermutlich leer“ **in** der Vorschau statt echter fast leerer Seite | Vorschau = echte Seite (`Thumbnail`, Shader, ADR-0011); `Badge` „Vermutlich leer“ rechts neben der H2 (P2b). Restpunkt aus v2, **nicht behoben** |
| „Beginnt mit: [Kein erkennbarer Text]“ bei Karte 4 | Zeile entfällt bei vermutlich leerer Seite (P2b). Restpunkt aus v2, **nicht behoben** |
| Nach der gelöschten Seite 3 heißt die letzte Karte „Seite 4 von 4“ | `NumberOf(i)` zählt nur nicht gelöschte Seiten → „Seite 3 von 3“ (P2a) |
| Seite 2 zeigt trotz gesetzter Trennstelle weiter „Hier teilen“ | Trennstelle schaltet um (`ToggleSplitAfter`); im Code Knopf als Schalter oder „Trennstelle entfernen“ – sonst doppelte Aktion |
| Hinweis „Private Dokumente kannst du nur an private Dokumente anhängen.“ | **falsch:** Anhängen ist erlaubt; ist B privat und A geteilt, kommt in der `ConfirmBox` „„{B}“ ist privat. Das Ergebnis wird deshalb privat.“ (P2b). Satz nicht übernehmen |
| Treffer-Knöpfe nur mit Titel, keine Rückfrage gezeigt | Knopf „Anhängen: {Titel}“ (+ `PrivateBadge`), danach `ConfirmBox` „„{B}“ an „{A}“ anhängen?“ mit „Die Seiten kommen ans Ende. …“ |
| „Dokument anhängen“ offen, obwohl Änderungen ungespeichert sind (Seite gedreht, gelöscht) | mit ungespeicherten Änderungen nur `NoticeBox` „Speichere oder verwirf zuerst deine Änderungen.“ (Entschieden, P2b) |
| fehlt | Zustand **nach „Speichern“**: `NoticeBox` Erfolg mit Link zum Dokument (bzw. „Zu prüfen“ beim Teilen), Liste schreibgeschützt (S0) |
| fehlt | Zustände lädt, Fehler/409, „Wird gerade bearbeitet …“, keine Berechtigung – im Plan genau beschrieben (P2b), Bausteine `NoticeBox`/`StatusText` vorhanden |
| Rückfrage „Alles verwerfen“ am Seitenende | im Code direkt unter der Knopfleiste (Nähe zum Auslöser), Fokus auf die Überschrift |
| Vorschauen als Platzhalter-Balken; Karte 1 trotz „Nach rechts gedreht“ nicht gedreht | echte Seite, Drehung per CSS-Klasse `pe-rot-*` |
| Kopf: „Status / Angemeldet als Anna“, „Deutsch | English“, „Abmelden“ | Kopf kommt aus `MainLayout` |

## Beim Übernehmen nicht mitnehmen (§2.5)

- Vorschau als `div role="img" aria-label="Vorschau Seite n"` – im Code `Thumbnail` `aria-hidden` (Inhalt über H2 und
  „Beginnt mit“, P2b).
- Karten als `li` ohne `article`/`aria-labelledby`; Knopfgruppe ohne `role="group"`/`aria-label` „Seite x bearbeiten“ –
  im Code wie P2b.
- Gelöschte Karte und Trennstelle ohne Überschrift – P2b: H2 „Gelöschte Seite (vorher Seite n)“.
- `focus:outline-none` an beiden Suchfeldern (Ersatz nur Randfarbe) – Fokus wie im Design-System (ADR-0026).
- Zweites `<header>` im Inhalt um H1, Knöpfe und Anhängen-Abschnitt – im Code `div.pe-actions`.
- `select-none` an den Vorschauen; Knöpfe mit fester Höhe `h-8`/`h-9` (Text muss umbrechen dürfen).
- Sprachumschalter ohne `aria-pressed`; „Hochladen“ als `button` statt Link; Navigationen der Seitenleiste ohne
  `aria-label`; kein „Zum Inhalt springen“ (kommt aus `MainLayout`).
- `::-webkit-scrollbar{display:none}`; Material-Symbols-Stylesheet dreifach; Tailwind-CDN, Google Fonts (ADR-0002).
- Nur Desktop entworfen; schmal stapelt `md:flex-row` Vorschau und Knöpfe – beim Umsetzen 375 px prüfen.

## Schrift

Konfiguration: `headline-*`/`body-*` = `["Geist"]`, `label-sm/-md/-lg` = `["JetBrains Mono"]` (Google Fonts, Gewichte
300–700 bzw. 400–600). Im `screen.png` korrekt Geist bzw. Mono, kein Serifen-Artefakt.

## Nachbessern in Stitch?

Ja, kleine Runde (per `edit_screens` auf v3 oder als „v4“), weil ein S0-Zustand fehlt und Restpunkte aus v2 wieder
auftauchen:
- Zustand **nach „Speichern“** zeigen (Erfolgsbox mit Link, Liste schreibgeschützt).
- Karte der leeren Seite: echte, fast leere Seite als Vorschau, Abzeichen „Vermutlich leer“ neben der H2, keine Zeile
  „Beginnt mit“.
- Nummerierung nach gelöschter Seite („Seite 3 von 3“); an Seite 2 mit Trennstelle kein zweites „Hier teilen“.
- Anhängen: falschen Privat-Satz ersetzen; Rückfrage mit Privat-Hinweis („Das Ergebnis wird deshalb privat.“) zeigen;
  Abschnitt nur ohne ungespeicherte Änderungen (oder die Demo-Seiten ohne Änderung).

Status: freigegeben durch den Nutzer (2026-11-23); Restpunkte werden beim Umsetzen nach Plan behoben

## Nachtrag 2026-11-24 (M4b P6, per MCP in Stitch geändert)

- Anhängen: Satz „Das Dokument behält danach Original und bearbeitete Fassung.“ (nur wenn das Dokument auf
  „Nur Original“ steht; Entscheidung des Nutzers 2026-11-24). Stitch setzte ihn unter den Privat-Hinweis; im Code
  steht er in der Rückfrage zum Anhängen.
- Verlassen-Rückfrage (F2): „Seite verlassen?“, „Deine Änderungen gehen verloren.“, „Abbrechen“, „Ja, verlassen“.
  Stitch hat dafür im Bild die Rückfrage „Alles verwerfen?“ ersetzt; im Code bleiben **beide** Rückfragen, „Alles
  verwerfen?“ unverändert.
