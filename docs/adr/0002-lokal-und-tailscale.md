# ADR-0002: Lokal und selbst gehostet, Zugriff nur über Tailscale

- **Status:** Angenommen
- **Datum:** 2026-11-18

## Kontext
Verträge, Steuer- und Arztunterlagen sind hochsensibel.

## Entscheidung
- Alles läuft per Docker Compose auf dem eigenen Proxmox-Server.
- Dokumente, OCR-Text und Metadaten verlassen nie das eigene Netz (keine Cloud-OCR/-LLMs).
- Der Server ist nicht öffentlich erreichbar; Zugriff von unterwegs nur über **Tailscale**.

## Verworfen
Cloud-Speicher (Daten bei Dritten), öffentliches HTTPS über Caddy (Angriffsfläche).

## Folgen
Handys brauchen Tailscale. Erkennung ist auf lokale Rechenleistung (CPU) begrenzt.
