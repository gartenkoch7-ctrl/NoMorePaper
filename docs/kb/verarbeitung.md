# Verarbeitung: Pipeline vom Upload bis Done

Beschreibt, wie ein Dokument im Worker verarbeitet wird. Entscheidung und Begründung der Werkzeuge:
[ADR-0016](../adr/0016-ocrmypdf-und-ubuntu-worker-image.md).

## Ablauf

1. **Upload:** Die API nimmt die Datei an (max. 100 MB) und legt sie im Eingangsordner `inbox`
   ab. Dazu gibt es einen Eintrag in der Warteschlange `processing_jobs`.
2. **Warteschlange:** Der Worker holt den Job mit `FOR UPDATE SKIP LOCKED`. Die Sperre hält
   15 Minuten.
3. **Worker:**
   1. `StartProcessing` setzt den Status auf „Processing“.
   2. OCR läuft in einen temporären Ordner, Ergebnis ist `archive.pdf`.
   3. Das Original wird ins Archiv verschoben.
   4. Status „Done“.
4. **Fehler:** Der Job wird nach 1, 5 und 30 Minuten erneut versucht. Nach 4 Versuchen steht er
   auf „Failed“. Gespeichert wird nur der technische Grund, ohne Dokumentinhalt.

## Werkzeuge und Pakete

| Schritt | Werkzeug | Hinweis |
|---|---|---|
| OCR und PDF/A | OCRmyPDF | Tesseract `deu+eng`, Aufruf `--output-type pdfa --rotate-pages --deskew --skip-text --jobs 2`, Zeitlimit 10 Minuten je Dokument. Exit-Code 2 heißt ungültige Eingabe. |
| Bilder zu PDF | img2pdf | JPG und PNG |
| HEIC | heif-convert | Paket `libheif-plugin-libde265` nötig, danach img2pdf |
| Text | pdftotext | Textextraktion |
| Basis | `mcr.microsoft.com/dotnet/aspnet:10.0` | Ubuntu 24.04, 691 MB auf der Platte, 189 MB komprimiert |

Python läuft nur als Werkzeug im Container. Die CI installiert dieselben apt-Pakete.

## Limits

- Upload: max. 100 MB.
- Text: wird bei 500 000 Zeichen abgeschnitten. Postgres erlaubt für `tsvector` höchstens 1 MB.
- OCR: 10 Minuten je Dokument.

## Gemessene Zeiten

Gemessen auf dem Windows-Entwicklungs-PC in Docker, nicht auf dem Server.

| Eingang | Dauer |
|---|---|
| Gescannter Brief, 3 Seiten, 300 dpi | 5,9 s |
| JPEG-Foto | 2,1 s |

Das Akzeptanzkriterium (Upload bis Done in unter 2 Minuten auf dem Server) ist damit noch
nicht gemessen. Siehe [m1-durchstich.md](../plan/m1-durchstich.md).

## Bekannte Lücken

- Wird der Server mitten in einem Dokument beendet, bleibt der Job bis zu 15 Minuten gesperrt
  und zählt einen Versuch.
- Nach einem Abbruch bleiben `.tmp`-Dateien liegen. Das Aufräumen fehlt noch.
