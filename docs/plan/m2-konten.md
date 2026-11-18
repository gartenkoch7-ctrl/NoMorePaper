# M2 – Konten & Haushalt

**Ziel:** NoMorePaper ist über Tailscale per HTTPS erreichbar, und nur angemeldete Mitglieder des
Haushalts sehen Dokumente. Private Dokumente sieht nur ihr Besitzer.
**Demo:** Erster Start → Verwalter-Konto einrichten (Notfall-Code wird angezeigt) → Mitglied per
Einladungslink aufnehmen → beide laden Dokumente hoch, eines privat → das Mitglied sieht das private
Dokument des Verwalters nirgends → Zugriff vom iPhone über `https://<name>.<tailnet>.ts.net`.
**Voraussetzungen:** M1. **Größe:** M–L.

Entscheidungen (Grilling 2026-11-18):
- Anmeldung mit **Benutzername + Passwort** (ASP.NET Identity), keine Passkeys
- **HTTPS über `tailscale serve`** (Zertifikat von Tailscale, Weiterleitung auf `127.0.0.1:8080`)
- **Ein Verwalter** (wer einrichtet), weitere Mitglieder per **Einladungslink** (einmalig, 7 Tage);
  der Verwalter kann Mitglieder entfernen, sonst sind alle gleich
- **Schlüsselring-Verschlüsselung (ADR-0015) wird in M2 vorgezogen**
- Angemeldet bleiben **30 Tage, gleitend**; „Abmelden“ und „Auf allen Geräten abmelden“
- Passwort vergessen: **Rücksetzlink durch den Verwalter**; der Verwalter selbst nutzt einen
  **Notfall-Code**, der beim Einrichten einmal angezeigt wird
- **Privat = nur der Besitzer** (wer hochgeladen hat) sieht es, auch der Verwalter nicht; umstellbar
  durch den Besitzer
- Nach **5 Fehlversuchen 5 Minuten Sperre**, Passwort mindestens 10 Zeichen, keine weiteren Regeln
- Dokumente aus M1 (ohne Besitzer) gehören nach der Einrichtung dem Verwalter, nicht privat
- Aus dem M1-Review: **Schritt 0 verschiebt die Anwendungsfälle nach Application** (AGENTS.md §2.2)

## Aufgaben

### 0. Anwendungsfälle nach Application (Schritt 0)
- [ ] Ports in Application: `IDocumentRepository` (anlegen inkl. Job, Liste, Detail, Existenz),
      `ISearchIndex`, `IJobQueue` (Claim/Start/Complete/Fail/Release mit `JobClaim`), bestehend
      `IDocumentStorage`, `IOcrEngine`
- [ ] Anwendungsfälle in Application: `ReceiveDocument`, `ListDocuments`, `GetDocument`,
      `SearchDocuments`, `ProcessNextDocument` (heute `Worker/Processing/DocumentPipeline`)
- [ ] Infrastructure setzt nur noch Ports um; Server-Endpunkte und Worker rufen nur Anwendungsfälle
- [ ] Architekturtest: Server und Worker referenzieren keine konkreten Infrastructure-Typen außer in
      `Composition/`; Verhalten unverändert (alle bestehenden Tests grün)

### 1. Konten und Anmeldung (Backend)
- [ ] ASP.NET Identity mit EF Core (eigene Tabellen `users`, …), Cookie-Anmeldung `SameSite=Strict`,
      `Secure`, `HttpOnly`, 30 Tage gleitend; Sperre 5 Fehlversuche / 5 Minuten; Passwort ≥ 10 Zeichen
- [ ] Einrichtung beim ersten Start (nur solange es kein Konto gibt): Verwalter + Notfall-Code
      (einmal angezeigt, nur Hash gespeichert); M1-Dokumente gehen an den Verwalter
- [ ] Einladungen (Token nur als Hash, 7 Tage, einmalig), Mitglieder auflisten/entfernen,
      Rücksetzlinks (Token als Hash, 24 Stunden), Notfall-Reset erzeugt neuen Notfall-Code
- [ ] „Auf allen Geräten abmelden“ über den Security-Stamp
- [ ] Jede `/api`-Route außer Einrichtung/Anmeldung/Einladung/Rücksetzen verlangt Anmeldung;
      `CrossSiteRequestGuard` bleibt

### 2. Besitz und „privat“
- [ ] `documents.owner_id` (Pflicht nach Migration), `documents.is_private`
- [ ] Liste, Detail, Dateien, Suche zeigen nur eigene + nicht private Dokumente; fremde private →
      404 (nicht 403, damit ihre Existenz nicht verraten wird)
- [ ] Upload mit Feld `private`; `PATCH /api/documents/{id}` `{ isPrivate }` nur durch den Besitzer
- [ ] Worker bleibt unberührt (verarbeitet alles); keine Besitzerdaten in Logs außer Konto-Id

### 3. Oberfläche
- [ ] Seiten: Einrichten, Anmelden, Einladung annehmen, Passwort zurücksetzen, Notfall-Reset,
      Konto (Passwort ändern, abmelden, überall abmelden), Mitglieder (nur Verwalter: einladen,
      Link kopieren, Rücksetzlink, entfernen mit Bestätigung)
- [ ] Ohne Anmeldung leitet jede Seite auf „Anmelden“; nach Anmeldung zurück zur Zielseite
- [ ] Schalter „Privat“ beim Hochladen und im Viewer; Kennzeichnung in Liste/Suche als Text + Symbol
- [ ] Sperre und Fehler verständlich und per Live-Region angesagt; Passwortfelder mit
      Sichtbar-Schalter; `autocomplete`-Attribute für Passwortmanager

### 4. Betrieb: HTTPS über Tailscale, Schlüsselring
- [ ] `docs/kb/betrieb.md`: `tailscale serve --bg --https=443 http://127.0.0.1:8080`, MagicDNS-Name,
      `AllowedHosts` per Umgebungsvariable um den Tailnet-Namen ergänzen; neues ADR-0020
      (ergänzt ADR-0002)
- [ ] ADR-0015 umsetzen: `ProtectKeysWithCertificate` mit PFX-Datei (Bind-Mount, schreibgeschützt)
      und Passwort aus `.env`; Skript `scripts/new-keyring-certificate.ps1`; ohne Zertifikat startet
      der Server nicht (außer `Development`)
- [ ] Forwarded-Header von `tailscale serve` (nur von `127.0.0.1`) auswerten, damit Cookies `Secure` sind

### 5. Tests und Doku
- [ ] Server.Tests: alle Konto-Abläufe, Sperre, Rechte, privat → 404, Einladung abgelaufen/benutzt
- [ ] UI.Tests: axe 0 Verstöße auf allen neuen Seiten DE/EN, Tastatur, Anmeldung/Abmeldung,
      privat-Schalter; bestehende UI-Tests melden sich per Fixture an
- [ ] KB: `konten.md` (Rollen, Abläufe, Tokens), Telemetrie-Katalog; ADR-0021 (Identity + Cookies)

## Akzeptanzkriterien
- [ ] Ohne Anmeldung liefert keine Dokument-Route Daten
- [ ] Mitglied sieht private Dokumente anderer nirgends (Liste, Suche, Viewer, Datei)
- [ ] Zugriff vom iPhone über HTTPS im Tailnet funktioniert, Cookie ist `Secure`
- [ ] Schlüsselring auf der Platte ist verschlüsselt
- [ ] axe 0 Verstöße; NVDA-Sichtprüfung der Anmeldung durch den Nutzer
- [ ] CI grün

## Parallelisierung

Integrationsbranch `feat/m02-konten` von `develop`.

```
Schritt 0 (Aufgabe 0) ─┬─► Spur A (1, 2) ─┐
                       ├─► Spur B (3) ────┼─► Merge ─► Spur D (5 Doku) ─► Review
                       └─► Spur C (4) ────┘
```

| Spur | Inhalt | Branch | wartet auf | EventIds |
|---|---|---|---|---|
| 0 | Anwendungsfälle nach Application | `feat/m02-konten` | – | Application 5000–5099 |
| A | Konten, Anmeldung, Besitz/privat (Backend) | `feat/m02-a-auth` | 0 | Server 1300–1399, Infrastructure 3400–3499, Application 5100–5199 |
| B | Konto-Seiten, Privat-Schalter (UI) | `feat/m02-b-ui` | 0 | Web.Client 4400–4499 |
| C | `tailscale serve`, Schlüsselring, Forwarded-Header | `feat/m02-c-ops` | 0 | Server 1400–1419 |
| D | KB, ADRs, Katalog | `feat/m02-konten` | A, B, C | – |

- **Exklusiv-Dateien:** A → `Server/Accounts/`, `Infrastructure/Accounts/`, `Application/Accounts/`,
  Besitz-Änderungen in `Application/Documents/` und `Infrastructure/Documents|Search/`;
  B → `Web.Client/Accounts/`, `Web.Client/Pages/Account*`, `UI.Tests/Accounts/`;
  C → `Server/Hosting/`, `Infrastructure/Composition/DataProtection*`, `scripts/new-keyring-certificate.ps1`,
  `docker-compose.yml`, `.env.example`
- **Hotspots:** `Program.cs`/`ServerComposition.cs`, `Contracts/`, `Strings*.resx`, `MainLayout.razor`,
  `DocumentApi.cs`, `ServerFixture.cs`, `Directory.Packages.props`, `docs/kb/telemetrie.md`
- **Merge-Reihenfolge:** C → A → B
- **API-Vertrag A ↔ B** (A baut, B nutzt; Fehler als `ProblemDetails`):

| Route | Antwort |
|---|---|
| `GET /api/setup` | `{ needed }` |
| `POST /api/setup` `{ userName, displayName, password }` | 200 `{ emergencyCode }`; 409 wenn schon eingerichtet |
| `POST /api/auth/login` `{ userName, password }` | 204; 401; 423 + `Retry-After` |
| `POST /api/auth/logout`, `POST /api/auth/logout-everywhere` | 204 |
| `GET /api/auth/me` | `{ id, userName, displayName, isAdmin }`; 401 |
| `POST /api/account/password` `{ currentPassword, newPassword }` | 204; 400 |
| `POST /api/invitations` (Verwalter) | `{ link, expiresAt }` |
| `GET /api/invitations/{token}` | 200 `{ expiresAt }`; 404 ungültig/benutzt/abgelaufen |
| `POST /api/invitations/{token}/accept` `{ userName, displayName, password }` | 204 (angemeldet) |
| `GET /api/members` (Verwalter) | `[{ id, userName, displayName, isAdmin }]` |
| `DELETE /api/members/{id}` (Verwalter, nicht sich selbst) | 204 |
| `POST /api/members/{id}/reset-link` (Verwalter) | `{ link, expiresAt }` |
| `POST /api/password-reset/{token}` `{ newPassword }` | 204; 404 |
| `POST /api/emergency-reset` `{ userName, emergencyCode, newPassword }` | 200 `{ emergencyCode }` (neu); 401 |
| `PATCH /api/documents/{id}` `{ isPrivate }` (Besitzer) | 204; 404 |

  Dazu: Upload-Formularfeld `private` (`true`/`false`); `DocumentSummaryDto`/`DocumentDetailDto` um
  `IsPrivate` und `IsMine` erweitert; nicht angemeldet → 401 auf allen geschützten Routen.

## Detailplan

**Erstellt von:** Opus (Stufe 3) · **Datum:** 2026-11-18 · **Offene Designfragen:** keine

### Einschätzung

| Aufgabenpunkt | Umfang | Risiko | Neuheit | Klarheit | Prüfbarkeit | Stufe |
|---|---|---|---|---|---|---|
| 0 Anwendungsfälle verschieben | ~30 Dateien, viel Umzug | mittel (Verhalten muss gleich bleiben) | Muster vorhanden | entschieden | bestehende Tests | 3 |
| 1–2 Konten, Besitz, privat | ~30 Dateien, ~1500 Zeilen | hoch (Sicherheit) | Identity Standard | entschieden | Server.Tests | 3 |
| 3 Oberfläche | ~15 Dateien, ~900 Zeilen | mittel (Barrierefreiheit) | Muster aus M1 | entschieden | axe + Tastatur | 2 |
| 4 Betrieb | ~8 Dateien, ~250 Zeilen | mittel (Zertifikat, Start) | Standard | entschieden | Compose + Tests | 2 |
| 5 Doku | ~6 Dateien | keins | Muster | entschieden | Lesen | 1 |

### Pakete

| Paket | Inhalt | Modell | Effort |
|---|---|---|---|
| P0 | Schritt 0 | **Opus** | `high` |
| P1 | Spur A: Konten, Besitz, privat | **Opus** | `high` |
| P2 | Spur B: Oberfläche | **Sonnet** | `medium` |
| P3 | Spur C: Betrieb | **Sonnet** | `medium` |
| P4 | Spur D: Doku | **Haiku** | `low` |

### Paket P0 – Anwendungsfälle nach Application · Modell: **Opus** · Effort: `high`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Application/Documents/IDocumentRepository.cs` | neu | `AddReceivedAsync(Document, CancellationToken)` (Dokument + Job in einer Transaktion), `ExistsAsync(Guid)`, `ListAsync(int page, int pageSize)`, `GetAsync(Guid)` → bestehende Read-Models | – | 30 |
| `Application/Search/ISearchIndex.cs` | neu | `SearchAsync(string query, int limit)` → `SearchHit[]` | – | 15 |
| `Application/Processing/IJobQueue.cs`, `JobClaim.cs`, `FailureOutcome.cs`, `RecordedFailure.cs` | neu/verschoben | Schnittstelle der heutigen `JobQueue` (gleiche Methoden/Semantik); `JobClaim`, `FailureOutcome`, `RecordedFailure` aus Infrastructure nach Application verschieben | – | 60 |
| `Application/Documents/ReceiveDocument.cs` | neu | Logik aus `Infrastructure/Documents/DocumentIntake` (Namen bereinigen → `DocumentNames` mit nach Application, Eingang speichern, Repository, Aufräumen bei Fehler wie heute inkl. Existenzprüfung) | Application.Tests | 120 |
| `Application/Documents/ListDocuments.cs`, `GetDocument.cs`, `Application/Search/SearchDocuments.cs` | neu | dünne Anwendungsfälle über die Ports (Grenzen `pageSize` 1–100, `limit` 1–50, Anfrage ≤ 200 Zeichen) | Application.Tests | 90 |
| `Application/Processing/ProcessNextDocument.cs` | neu | Inhalt von `Worker/Processing/DocumentPipeline` (Ablauf, Fehler, permanent/retry) über `IJobQueue`, `IDocumentStorage`, `IOcrEngine`; Arbeitsordner über neuen Port `IWorkingFolder` | Worker.Tests (bestehend) | 200 |
| `Infrastructure/Documents/EfDocumentRepository.cs`, `Search/PostgresSearchIndex.cs`, `Processing/JobQueue.cs` | neu/ändern | setzen die Ports um (SQL unverändert); `DocumentIntake`, `DocumentQueries`, `DocumentSearch` entfallen | Infrastructure.Tests anpassen | – |
| `Worker/Processing/*` | ändern | `DocumentProcessor` ruft `ProcessNextDocument`; `WorkingFolder` setzt `IWorkingFolder` um; `DocumentPipeline` entfällt | Worker.Tests | – |
| `Server/Documents/*` | ändern | Endpunkte/`DocumentUpload` rufen nur Anwendungsfälle | Server.Tests | – |
| `Application/Composition/ApplicationServiceCollectionExtensions.cs` | neu | `AddApplication()` registriert die Anwendungsfälle | – | 30 |
| `tests/NoMorePaper.Application.Tests/` (neu, in `.slnx`) | neu | Anwendungsfälle mit Fakes der Ports | selbst | 250 |
| `tests/NoMorePaper.Architecture.Tests/LayerTests.cs` | ändern | Server/Worker nutzen Infrastructure nur in `Composition/` | selbst | 20 |
- **Telemetrie:** bestehende EventIds bleiben (Ereignisse ziehen mit um, Kategorien ändern sich –
  Katalog anpassen); neue nur aus 5000–5099
- **Akzeptanz:** keine Verhaltensänderung: alle bestehenden Tests grün (Inhalt unverändert, nur
  Typnamen/Fixtures angepasst), Architekturtest grün, Dateien ≤ 300 Zeilen

Die Dateitabellen für P1–P3 schreibt die steuernde Sitzung nach P0 (sie hängen an den neuen Ports).

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|

## Offene Fragen / Risiken
- Bestehende UI-Tests brauchen ab M2 eine angemeldete Sitzung → Fixture legt ein Testkonto an.
- `tailscale serve` läuft auf dem Host, nicht im Container; Einrichtung einmalig von Hand (Doku).
- Nach einem GPU-Reset überschriebene Ansage (aus M1) – in Spur B mitnehmen, falls Zeit.
