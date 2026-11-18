# Build und Tooling

Befehle für Build, Tests, Docker und Prüfungen stehen auch in [AGENTS.md §6](../../AGENTS.md).
Dieses Dokument erklärt Voraussetzungen, Eigenheiten und die CI.

## Voraussetzungen
- **.NET SDK 10.0.x**: [global.json](../../global.json) pinnt `10.0.100` mit `rollForward: latestFeature`
  und nutzt den Test-Runner Microsoft.Testing.Platform (MTP).
- **Docker**: Docker Desktop oder Rancher Desktop. Testcontainers und `docker compose` brauchen
  einen laufenden Docker-Daemon.
- **PowerShell**: Windows PowerShell 5.1 oder pwsh 7. Die Skripte laufen mit
  `-ExecutionPolicy Bypass` (siehe Eigenheiten).

## Befehle

```powershell
dotnet build NoMorePaper.slnx
dotnet test --solution NoMorePaper.slnx   # Microsoft.Testing.Platform, xunit v3; Docker muss laufen
powershell -NoProfile -ExecutionPolicy Bypass -File tests/NoMorePaper.UI.Tests/bin/Debug/net10.0/playwright.ps1 install chromium   # einmal nach dem ersten Build
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-file-length.ps1
git config core.hooksPath .githooks   # einmal pro Clone
cp .env.example .env                  # POSTGRES_PASSWORD setzen (nur Buchstaben und Ziffern)
docker compose up -d --build
```

## Lokal ohne Docker
- Der Server braucht die Umgebungsvariable `ConnectionStrings__NoMorePaper`. Fehlt sie, bricht
  der Start mit einer klaren Meldung ab.
- Ports laut `launchSettings`: Server **5080**, Worker **5081**.

## UI-Tests
- Die UI-Tests starten Postgres über Testcontainers und den echten Server.
- Optional: Die Umgebungsvariable `NOMOREPAPER_UI_TESTS_BASE_URL` lässt die Tests gegen einen
  bereits laufenden Server laufen.
- **WebGL im headless Chromium (Viewer, Pixeltest):** Playwright startet Chromium schon mit
  `--enable-unsafe-swiftshader`; WebGL2 läuft dann ohne GPU über SwiftShader (ANGLE/Vulkan), lokal
  unter Windows genauso wie in CI auf `ubuntu-latest`. Eigene Startoptionen braucht
  `ServerFixture` dafür nicht. Gemessen 2026-11-18: SwiftShader und RTX 3060 Ti (D3D11, nur mit
  `--use-angle=d3d11 --enable-gpu --ignore-gpu-blocklist`) liefern dieselben Pixel
  (Weiß → `#000000`, Rot `#c62828` → `#fe7368`, Foto unverändert).
- In headless Chromium kommt `Strg` + Mausrad aus Playwright (`Mouse.WheelAsync` bei gedrückter
  Taste) nicht bei der Seite an; der Viewer-Test schickt das `WheelEvent` deshalb per Skript.

## EF-Migrationen
- Liegen in [src/NoMorePaper.Infrastructure/Persistence/Migrations](../../src/NoMorePaper.Infrastructure/Persistence/Migrations/).
- Erzeugt mit `dotnet-ef` 10.0.15 über `DesignTimeAppDbContextFactory`.
- Der Server wendet die Migrationen beim Start an, bevor Kestrel lauscht.
- Der Ordner `Migrations` hat eine `.editorconfig` mit `generated_code=true`; die Zeilenprüfung
  nimmt ihn aus.

## Eigenheiten
- **(a) Blazor-Framework-Dateien:** In .NET 10 liefert `app.UseBlazorFrameworkFiles()` zusammen mit
  `MapStaticAssets()` für `/_framework` HTTP 500. Im Server steht deshalb nur `MapStaticAssets()`
  ([src/NoMorePaper.Server/Program.cs](../../src/NoMorePaper.Server/Program.cs)).
- **(b) Alpine ohne ICU:** Die Images basieren auf `aspnet:10.0-alpine` mit invarianter
  Globalisierung (kein ICU). Wenn serverseitige Kultur-Formatierung nötig ist, `icu-libs`
  installieren.
- **(c) Npgsql auf Alpine:** Die Verbindungszeichenfolge braucht `GSS Encryption Mode=Disable`
  (siehe [docker-compose.yml](../../docker-compose.yml)).
- **(d) `.dockerignore`:** Ist zwingend nötig. Die Windows-`obj/`-Ordner brechen das Restore im
  Container.
- **(e) Execution Policy:** Unter Windows laufen Skripte nur mit `-ExecutionPolicy Bypass`.
- **(f) Sprachen:** `Web.Client` setzt `BlazorWebAssemblyLoadAllGlobalizationData=true`, damit
  DE und EN unabhängig von der Browsersprache funktionieren.
- **(g) Logging:** Analyzer CA1848 erzwingt `[LoggerMessage]` statt direkter Logger-Aufrufe
  (Regeln in [telemetrie.md](telemetrie.md)).

## CI
Datei: [.github/workflows/ci.yml](../../.github/workflows/ci.yml). Läuft auf `ubuntu-latest`,
ausgelöst durch Pushes auf `main`, `develop` und `feat/**` sowie PRs auf `main` und `develop`.

Schritte:
1. Restore
2. Build in Release
3. Playwright-Chromium mit Abhängigkeiten installieren (`install --with-deps chromium`)
4. `dotnet test` in Release
5. Zeilenprüfung (maximal 300 Zeilen je Datei)
