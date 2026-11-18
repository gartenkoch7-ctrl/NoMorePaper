# ADR-0017: PDF-Darstellung im Browser mit PDF.js

- **Status:** Angenommen
- **Datum:** 2026-11-18

## Kontext
ADR-0011 verlangt, dass PDFs im Browser gerendert und per WebGL-Shader invertiert werden. Für
das Rendern braucht es eine feste Bibliothek, die scharf bis 1600 % zoomt und eine Textebene für
Screenreader liefert.

## Entscheidung
- **PDF.js 6.5.438**, Lizenz Apache-2.0.
- Die Dateien liegen im Repo unter `src/NoMorePaper.Web.Client/wwwroot/lib/pdfjs`, inklusive
  `cmaps`, `standard_fonts` und `wasm`. Es gibt **kein CDN**.
- Ein gemeinsamer **PDFWorker** und **ein WebGL-Kontext** für Viewer und Vorschaubilder.
- Nur der **sichtbare Ausschnitt** wird gerendert. Die Darstellung ist scharf bis **1600 %**.
- Die **PDF.js-TextLayer** liefert den Text für Screenreader.

## Verworfen
- **Serverseitige Seitenbilder:** Zoomen wäre nur in Stufen möglich, und es entsteht mehr
  Datenverkehr.

## Folgen
- Der Download der Web-Oberfläche wird größer, weil PDF.js mit Schriften und Wasm ausgeliefert
  wird.
- Pinch-Zoom auf Android ist noch nicht umgesetzt.
- Nach einem Verlust des WebGL-Kontexts wird er erst beim nächsten Öffnen des Viewers wieder
  aufgebaut.
- Die Werte des Shaders stehen in [ADR-0019](0019-darstellung-werte-aus-spike.md) und
  [darstellung.md](../kb/darstellung.md).
