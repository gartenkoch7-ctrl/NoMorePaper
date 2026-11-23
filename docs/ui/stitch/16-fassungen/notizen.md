# 16 – Fassungen im Viewer (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `be2cede630c04e43ab9c024a0b33413a` (v2,
  ersetzt v1 `8944a0b58f7940e3b4313018cebbb2bd`),
  HTML-Datei `projects/17713397687229704853/files/1d13778fabe7446f87d69dea42b0aeb1`
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** geplanter Bereich „Fassungen“ in
  `src/NoMorePaper.Web.Client/Pages/DocumentViewer.razor` (M4b, Spur B), noch nicht gebaut
- **Prompt:** vom Agenten erzeugt (2026-11-23) aus [prompts.md](../prompts.md) §4.3; v2 mit den
  Befunden aus v1 nachgebessert

## Übernehmen (passt zur App)

- **Nur Design-Token** von „Obsidian Slate“: keine Hex-Werte, keine Rahmenlinien, kein Grün,
  Gewichte 500/600. ✔
- **Kopf des Viewers** wie 03 / heutiger Viewer: H1 Dokumenttitel, Abzeichen „Privat“ (Symbol +
  Text), „Herunterladen“. ✔
- **Reiter** Angaben / Erkannter Text / Verlauf wie heute (`TabList`); **„Fassungen“ als Abschnitt im
  Reiter „Angaben“** (`section aria-labelledby`, H2) unter den Angaben (prompts.md §4.3). ✔
- **Liste der Fassungen** je Eintrag: Art („Original“ / „Bearbeitet“), Datum, Zustand als **Symbol +
  Text** („Aktiv“, „wird angezeigt“) – nicht nur über Farbe; „Gespeichert“ entfällt. ✔
- **Wahl „Behalten“** als `fieldset`/`legend` mit drei Radioknöpfen (Beides / Nur bearbeitete
  Fassung / Nur Original).
- **„Auswahl speichern“** als Hauptknopf (weiß), neutral.
- **Link „Seiten bearbeiten“** (führt zu `/dokumente/{id}/seiten`, Entwurf 14).
- **Hinweis** „Nicht behaltene Fassungen kommen 30 Tage in den Papierkorb.“ mit Symbol. ✔
- **Rückfrage in der Seite** für „Nur bearbeitete Fassung“: „Original in den Papierkorb legen?“, „Das
  Original bleibt 30 Tage im Papierkorb und kann bis dahin wiederhergestellt werden.“, „Abbrechen“
  zuerst, Bestätigung neutral (weiß, **kein Rot**, Nutzerentscheidung 2026-11-23). ✔
- Viewer-Leiste: Seite x von y, Zoom −/+ mit `aria-label`.

## Abgleich mit dem Plan M4b

| Im Entwurf | Plan M4b / ADR-0013 / heutiger Viewer |
|---|---|
| Brotkrume „Einstellungen / Papierkorb“, keine Navigation aktiv | falsch aus 15 übernommen: Brotkrume „Dokumente / <Titel>“, „Dokumente“ aktiv |
| ein Knopf „Herunterladen“ | heutiger Viewer: **Original** und **Archiv** herunterladen (`doc-view-downloads`) – beide behalten |
| Knopf „Weitere Aktionen“ (⋮) | nicht im Plan; weglassen (Löschen-Knopf im Viewer kommt aus Spur C als sichtbarer Knopf mit Bestätigung) |
| Abzeichen „Fassung 2 von 2“ | nicht im Plan; doppelt zur Liste „Fassungen“ – weglassen |
| „Ansehen“ am Original | nicht im Plan (Viewer zeigt die aktive Fassung); nur übernehmen, wenn der Plan es ergänzt |
| Leiste: Vollbild statt „100 %“-Zurücksetzen und „Anpassen“ | heutiger Viewer hat Zurücksetzen und Breite anpassen – beibehalten |
| Erklärzeilen unter den Radioknöpfen fehlen (v1 hatte sie) | je Option kurz sagen, was in den Papierkorb kommt; „Beides“ ist Werkswert (je Konto) |
| „Nur Original“: Rückfrage nicht gezeigt | gleiche Rückfrage („Bearbeitete Fassung in den Papierkorb legen?“) |
| Knopf „Ja, so behalten“ im `screen.png` abgeschnitten (Text bricht in zu kleiner Höhe `h-9`) | Höhe nicht fest, Text muss umbrechen dürfen |
| fehlt | Zustand „**wird bearbeitet**“ (Worker läuft, Viewer zeigt bisherige Fassung) |
| fehlt | Dokument ohne Bearbeitung: nur „Original“, Wahl ausgeblendet oder deaktiviert mit Erklärung |
| fehlt | Erfolgs-/Fehleransage nach „Auswahl speichern“ (`LiveRegion`, Fehlerbox `role="alert"`) |
| Seite als nachgebautes HTML (Rechnung) | echte Seite als invertiertes Bild (Shader, ADR-0011) – nur Platzhalter |

## Beim Übernehmen nicht mitnehmen (§2.5)

- Reiter als `nav` mit `button`s ohne `role="tablist"`/`tab`/`aria-selected` – App nutzt `TabList`.
- Liste der Fassungen als `div`s – in der App `ul`/`li`.
- `check_circle` mit `aria-label`/`title` „Derzeit in Anzeige“ neben dem Text „Aktiv … wird
  angezeigt“ – doppelte Ansage; Symbol `aria-hidden`.
- `title=` an Symbol-Knöpfen, die schon `aria-label` haben.
- `focus:ring-*` an Radios und `focus:outline-none focus:ring-1` am Suchfeld – eigener Fokusring,
  kommt nicht mit (ADR-0026).
- `select-none` an Seitenzahl, Zoomwert und Radio-Beschriftungen.
- Rückfrage als `section` **im** `form` mit H3 – in der App eigener Abschnitt, Fokus beim Öffnen
  auf die Überschrift.
- Dokumentinhalt mit `h2`/`table`/`address` (Rechnungsnachbau) – entfällt.
- `hover:text-error` an „Abmelden“ (Rot beim Überfahren) – kein Rot.
- Sprachumschalter ohne `aria-pressed`; „Hochladen“ als `button` statt Link; Symbole der
  Leiste/Navigation ohne `aria-hidden`; Navigationen ohne `aria-label`; kein „Zum Inhalt springen“
  (kommt aus `MainLayout`).
- Material-Symbols-Stylesheet doppelt eingebunden.
- Tailwind-CDN und Google Fonts (`cdn.tailwindcss.com`, `fonts.googleapis`, `fonts.gstatic`) –
  ADR-0002.

## Schrift

`label-sm`/`label-md`/`label-lg` sind als `["JetBrains Mono"]` **ohne Ausweichschrift** definiert
(per Google Fonts geladen); Datumsangaben zusätzlich mit Tailwind-`font-mono`. Das `screen.png` zeigt
Mono korrekt, keine Serifen-Artefakte. In der App `monospace` als Ausweichschrift mitgeben.

## Nachbessern in Stitch?

Ja (Regel: jede UI-Änderung zuerst in Stitch):
- Brotkrume „Dokumente / Stromrechnung März“, „Dokumente“ in der Navigation aktiv.
- Kopf wie heute: „Original herunterladen“ und „Archiv herunterladen“; „Weitere Aktionen“ und
  „Fassung 2 von 2“ entfernen.
- Viewer-Leiste wie heute (Zurücksetzen, Breite anpassen) statt Vollbild.
- „Ansehen“ am Original entfernen (nicht im Plan).
- Erklärzeile je Radio-Option wieder zeigen.
- Bestätigungsknopf „Ja, so behalten“ ohne Abschneiden.

Status: Vorschlag – Freigabe durch den Nutzer offen
