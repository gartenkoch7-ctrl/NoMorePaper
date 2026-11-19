# 03 – Dokument ansehen (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen-ID fehlt noch
- **Dateien:** [code.html](code.html) aus Stitch; nur die leeren `<style class="darkreader …">`-Tags
  der Browser-Erweiterung Dark Reader sind entfernt. `screen.png` fehlt noch.
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/DocumentViewer.razor` (`/dokumente/{id}`),
  `Classification/DocumentFieldsPanel.razor`, `Classification/DocumentFieldsEditor.razor`

## Übernehmen (passt zur App)

- **Zurück-Leiste:** „Zurück zur Liste“ + Pfad Dokumente / Art / Titel; Eingangszeit daneben.
- **Kopf:** Titel, Abzeichen, Aktionsknöpfe in einer Reihe (Herunterladen, Löschen).
- **Zwei Spalten:** links der Viewer (≈ 7/12), rechts die Angaben (≈ 5/12); schmal untereinander.
- **Viewer-Werkzeugleiste:** Seite x von y mit vor/zurück, Zoom −/+, Prozentanzeige, Breite
  anpassen, Vollbild.
- **Reiter rechts:** „Angaben“ und **„Erkannter Text“** (OCR-Text lesbar und kopierbar – für
  Screenreader sehr nützlich, gibt es heute so nicht).
- **Hinweis „Automatisch erkannt“** oben in den Angaben.
- **Felder** Absender, Datum, Betrag, Tags als Chips mit „+ Tag“; Knopf „Änderungen übernehmen“.
- **Verlauf** als Zeitleiste (Eingang, erkannt, bestätigt, bearbeitet) – passt später zu M4b
  (Versionen).

## Abgleich mit der App

| Im Entwurf | In NoMorePaper |
|---|---|
| GoBD, WORM, S3, SHA-256-Leiste, RFC-3161-Zeitstempel, Aufbewahrungsfrist § 147 AO, Verfahrensdokumentation | gibt es nicht – weglassen; Reiter „GoBD & Audit“ entfällt |
| QES signieren, Freigeben, DATEV-Export | gibt es nicht |
| ZUGFeRD/XRechnung-Karte | gibt es nicht |
| Workflow-Freigaben (Stufe 2 von 2, Buchhaltung) | gibt es nicht; Reiter wird **„Verlauf“** |
| Interne Notiz | gibt es nicht (Idee für später) |
| Netto / USt / Gesamt, USt-IdNr., IBAN, Rechnungsnummer | App kennt nur **Betrag + Währung** |
| DATEV-Konto, Kostenstelle, Ordnerpfad | gibt es nicht; stattdessen **Art** |
| „Fälligkeit“ | erst M5 (Fristen) |
| „Konfidenz 99.8 %“ | gibt es nicht; nur „automatisch erkannt“ / „bestätigt“ |
| „Tesseract / Cloud OCR“ | **nie Cloud** (ADR-0002) – Text weglassen |
| „Automatisch gespeichert“ | App speichert erst mit „Speichern“; Status über Ansage |
| OCR-Layer-Schalter, Text auswählen | gibt es nicht |
| Drehen | erst M4b („Seiten bearbeiten“) |
| Dokument als nachgebautes HTML | echte Seite als **invertiertes Bild** (Shader, ADR-0011) |
| Dateiname als Überschrift, „1.84 MB“ | Überschrift = **Titel** des Dokuments |
| fehlt | **Zusammenfassung** zuerst, „Aus der Mail“, Duplikat-Hinweis, **Bestätigen**, Schalter **Privat**, **Bearbeiter**, Besitz-Prüfung („Passt so“ / „Weitergeben“) |
| fehlt | Downloads **Original** und **Archiv-PDF** getrennt; Knopf **„Originalfarben“**; Zurücksetzen-Zoom; Tastenhinweis |

## Beim Übernehmen nicht mitnehmen (§2.5)

- Viewer-Knöpfe nur als Symbol mit `title` – brauchen sichtbaren Text oder zugänglichen Namen.
- Reiter als einfache Knöpfe ohne `role="tab"`, `aria-selected`, Pfeiltasten.
- Felder als `div` neben `<label>` ohne Verbindung; Tag-Entfernen als `span` ohne Knopf und Namen.
- Schrift 10–14 px; Status als Punkt ohne Text; `focus:outline-none`.
- Tailwind-CDN und Google Fonts – Werte ins MudBlazor-Theme und `app.css` übertragen.
