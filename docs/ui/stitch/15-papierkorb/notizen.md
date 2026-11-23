# 15 – Papierkorb (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `cbe387f127464f0c898ea30f3addc4cd`,
  HTML-Datei `projects/17713397687229704853/files/b80b315f29574b4b9e8062a78303e152`
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** geplante Seite `/papierkorb` (M4b, Spur C), noch nicht gebaut
- **Prompt:** vom Agenten erzeugt (2026-11-23) aus [prompts.md](../prompts.md) §4.2

## Übernehmen (passt zur App)

- **H1 „Papierkorb“** + Einleitung „Gelöschtes bleibt 30 Tage hier. Du siehst nur deine eigenen
  Dokumente.“ (passt zu ADR-0013 und M4b: nur eigene Dokumente).
- **Abschnitt** `section aria-labelledby` mit H2 „Gelöschte Einträge“ und Anzahl „4 Einträge“.
- **Echte Tabelle** mit `caption` (sr-only) und `th scope="col"`: Titel | Was | Gelöscht am |
  Endgültig gelöscht am | Aktionen. „Was“ als Text (Dokument / Original / Ältere Fassung), Symbol
  nur zusätzlich – keine Information nur über Symbol oder Farbe.
- **Aktionen je Zeile** mit sichtbarem Text „Wiederherstellen“ / „Endgültig löschen“, neutral
  (`surface-container`).
- **„Papierkorb leeren“** mit **Rückfrage in der Seite** (kein Modaldialog): H2 „Papierkorb
  wirklich leeren?“, Folgen in Worten, „Abbrechen“ zuerst.
- **Bestätigungsknopf neutral** (Nutzerentscheidung 2026-11-23): „Ja, leeren“ ist
  `surface-container-highest` mit weißer Schrift, **kein Rot**. Rot (`error`) kommt im Entwurf
  nicht vor. ✔
- **Leerzustand** „Der Papierkorb ist leer.“ als ruhige Fläche mit Symbol und Text.
- Zeiten im Format „28.02.2026, 11:15“ wie im übrigen Design.

## Abgleich mit dem Plan M4b

| Im Entwurf | Plan M4b / ADR-0013 |
|---|---|
| Spalte „Was“: Dokument / Original / **Ältere Fassung** | Plan: gelöschte Dokumente, entfernte Originale, alte Versionen – passt; Begriff „Fassung“ wie im Viewer (16) einheitlich nutzen |
| Titel als Dateiname („…(Entwurf).pdf“, „Scan_…_raw.jpg“) | App zeigt den **Dokumenttitel**; für Original/Fassung „<Titel>“ + Art in „Was“ |
| „Endgültig gelöscht am“ = gelöscht + 30 Tage | passt (Aufräumdienst im Worker, 30 Tage) |
| „Endgültig löschen“ je Zeile **ohne Rückfrage** | unwiderruflich → braucht dieselbe Rückfrage in der Seite wie „Leeren“ (neutral, „Abbrechen“ zuerst) |
| „Papierkorb leeren“ für alle sichtbar | Leeren und endgültig Entfernen nur der **Besitzer**; Seite zeigt ohnehin nur eigene Einträge – passt |
| Brotkrumen „Einstellungen > Papierkorb“, „Einstellungen“ als aktuelle Seite | Plan nennt nur `/papierkorb`; Einordnung unter Einstellungen ist neu – Nutzer entscheidet (sonst eigener Link) |
| fehlt | Erfolgs-/Fehleransage nach Wiederherstellen/Löschen (`LiveRegion`, Fehlerbox `role="alert"`) |
| fehlt | Fokus nach „Abbrechen“ bzw. nach Löschen einer Zeile (zurück auf „Papierkorb leeren“ bzw. nächste Zeile) |

## Beim Übernehmen nicht mitnehmen (§2.5)

- **Zwei Brotkrumen** (`nav aria-label="Breadcrumb"` oben in der Leiste und nochmals im Inhalt)
  – nur eine.
- `truncate` an den Titeln – schneidet Namen ab.
- Tabelle `overflow-x-auto`: Spalte „Aktionen“ ist im `screen.png` abgeschnitten („En lö“) und
  der Kopf „Aktionen“ ebenfalls – Aktionen müssen ohne waagrechtes Scrollen erreichbar sein.
- `title=` an Knöpfen, die schon Text haben (doppelte Ansage möglich).
- `focus:outline-none focus:ring-1` am Suchfeld – Fokus wie im Design-System, kein eigener Ring
  (ADR-0026).
- Inline-`<script>` mit `classList` für die Rückfrage – in der App Blazor-Zustand; im Entwurf
  sind Rückfrage und Leerzustand gleichzeitig sichtbar (nur Zustandsdemo).
- Sprachumschalter „DE/EN“ ohne `aria-pressed`; „Hochladen“ als `button` statt Link `hochladen`.
- Navigationen ohne `aria-label` (Haupt- und untere Navigation).
- Kein „Zum Inhalt springen“ (kommt aus `MainLayout`).
- Tailwind-CDN und Google Fonts (`cdn.tailwindcss.com`, `fonts.googleapis`) – ADR-0002.

## Schrift (Mono wirkt im Bild serifig)

`label-sm`/`label-md`/`label-lg` sind als `["JetBrains Mono"]` **ohne Ausweichschrift** definiert
und werden per Google Fonts geladen. Die Serifenschrift im `screen.png` (Spalte „Was“, Daten,
Tabellenkopf, „4 Einträge“, kleine Brotkrume, „DE/EN“) ist ein **Darstellungsartefakt** des
Stitch-Screenshots, keine Abweichung. In der App `monospace` als Ausweichschrift mitgeben.

## Nachbessern in Stitch?

Ja (Regel: jede UI-Änderung zuerst in Stitch):
- Nur **eine** Brotkrume.
- Tabelle so umbauen, dass „Aktionen“ ohne Abschneiden passt (z. B. Aktionen unter dem Titel
  oder schmalere Spalten); Titel als Dokumenttitel statt Dateiname.
- Rückfrage auch für „Endgültig löschen“ je Zeile zeigen (neutral, „Abbrechen“ zuerst).
- Einordnung unter „Einstellungen“ erst nach Entscheidung des Nutzers beibehalten.

Design-System (Farben-Token, Geist/JetBrains Mono, Stufen ohne Rahmen) eingehalten; kein Rot.

Status: Vorschlag – Freigabe durch den Nutzer offen
