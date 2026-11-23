# 15 – Papierkorb (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `2e9f50103c9347d88b1779332d37f5da` (v2,
  ersetzt v1 `cbe387f127464f0c898ea30f3addc4cd`),
  HTML-Datei `projects/17713397687229704853/files/29922a052f9c426d8161cfab71787cff`
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** geplante Seite `/papierkorb` (M4b, Spur C), noch nicht gebaut
- **Prompt:** vom Agenten erzeugt (2026-11-23) aus [prompts.md](../prompts.md) §4.2; v2 mit den
  Befunden aus v1 nachgebessert
- **Einordnung:** unter „Einstellungen“ (Brotkrume „Einstellungen / Papierkorb“), wie in
  [prompts.md](../prompts.md) §2 geplant

## Übernehmen (passt zur App)

- **H1 „Papierkorb“** + Einleitung „Gelöschtes bleibt 30 Tage hier. Du siehst nur deine eigenen
  Dokumente.“ (passt zu ADR-0013 und M4b: nur eigene Dokumente).
- **Nur eine Brotkrume** (oben in der Leiste). ✔ (v1: doppelt)
- **Fläche „Gelöschte Einträge“** (H2) mit **Tabelle über die volle Breite**, `table-fixed`, ohne
  waagrechtes Scrollen; „Aktionen“ ist im `screen.png` vollständig sichtbar. ✔
- `th scope="col"`: Titel | Was | Gelöscht am | Endgültig gelöscht am | Aktionen. „Was“ als Text
  (Dokument / Original / Ältere Fassung) – keine Information nur über Symbol oder Farbe.
- **Dokumenttitel** statt Dateiname („Stromrechnung März“, „Arztbrief Februar“). ✔
- **Titel umbrechen** (`break-words`) statt abschneiden. ✔
- **Aktionen je Zeile** „Wiederherstellen“ / „Endgültig löschen“, neutral (`surface-container`).
- **Rückfrage in der Seite je Zeile** („„Arztbrief Februar“ endgültig löschen?“, „Das kann nicht
  rückgängig gemacht werden.“) und für **„Papierkorb leeren“** („4 Einträge werden endgültig
  gelöscht …“); jeweils „Abbrechen“ zuerst, kein Modaldialog. ✔
- **Bestätigungsknöpfe neutral** (Nutzerentscheidung 2026-11-23): „Ja, endgültig löschen“ / „Ja,
  leeren“ als weißer Hauptknopf (`primary`), **kein Rot**. ✔
- **Leerzustand** „Der Papierkorb ist leer.“ als ruhige Fläche mit Symbol und Text.
- Zeiten im Format „28.02.2026, 11:15“ wie im übrigen Design.
- Kein Inline-`<script>` mehr. ✔

## Abgleich mit dem Plan M4b

| Im Entwurf | Plan M4b / ADR-0013 |
|---|---|
| Spalte „Was“: Dokument / Original / Ältere Fassung | passt (gelöschte Dokumente, entfernte Originale, alte Versionen); Begriff „Fassung“ wie im Viewer (16) |
| Titel mit Zusatz „– Original“ / „– ältere Fassung“ | doppelt zur Spalte „Was“ (Screenreader liest es zweimal); nur Dokumenttitel, Art in „Was“ |
| „Endgültig gelöscht am“ = gelöscht + 30 Tage | passt (Aufräumdienst im Worker, 30 Tage) |
| „Papierkorb leeren“ / „Endgültig löschen“ | nur Besitzer; Seite zeigt ohnehin nur eigene Einträge – passt |
| Wiederherstellen einer Fassung, deren Platz belegt ist | Plan: „belegter Platz → Tausch“; Hinweis/Rückfrage dazu fehlt im Entwurf |
| Anzahl der Einträge fehlt (v1: „4 Einträge“) | Anzahl an der Überschrift hilft dem Screenreader-Nutzer; wieder aufnehmen |
| fehlt | Erfolgs-/Fehleransage nach Wiederherstellen/Löschen (`LiveRegion`, Fehlerbox `role="alert"`) |
| fehlt | Fokus nach „Abbrechen“ bzw. nach Löschen einer Zeile (zurück auf den auslösenden Knopf bzw. nächste Zeile) |

## Beim Übernehmen nicht mitnehmen (§2.5)

- Knopftext `hidden xl:inline`: unter `xl` sind „Wiederherstellen“ / „Endgültig löschen“ **nur
  Symbol mit `title=`** – kein verlässlicher zugänglicher Name, und zwei gleichnamige Knöpfe je
  Zeile ohne Bezug zum Titel. In der App Text immer sichtbar, `aria-label` mit Titel
  („Arztbrief Februar wiederherstellen“) oder `aria-describedby` auf die Titelzelle.
- `title=` an Knöpfen, die schon Text haben (doppelte Ansage möglich).
- Tabelle ohne `caption`; Fläche ohne `section aria-labelledby` (v1 hatte beides) – in der App
  ergänzen.
- `overflow-x-auto` um die Tabelle – bei Zoom/Lupe muss der Inhalt umbrechen, nicht scrollen; auf
  schmalen Breiten Zeilen als Liste darstellen.
- Brotkrume ohne `nav aria-label`, „Einstellungen“ als `span` mit `cursor-pointer` statt Link.
- Zeile 4 als „ausgewählt“ hervorgehoben – nur Zustandsdemo zur Rückfrage; Rückfrage gehört in der
  App direkt unter die Zeile bzw. den Knopf, Rückfrage, Liste und Leerzustand nie gleichzeitig.
- Zwei Rückfragen als `div` mit H2 – in der App je `section aria-labelledby`, Fokus beim Öffnen auf
  die Überschrift.
- `focus:outline-none focus:ring-1` am Suchfeld – Fokus wie im Design-System, kein eigener Ring
  (ADR-0026).
- `hover:text-error` an „Abmelden“ (Rot beim Überfahren) – kein Rot.
- Sprachumschalter „Deutsch | English“ ohne `aria-pressed`; „Hochladen“ als `button` statt Link
  `hochladen`; Symbole der Leiste/Navigation ohne `aria-hidden`.
- Navigationen ohne `aria-label` (Haupt- und untere Navigation).
- Kein „Zum Inhalt springen“ (kommt aus `MainLayout`).
- Tailwind-CDN und Google Fonts (`cdn.tailwindcss.com`, `fonts.googleapis`, `fonts.gstatic`) –
  ADR-0002.

## Schrift

`label-sm`/`label-md`/`label-lg` sind als `["JetBrains Mono"]` **ohne Ausweichschrift** definiert
und werden per Google Fonts geladen. Das v2-`screen.png` zeigt Mono korrekt (Tabellenkopf, „Was“,
Daten, Brotkrume, „Deutsch | English“); keine Serifen-Artefakte. In der App `monospace` als
Ausweichschrift mitgeben.

## Nachbessern in Stitch?

Ja, kleine Punkte (Regel: jede UI-Änderung zuerst in Stitch):
- Knopftexte „Wiederherstellen“ / „Endgültig löschen“ auf allen Breiten sichtbar (nicht
  `hidden xl:inline`).
- Zusatz „– Original“ / „– ältere Fassung“ aus der Titelspalte nehmen (Art steht in „Was“).
- Anzahl „4 Einträge“ an „Gelöschte Einträge“ wieder zeigen.
- Optional: Hinweis zum Tausch beim Wiederherstellen einer Fassung, deren Platz belegt ist.

Design-System (Farben-Token, Geist/JetBrains Mono, Stufen ohne Rahmen) eingehalten; kein Rot außer
`hover:text-error` an „Abmelden“ (Rahmen der App, nicht übernehmen).

Status: freigegeben durch den Nutzer (2026-11-23); Restpunkte werden beim Umsetzen behoben
