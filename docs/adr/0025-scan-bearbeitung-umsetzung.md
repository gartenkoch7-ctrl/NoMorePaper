# ADR-0025: Scan-Bearbeitung – Umsetzung, Fassungen und Papierkorb

- **Status:** Angenommen
- **Datum:** 2026-11-19
- **Ergänzt:** [ADR-0013](0013-scan-bearbeitung-versionen-papierkorb.md) (bleibt gültig)

## Kontext
ADR-0013 legt fest, was die Scan-Bearbeitung kann. Offen waren Bedienung, Rechte, Umfang von Fassungen
und Papierkorb, die Werkzeuge und wo gerechnet wird. Grilling 2026-11-19.

## Entscheidung
- **Bedienung:** eigene Seite „Seiten bearbeiten“; Änderungen werden gesammelt und mit „Speichern“ als
  **eine** neue Fassung angelegt.
- **Rechte:** bearbeiten, zusammenfügen, teilen = Bearbeitungsrecht aus M3 (Besitzer, Bearbeiter); Original
  endgültig entfernen und Papierkorb leeren = nur Besitzer.
- **Fassungen:** es bleiben Original und **neueste** bearbeitete Fassung (Datei-Art `Edited`); das Archiv
  (PDF/A, Suche) entsteht immer aus der aktuellen Quelle (bearbeitet, sonst Original). Werkswert je Konto
  (beides / nur bearbeitet / nur Original), je Dokument änderbar.
- **Papierkorb** für Dokumente, Originale und ältere Fassungen; 30 Tage; Dokumente bleiben als Zeile mit
  `deleted_at` (überall ausgeblendet), Dateien von Fassungen wandern in einen Papierkorb-Ordner; ein
  täglicher Dienst im Worker löscht endgültig.
- **Werkzeuge:** Seitenoperationen (wählen, drehen, umsortieren, zusammensetzen, teilen) im Worker mit
  **qpdf** (Apache-2.0, verlustfrei); danach OCRmyPDF wie beim Eingang. **Zuschnitt und Geraderichten im
  Browser** mit OpenCV.js (wie die Kamera, ADR-0024); die Seite wird als Bild ersetzt.
- **Zusammenfügen** in das geöffnete Dokument; ist das angehängte privat, wird das Ergebnis privat.
  **Teilen** erzeugt neue Dokumente, die neu erkannt werden; das Ausgangsdokument kommt in den Papierkorb.
- **Stapel-Trennung** für mehrseitige Scans: Heuristik (leere Seite, Seitenzähler, Anrede) und ein
  LLM-Aufruf mit den Seitenanfängen; nur Vorschlag in „Zu prüfen“.

## Verworfen
- Jede Änderung sofort speichern (viele Fassungen und OCR-Läufe).
- Alle Fassungen dauerhaft behalten (Platz, lange Liste).
- Zuschnitt auf dem Server (keine Vorschau und Ansage beim Einstellen der Ecken).
- PDFsharp für Seitenoperationen (verliert Strukturen bestehender PDFs leichter als qpdf; PDFsharp bleibt für
  Bilder → PDF).
- Gelöschte Dokumente sofort entfernen (unumkehrbar).

## Folgen
- Jede Abfrage über Dokumente braucht den Papierkorb-Filter; ein Architekturtest prüft rohe SQL-Abfragen.
- Ein Dokument belegt bis zu dreimal Platz (Original, bearbeitet, Archiv), im Papierkorb zusätzlich 30 Tage.
- Eine zugeschnittene Seite verliert eine vorhandene Textebene und wird neu erkannt.
- Der Worker braucht `qpdf` im Image.
