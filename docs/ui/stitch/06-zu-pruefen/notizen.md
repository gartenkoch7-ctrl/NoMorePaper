# 06 – Zu prüfen (Stitch-Entwurf)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `5296868046944013a737fdf5418a98c6` (v2,
  M4b-Nachtrag; ersetzt v1 `a4769d0a4c704207a371768f10d2bcf2`),
  HTML-Datei `projects/17713397687229704853/files/468572fd45f84767a04ce5f1435b44d6`
  (v1: `projects/17713397687229704853/files/d2cdbbd212bd4d748152a8a75970f607`)
- **Dateien:** [code.html](code.html) (v2, unverändert aus Stitch), [screen.png](screen.png) (v2)
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/Review.razor` + `Review.razor.cs` (`/zu-pruefen`); ab M4b
  dazu `Classification/SplitSuggestionSection.razor` (P4d)
- Die Abschnitte „Übernehmen“ bis „Nachbessern in Stitch?“ beschreiben den **bestehenden Teil** (Prüfung v1,
  in M4u umgesetzt). v2 zeigt Duplikat-Abschnitt und Rückfrage „Wirklich löschen?“ nicht mehr – dafür gilt
  weiter v1 bzw. die Umsetzung. Der neue Teil steht unter „M4b-Nachtrag“.
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

Status (bestehender Teil): umgesetzt (M4u)

## M4b-Nachtrag (Trennvorschlag)

Gebraucht von P4d (`SplitSuggestionSection`). S0-Vorgabe: Abschnitt „Trennvorschlag“ in einer Prüfkarte (Satz,
Liste der Teile, Knöpfe „Teilen“, „Anpassen“, „Nicht teilen“).

### S0-Vorgabe – erfüllt

- Karte 1 „Scan vom 05.03.2026“ (Stapelscan, 6 Seiten) mit innerer Fläche „Trennvorschlag“ (H3 + Symbol
  `content_cut` mit `aria-hidden`) zwischen Angaben und Kartenknöpfen. ✔
- Satz „Trennvorschlag: 3 Dokumente (Seiten 1–2, 3, 4–6), Seite 5 vermutlich leer.“ – wörtlich wie P4d. ✔
- Liste der Teile: „Dokument 1: Seiten 1–2 – beginnt mit „Sehr geehrte Frau …““ usw. – Teil, Seiten und
  Textanfang als Text. ✔
- Knöpfe „Teilen“ (weiß), „Anpassen“, „Nicht teilen“ (dunkel). ✔
- Rückfrage in der Seite (kein Modal): H4 „In 3 Dokumente teilen?“, „Es entstehen 3 neue Dokumente. Das
  ursprüngliche Dokument kommt 30 Tage in den Papierkorb.“, „Abbrechen“ zuerst, dann „Ja, teilen“. Inhalt passt
  zum Plan (SplitStep: Ausgang erst nach allen Teilen in den Papierkorb). ✔
- Karte 2 ohne Vorschlag zeigt, dass der Abschnitt nur bei Stapeln erscheint. ✔
- Design-System: Fläche `surface-container-lowest` in der Karte, Rückfrage `surface-container`, keine Rahmen, kein
  Rot. Schrift: `fontFamily` korrekt (Geist / JetBrains Mono), `screen.png` ohne Serifen. ✔

### Übernehmen

- Aufbau Überschrift → Satz → Liste → Knopfzeile → (nach „Teilen“) Rückfrage darunter.
- Wortlaut von Satz, Listeneinträgen und Rückfrage.

### Abgleich mit der App

| Im Entwurf | In NoMorePaper (P4d) |
|---|---|
| „Anpassen“ als `button` | **Link** `DocumentLinks.PageEditor(id, split: true)` mit Knopf-Optik (`nmp-button`) |
| „Ja, teilen“ weiß (`primary`) | P4d: `ConfirmBox … neutral` (Standard, kein `PrimaryConfirm`); Rot nie |
| Rückfrage dauerhaft sichtbar | nur nach „Teilen“; `ConfirmBox Id="review-split-{key}" @key`, Fokus auf die Überschrift, Escape bricht ab |
| Rückfrage als H4 | `ConfirmBox HeadingLevel` passend unter H3 „Trennvorschlag“ (H4) |
| fehlt | nach „Teilen“/„Nicht teilen“ Ansage + Karte verschwindet (`OnAnswered` → `DoneAsync`); Fehler als `NoticeBox` |
| Karte „Scan vom …“ mit Art „Rechnung & Korrespondenz“ | nur Beispieldaten; Angaben wie bei jeder Karte |

### Beim Übernehmen nicht mitnehmen (§2.5)

- Fläche als `section` ohne Namen – in der App `section aria-labelledby` auf die H3.
- Aufzählungspunkte „•“ als eigene `span` ohne `aria-hidden` (Screenreader liest „Aufzählungszeichen“ doppelt) –
  echte `ul` mit CSS-Punkten.
- Knöpfe ohne Bezug zum Dokument („Teilen“, „Nicht teilen“) – `aria-label` bzw. `aria-describedby` mit Titel,
  wie „Bestätigen“ heute.
- Rückfrage als `div` ohne `role="group"`/`aria-labelledby` – kommt aus `ConfirmBox`.
- Knöpfe der Fläche in `font-label-md` 11 px (Mono) – in der App normale Knopfschrift (`nmp-button`).

### Bestehender Teil in v2 – nicht übernehmen (bestehende Umsetzung gilt)

- Abzeichen „Automatisch erkannt“ rechts oben in beiden Karten, eckig – App: unter der Zusammenfassung
  (`fields-recognition`, `Badge`).
- Knopfreihenfolge „Bearbeiten“ → „Bestätigen“ – App: „Bestätigen“ zuerst (nur bei `Automatic`), dann
  „Bearbeiten“.
- Angaben im 5-Spalten-Raster mit `truncate` an Absender/Art („Stadtwerke Mustersta…“) – App: Raster der
  Umsetzung, nichts abschneiden.
- Karten `surface-container-low` (v1/App: `surface-container`); Knopftexte der Karte in JetBrains Mono.
- Brotkrume „Posteingang / Prüfliste“ – gibt es in der App nicht; Rahmen (Seitenleiste „Server Archive“,
  „DE | EN“, `title=` an „Abmelden“) – App-Rahmen aus `MainLayout` gilt.
- `focus:outline-none` am Suchfeld, Symbole im Rahmen ohne `aria-hidden`, CDN/Google Fonts – wie oben.

### Nachbessern in Stitch?

Nein. S0-Vorgabe vollständig gezeigt. „Ja, teilen“ weiß statt neutral ist eine kleine Abweichung zum Plan;
in der App neutral umsetzen (oder der Nutzer entscheidet bei der Freigabe, ob „Ja, teilen“ weiß sein darf –
im Papierkorb ist „Ja, leeren“ weiß freigegeben).

Status (M4b-Nachtrag): Vorschlag – Freigabe durch den Nutzer offen
