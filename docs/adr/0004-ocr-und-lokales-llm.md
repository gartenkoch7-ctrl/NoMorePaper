# ADR-0004: Erkennung: OCR + lokales LLM (Ollama, CPU)

- **Status:** Angenommen
- **Datum:** 2026-11-18

## Entscheidung
- OCR erzeugt das PDF/A mit Textschicht (Engine/Wrapper entscheidet ein Spike).
- Ein **lokales LLM** über **Ollama** (eigener Container, CPU) ordnet zu: Absender, Dokumentart,
  Datum, Betrag, Fristen, Tags. Modell entscheidet ein Spike.
- Läuft asynchron im Worker; der Nutzer kann jede Zuordnung korrigieren.

## Verworfen
Nur Regeln (ungenau), Cloud-KI (verletzt ADR-0002).

## Folgen
Zuordnung kann pro Dokument Sekunden bis eine Minute dauern ⚠️ unverifiziert. GPU später möglich.
