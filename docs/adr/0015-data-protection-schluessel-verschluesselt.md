# ADR-0015: Data-Protection-Schlüsselring mit Zertifikat verschlüsseln

- **Status:** Angenommen (Umsetzung vor dem ersten Speichern von Zugangsdaten, spätestens M4)
- **Datum:** 2026-11-18

## Kontext
Zugangsdaten (IMAP, Apple-ID-Sitzung, CalDAV-Passwort) werden mit ASP.NET Data Protection
verschlüsselt (AGENTS.md §2.4). Der Schlüsselring lag in M0 unverschlüsselt im Volume `dpkeys`.
Ein Backup beider Volumes (ADR-0008) enthielte damit Daten **und** Schlüssel.

## Entscheidung
- Der Schlüsselring wird mit einem **Zertifikat** verschlüsselt (`ProtectKeysWithCertificate`).
- Das Zertifikat liegt als Datei **außerhalb** der Daten-Volumes und wird per Bind-Mount
  schreibgeschützt eingebunden; sein Passwort steht in `.env`.
- Das Zertifikat wird **getrennt** von den Backups gesichert (z. B. Passwortmanager).

## Folgen
Ohne Zertifikat ist ein Backup für Angreifer nutzlos – aber auch für die Wiederherstellung. Die
Restore-Anleitung (M7) muss das Zertifikat ausdrücklich nennen. Server und Worker starten nicht,
wenn das Zertifikat fehlt.
