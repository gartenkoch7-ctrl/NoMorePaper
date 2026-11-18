# ADR-0008: Eingebautes, verschlüsseltes Backup

- **Status:** Angenommen
- **Datum:** 2026-11-18

## Entscheidung
- Nächtliches Backup von Datenbank-Dump **und** Dateien, verschlüsselt, auf NAS und zusätzlich
  an ein externes Ziel.
- Die Wiederherstellung ist dokumentiert und wird regelmäßig getestet.

## Verworfen
Nur Proxmox-Backups (kein Einzel-Restore), nur Export als Ordner.

## Folgen
Schlüsselverwaltung muss dokumentiert sein – ohne Schlüssel ist das Backup wertlos.
