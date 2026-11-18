# ADR-0013: Scan-Bearbeitung: Versionen, Wahl je PDF, Papierkorb

- **Status:** Angenommen
- **Datum:** 2026-11-18
- **Ergänzt:** [ADR-0003](0003-ablage-dateisystem-pdfa.md) (bleibt gültig, außer „Original bleibt immer“)

## Kontext
Scans müssen bearbeitet werden: drehen, löschen, zusammenfügen, teilen, umsortieren, zuschneiden
und entzerren. ADR-0003 sah vor, das Original immer zu behalten.

## Entscheidung
- Eine Bearbeitung erzeugt eine **neue Version** (PDF/A). Ab Werk bleiben **Original und
  bearbeitete Version** erhalten.
- **Je PDF** kann der Nutzer wählen, nur die bearbeitete Version oder nur das Original zu behalten.
  Der Werkswert ist in den Einstellungen änderbar.
- Ein entferntes Original kommt in einen **Papierkorb** und wird nach **30 Tagen** endgültig
  gelöscht. Bis dahin lässt es sich wiederherstellen. In älteren Backups bleibt es bis zu deren Ablauf.
- **Stapel trennen:** OCR und LLM schlagen Dokumentgrenzen (neuer Absender, Anrede, „Seite 1/3“) und
  leere Seiten vor. Der Nutzer bestätigt oder korrigiert.
- **Bedienung:** vollständig per Tastatur. Jede Seite hat eine Screenreader-Ansage (Seitenzahl,
  erkannter Inhalt, „vermutlich leer“), dazu eine große Vorschau mit Shader (ADR-0011).
  Drag & Drop gibt es nur zusätzlich.

## Verworfen
Original immer überschreiben (unumkehrbar), sofort löschen, Trennblätter, nur manuelles Trennen.

## Folgen
Ein Dokument hat eine oder mehrere Dateiversionen; Suche und OCR beziehen sich auf die aktuelle
Version. Der Papierkorb braucht einen täglichen Aufräumjob im Worker.
