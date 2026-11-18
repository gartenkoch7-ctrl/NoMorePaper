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

## Anwendungsfälle und Ports (seit M2)
- **Application** enthält die Anwendungsfälle als Klassen mit einer Methode: `ReceiveDocument`,
  `ListDocuments`, `GetDocument`, `OpenDocumentFile`, `SearchDocuments` (Server) und
  `ProcessNextDocument` (Worker). Sie arbeiten nur über Ports aus Application: `IDocumentRepository`,
  `IDocumentStorage`, `ISearchIndex`, `IJobQueue`, `IOcrEngine`, `IWorkingFolder`; für Betrieb
  `IHeartbeatStore`, `ISchemaMigrator`, `IDatabaseProbe`.
- **Infrastructure** setzt die Ports um (`EfDocumentRepository`, `FileSystemDocumentStorage`,
  `PostgresSearchIndex`, `JobQueue`, `OcrMyPdfEngine`, …); `IWorkingFolder` setzt der Worker selbst um.
- **Registrierung:** `AddInfrastructure` (Ports), `AddApplication` (Anwendungsfälle des Servers),
  `AddDocumentProcessing` (`ProcessNextDocument`, nur Worker, braucht OCR und Arbeitsordner).
- Server und Worker nennen Infrastructure-Typen nur in `Composition/` (Architekturtest). Grenzen
  wie Seitengröße 1–100, Suchlimit 1–50 und Anfrage ≤ 200 Zeichen setzen die Anwendungsfälle.

## Dokumentablage und Schema (M1)
- **Ablage** (`IDocumentStorage`, umgesetzt von `FileSystemDocumentStorage`): Wurzel aus
  `NoMorePaper:DataPath` (Werk `/data`). Eingang `inbox/<id>/original.<ext>`, Archiv
  `archive/<jjjj>/<mm>/<id>/original.<ext>` und `archive.pdf`. Jahr und Monat stammen aus dem
  Zeitstempel der Guid-v7-Id (UTC), die Endung nur aus der bereinigten Endung des Eingangsnamens
  (`[a-z0-9]{1,10}`, sonst `bin`) – Nutzer-Dateinamen landen nie im Pfad.
- **Atomar:** Schreiben in `.<guid>.tmp` im Zielordner, `fsync`, dann Umbenennen. Ein Abbruch
  hinterlässt keine halbe Datei. Das Original wandert per `ArchiveOriginalAsync` (Umbenennen,
  wiederholbar) vom Eingang ins Archiv.
- **Dauerhaft:** Unter Linux überlebt ein umbenannter Eintrag einen Stromausfall erst, wenn auch
  der Ordner geflusht ist. `IDirectorySync` ruft nach jedem Umbenennen `fsync` auf Zielordner und
  dessen Elternordner auf (`LibcDirectorySync`, unter glibc und musl/Alpine geprüft); unter Windows
  wählt `AddInfrastructure` `NoDirectorySync`, das nichts tut.
- **Annahme:** `ReceiveDocument` committet mit `CancellationToken.None` – ein abgebrochener Upload
  darf einen schon laufenden Commit nicht abbrechen. Nach einem Fehler beim Commit wird die
  Eingangsdatei nur gelöscht, wenn sicher keine Zeile in `documents` existiert (Ereignisse 3203/3204).
- **Windows-Eigenheit:** Eine Datei, die gerade gelesen wird, lässt sich unter Windows nicht
  ersetzen (auch nicht mit `FileShare.Delete`); unter Linux schon. Produktion läuft auf Linux.
- **Tabellen:** `documents` (`id`, `title`, `received_at`, `state`, `content_text`,
  `failure_reason`, generiert `search_vector`), `document_files` (Schlüssel `document_id` + `kind`),
  `processing_jobs` (Schlüssel `document_id`, `attempt`, `next_attempt_at`, `locked_until`,
  `last_error`). Zustände und Dateiarten stehen als Text (`Queued`, `Original`, …) in der Tabelle.
- **Suche:** `search_vector` = Titel deutsch (Gewicht A) + Text deutsch + Text englisch, GIN-Index;
  `pg_trgm` mit GIN-Trigramm-Index auf `content_text` für OCR-Fehler. Der Rückfall filtert mit
  `q <% content_text` (nutzt den Index, `word_similarity(...) > x` nicht) und setzt die Schwelle
  per `SET LOCAL pg_trgm.word_similarity_threshold = 0.4` in einer eigenen Transaktion.

## Schutz der API ohne Anmeldung (M1)
M1 hat keine Anmeldung; der Server ist nur über `127.0.0.1` (und später Tailscale) erreichbar.
Damit keine fremde Webseite im Browser des Nutzers die API benutzt:
- **Hostnamen:** `AllowedHosts` (Werk in `appsettings.json`: `localhost;127.0.0.1`) – andere
  `Host`-Header bekommen 400. Das stoppt DNS-Rebinding. Für den Zugriff über einen weiteren Namen
  (z. B. Tailscale) die Umgebungsvariable `AllowedHosts` setzen, z. B.
  `AllowedHosts=localhost;127.0.0.1;nomorepaper.<tailnet>.ts.net`.
- **Ändernde Anfragen** (alles außer GET/HEAD unter `/api`, `CrossSiteRequestGuard`): erlaubt nur
  mit `Sec-Fetch-Site: same-origin` oder – ohne diesen Header – mit `X-NoMorePaper-Request: 1`;
  sonst 403 ProblemDetails. Fremde Seiten können den eigenen Header nicht ohne CORS-Preflight
  setzen, und CORS ist nicht freigegeben. Web.Client sendet den Header immer; Skripte und später
  die HA-Integration müssen ihn senden.

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
