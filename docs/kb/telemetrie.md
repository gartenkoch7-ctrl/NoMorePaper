# Telemetrie

Regeln: [AGENTS.md §2.3](../../AGENTS.md) (jedes Feature bringt seine Telemetrie mit, keine Inhalte
des Nutzers) und §2.4 (nichts verlässt das eigene Netz). Geprüft durch
`tests/NoMorePaper.Architecture.Tests/TelemetryConventionTests.cs`.

## Grundsätze
- **Logs nur über `[LoggerMessage]`**: je Feature eine `internal static partial class <Feature>Log`
  im Feature-Ordner. Direkte Aufrufe wie `logger.LogInformation(…)` verhindert schon der Build
  (CA1848), `Console`/`Debug`/`Trace`-Ausgaben der Architekturtest.
- **Keine Nutzerinhalte**: keine Dokumentinhalte, Namen, Titel, Betreffzeilen, Mailadressen,
  vollen Pfade oder Zugangsdaten in Log-Platzhaltern oder Activity-Tags. Erlaubt sind Zahlen,
  Dauern, Zustände und technische Kennungen. Der Architekturtest weist Platzhalter mit
  verdächtigen Namen (`Path`, `File`, `Name`, `Title`, `Content`, `Mail`, `User`, `Token`, …) ab.
- **Je Projekt eine `ActivitySource` und ein `Meter`**, beide mit dem Projektnamen
  (`NoMorePaper.Server`, `NoMorePaper.Worker`, `NoMorePaper.Infrastructure`), definiert in
  `<Projekt>/Telemetry/<Projekt>Telemetry.cs`.
- **Export nur auf Wunsch**: Ohne `OTEL_EXPORTER_OTLP_ENDPOINT` wird OpenTelemetry gar nicht
  registriert – es gibt keinen Export und keine Kosten. Ist die Variable gesetzt, gehen Traces,
  Metriken und Logs per OTLP an diesen Collector; er muss im eigenen Netz stehen.
- Aufrufe von `/health` erzeugen im Server keine Traces (Filter in der ASP.NET-Core-Instrumentierung).

## Namen
Activities und Messinstrumente heißen `nomorepaper.<feature>.<vorgang>`: nur Kleinbuchstaben,
Ziffern und `_`, mindestens drei Teile, z. B. `nomorepaper.worker.heartbeat`.

## EventId-Bereiche

| Projekt | Bereich |
|---|---|
| `NoMorePaper.Server` | 1000–1999 |
| `NoMorePaper.Worker` | 2000–2999 |
| `NoMorePaper.Infrastructure` | 3000–3999 |
| `NoMorePaper.Web.Client` | 4000–4999 |

Innerhalb eines Bereichs bekommt jedes Feature einen eigenen Hunderterblock (z. B. Server-Start
1000–1099). EventIds sind solutionweit eindeutig.

## Katalog

### Log-Ereignisse

| EventId | Level | Klasse | Bedeutung |
|---|---|---|---|
| 1000 | Information | `Server/Startup/StartupLog` | Start-Aufgaben (Migration) fertig, Dauer in ms |
| 1001 | Critical | `Server/Startup/StartupLog` | Start-Aufgaben fehlgeschlagen, der Host beendet sich |
| 2000 | Information | `Worker/Heartbeat/HeartbeatLog` | Heartbeat gestartet, Intervall in s |
| 2001 | Warning | `Worker/Heartbeat/HeartbeatLog` | Heartbeat nicht geschrieben (nur der erste Fehler eines Ausfalls) |
| 2002 | Information | `Worker/Heartbeat/HeartbeatLog` | Heartbeat nach einem Ausfall wieder geschrieben |
| 3000 | Information | `Infrastructure/Persistence/PersistenceLog` | Migration beginnt, Anzahl bekannter Migrationen |
| 3001 | Information | `Infrastructure/Persistence/PersistenceLog` | Schema aktuell, Dauer in ms |
| 3002 | Error | `Infrastructure/Persistence/PersistenceLog` | Migration fehlgeschlagen |
| 4000 | Information | `Web.Client/Telemetry/WebClientLog` | Kultur (Sprache) angewendet |
| 4001 | Warning | `Web.Client/Telemetry/WebClientLog` | Gespeicherte Sprache wird nicht unterstützt, Standard wird genommen |
| 4002 | Warning | `Web.Client/Telemetry/WebClientLog` | Sprachpräferenz nicht lesbar oder nicht speicherbar |
| 4003 | Information | `Web.Client/Telemetry/WebClientLog` | Sprachwechsel angefordert |

Web.Client: Die Ereignisse erscheinen nur in der Browser-Konsole und enthalten nur Sprachkürzel.
In WASM gibt es keine `ActivitySource` und kein `Meter` (kein Exporter).

### Activities

| Name | Quelle | Ablauf |
|---|---|---|
| `nomorepaper.server.startup` | `NoMorePaper.Server` | Start-Aufgaben des Servers vor dem ersten Request |
| `nomorepaper.database.migrate` | `NoMorePaper.Infrastructure` | EF-Core-Migrationen anwenden (Kind von `server.startup`) |
| `nomorepaper.worker.heartbeat` | `NoMorePaper.Worker` | ein Heartbeat in `worker_heartbeats` schreiben |

### Messwerte

| Name | Art | Meter | Bedeutung |
|---|---|---|---|
| `nomorepaper.worker.heartbeat_failures` | Counter | `NoMorePaper.Worker` | nicht geschriebene Heartbeats |

## Health
- **Server** `/health`: 200, wenn die Datenbank erreichbar ist, sonst 503.
- **Worker** `/health` (interner Port 8080, nicht veröffentlicht): 200, wenn der letzte Heartbeat
  in der Datenbank höchstens 60 s alt ist (Intervall 15 s). Prüft so Schleife und Datenbank.
- Docker-Healthchecks rufen beide Endpunkte per `wget` im Container auf (`docker-compose.yml`).
