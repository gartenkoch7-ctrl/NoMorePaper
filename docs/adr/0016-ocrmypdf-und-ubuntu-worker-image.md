# ADR-0016: OCRmyPDF im Worker und Ubuntu-Basis für das Worker-Image

- **Status:** Angenommen
- **Datum:** 2026-11-18

## Kontext
Der Worker erkennt Text in gescannten Dokumenten und legt ein durchsuchbares PDF/A ab
(ADR-0004, ADR-0003). Dazu kommen Bilder (JPG, PNG) und HEIC, die vorher in ein PDF gewandelt
werden müssen, sowie die Textextraktion aus PDFs. Das Worker-Image muss all diese Werkzeuge
mitbringen.

## Entscheidung
- **OCR mit OCRmyPDF** im Worker, Sprachen Tesseract `deu+eng`, Aufruf mit
  `--output-type pdfa --rotate-pages --deskew --skip-text --jobs 2`.
- **Zeitlimit 10 Minuten je Dokument.**
- **Bilder** (JPG, PNG) werden mit **img2pdf** in PDF gewandelt.
- **HEIC** wird mit **heif-convert** gewandelt. Dafür wird das Paket `libheif-plugin-libde265`
  benötigt.
- **Text** kommt über **pdftotext**. Der Text wird bei **500 000 Zeichen** abgeschnitten, weil
  die Postgres-Grenze für `tsvector` bei 1 MB liegt.
- **Basis des Worker-Images:** `mcr.microsoft.com/dotnet/aspnet:10.0`, also **Ubuntu 24.04**.
  Größe: 691 MB auf der Platte, 189 MB komprimiert.

## Verworfen
- **Tesseract plus eigene .NET-PDF/A-Erzeugung:** zu viel Eigenbau für PDF/A.
- **Separater OCR-Container** als eigener Dienst.
- **Alpine als Basis:** nicht gewählt. Ubuntu hat die nötigen Pakete und enthält ICU.

## Folgen
- Python läuft nur als Werkzeug im Container (OCRmyPDF, img2pdf). Die Anwendung selbst bleibt
  .NET.
- Die CI installiert dieselben apt-Pakete wie das Image, damit Tests und Produktion gleich
  sind.
- OCRmyPDF liefert Exit-Code 2 bei ungültiger Eingabe.
- Das Image ist deutlich größer als ein Alpine-Image. Siehe [build-und-tooling.md](../kb/build-und-tooling.md)
  für die Hinweise zum lokalen Bauen und zu den OCR-Tests.
- Ablauf und Grenzen der Pipeline stehen in [verarbeitung.md](../kb/verarbeitung.md).
