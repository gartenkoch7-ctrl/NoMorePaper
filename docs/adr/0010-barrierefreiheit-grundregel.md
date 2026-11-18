# ADR-0010: Barrierefreiheit als Grundregel, ab M1

- **Status:** Angenommen
- **Datum:** 2026-11-18

## Kontext
Der Hauptnutzer ist blind bzw. stark sehbehindert. Ohne barrierefreie Oberfläche ist die App
für ihn nicht nutzbar.

## Entscheidung
- **Jede Oberfläche** ist dunkel und kontrastreich, mit großer Schrift und deutlichem
  Fokusrahmen, und vollständig per Screenreader (VoiceOver, NVDA) bedienbar.
- Der Viewer zoomt **stufenlos und scharf**: Er rendert das PDF bei jeder Zoomstufe neu (vektorbasiert),
  statt Pixel zu strecken.
- Der OCR-Text ist für Screenreader als echter Text verfügbar.
- Umsetzung ab **M1**: Der Durchstich zeigt Dokumente bereits barrierefrei.

## Verworfen
Eigener späterer Meilenstein oder Nachrüsten vor dem Release (Nachrüsten ist teuer).

## Folgen
Jede UI-Aufgabe hat Barrierefreiheit in der Definition of Done. MudBlazor-Theme wird dunkel mit
hohem Kontrast angelegt.
