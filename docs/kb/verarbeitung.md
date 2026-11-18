# Verarbeitung: Pipeline vom Upload bis Done

Beschreibt, wie ein Dokument im Worker verarbeitet wird. Entscheidung und Begründung der Werkzeuge:
[ADR-0016](../adr/0016-ocrmypdf-und-ubuntu-worker-image.md).

## Ablauf

1. **Upload:** Die API nimmt die Datei an (max. 100 MB) und legt sie im Eingangsordner `inbox`
   ab. Dazu gibt es einen Eintrag in der Warteschlange `processing_jobs`.
2. **Warteschlange:** Der Worker holt den Job mit `FOR UPDATE SKIP LOCKED` und zählt dabei
   `attempt` hoch. Die Sperre hält 15 Minuten.
3. **Worker:**
   1. `StartProcessing` setzt den Status auf „Processing“.
   2. Das Original wird in einen eigenen Arbeitsordner `/data/tmp/attempt-<guid>` kopiert. OCR
      läuft dort, Ergebnis ist `archive.pdf`.
   3. Das Original wird ins Archiv verschoben.
   4. Status „Done“.
   5. Der Arbeitsordner wird gelöscht, auch nach einem Fehler.
4. **Fehler:** Der Job wird nach 1, 5 und 30 Minuten erneut versucht. Nach 4 Versuchen steht er
   auf „Failed“. Gespeichert wird nur der technische Grund, ohne Dokumentinhalt.
5. **Dauerhafte Fehler:** Kann die Eingabe selbst nicht verarbeitet werden, steht das Dokument
   sofort auf „Failed“, ohne weitere Versuche. Das gilt für OCRmyPDF-Exit-Code 2 (ungültige
   Eingabe) und 8 (verschlüsseltes PDF) und für nicht unterstützte Medientypen.

## Besitz eines Jobs

- Die Versuchsnummer, die das Holen zurückgibt, ist der Ausweis des Workers. Starten,
  Abschließen, Fehlschlag und Zurückgeben wirken nur, solange der Job noch diese Nummer hat
  (`WHERE document_id = … AND attempt = …`, Zeile mit `FOR UPDATE` gesperrt).
- Läuft die Sperre ab und holt ein anderer Worker den Job, zählt er einen neuen Versuch. Der alte
  Worker ändert danach nichts mehr an Job und Dokument, sein Ergebnis wird verworfen (Log 2110).
- **Herunterfahren:** Wird der Worker mitten in einem Dokument beendet, gibt er den Job zurück:
  `locked_until = NULL`, `attempt - 1`. Der Job ist sofort wieder fällig und der abgebrochene
  Versuch zählt nicht. Dafür bleiben höchstens 5 Sekunden.

## Arbeitsordner

- Arbeitskopien und temporäre Dateien der Werkzeuge liegen in `/data/tmp` auf dem Daten-Volume,
  nicht in `/tmp`. `/tmp` ist im Container ein tmpfs, also Arbeitsspeicher; ein Scan mit seinen
  Zwischenbildern braucht leicht Hunderte MB.
- Die Werkzeuge bekommen `TMPDIR` auf den Arbeitsordner des Versuchs gesetzt. OCRmyPDF,
  Tesseract und Ghostscript schreiben ihre Zwischendateien so dorthin und sie verschwinden mit
  dem Ordner, auch wenn ein Werkzeug wegen Zeitüberschreitung beendet wird.
- Der Worker selbst behält `/tmp` als `TMPDIR` (nur kleine Dateien der .NET-Laufzeit).
- **Beim Start** legt der Worker `/data/tmp` an und löscht darin alles, was älter als einen Tag
  ist. Das sind Reste von Versuchen, bei denen der Worker hart beendet wurde (Absturz, Speicher).
  Jüngere Einträge bleiben, weil ein zweiter Worker auf demselben Volume sie noch nutzen könnte.
- Der Server ist davon nicht betroffen; er puffert Uploads weiter in seinem `/tmp`.

## Werkzeuge und Pakete

| Schritt | Werkzeug | Hinweis |
|---|---|---|
| OCR und PDF/A | OCRmyPDF 15.2 | Tesseract `deu+eng`, Aufruf `--output-type pdfa --rotate-pages --deskew --skip-text --jobs 2 --invalidate-digital-signatures`, Zeitlimit 10 Minuten je Dokument. Exit-Codes: 2 ungültige Eingabe, 8 verschlüsselt (beide dauerhaft). |
| Bilder zu PDF | img2pdf 0.5.1 | JPG und PNG. PNG mit Alphakanal (geprüft: RGBA und Graustufen mit Alpha, auch mit schwarzen durchsichtigen Pixeln) übernimmt img2pdf als weiche Maske (`/SMask`); OCRmyPDF rendert sie auf Weiß und erkennt den Text. OCRmyPDF selbst lehnt Bilder mit Alphakanal ab, deshalb gehen Bilder immer erst durch img2pdf. |
| HEIC | heif-convert | Paket `libheif-plugin-libde265` nötig, danach img2pdf |
| Text | pdftotext | Textextraktion |
| Basis | `mcr.microsoft.com/dotnet/aspnet:10.0` | Ubuntu 24.04, 691 MB auf der Platte, 189 MB komprimiert |

Python läuft nur als Werkzeug im Container. Die CI installiert dieselben apt-Pakete.

### Signierte PDFs

OCRmyPDF verweigert ein PDF mit digitaler Signatur (erkannt an `/SigFlags` im Formular-Wörterbuch)
mit Exit-Code 2. Mit `--invalidate-digital-signatures` läuft es durch, die Signatur ist danach
nur im Archiv-PDF ungültig. Das Original mit gültiger Signatur bleibt unverändert im Archiv und
ist per Download erreichbar.

## OCR-Tests lokal

Auf Windows fehlen die Werkzeuge, die OCR-Tests werden übersprungen. Im Container laufen sie so:

```powershell
# Einmal: SDK-Image mit denselben apt-Paketen wie in der CI bauen (Dockerfile mit FROM mcr.microsoft.com/dotnet/sdk:10.0 + apt-get install aus ci.yml)
docker run --rm -v "${PWD}:/src:ro" <image> bash -c 'mkdir /work && cd /src && tar --exclude=bin --exclude=obj -cf - . | tar -xf - -C /work && cd /work && dotnet test --project tests/NoMorePaper.Infrastructure.Tests/NoMorePaper.Infrastructure.Tests.csproj -- --filter-namespace NoMorePaper.Infrastructure.Tests.Ocr'
```

Die Datenbanktests brauchen Docker im Container und laufen deshalb besser auf dem Host.

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
| Gescanntes PDF, 8 Seiten, 300 dpi (img2pdf), Upload bis Done | 13–14 s |
| PNG mit Alphakanal, 1 Seite, 300 dpi | 3,0 s |

Das Akzeptanzkriterium (Upload bis Done in unter 2 Minuten auf dem Server) ist damit noch
nicht gemessen. Siehe [m1-durchstich.md](../plan/m1-durchstich.md).

## Bekannte Lücken

- Wird der Worker hart beendet (Absturz, `kill -9`), bleibt der Job bis zu 15 Minuten gesperrt
  und zählt einen Versuch. Nach vier solchen Abbrüchen steht das Dokument auf „Failed“.
- Ein Worker, dessen Sperre abgelaufen ist, kann das Archiv-PDF noch überschreiben, bevor sein
  Abschluss abgelehnt wird. Das braucht einen Versuch über 15 Minuten, OCR bricht nach 10 ab.
- Fehler von img2pdf und heif-convert (z. B. ein kaputtes Bild) gelten nicht als dauerhaft und
  werden viermal versucht.
