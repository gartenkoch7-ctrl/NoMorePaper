# 16 – Fassungen im Viewer (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `bd92dc1f4e1548d3b778144b7f14f31c` (v3),
  HTML-Datei `projects/17713397687229704853/files/2cd26cef925046438ba679dbea47cde5`
- **Ersetzt:** v2 `be2cede630c04e43ab9c024a0b33413a` (Rückfrage „Original in den Papierkorb legen?“; Stand im Commit
  `15c7f89`), davor v1 `8944a0b58f7940e3b4313018cebbb2bd`
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** geplanter Bereich „Fassungen“ (`WC/Editing/VersionsSection.razor`, P2d) und Knopf „In den
  Papierkorb“ (`WC/Trash/MoveToTrashButton.razor`, P3c) in `src/NoMorePaper.Web.Client/Pages/DocumentViewer.razor`;
  noch nicht gebaut
- **Grundlage:** Detailplan [m4b-scan-bearbeitung-detail.md](../../../plan/features/m4b-scan-bearbeitung-detail.md)
  („Entschieden“, „Stitch-Nachtrag (S0)“, P2d, P3c), [ADR-0013](../../../adr/0013-scan-bearbeitung-versionen-papierkorb.md),
  [ADR-0025](../../../adr/0025-scan-bearbeitung-umsetzung.md)
- **Prompt:** vom Agenten erzeugt (2026-11-23) aus [prompts.md](../prompts.md) §4.3; v3 mit den S0-Nachträgen

## Übernehmen (passt zur App)

- **Nur Design-Token** von „Obsidian Slate“, monochrom, kein Rot, kein Grün. ✔
- **Brotkrume** „Dokumente / Stromrechnung März“, „Dokumente“ in der Navigation aktiv. ✔
- **Kopf des Viewers:** H1 Dokumenttitel, Abzeichen „Privat“ (Symbol + Text), Knöpfe **„Original“** und **„Archiv-PDF“**
  (Herunterladen) und **„In den Papierkorb“** – alle neutral (S0). ✔ „Weitere Aktionen“ und „Fassung 2 von 2“ entfernt. ✔
- **Rückfrage „Dokument in den Papierkorb legen?“** in der Seite unter dem Kopf: „Es bleibt 30 Tage im Papierkorb und
  kann bis dahin wiederhergestellt werden.“, „Abbrechen“ zuerst, „Ja, in den Papierkorb“ **neutral** (P3c,
  `ConfirmBox Id="viewer-trash"`). ✔
- **Reiter** Angaben / Erkannter Text / Verlauf (`TabList`); „Fassungen“ im Reiter „Angaben“ unter den Angaben, H2. ✔
- **Liste der Fassungen** als `ul`/`li`: Art („Original“ / „Bearbeitet“), Datum (Mono), Zustand „wird angezeigt“ als
  Symbol + Text, **„Herunterladen“ je Fassung** (S0, Entschieden „Plan B“). ✔
- **Wahl „Behalten“** als `fieldset`/`legend`, drei Radios **mit Erklärzeile** (Beides / Nur bearbeitete Fassung / Nur
  Original). ✔ „Auswahl speichern“ als Hauptknopf (weiß). ✔
- **Zustand „Wird bearbeitet …“** („Die neue Fassung erscheint, sobald sie fertig ist.“) mit Symbol (S0). ✔
- **Dokument ohne Bearbeitung:** „Noch keine bearbeitete Fassung. Mit „Seiten bearbeiten“ legst du eine an.“ (S0). ✔
- Knopf „Seiten bearbeiten“ (im Code Link `nmp-button` nach `/dokumente/{id}/seiten`). „Ansehen“ am Original entfernt. ✔
- Viewer-Leiste: Seite x von y, Zoom −/+ mit `aria-label`; Vollbild entfernt. ✔

## Abgleich mit dem Plan M4b

| Im Entwurf | Plan M4b / heutiger Viewer |
|---|---|
| Viewer-Leiste nur −/100 %/+ | heutiger Viewer hat zusätzlich Zurücksetzen und „Breite anpassen“ – beibehalten (Leiste ändert sich in M4b nicht) |
| Zustand „wird angezeigt“ ohne „Aktiv“ | P2d: „Aktiv – wird angezeigt“; Symbol `visibility` doppelt (Zeilen-Symbol und Zustand) – Zeilen-Symbol im Code je Art (`description`/`edit_document`) |
| Rückfrage zu „Nur bearbeitete Fassung“/„Nur Original“ nicht gezeigt | `ConfirmBox Id="versions-confirm"` mit Text aus v2 (Commit `15c7f89`), neutral – im Plan genau beschrieben |
| Hinweis „Nicht behaltene Fassungen kommen 30 Tage in den Papierkorb.“ fehlt | durch die Erklärzeilen abgedeckt; im Code optional (P2d nennt ihn) |
| „Wird bearbeitet“ und „Noch keine bearbeitete Fassung“ als Kästen **unter** dem Panel, gleichzeitig mit der Wahl | Beispielzustände: im Code **in** `VersionsSection`; ohne Bearbeitung **keine** Wahl, bei „Wird bearbeitet“ kein „Seiten bearbeiten“ |
| „In den Papierkorb“ immer sichtbar | nur für den Besitzer (`IsMine`); „Original“ nur, wenn vorhanden (statisch nicht darstellbar) |
| Rückfrage-Titel „Dokument in den Papierkorb legen?“ | P3c: „„{Titel}“ in den Papierkorb legen?“; danach `NoticeBox` Erfolg + Link „Zur Dokumentliste“ (nicht entworfen, Baustein vorhanden) |
| Erfolgs-/Fehleransage nach „Auswahl speichern“ fehlt | `LiveRegion` bzw. `NoticeBox Announce` – Bausteine vorhanden |
| Seite als nachgebautes HTML (Rechnung) | echte Seite als invertiertes Bild (Shader, ADR-0011) – nur Platzhalter |

## Beim Übernehmen nicht mitnehmen (§2.5)

- Reiter mit `role="tablist"`/`tab`, aber ohne `tabpanel`/`aria-controls`, Pfeiltasten – App nutzt `TabList`.
- „Fassungen“ als `div` mit H2 – im Code `section aria-labelledby="versions-heading"`.
- Alle Download-Knöpfe heißen „Herunterladen“ und sind `button`s – im Code Links mit eindeutigem Namen
  („Herunterladen: Original“ / „Herunterladen: Bearbeitete Fassung“).
- Radios mit `focus:ring-0`, Suchfeld `focus:outline-none focus:ring-1` – kein eigener Fokusring (ADR-0026);
  Erklärzeilen im Code per `aria-describedby`.
- Rückfrage als `section` mit `id="dialog-title"`; Inline-Skript (Hervorheben, `opacity-40`) – im Code `ConfirmBox`,
  Fokus beim Öffnen auf die Überschrift, nach Abbrechen zurück auf den Auslöser.
- `select-none` am Seitenbereich; Knöpfe mit fester Höhe `h-9`/`h-10` (Text muss umbrechen dürfen).
- Abzeichen „Privat“ und „Hochladen“ in Großbuchstaben (`uppercase`) – App nutzt `PrivateBadge` bzw. normale Schreibung.
- Symbole der Seitenleiste und des Kopfes ohne `aria-hidden`; Navigationen ohne `aria-label`; Sprachumschalter ohne
  `aria-pressed`; „Hochladen“ und „Abmelden“ als `button` statt Link; kein „Zum Inhalt springen“ (kommt aus `MainLayout`).
- Dokumentinhalt mit `h3`/Rasterzeilen (Rechnungsnachbau) – entfällt.
- Inline-`shadow-[…rgba…]`, `::-webkit-scrollbar{display:none}`, Material-Symbols-Stylesheet doppelt, Tailwind-CDN und
  Google Fonts (ADR-0002).

## Schrift

`label-sm`/`label-md`/`label-lg` = `["JetBrains Mono"]`, `headline-*`/`body-*` = `["Geist"]`, jeweils **ohne
Ausweichschrift**. Das `screen.png` zeigt Mono-Stellen (Kopfknöpfe, Datum, Leiste, „Hochladen“, „Abmelden“) in einer
Serifenschrift: Render-Artefakt, keine Designabweichung. Ursache vermutlich die Google-Fonts-Anfrage
`JetBrains+Mono:wght@100..900` (die Schrift reicht nur bis 800), sodass die Mono nicht lädt und ohne Ausweichschrift die
Browser-Standardschrift greift. In der App kommt die Schrift aus dem Design-System mit `monospace` als Ausweichschrift.

## Nachbessern in Stitch?

Nein, nicht nötig: alle S0-Punkte sind gezeigt; die offenen Punkte (Viewer-Leiste wie heute, Rückfrage zur Wahl aus v2,
Zustände nur in `VersionsSection`, Erfolgsansagen) sind entweder unverändert bestehende App-Teile oder im Detailplan
genau beschrieben. Optional: „Aktiv – wird angezeigt“ und die Rückfrage zur Wahl noch einmal in v3 zeigen.

Status: freigegeben durch den Nutzer (2026-11-23); Restpunkte werden beim Umsetzen nach Plan behoben
