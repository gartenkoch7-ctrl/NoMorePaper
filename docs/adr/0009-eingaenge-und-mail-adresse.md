# ADR-0009: Eingänge im MVP und eigene Mail-Adresse

- **Status:** Angenommen
- **Datum:** 2026-11-18

## Entscheidung
- Im MVP: Upload im Browser, Kamera in der PWA, Scanner-Ordner (SMB), IMAP, iCloud Drive.
- Für Mail gibt es eine **eigene Adresse nur für Dokumente**; alles darin wird verarbeitet.
- Das Handy wird als **PWA** angebunden (kein Web Share Target auf iOS).

## Verworfen
Bestehendes privates Postfach mit Filter (Server hätte Zugriff auf alles), native App im MVP.

## Folgen
Großer MVP; Eingänge sind als gemeinsames Interface gebaut, damit sie parallel entstehen können.
