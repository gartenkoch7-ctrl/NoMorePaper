# 06 – Zu prüfen (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `a4769d0a4c704207a371768f10d2bcf2`
  (HTML-Datei `projects/17713397687229704853/files/d2cdbbd212bd4d748152a8a75970f607`)
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/Review.razor` + `Review.razor.cs` (`/zu-pruefen`)
- **Prompt:** [prompts-spur-e.md](../prompts-spur-e.md) „06 – Zu prüfen“, Beschreibung prompts.md §3.6

## Übernehmen (passt zur App)

- **H1 „Zu prüfen“** + Zählsatz in `body-lg`; Breite `max-w-4xl`; Karten untereinander (`gap-space-lg`).
- **Karte je Dokument** (`surface-container`, `rounded-xl`, `p-space-lg`): H2-Titel als Link,
  „Zusammenfassung: …“, Abzeichen „Automatisch erkannt“ mit Symbol + Text (Pille, `rounded-full`).
- **Angaben als `dl`** im Raster (3 Spalten, Etiketten `label-sm` groß geschrieben, Werte Datum/Betrag in
  JetBrains Mono), Tags als kleine Chips – entspricht der heutigen `fields-list`.
- **Knöpfe** „Bestätigen“ (weiß, Haken) und „Bearbeiten“ (dunkel, Stift).
- **Duplikat-Abschnitt** als innere Fläche (`surface-container-low`): H3 mit Symbol, Erklärsatz,
  Vergleichstabelle mit `th scope="col"`/`scope="row"` (Titel, Eingegangen, Datum, Absender) – genau wie
  heute; Knöpfe „Beide behalten“ / „Dieses löschen“ (rote Schrift, Mülleimer).
- **Rückfrage in der Seite** (kein Modal): H3 „Wirklich löschen?“, Satz, „Abbrechen“ zuerst, dann rotes
  „Löschen“ – passt zu `confirm-box` und zum geplanten `Design/ConfirmBox` (Spur E).

## Abgleich mit der App

| Im Entwurf | In NoMorePaper |
|---|---|
| Satz „3 Dokumente warten auf Prüfung“ | `ReviewCount` „3 Dokumente zu prüfen.“ (Einzahl `ReviewCountOne`) |
| Abzeichen mal rechts oben (Karte 1), mal unter dem Text (Karten 2/3) | einheitlich unter der Zusammenfassung, wie heute `fields-recognition` |
| „Bestätigen“ immer da | nur bei `Recognition == Automatic`; sonst Abzeichen „Bestätigt“ (Haken) |
| Tags als Chips | heute Text mit Komma; Chips nur optisch, Inhalt gleich; leer = „–“ |
| „Anderes Dokument“ in der Tabelle als Text | Titel des anderen ist **Link** zum Viewer |
| Tabelle ohne Überschrift | `caption` `ReviewCompareCaption` bleibt |
| Rückfrage „Wirklich löschen?“ ohne Titel | `ReviewDeleteQuestion` nennt den Titel |
| „… unwiderruflich aus NoMorePaper entfernt.“ | `ReviewDeleteExplain`: „…kann nicht rückgängig gemacht werden. Das vorhandene Dokument bleibt.“ (ab M4b ggf. Papierkorb) |
| „Löschen“-Knopf mit `delete_forever` | Text „Löschen“ (`ReviewDeleteConfirm`); Symbol optional |
| Rückfrage dauerhaft sichtbar | nur nach „Dieses löschen“ (`_deleting`), Fokus auf „Abbrechen“, Escape bricht ab |
| fehlt | Abschnitt **„Wem gehört das?“** (`OwnerCheckSection`) bei unklarer Herkunft |
| fehlt | Editor `DocumentFieldsEditor` erscheint in der Karte statt der Knöpfe |
| fehlt | Zustände: lädt, Fehler + „Erneut versuchen“, leer („Nichts zu prüfen“) |
| fehlt | Trennvorschlag für Stapel – erst M4b, darf fehlen |

## Beim Übernehmen nicht mitnehmen (§2.5)

- `focus:outline-none` an den Titel-Links (nur Unterstreichung als Ersatz) und am Suchfeld – Fokus wie im
  Design-System, kein eigener Rahmen (ADR-0026).
- Karten als `section` ohne Namen; heute `ul`/`li` + `article aria-labelledby` – bleibt.
- Knöpfe ohne Bezug zum Dokument („Bestätigen“ ×3) – `aria-label` „… für ‚Titel‘“ wie heute.
- Rückfrage ohne `role="group"`/`aria-labelledby`; Inline-Skript (Fokus, `remove()`) – Bedienlogik kommt
  aus `Review.razor.cs` (LiveRegion-Ansagen, `PendingFocus`).
- `data-action`, `id`s fest (`btn-delete-this`) – je Dokument eindeutige IDs wie heute.
- Kopf/Seitenleiste: Symbole ohne `aria-hidden`, Suchfeld ohne `label`, Sprachknöpfe ohne Zustand.
- Rotes „Löschen“ ist auch per Text erkennbar (gut) – Farbe nie allein.
- Google-Fonts-Link für Material Symbols **doppelt**, Tailwind-CDN, Google Fonts – nichts davon in den Build.
- `::-webkit-scrollbar { display: none; }` nicht übernehmen.
- Viewport ohne Zoom-Verbot (gut).

## Nachbessern in Stitch?

- Keine groben Verstöße (keine erfundenen Firmen-/Cloud-Funktionen, Design-System eingehalten).
- Klein: Abzeichen-Position in Karte 1 vereinheitlichen; Rückfrage-Text („unwiderruflich aus NoMorePaper“)
  an den echten Text angleichen; optional eine Karte mit „Wem gehört das?“ ergänzen, damit der Entwurf
  die echte Seite zeigt (Regel „Mockups zeigen die echte App“).

Status: Vorschlag – Freigabe durch den Nutzer offen
