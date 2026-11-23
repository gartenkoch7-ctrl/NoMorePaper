# 15 – Papierkorb (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `880fdad9fccb410dad8f454cc29ca67a` (v3, ersetzt v2
  `2e9f50103c9347d88b1779332d37f5da` und v1 `cbe387f127464f0c898ea30f3addc4cd`),
  HTML-Datei `projects/17713397687229704853/files/5a5ff1b4039a4a559e81bb2862a0de04`
- **Dateien:** [code.html](code.html) (v3, unverändert aus Stitch), [screen.png](screen.png) (v3)
- **Ziel im Code:** geplante Seite `/papierkorb` (M4b, P3c `Pages/Trash.razor`), noch nicht gebaut
- **Prompt:** vom Agenten erzeugt aus [prompts.md](../prompts.md) §4.2; v2 mit den Befunden aus v1, v3 mit dem
  S0-Nachtrag aus dem M4b-Detailplan
- **Einordnung:** unter „Einstellungen“ (Brotkrume „Einstellungen / Papierkorb“), wie in
  [prompts.md](../prompts.md) §2 geplant

## S0-Vorgabe (v3) – erfüllt

- **Anzahl an der Überschrift:** H2 „Gelöschte Einträge (4)“. ✔
- **Rückfrage „Wiederherstellen mit Tausch“:** „„Mietvertrag“ wiederherstellen?“, „Das Dokument hat inzwischen eine
  neuere Fassung. Beim Wiederherstellen tauschen beide die Plätze: die jetzige Fassung kommt in den Papierkorb.“,
  „Abbrechen“ zuerst, „Ja, tauschen“ – passt zu ADR-0013 / Plan „belegter Platz → Tausch“. ✔
- **Schmale Liste:** Abschnitt „Schmale Ansicht“ mit einer Karte je Eintrag (Titel, Art-Abzeichen, „Gelöscht: … ·
  Bis: …“, beide Knöpfe mit Text). ✔ (Entschieden: unter 48 rem Liste, darüber Tabelle.)
- Restpunkte aus v2 behoben: Knopftexte „Wiederherstellen“ / „Endgültig löschen“ auf allen Breiten sichtbar (kein
  `hidden xl:inline`, kein `title=`); kein Zusatz „– Original“ / „– ältere Fassung“ im Titel; Tabelle mit
  `caption` (`sr-only`). ✔

## Übernehmen (passt zur App)

- **H1 „Papierkorb“** + Einleitung „Gelöschtes bleibt 30 Tage hier. Du siehst nur deine eigenen Dokumente.“
  (ADR-0013, nur eigene Dokumente).
- **Fläche „Gelöschte Einträge (n)“** (H2) mit Tabelle: Titel | Was | Gelöscht am | Endgültig gelöscht am |
  Aktionen, `th scope="col"`. „Was“ als Text (Dokument / Original / Ältere Fassung) – nie nur Symbol oder Farbe.
- **Dokumenttitel** statt Dateiname.
- **Aktionen je Zeile** „Wiederherstellen“ / „Endgültig löschen“, neutral (`surface-container`), Symbole
  `aria-hidden`.
- **Rückfragen in der Seite** (kein Modaldialog): Tausch und „Papierkorb wirklich leeren?“ („4 Einträge werden
  endgültig gelöscht. Das kann nicht rückgängig gemacht werden.“), jeweils „Abbrechen“ zuerst.
- **Bestätigungsknöpfe neutral** (Nutzerentscheidung 2026-11-23): „Ja, tauschen“ / „Ja, leeren“ als weißer
  Hauptknopf, **kein Rot**. ✔
- **Schmale Liste** wie gezeigt: Karte `surface-container`, Titel oben, Art rechts, Zeiten darunter, zwei gleich
  breite Knöpfe.
- **Leerzustand** „Der Papierkorb ist leer.“ als ruhige Fläche mit Symbol und Text.
- Zeiten im Format „28.02.2026, 11:15“. Kein Inline-`<script>`.

## Abgleich mit dem Plan M4b

| Im Entwurf | Plan M4b (P3c) / ADR-0013 |
|---|---|
| Rückfrage je Zeile „„{Titel}“ endgültig löschen?“ fehlt in v3 (war in v2) | bleibt laut P3c: `ConfirmBox Id="trash-confirm-{id}"` unter der Zeile, Text „Das kann nicht rückgängig gemacht werden.“, „Ja, endgültig löschen“ (`PrimaryConfirm`) – Wortlaut aus v2 |
| Rückfragen als eigene Flächen unter der Tabelle | Rückfrage **unter der Zeile** bzw. im Artikel der Liste; Rückfrage, Liste und Leerzustand nie gleichzeitig (Zustandsdemo im Entwurf) |
| Tabelle und „Schmale Ansicht“ gleichzeitig | Entschieden: ab 48 rem nur Tabelle (im benannten Scrollbereich), darunter nur Liste (`ul` + `article` je Eintrag, Titel als H3, Zeiten als `dl`); die andere Darstellung `display:none` |
| Liste: „Gelöscht: … · Bis: …“ | „Bis“ = „Endgültig gelöscht am“; in der `dl` dieselben Begriffe wie in der Tabelle |
| „Aufbewahrungsfrist: 30 Tage“ rechts an der Überschrift | doppelt zur Einleitung; optional, Nutzer entscheidet |
| „Papierkorb leeren“ unter der Tabelle | passt (nur eigene Einträge); Knopf nur, wenn Einträge da sind |
| fehlt | Erfolgs-/Fehleransage (`LiveRegion`, Fehlerbox `role="alert"`); Fokus nach Aktion auf die nächste Zeile, sonst H2; nach „Abbrechen“ auf den Auslöser |

## Beim Übernehmen nicht mitnehmen (§2.5)

- Knöpfe je Zeile ohne Bezug zum Titel (je Zeile zweimal gleich) – `aria-label` „{Titel} wiederherstellen“ /
  „{Titel} endgültig löschen“.
- Tabelle ohne `table-fixed`: Datumsspalten brechen um, „Endgültig löschen“ zweizeilig – in der App
  `table-layout: fixed` mit Spaltenbreiten; Titel als `th scope="row"`, `overflow-wrap:anywhere`.
- `overflow-x-auto` ohne Namen – in der App benannter Scrollbereich (`role=region tabindex=0 aria-labelledby`).
- `aria-label` an der Tabelle **und** `caption` – nur `caption`.
- Fläche „Gelöschte Einträge“ ohne `aria-labelledby`; Rückfragen als `div` mit H2 – in der App `ConfirmBox`
  (`role="group"`, Fokus auf die Überschrift, Escape).
- `truncate` an den Titeln der schmalen Liste – Titel umbrechen.
- Brotkrume als `div` ohne `nav aria-label` und ohne Link – `<Breadcrumb>` der App.
- H1 `font-bold` (700) – Design-System `headline-xl` hat 600.
- `focus:outline-none` am Suchfeld – Fokus wie im Design-System, kein eigener Ring (ADR-0026).
- Rahmen: Symbole in Seitenleiste/Kopfzeile ohne `aria-hidden`, Navigationen ohne `aria-label`, Sprachumschalter
  ohne `aria-pressed`, „Hochladen“ als `button`, „Angemeldet als“ `hidden xl:inline` – App-Rahmen aus
  `MainLayout` gilt. Kein „Zum Inhalt springen“ (kommt aus `MainLayout`).
- Tailwind-CDN und Google Fonts (`cdn.tailwindcss.com`, `fonts.googleapis`) – ADR-0002.

## Schrift

`fontFamily` ist korrekt: `label-*` = `["JetBrains Mono"]`, sonst `["Geist"]` – keine Abweichung vom
Design-System. Im v3-`screen.png` erscheinen alle `label-*`-Texte (Tabellenkopf, „Was“, Daten, Brotkrume,
„Deutsch | English“, Zeiten der Liste) aber **mit Serifen**. Ursache im Entwurf: der Google-Fonts-Link fordert
`JetBrains+Mono:wght@100..900` (die Schrift hat nur 100–800), die Anfrage schlägt damit vermutlich fehl, und ohne
Ausweichschrift greift die Serifen-Standardschrift. Kein Designfehler, nur Render-Artefakt: in der App die
Schriften selbst ausliefern und `monospace` als Ausweichschrift mitgeben. Beim Prüfen des Bildes die Mono-Stellen
als Mono lesen.

## Nachbessern in Stitch?

Nein, keine weitere Runde nötig: S0-Vorgabe vollständig, keine groben Verstöße, Design-System (Token, Stufen ohne
Rahmen, kein Rot) eingehalten. Die fehlende Rückfrage je Zeile ist in v2 entworfen und im Plan festgelegt.
Optional (Nutzer entscheidet bei der Freigabe): Rückfrage „endgültig löschen“ je Zeile wieder zeigen und den
Schrift-Link korrigieren, damit das Bild Mono zeigt.

Status: Vorschlag v3 – Freigabe durch den Nutzer offen
