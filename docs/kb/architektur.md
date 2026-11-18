# Architektur

Regeln: [AGENTS.md §2.2](../../AGENTS.md), Entscheidung: [ADR-0001](../adr/0001-stack-dotnet-blazor-postgres.md).

## Projekte

| Projekt | Rolle | Kennt |
|---|---|---|
| `NoMorePaper.Domain` | Reine Logik: Dokument, Felder, Tags, Fristen, Duplikat-Regeln | – |
| `NoMorePaper.Application` | Anwendungsfälle, Interfaces für Speicher, OCR, LLM, Eingänge | Domain |
| `NoMorePaper.Infrastructure` | EF Core/Postgres, Dateiablage, OCR, Ollama, IMAP, iCloud, Ordner | Application |
| `NoMorePaper.Contracts` | DTOs der API | – |
| `NoMorePaper.Server` | ASP.NET Core API, Anmeldung, liefert Web.Client aus; Composition Root | Infrastructure, Contracts |
| `NoMorePaper.Worker` | Verarbeitung und Abholen; Composition Root | Infrastructure |
| `NoMorePaper.Web.Client` | Blazor WASM + MudBlazor, PWA | Contracts |
| `ha-integration/` | Home-Assistant-Integration (Python, HACS), spricht nur die API | – |

## Laufzeit (Docker Compose)

```
Handy/PC ──Tailscale──► server ──► postgres ◄── worker ──► ollama
                           │                      │
                           └──── /data (Volume) ──┘   worker holt ab: SMB-Ordner, IMAP, iCloud
```

## Datenfluss eines Dokuments
1. **Eingang** (Upload, Kamera, Ordner, IMAP, iCloud) → Original in `/data/inbox`, Job in
   Postgres-Warteschlange.
2. **Worker:** Prüfsumme → Duplikat? → OCR → PDF/A → LLM-Zuordnung (Felder, Tags, Fristen).
3. **Ablage:** Original + PDF/A in lesbarer Ordnerstruktur unter `/data/archive`, Metadaten und
   Volltext in Postgres.
4. **Nutzer** prüft/korrigiert in der Web-App; Fristen erscheinen in Liste und HA.
5. **Kalender:** Der Worker schreibt bestätigte bzw. automatisch übernommene Fristen per CalDAV
   in den iCloud-Kalender des Kontos und merkt sich die Termin-UID (ADR-0012).

## Dokumentablage und Schema (M1)
- **Ablage** (`IDocumentStorage`, umgesetzt von `FileSystemDocumentStorage`): Wurzel aus
  `NoMorePaper:DataPath` (Werk `/data`). Eingang `inbox/<id>/original.<ext>`, Archiv
  `archive/<jjjj>/<mm>/<id>/original.<ext>` und `archive.pdf`. Jahr und Monat stammen aus dem
  Zeitstempel der Guid-v7-Id (UTC), die Endung nur aus der bereinigten Endung des Eingangsnamens
  (`[a-z0-9]{1,10}`, sonst `bin`) – Nutzer-Dateinamen landen nie im Pfad.
- **Atomar:** Schreiben in `.<guid>.tmp` im Zielordner, `fsync`, dann Umbenennen. Ein Abbruch
  hinterlässt keine halbe Datei. Das Original wandert per `ArchiveOriginalAsync` (Umbenennen,
  wiederholbar) vom Eingang ins Archiv.
- **Windows-Eigenheit:** Eine Datei, die gerade gelesen wird, lässt sich unter Windows nicht
  ersetzen (auch nicht mit `FileShare.Delete`); unter Linux schon. Produktion läuft auf Linux.
- **Tabellen:** `documents` (`id`, `title`, `received_at`, `state`, `content_text`,
  `failure_reason`, generiert `search_vector`), `document_files` (Schlüssel `document_id` + `kind`),
  `processing_jobs` (Schlüssel `document_id`, `attempt`, `next_attempt_at`, `locked_until`,
  `last_error`). Zustände und Dateiarten stehen als Text (`Queued`, `Original`, …) in der Tabelle.
- **Suche:** `search_vector` = Titel deutsch (Gewicht A) + Text deutsch + Text englisch, GIN-Index;
  `pg_trgm` mit GIN-Trigramm-Index auf `content_text` für OCR-Fehler.

## Darstellung im Client
Web.Client rendert PDFs im Browser, ein WebGL-Shader invertiert intelligent (ADR-0011). Server
und Speicher kennen nur Originale bzw. PDF/A – die Darstellung ist reine Client-Sache.

## Ordnerstruktur im Code
Nach **Feature**, nicht nach Typart: `Ingestion/Imap/`, `Ingestion/ICloud/`, `Recognition/`,
`Search/`, `Deadlines/`, `Calendar/`, `Contracts/`, `PageEditing/`, `Trash/`, `Viewer/`, `Backup/`.

## Versionen und Papierkorb
Ein Dokument hat eine oder mehrere **Dateiversionen** (Original, bearbeitete PDF/A). Suche und OCR
gelten für die aktuelle Version. Entfernte Versionen kommen in den Papierkorb, ein täglicher Job im
Worker löscht sie nach 30 Tagen endgültig (ADR-0013).

## Verträge
Ein **Vertrag** verweist auf viele Dokumente. Kündigungsfristen und Kosten berechnet die Domain;
die Kündigungsfrist wird als normale Frist angelegt und läuft so in Kalender und HA (ADR-0014).
