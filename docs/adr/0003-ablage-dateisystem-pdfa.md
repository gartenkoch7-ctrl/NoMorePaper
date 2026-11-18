# ADR-0003: Ablage: Original + PDF/A im Dateisystem, Metadaten in Postgres

- **Status:** Angenommen, ergänzt durch [ADR-0013](0013-scan-bearbeitung-versionen-papierkorb.md)
- **Datum:** 2026-11-18

## Entscheidung
- Das **Original** bleibt unverändert, dazu ein durchsuchbares **PDF/A**.
- Dateien liegen in einem Volume in lesbarer Ordnerstruktur, Metadaten und Volltext in Postgres.
- Ordnung über **Felder + Tags**, nicht über Ordnerbäume.

## Verworfen
Dateien als BLOB in Postgres (große DB, ohne App unzugänglich), nur Archiv-PDF (unumkehrbar),
Ordnerbaum (ein Dokument nur an einem Ort).

## Folgen
Backup muss DB **und** Dateien konsistent sichern (ADR-0008). Dokumente bleiben ohne App lesbar.
