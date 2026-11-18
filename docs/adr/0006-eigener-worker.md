# ADR-0006: Verarbeitung in eigenem Worker-Container

- **Status:** Angenommen
- **Datum:** 2026-11-18

## Entscheidung
Server (API/UI) und **Worker** (OCR, LLM, Abholen von Ordner/IMAP/iCloud) sind getrennte
Prozesse/Container. Die Warteschlange liegt in Postgres.

## Verworfen
Hintergrunddienste im Server-Prozess (ein Absturz trifft alles, lange Läufe bremsen die UI).

## Folgen
Zwei Composition Roots; Jobs müssen idempotent und wiederaufnehmbar sein.
