# ADR-0012: Fristen in den iCloud-Kalender per CalDAV

- **Status:** Angenommen
- **Datum:** 2026-11-18

## Kontext
Erkannte Fristen sollen im Kalender auf dem iPhone landen. Ein Termin im iCloud-Kalender liegt
zwangsläufig bei Apple – das ist eine **bewusste Ausnahme** von ADR-0002.

## Entscheidung
- Jedes Konto verbindet optional den **eigenen iCloud-Kalender** per **CalDAV** mit einem
  app-spezifischen Passwort, das verschlüsselt gespeichert wird. NoMorePaper schreibt in einen
  eigenen Kalender „NoMorePaper“.
- Ein Termin enthält nur einen **Titel und einen Link** (z. B. „Kündigungsfrist:
  Kfz-Versicherung“), keinen Dokumentinhalt und keine Beträge.
- **Übernahme je Fristart einstellbar** (Zahlung, Kündigung, Termin, Ablauf …): automatisch,
  nach Bestätigung oder nie. Ab Werk gilt „nach Bestätigung“.
- **Einseitig:** NoMorePaper ist führend. Ändern, Erledigen oder Löschen in der App aktualisiert
  den Termin, Änderungen im Kalender werden überschrieben.
- **Erinnerung:** Alarm im Termin und zusätzlich ein Hinweis über HA (ADR-0007), der Vorlauf ist je
  Fristart einstellbar.
- Fristen aus **privaten** Dokumenten landen nur im Kalender des Besitzers.

## Verworfen
Kalender-Abo per ICS (nur lesend, Abruf nur bei aktivem VPN), beidseitiger Abgleich (Konflikte),
ausführliche Termine (zu viele Daten bei Apple), ein gemeinsamer Haushaltskalender.

## Folgen
Die Zuordnung zwischen Frist und Termin (UID) wird gespeichert, damit Termine aktualisiert und
gelöscht werden können. Der CalDAV-Abgleich läuft im Worker.
