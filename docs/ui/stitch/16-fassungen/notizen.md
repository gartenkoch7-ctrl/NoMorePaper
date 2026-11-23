# 16 – Fassungen im Viewer (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `8944a0b58f7940e3b4313018cebbb2bd`,
  HTML-Datei `projects/17713397687229704853/files/c5c1597c8b3f4709b2ec2103b3d121b9`
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** geplanter Bereich „Fassungen“ in
  `src/NoMorePaper.Web.Client/Pages/DocumentViewer.razor` (M4b, Spur B), noch nicht gebaut
- **Prompt:** vom Agenten erzeugt (2026-11-23) aus [prompts.md](../prompts.md) §4.3

## Übernehmen (passt zur App)

- **H2 „Fassungen“** als eigener Abschnitt (`section aria-labelledby`) in der rechten Spalte.
- **Liste der Fassungen** je Eintrag: Art („Original“ / „Bearbeitet“), Datum, Zustand als
  **Symbol + Text** („Aktiv (wird angezeigt)“) – nicht nur über Farbe.
- **Wahl „Behalten“** als `fieldset`/`legend` mit drei Radioknöpfen (Beides / Nur bearbeitete
  Fassung / Nur Original), je mit Erklärzeile im `label`; „Beides“ vorgewählt (Werkswert).
- **„Auswahl speichern“** als Hauptknopf (weiß), neutral – **kein Rot** im ganzen Entwurf. ✔
- **Link „Seiten bearbeiten“** (führt zu `/dokumente/{id}/seiten`, Entwurf 14).
- **Hinweis** „Nicht behaltene Fassungen kommen 30 Tage in den Papierkorb.“ mit Symbol.
- Viewer-Leiste (Seite x von y, Zoom −/+, %, Anpassen) und Brotkrume wie in 03 / heutigem Viewer.

## Abgleich mit dem Plan M4b

| Im Entwurf | Plan M4b / ADR-0013 / heutiger Viewer |
|---|---|
| „Angaben“ als zugeklappter Knopf über „Fassungen“ | Viewer hat heute **Reiter** (`TabList`: Angaben / Erkannter Text / Verlauf). Fassungen als **vierter Reiter** oder als Abschnitt im Reiter „Angaben“ einordnen – Klappknopf passt nicht |
| Abzeichen „2 Versionen“ | Begriff einheitlich „**2 Fassungen**“ |
| Original: Zustand „Gespeichert“ | nichtssagend; besser „Wird nicht angezeigt“ o. ä. bzw. weglassen |
| „Nur bearbeitete Fassung – Verwirft das Original nach Bestätigung“ | Original kommt **30 Tage in den Papierkorb**, nicht sofort weg; Text anpassen. Original endgültig entfernen nur **Besitzer** |
| „Nur Original – Setzt alle bisherigen Bearbeitungen zurück“ | bearbeitete Fassung kommt in den Papierkorb – so formulieren |
| Rückfrage beim Speichern einer verwerfenden Wahl nicht gezeigt | Rückfrage in der Seite nötig (neutral, „Abbrechen“ zuerst, wie Entwurf 15) |
| fehlt | Zustand „**wird bearbeitet**“ (Worker läuft, Viewer zeigt bisherige Fassung) |
| fehlt | Dokument ohne Bearbeitung: nur „Original“, Wahl ausgeblendet oder deaktiviert mit Erklärung |
| fehlt | Kopf des Viewers (Titel, Privat-Abzeichen, Herunterladen Original/Archiv) aus 03 / heutigem Viewer |
| Seite als nachgebautes HTML (Rechnung) | echte Seite als invertiertes Bild (Shader, ADR-0011) – nur Platzhalter |

## Beim Übernehmen nicht mitnehmen (§2.5)

- **Eigene Tailwind-Konfiguration statt Design-System:** feste Hex-Werte (`#0e0e0e`, `#242424`,
  `#f5f5f5`, `neutral-*`) und **Rahmen** (`border-[#242424]`, `border-[#2a2929]`) überall; in der
  App Theme-Werte von „Obsidian Slate“ (Stufen statt Linien).
- **Grün** (`emerald-400`) für „Aktiv (wird angezeigt)“ und „- 960,00 €“ – Farbe nicht im
  Design-System; Zustand in Weiß/`on-surface` mit Symbol + Text.
- `font-bold` (700) für Logo und H2 – Design-System nutzt 500/600.
- Symbol-Knöpfe der Viewer-Leiste nur mit `title=`, ohne `aria-label` (App hat `aria-label`).
- `focus:outline-none` + `focus-visible:ring-*` und `focus:ring-*` an Radios – eigener Fokusring,
  kommt nicht mit (ADR-0026).
- `role="list"`/`listitem` an `div` – in der App `ul`/`li`.
- Dokumentinhalt mit `h3`/`h4` (Rechnungsnachbau) – entfällt.
- `select-none` an Navigation und Leiste.
- Sprachumschalter ohne `aria-pressed`; „Hochladen“ als `button` statt Link; kein „Zum Inhalt
  springen“ (kommt aus `MainLayout`).
- Tailwind-CDN und Google Fonts (`cdn.tailwindcss.com`, `fonts.googleapis`, `fonts.gstatic`) –
  ADR-0002.

## Schrift

`mono` ist hier als `['"JetBrains Mono"', 'monospace']` mit Ausweichschrift definiert; das
`screen.png` zeigt Mono korrekt. Keine Serifen-Artefakte.

## Nachbessern in Stitch?

Ja (Regel: jede UI-Änderung zuerst in Stitch):
- **Design-System anwenden** (`apply_design_system` Obsidian Slate): Token statt Hex, keine
  Rahmenlinien, kein Grün, Gewichte 500/600.
- Rechte Spalte an den heutigen Viewer angleichen: Reiter statt Klappknopf „Angaben“, Fassungen als
  Reiter oder Abschnitt; Kopf mit Titel/Privat/Herunterladen wie 03.
- Texte: „2 Fassungen“; Erklärzeilen der Wahl mit „kommt 30 Tage in den Papierkorb“; „Gespeichert“
  ersetzen.
- Rückfrage in der Seite für „Nur bearbeitete Fassung“ / „Nur Original“ zeigen (neutral).

Status: Vorschlag – Freigabe durch den Nutzer offen
