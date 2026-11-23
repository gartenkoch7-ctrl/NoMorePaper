# 08 – Listen (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen „Listen v2“
  `a916b4bb2f634001849632c74227fd02`, HTML-Datei
  `projects/17713397687229704853/files/61656030942c40b1bde3496fbfea891d`
- **Ersetzt:** Screen `9e310f407b2145998bf8c5a70ba3a2cd` (erste Fassung). Die Nachbesserung dort per
  `edit_screens` wurde nicht gespeichert; deshalb neu erzeugt als „Listen v2“.
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/Lists.razor` (`/listen`),
  `Classification/ListSection.razor`

## Übernehmen (passt zur App)

- **Brotkrumen „Einstellungen / Listen“**, Navigation „Einstellungen“ aktiv: `/listen` wird über
  die Kachel in `Pages/Settings.razor` erreicht, wie `/eingaenge` und die Mail-Einstellungen, die
  schon `<Breadcrumb>` nutzen. Beim Umsetzen `<Breadcrumb>` in `Lists.razor` ergänzen
  (Einstellungen → `einstellungen`).
- **H1 „Listen“ + Einleitung**, drei Abschnitte als Karte mit H2 „Absender“, „Dokumentarten“,
  „Tags“ – entspricht den drei `ListSection`.
- **Tabelle** Name | Zum Zusammenführen (Checkbox) | Aktionen („Umbenennen“ mit Stift-Symbol und
  Text), Kopfzeile in kleiner Mono-Schrift in Großbuchstaben.
- **Zeile darunter:** „2 ausgewählt“ links, „Zusammenführen“ rechts.
- **Umbenennen-Box in der Seite:** H3 „„Stadtwerke“ umbenennen“, Feld „Neuer Name“,
  „Abbrechen“ (dunkel) vor dem weißen Bestätigen-Knopf.
- **Zusammenführen-Box in der Seite:** H3 „2 Einträge zusammenführen?“, Erklärung, Auswahl
  des Eintrags, der bleibt, „Abbrechen“ vor „Zusammenführen“.
- Je Abschnitt höchstens eine Box offen (Absender: Umbenennen, Dokumentarten: Zusammenführen) –
  passt zur App, jede `ListSection` hat ihren eigenen Zustand.
- Tags ohne „#“, Name in Zeile und Checkbox-Namen gleich.

## Abgleich mit der App

| Im Entwurf | In NoMorePaper |
|---|---|
| Einleitung „Absender, Dokumentarten und Tags des Haushalts“ | App-Text `ListsIntro` (inkl. Hinweis auf Bearbeitungsrechte) |
| Abschnitts-Untertitel („Alle erkannten oder manuell vergebenen …“ usw.) | heute nur H2; Text wäre neu – nur, wenn im Plan |
| Hinweis unter „Neuer Name“ („Wird sofort für alle … aktualisiert.“) | gibt es nicht; Text wäre neu – nur, wenn im Plan |
| Spaltenkopf „Zum Zusammenführen auswählen“ | `ListsColMerge` „Zum Zusammenführen“ |
| Checkbox-Name „<Name> zum Zusammenführen auswählen“ | `ListsSelectFor` „Zum Zusammenführen auswählen: <Name>“ |
| Knopf „Zusammenführen“ unter der Tabelle | `ListsMerge` „Ausgewählte zusammenführen“ |
| Bei 0 Auswahl Knopf `disabled` und gedimmt | Knopf bleibt bedienbar; bei < 2 Auswahl Fehleransage `ListsMergeTooFew` (deaktivierte Knöpfe sind nicht fokussierbar) |
| Bestätigen „Umbenennen“ | `ListsRenameConfirm` „Neuen Namen speichern“ |
| Erklärung „Alle Dokumente werden auf den Eintrag umgestellt, der behalten wird.“ | `ListsMergeExplain` – sagt zusätzlich, dass die anderen Einträge gelöscht werden und es nicht rückgängig geht |
| Auswahl-Label „Behalten wird“ | `ListsMergeTarget` „Dieser Eintrag bleibt erhalten“ |
| Bestätigen „Zusammenführen“ (mit Häkchen) | `ListsMergeConfirm` „Ja, zusammenführen“ |
| Symbole an H3 der Boxen (`edit_note`, `merge`) | gibt es nicht; nur dekorativ, darf mit `aria-hidden` dazu |
| fehlt | Tabellen-`caption` (`ListsCaption`), Name als `th scope="row"` |
| fehlt | Zustände: Laden („ListsLoading“), Ladefehler + „Erneut versuchen“, leere Liste („ListsEmpty“) |
| fehlt | Ergebnis-Ansage per `LiveRegion`, Fehler assertiv; Escape bricht Box ab, Fokus auf „Abbrechen“ bzw. Namensfeld |

## Beim Übernehmen nicht mitnehmen (§2.5)

- `focus:outline-none` an allen Knöpfen, Feldern, Auswahl, Suche und Brotkrumen-Link;
  `focus:ring-0` an Checkboxen – Fokus unsichtbar (Ersatz nur als leichter Hintergrundwechsel).
  Fokus kommt aus dem Design-System, kein eigener Rahmen (ADR-0026).
- „Umbenennen“-Knöpfe ohne Eintragsnamen (`aria-label` fehlt; App: „Umbenennen: <Name>“).
- `disabled` am Zusammenführen-Knopf bei 0 Auswahl (siehe Abgleich).
- Seitenleiste und Kopf: Material-Symbols ohne `aria-hidden` (Navigation, Suche, Hochladen,
  Avatar); Sprachwahl als `span` mit `cursor-pointer`; „Hochladen“ ohne `type` und als Knopf statt
  Link; kein „Zum Inhalt springen“. Rahmen/Shell kommt aus `MainLayout`/`SideNav`, nicht aus
  diesem Entwurf.
- `form onsubmit="event.preventDefault();"` und `type="submit"` – Bedienlogik kommt aus
  `ListSection` (Enter im Namensfeld speichert dort per `OnRenameKey`).
- Boxen ohne `role="group"`/`aria-labelledby`.
- Hinweistext in `label-sm` (10 px) – zu klein für Fließtext; falls übernommen, `body-sm`.
- Tailwind-CDN und Google Fonts (`cdn.tailwindcss.com`, `fonts.googleapis`) – ADR-0002.

## Schrift (Mono-Texte im Bild mit Serifen)

`label-sm`, `label-md`, `label-lg` sind in der Tailwind-Konfiguration als `["JetBrains Mono"]`
**ohne Ersatzschrift** definiert; JetBrains Mono wird per Google-Fonts-Link geladen. Im
`screen.png` erscheinen Spaltenköpfe, Sprachwahl, Zähler „3“ und der Hinweistext trotzdem mit
Serifen: Der Stitch-Renderer hat das Bild aufgenommen, bevor die Webschrift geladen war, und fiel
mangels `monospace`-Ersatz auf die Standardschrift (Serif) zurück. **Darstellungsfehler der
Aufnahme, keine Abweichung im Design.** In der App kommen die Schriften lokal aus dem Theme
(mit `monospace`-Ersatz); nichts in Stitch zu ändern.

## Nachbessern in Stitch?

Nicht nötig – Aufbau und Funktionen stimmen jetzt mit der App überein; die frühere Liste
(erfundene Statusangaben, Pulsieren, Symbole je Art, „#“-Chips, Dokumentzählung, „Hauptkategorie“,
Fußzeile) ist erledigt. Rest sind App-Texte und Unsichtbares, das beim Umsetzen geregelt wird.
Optional, falls der Entwurf die App exakt zeigen soll: Knopf- und Label-Texte auf die App-Texte
oben setzen, Zusammenführen-Knopf bei 0 Auswahl nicht deaktivieren. Shell weicht von Screen 02
ab (Logo-Symbol, Seitenleisten-Farbe) – wird nicht übernommen.

Design-System (Farben, Geist/JetBrains Mono, Stufen) eingehalten.

Status: freigegeben durch den Nutzer (2026-11-23); Restpunkte werden beim Umsetzen behoben
