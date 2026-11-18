# M0 – Fundament

**Ziel:** Projektgerüst, das alle Architektur-, Barrierefreiheits- und Datenschutzregeln von Anfang
an automatisch prüft.
**Demo:** `docker compose up` startet Server, Worker und Postgres; die leere Web-App öffnet im
dunklen Kontrast-Theme (DE/EN). `dotnet build` und `dotnet test` (inkl. Postgres per
Testcontainers und axe-Prüfung) sind grün, die CI auf GitHub ist grün.
**Voraussetzungen:** keine. **Größe:** S–M.

Entscheidungen (Grilling 2026-11-18): Repo `github.com/gartenkoch7-ctrl/NoMorePaper` (öffentlich,
leer) · Branches `main` + `develop` · DB-Tests mit Testcontainers · Barrierefreiheit per
Playwright + axe-core ab M0.

## Aufgaben

### 1. Build-Konfiguration
- [x] `global.json` – SDK `10.0.100`, `rollForward: latestFeature`, Test-Runner MTP
- [x] `Directory.Build.props` – `Nullable`, `TreatWarningsAsErrors`, `ImplicitUsings`,
      `LangVersion=latest`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`,
      Central Package Management
- [x] `Directory.Packages.props` – zentrale Paketversionen
- [x] `.editorconfig`, `.gitattributes` (`eol=crlf`, Shell/Hook `eol=lf`), `.gitignore`
- [x] `LICENSE` (MIT), `README.md` (Kurzbeschreibung, Start, Badge)

### 2. Projekte (nach [architektur.md](../kb/architektur.md))
| Projekt | SDK / TFM | Pakete |
|---|---|---|
| `src/NoMorePaper.Domain` | `net10.0` | keine |
| `src/NoMorePaper.Application` | `net10.0` | `Microsoft.Extensions.*.Abstractions` |
| `src/NoMorePaper.Infrastructure` | `net10.0` | `Npgsql.EntityFrameworkCore.PostgreSQL` |
| `src/NoMorePaper.Contracts` | `net10.0` | keine |
| `src/NoMorePaper.Server` | `Microsoft.NET.Sdk.Web` | OpenTelemetry, hostet Web.Client |
| `src/NoMorePaper.Worker` | `Microsoft.NET.Sdk.Worker` | OpenTelemetry |
| `src/NoMorePaper.Web.Client` | `Microsoft.NET.Sdk.BlazorWebAssembly` | MudBlazor, Localization |
| `tests/NoMorePaper.Domain.Tests` | `net10.0` | xunit v3 |
| `tests/NoMorePaper.Architecture.Tests` | `net10.0` | NetArchTest.Rules |
| `tests/NoMorePaper.Infrastructure.Tests` | `net10.0` | Testcontainers.PostgreSql |
| `tests/NoMorePaper.UI.Tests` | `net10.0` | Microsoft.Playwright, Deque.AxeCore.Playwright |

- [x] `NoMorePaper.slnx` mit allen Projekten, Referenzen exakt nach AGENTS.md §2.2

### 3. Architekturtests
- [x] Domain hängt von keinem anderen Projekt und keinem `Microsoft.EntityFrameworkCore`/`Microsoft.AspNetCore`/`System.Net.Http`/`System.IO`-Dateizugriff ab
- [x] Application kennt nur Domain; Infrastructure nicht Server/Worker
- [x] Web.Client kennt nur Contracts
- [x] Keine Namespaces/Ordner `Services`, `Helpers`, `Utils`
- [x] Gegenprobe: absichtlich falsche Referenz → Test rot (danach entfernt)

### 4. Laufzeit-Grundgerüst
- [x] `AppDbContext` (leer) + erste Migration; Server wendet Migrationen beim Start an
- [x] `/health` in Server und Worker (Worker: Heartbeat-Tabelle oder eigener Port)
- [x] Telemetrie nach AGENTS.md §2.3: `[LoggerMessage]`-Klassen, `ActivitySource`/`Meter` je
      Projekt, OTLP-Export nur wenn `OTEL_EXPORTER_OTLP_ENDPOINT` gesetzt; Katalog
      `docs/kb/telemetrie.md` mit EventId-Bereichen je Projekt
- [x] Datenschutz-Grundlage: ASP.NET Data Protection mit persistentem Schlüssel-Volume

### 5. Web-App-Grundgerüst (barrierefrei)
- [x] MudBlazor-Theme dunkel mit hohem Kontrast (WCAG AAA-Kontrast für Text), große
      Grundschrift, deutlicher Fokusrahmen, kein Hell-Modus ab Werk
- [x] Startseite „NoMorePaper“ mit Sprachumschaltung DE/EN (`IStringLocalizer`, `.resx`)
- [x] Landmarks (`header`, `main`, `nav`), Überschriftenhierarchie, „Zum Inhalt springen“-Link
- [x] PWA-Manifest und Service Worker (Grundgerüst, noch ohne Offline-Logik)
- [x] UI-Tests: Playwright startet den Server, axe-core prüft jede Seite (0 Verstöße),
      Tastaturtest (Tab-Reihenfolge, sichtbarer Fokus)

### 6. Docker
- [x] `Dockerfile` je Server und Worker (Multi-Stage, Nicht-Root-Benutzer)
- [x] `docker-compose.yml`: `server` (nur `127.0.0.1`), `worker`, `db` (postgres:17) mit
      Healthchecks; Volumes `data`, `pgdata`, `dpkeys`; `.env.example` ohne Geheimnisse
- [x] Ollama-Container kommt erst mit M1/M3 (nicht in M0)

### 7. Qualitätsprüfungen und CI
- [x] `scripts/check-file-length.ps1` (Endungen `.cs`, `.razor`, `.ps1`, `.js`, `.css`;
      generierte Dateien und Migrationen ausgenommen)
- [x] `.githooks/pre-commit` ruft die Zeilenprüfung auf
- [x] `.github/workflows/ci.yml` auf `ubuntu-latest`: Build, alle Tests (Docker für
      Testcontainers vorhanden), `playwright install --with-deps chromium`, Zeilenprüfung (pwsh)
- [ ] Remote `origin` = `https://github.com/gartenkoch7-ctrl/NoMorePaper.git`, Branch `develop`
      von `main`; Push von `main`/`develop` nur mit Freigabe (AGENTS.md §8)

### 8. Doku
- [x] `docs/kb/build-und-tooling.md`: Befehle, MTP, Testcontainers (Docker/Rancher Desktop),
      Playwright-Installation
- [x] `docs/kb/testumgebung.md`: Rechner, Docker, Browser, Screenreader (VoiceOver/NVDA)
- [x] `docs/kb/telemetrie.md`: EventId-Bereiche, Namenskonventionen
- [x] AGENTS.md §6 Befehle an die Wirklichkeit anpassen, KB-Index ergänzen

## Akzeptanzkriterien
- [x] Clean Clone → `dotnet build` + `dotnet test --solution NoMorePaper.slnx` grün (Docker läuft)
- [x] `docker compose up -d --build` → `server` und `worker` healthy, Web-App erreichbar
- [x] axe-core: 0 Verstöße auf allen Seiten; Bedienung nur per Tastatur möglich
- [x] Architektur-Gegenprobe erfolgreich
- [ ] CI auf GitHub grün

## Parallelisierung

Integrationsbranch `feat/m00-fundament` von `develop`.

```
Schritt 0 (1, 2) ─┬─► Spur A (3, 4, 6) ─┐
                  ├─► Spur B (5)        ├─► Merge ─► Spur D (8) ─► Gesamt-Review
                  └─► Spur C (7)        ┘
```

| Spur | Inhalt | Branch | wartet auf | EventIds |
|---|---|---|---|---|
| 0 | Build-Konfiguration, leere Projekte, `.slnx` | `feat/m00-fundament` | – | – |
| A | Architekturtests, DbContext, Health, Telemetrie, Docker | `feat/m00-a-backend` | 0 | Server 1000–1999, Worker 2000–2999, Infrastructure 3000–3999 |
| B | Theme, Startseite, DE/EN, PWA, UI-Tests mit axe | `feat/m00-b-webclient` | 0 | Web.Client 4000–4999 |
| C | Zeilenprüfung, Hook, CI, Remote | `feat/m00-c-ci` | 0 | – |
| D | KB, AGENTS.md §6 | `feat/m00-fundament` | A, B, C | – |

- **Exklusiv-Dateien:** A → `src/*Server*`, `*Worker*`, `*Infrastructure*`, `tests/*Architecture*`,
  `tests/*Infrastructure*`, `Dockerfile*`, `docker-compose.yml`; B → `src/*Web.Client*`,
  `tests/*UI.Tests*`; C → `scripts/`, `.githooks/`, `.github/`
- **Hotspots** (nur ergänzen): `Directory.Packages.props`, `NoMorePaper.slnx`, `docs/kb/telemetrie.md`
- **Merge-Reihenfolge:** C → A → B (B braucht für UI-Tests den Server aus A: Server-Start in
  `UI.Tests` gegen den Stand nach dem Merge von A prüfen)

## Detailplan

**Erstellt von:** Opus (Stufe 3) · **Datum:** 2026-11-18 · **Offene Designfragen:** keine

### Einschätzung

| Aufgabenpunkt | Umfang | Risiko | Neuheit | Klarheit | Prüfbarkeit | Stufe |
|---|---|---|---|---|---|---|
| 1 Build-Konfiguration | 8 Dateien, ~150 Zeilen | gering | Muster ZoomIN | entschieden | Build | 2 |
| 2 Projekte + `.slnx` | 11 Projekte | gering | Standard | entschieden | Build | 2 |
| 3 Architekturtests | 2–3 Dateien, ~150 Zeilen | gering | Muster ZoomIN | entschieden | Tests + Gegenprobe | 2 |
| 4 DbContext, Health, Telemetrie, Data Protection | ~12 Dateien, ~400 Zeilen | mittel (Start-Reihenfolge, Migrationen) | teils neu | entschieden | Testcontainers | 3 |
| 5 Theme, a11y, DE/EN, PWA, UI-Tests | ~12 Dateien, ~450 Zeilen | hoch (Hauptnutzer sehbehindert) | neu | entschieden | axe + Tastaturtest | 3 |
| 6 Docker | 4 Dateien, ~150 Zeilen | mittel (Sicherheit) | Standard | entschieden | `compose up` | 2 |
| 7 Zeilenprüfung, Hook, CI, Remote | 4 Dateien, ~120 Zeilen | gering | Muster ZoomIN | entschieden | CI-Lauf | 2 |
| 8 Doku | 5 Dateien | keins | Muster | entschieden | Lesen | 1 |

### Paket P0 – Gerüst (Aufgaben 1, 2) · Modell: **Sonnet** · Effort: `medium`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `global.json`, `Directory.Build.props`, `Directory.Packages.props` | neu | wie ZoomIN, ohne Windows-TFM; `Company/Product` = NoMorePaper | Build | 60 |
| `.editorconfig`, `.gitattributes`, `.gitignore` | neu | `.editorconfig` und `.gitattributes` aus ZoomIN übernehmen; `.gitignore` für .NET, Node, Playwright, `.env` | – | 120 |
| `LICENSE`, `README.md` | neu | MIT; README: Zweck, `docker compose up`, Link AGENTS.md | – | 60 |
| `src/*/*.csproj`, `tests/*/*.csproj`, `NoMorePaper.slnx` | neu | 11 Projekte laut Tabelle, Referenzen nach §2.2, je ein Platzhaltertyp | Build | 150 |
- **Akzeptanz:** `dotnet build NoMorePaper.slnx` ohne Warnungen

### Paket P1 – Backend (Aufgaben 3, 4, 6) · Modell: **Opus** · Effort: `high`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `tests/NoMorePaper.Architecture.Tests/LayerTests.cs` | neu | Regeln aus Aufgabe 3 mit NetArchTest, wie ZoomIN `LayerTests` | selbst | 120 |
| `src/NoMorePaper.Infrastructure/Persistence/AppDbContext.cs` + `Migrations/` | neu | leerer Kontext, Migration `Initial` | Testcontainers | 40 |
| `src/NoMorePaper.Infrastructure/Composition/InfrastructureServiceCollectionExtensions.cs` | neu | `AddInfrastructure(IConfiguration)`: DbContext, Data Protection mit Pfad aus Konfiguration | – | 50 |
| `src/NoMorePaper.Server/Program.cs`, `Composition/`, `Health/`, `Telemetry/` | neu | Migration beim Start, `/health`, OTel, `ServerLog` mit `[LoggerMessage]` (1000–1999), Web.Client hosten | UI.Tests | 200 |
| `src/NoMorePaper.Worker/Program.cs`, `Heartbeat/`, `Telemetry/` | neu | `BackgroundService` schreibt Heartbeat, `/health` wertet ihn aus; `WorkerLog` (2000–2999) | Infra-Test | 150 |
| `tests/NoMorePaper.Infrastructure.Tests/MigrationTests.cs` | neu | Testcontainers-Postgres, Migration läuft, DB erreichbar | selbst | 60 |
| `Dockerfile.server`, `Dockerfile.worker`, `docker-compose.yml`, `.env.example` | neu | Multi-Stage, Nicht-Root, Healthchecks, Port nur `127.0.0.1` | `compose up` | 150 |
- **Telemetrie:** Activities `nomorepaper.server.startup`, `nomorepaper.worker.heartbeat`
- **Akzeptanz:** Tests grün, Gegenprobe rot/grün, `docker compose up -d --build` healthy

### Paket P2 – Web-Client barrierefrei (Aufgabe 5) · Modell: **Opus** · Effort: `high`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Web.Client/Theme/ContrastTheme.cs` | neu | `MudTheme` nur dunkel, Text ≥ 7:1, Fokusrahmen 3 px, Grundschrift 18 px | axe | 80 |
| `Layout/MainLayout.razor`, `Pages/Home.razor` | neu | Landmarks, Skip-Link, H1, Sprachumschaltung | axe + Tastatur | 120 |
| `Localization/Strings.resx`, `Strings.en.resx` | neu | Texte DE (Standard) und EN | – | 40 |
| `wwwroot/manifest.webmanifest`, `service-worker.js`, Icons | neu | PWA-Grundgerüst, `theme_color` schwarz | – | 60 |
| `tests/NoMorePaper.UI.Tests/AccessibilityTests.cs`, `KeyboardTests.cs`, `ServerFixture.cs` | neu | Server starten (WebApplicationFactory mit Kestrel oder Prozess), axe ohne Verstöße, Tab-Reihenfolge, Fokus sichtbar | selbst | 220 |
- **Akzeptanz:** UI-Tests grün; Sichtprüfung durch den Nutzer mit Screenreader (VoiceOver oder NVDA)

### Paket P3 – Qualität und CI (Aufgabe 7) · Modell: **Sonnet** · Effort: `medium`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `scripts/check-file-length.ps1` | neu | wie ZoomIN, Endungen und Ausnahmen laut Aufgabe 7 | lokal | 50 |
| `.githooks/pre-commit` | neu | ruft das Skript per `pwsh` auf | Commit | 10 |
| `.github/workflows/ci.yml` | neu | Push/PR auf `main`, `develop`, `feat/**`: setup-dotnet 10, build, Playwright installieren, test, Zeilenprüfung | CI | 60 |
- **Akzeptanz:** CI-Lauf auf dem Feature-Branch grün

### Paket P4 – Doku (Aufgabe 8) · Modell: **Haiku** · Effort: `low`
Dateien aus Aufgabe 8, Inhalt aus den Ergebnissen von P0–P3 (Befehle, Versionen, EventId-Bereiche).
- **Akzeptanz:** alle Links gültig, AGENTS.md §6 entspricht den echten Befehlen

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|

## Offene Fragen / Risiken
- Testcontainers lokal mit Rancher Desktop (Docker-Socket) ⚠️ unverifiziert
- Playwright mit Blazor WASM in CI: Startzeit/Wartebedingung bis WASM geladen ist
- axe-core prüft nicht alles (z. B. sinnvolle Ansagen) → zusätzlich Screenreader-Sichtprüfung
