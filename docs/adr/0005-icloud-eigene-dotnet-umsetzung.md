# ADR-0005: iCloud Drive: eigene .NET-Umsetzung, Spike in M1

- **Status:** Angenommen
- **Datum:** 2026-11-18

## Kontext
Apple bietet keine offizielle Server-API für iCloud Drive. Gepflegte inoffizielle Bibliotheken
gibt es nur in Python (pyicloud).

## Entscheidung
- Der iCloud-Zugriff (Anmeldung mit Apple-ID inkl. 2FA, Ordner lesen) wird **selbst in .NET**
  umgesetzt, als Eingangsquelle in Infrastructure.
- Weil das das größte technische Risiko ist, kommt ein **zeitbegrenzter Spike in M1**.
- Die Apple-ID-Sitzung wird verschlüsselt gespeichert; die 2FA-Erneuerung läuft über die Web-App.

## Verworfen
Python-Sidecar (zweiter Stack), Fertig-Tool wie rclone, Mac/PC als Brücke, iOS-Kurzbefehl.

## Folgen
Scheitert der Spike, wird per neuem ADR auf einen Fallback umgestellt (Kurzbefehl oder
Fertig-Tool). Apple-Änderungen können den Eingang jederzeit brechen → Telemetrie und klare
Fehlermeldung in der App.
