# ADR-0007: Home Assistant: eigene Integration, Hinweise über HA

- **Status:** Angenommen
- **Datum:** 2026-11-18

## Entscheidung
- Eine eigene **HA-Integration** (Python, HACS) fragt die NoMorePaper-API ab und stellt
  Sensoren bereit (neue Dokumente, offene Fristen).
- Erinnerungen an Fristen laufen **über Home Assistant** (Companion-App), keine eigene
  Push-Infrastruktur im MVP.
- Die Integration authentifiziert sich mit einem eigenen API-Token mit Leserechten.

## Verworfen
MQTT-Discovery, Aufruf der HA-REST-API mit Long-Lived-Token, Web Push (später möglich), E-Mail.

## Folgen
Zweites Teilprojekt in Python (`ha-integration/`); die API braucht stabile, versionierte
Endpunkte für die Integration.
