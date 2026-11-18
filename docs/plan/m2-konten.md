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
- Nachtrag 2026-11-18: Setzt ein Rücksetzlink des Verwalters das Passwort, sieht das Mitglied bei der
  nächsten Anmeldung auf der Startseite einen Hinweis (mit Datum), bis es „Verstanden“ drückt
  (`users.password_reset_by_admin_at`); eigene Änderung und Notfall-Code lösen ihn nicht aus
- Aus dem M1-Review: **Schritt 0 verschiebt die Anwendungsfälle nach Application** (AGENTS.md §2.2)

## Aufgaben

### 0. Anwendungsfälle nach Application (Schritt 0)
- [x] Ports in Application: `IDocumentRepository` (anlegen inkl. Job, Liste, Detail, Existenz),
      `ISearchIndex`, `IJobQueue` (Claim/Start/Complete/Fail/Release mit `JobClaim`), bestehend
      `IDocumentStorage`, `IOcrEngine`
- [x] Anwendungsfälle in Application: `ReceiveDocument`, `ListDocuments`, `GetDocument`,
      `SearchDocuments`, `ProcessNextDocument` (heute `Worker/Processing/DocumentPipeline`)
- [x] Infrastructure setzt nur noch Ports um; Server-Endpunkte und Worker rufen nur Anwendungsfälle
- [x] Architekturtest: Server und Worker referenzieren keine konkreten Infrastructure-Typen außer in
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
| `GET /api/auth/me` | `{ id, userName, displayName, isAdmin, adminResetAt? }`; 401 |
| `POST /api/account/acknowledge-reset` | 204 (Hinweis gesehen) |
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

Die Dateitabellen für P1 und P2 schreibt die steuernde Sitzung nach P0 (sie hängen an den neuen
Ports). P3 hängt nicht an P0 und startet parallel dazu.

### Paket P1 – Konten, Besitz, privat (Spur A) · Modell: **Opus** · Effort: `high`
Grundlage: Ports und Anwendungsfälle aus P0 (`d39a281`), Betrieb aus P3 (`717ae11`).

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Domain/Documents/Document.cs` | ändern | **Kein** eigenes Domain-Modell für Konten (Identity bleibt Infrastruktur); Domain bekommt nur `Document.OwnerId` (`Guid`) und `Document.IsPrivate` mit `MakePrivate(Guid actor)`/`Share(Guid actor)` (nur Besitzer, sonst `InvalidOperationException`) und `IsVisibleTo(Guid accountId)` | Domain.Tests | 40 |
| `src/NoMorePaper.Application/Accounts/ICurrentAccount.cs` | neu | `Guid Id`, `bool IsAdmin` des angemeldeten Kontos (Server setzt es aus dem Cookie-Principal um) | – | 15 |
| `src/NoMorePaper.Application/Accounts/IAccountStore.cs` + Anwendungsfälle `SetUpHousehold`, `InviteMember`, `AcceptInvitation`, `ListMembers`, `RemoveMember`, `CreateResetLink`, `ResetPassword`, `ResetWithEmergencyCode`, `ChangePassword` | neu | Ports + Ablauf-Regeln: Einrichtung nur ohne Konten (sonst Konflikt), Verwalter kann sich nicht entfernen, Tokens 32 Byte zufällig, nur SHA-256 gespeichert, Einladung 7 Tage einmalig, Rücksetzlink 24 h einmalig, Notfall-Code 20 Zeichen (Base32), nur Hash gespeichert, nach Benutzung neuer Code; Zeit über `TimeProvider` | Application.Tests | je < 150 |
| `Application/Documents/*` (aus P0) | ändern | `ReceiveDocument` bekommt `bool isPrivate` und setzt `OwnerId = ICurrentAccount.Id`; `ListDocuments`, `GetDocument`, `OpenDocumentFile`, `SearchDocuments` filtern über den Port (`accountId`-Parameter); neuer Anwendungsfall `SetDocumentPrivacy` (nur Besitzer, sonst „nicht gefunden“) | Application.Tests | 80 |
| `IDocumentRepository`, `ISearchIndex` | ändern | `ListAsync/GetAsync/SearchAsync(..., Guid viewerId, ...)`: Sichtbarkeit `owner_id = viewer OR NOT is_private` **im SQL**; `SetPrivacyAsync` | Infrastructure.Tests | 40 |
| `src/NoMorePaper.Infrastructure/Accounts/*` | neu | ASP.NET Identity (`IdentityUser<Guid>`, Tabellen `users`, `user_claims`, … mit snake_case), `AccountStore` setzt `IAccountStore` um, Tabellen `invitations`, `password_reset_tokens`, Spalte `users.emergency_code_hash`, `users.is_admin`, `users.display_name`; Identity-Optionen: Passwort nur `RequiredLength = 10` (keine Zeichenklassen), Lockout 5 / 5 min, `RequireUniqueEmail = false` | Infrastructure.Tests | ~400 gesamt |
| Migration `Accounts` | neu | Tabellen oben; `documents.owner_id uuid NULL`, `documents.is_private bool NOT NULL DEFAULT false`; `SetUpHousehold` setzt danach alle `owner_id IS NULL` auf den Verwalter (eine Transaktion) | MigrationTests | gen. |
| `src/NoMorePaper.Server/Accounts/*` | neu | Endpunkte laut API-Vertrag; Cookie-Auth: Name `__Host-nmp`, `HttpOnly`, `SameSite=Strict`, `SecurePolicy = Always` außer `Development` (`SameAsRequest`), 30 Tage gleitend, bei API-Routen 401 statt Umleitung; `ICurrentAccount`-Umsetzung; Security-Stamp-Prüfung alle 5 min | Server.Tests | ~350 gesamt |
| `Server/Documents/*`, `Server/Search/*` | ändern | `RequireAuthorization()` auf der Gruppe `/api` (Ausnahmen laut Vertrag `AllowAnonymous`), Upload-Feld `private`, `PATCH /api/documents/{id}` | Server.Tests | 40 |
| `Server/Hosting/ForwardedHeadersRegistration.cs` | ändern | zusätzlich vertrauenswürdige Netze aus `NoMorePaper:TrustedProxies` (Liste CIDR, Werk leer); `docker-compose.yml` setzt `172.16.0.0/12` für den Server (Port ist nur an `127.0.0.1` gebunden, Docker-NAT liefert die Bridge-Adresse) | Server.Tests | 20 |
| `Contracts/Accounts/*`, `Contracts/Documents/*` | neu/ändern | DTOs laut API-Vertrag; `DocumentSummaryDto`/`DocumentDetailDto` + `IsPrivate`, `IsMine` | – | 60 |
| `tests/NoMorePaper.Server.Tests/Accounts/*` | neu | Einrichtung einmalig (409 danach), Anmeldung, falsches Passwort, Sperre 423 + `Retry-After`, Abmelden, überall abmelden (alter Cookie ungültig), Einladung gültig/benutzt/abgelaufen, Rücksetzlink, Notfall-Code (alter Code danach ungültig), Mitglied entfernen (Cookie danach ungültig), Rechte (Mitglied darf nicht einladen → 403), privat: fremd → 404 auf Detail/Datei/PATCH, nicht in Liste/Suche; M1-Dokumente gehören nach Einrichtung dem Verwalter | selbst | ~600 gesamt (mehrere Dateien) |
- **Telemetrie:** Server 1300–1399 (Anmeldung ok/fehlgeschlagen/gesperrt nur mit Konto-Id, nie Benutzername/Passwort), Infrastructure 3400–3499 (3400/3401 sind belegt → ab 3410), Application 5100–5199
- **Hinweis Hotspots:** `ServerFixture` (UI-Tests) gehört Spur B; `ApiFixture` (Server.Tests) gehört Spur A
- **Akzeptanz:** alle Tests grün; ohne Anmeldung liefert keine geschützte Route Daten

### Paket P2 – Konto-Seiten, Privat-Schalter (Spur B) · Modell: **Sonnet** · Effort: `medium`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Web.Client/Accounts/AccountApi.cs` | neu | typisierter Client für alle Konto-Routen (Header `X-NoMorePaper-Request: 1` bei Nicht-GET), `ProblemDetails`/423 → lokalisierte Meldung mit Wartezeit | UI.Tests | 150 |
| `Accounts/AuthState.cs` | neu | Dienst: `me` laden, `IsSignedIn`, `IsAdmin`, Ereignis bei Änderung; 401 aus `DocumentApi`/`AccountApi` → Anmeldung | UI.Tests | 80 |
| `Accounts/RequireSignIn.razor` | neu | um `Router`/Layout: nicht angemeldet → `/anmelden?ziel=<pfad>`; Einrichtung nötig → `/einrichten` | UI.Tests | 60 |
| `Pages/Setup.razor` (`/einrichten`), `SignIn.razor` (`/anmelden`), `AcceptInvitation.razor` (`/einladung/{token}`), `ResetPassword.razor` (`/passwort/{token}`), `EmergencyReset.razor` (`/notfall`), `Account.razor` (`/konto`), `Members.razor` (`/mitglieder`, nur Verwalter) | neu | Formulare mit sichtbaren Labels, `autocomplete` (`username`, `current-password`, `new-password`), Schalter „Passwort anzeigen“ (`aria-pressed`), Fehler über `LiveRegion` + Fokus auf erstes fehlerhaftes Feld; Notfall-Code groß, kopierbar, mit Bestätigung „aufbewahrt“; Mitglieder: einladen → Link mit „Kopieren“-Knopf, Rücksetzlink, Entfernen mit Bestätigungsdialog | UI.Tests | je < 200 |
| `Layout/MainLayout.razor` | ändern | Kontoname + „Konto“, „Mitglieder“ (nur Verwalter), „Abmelden“ in der Navigation | UI.Tests | 30 |
| `Pages/Upload.razor`, `Pages/DocumentViewer.razor(.cs)`, `Pages/Documents.razor`, `Pages/Search.razor` | ändern | Schalter „Privat“ (Checkbox mit Label) beim Hochladen; im Viewer Schalter „Privat“ nur für eigene Dokumente; in Liste/Suche Kennzeichnung „Privat“ als Text + Symbol | UI.Tests | 60 |
| `Documents/DocumentApi.cs` | ändern | Upload-Feld `private`, `SetPrivacyAsync` | – | 20 |
| `Strings*.resx` | ändern | alle Texte DE/EN | – | 150 |
| `tests/NoMorePaper.UI.Tests/Hosting/ServerFixture.cs` | ändern | richtet beim Start per API einen Haushalt ein (Verwalter `testadmin`) und meldet jede Sitzung an (Cookie per API-Login in den Browser-Kontext); Methode `OpenAsync(path, culture, signedIn: true)` | – | 50 |
| `tests/NoMorePaper.UI.Tests/Accounts/*` | neu | axe 0 Verstöße auf allen neuen Seiten DE/EN; Anmelden/Abmelden per Tastatur; Sperre wird angesagt; Einladung annehmen; Notfall-Code-Seite; Privat-Schalter; Weiterleitung auf `/anmelden` ohne Sitzung | selbst | ~500 gesamt |
- **Telemetrie:** Web.Client 4400–4499 (nie Benutzername, Passwort, Tokens)
- **Akzeptanz:** alle UI-Tests grün (auch die bestehenden mit angemeldeter Fixture)

### Paket P3 – Betrieb (Spur C) · Modell: **Sonnet** · Effort: `medium`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Infrastructure/Composition/DataProtectionRegistration.cs` | neu | `AddNoMorePaperDataProtection(IServiceCollection, IConfiguration)`: bisheriger Code aus `AddInfrastructure` (Name, `FileSystemXmlRepository`) plus `ProtectKeysWithCertificate(X509Certificate2)` aus `NoMorePaper:KeyRingCertificatePath` + `NoMorePaper:KeyRingCertificatePassword` (Laden mit `X509CertificateLoader.LoadPkcs12FromFile`); fehlt das Zertifikat außerhalb `Development` → `InvalidOperationException` mit klarer Meldung; in `AddInfrastructure` nur noch ein Aufruf (Hotspot-Zeile) | Infrastructure.Tests | 90 |
| `InfrastructureSettings.cs` | ändern | Konstanten `KeyRingCertificatePathKey`, `KeyRingCertificatePasswordKey` | – | 10 |
| `src/NoMorePaper.Server/Hosting/ForwardedHeadersRegistration.cs` | neu | `ForwardedHeadersOptions`: `XForwardedFor \| XForwardedProto \| XForwardedHost`, `KnownProxies` nur `127.0.0.1` und `::1`, `ForwardLimit = 1`; `app.UseForwardedHeaders()` als erste Middleware (Hotspot `Program.cs`) | Server.Tests | 40 |
| `src/NoMorePaper.Server/Hosting/HostingLog.cs` | neu | EventIds 1400–1419 (Schlüsselring geschützt mit Zertifikat-Fingerabdruck, Forwarded-Header verworfen) | – | 25 |
| `scripts/new-keyring-certificate.ps1` | neu | erzeugt selbstsigniertes RSA-3072-Zertifikat (10 Jahre) als PFX mit zufälligem Passwort, schreibt Pfad + Passwort-Hinweis; läuft unter PowerShell 5.1 und 7 | manuell | 60 |
| `docker-compose.yml`, `.env.example` | ändern | Bind-Mount `${NOMOREPAPER_KEYRING_CERT}` → `/run/secrets/keyring.pfx:ro` für Server und Worker, Umgebungsvariablen für Pfad/Passwort, `AllowedHosts` per `NOMOREPAPER_ALLOWED_HOSTS` (Werk `localhost;127.0.0.1`) | Compose-Lauf | 20 |
| `docs/kb/betrieb.md` | neu | Einrichtung `tailscale serve --bg --https=443 http://127.0.0.1:8080`, MagicDNS-Name in `NOMOREPAPER_ALLOWED_HOSTS`, Zertifikat erzeugen und **getrennt** sichern, Prüfliste | – | 100 |
| `docs/adr/0020-https-ueber-tailscale-serve.md` | neu | ergänzt ADR-0002; Status in ADR-0002 auf „ergänzt durch 0020“ | – | 40 |
| `tests/NoMorePaper.Infrastructure.Tests/Composition/DataProtectionTests.cs`, `tests/NoMorePaper.Server.Tests/Hosting/ForwardedHeadersTests.cs` | neu | Schlüssel-XML auf Platte enthält kein `<masterKey>` im Klartext (verschlüsselt), Entschlüsseln mit Zertifikat gelingt, ohne Zertifikat → Fehler; `X-Forwarded-Proto: https` von 127.0.0.1 → `Request.IsHttps`, von fremder IP ignoriert | selbst | 160 |
- **Akzeptanz:** Tests grün; `docker compose up` mit erzeugtem Zertifikat → Server/Worker healthy,
  Schlüssel-Dateien im Volume verschlüsselt; ohne Zertifikat startet der Server mit klarer Meldung nicht

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|

## Offene Fragen / Risiken
- Bestehende UI-Tests brauchen ab M2 eine angemeldete Sitzung → Fixture legt ein Testkonto an.
- `tailscale serve` läuft auf dem Host, nicht im Container; Einrichtung einmalig von Hand (Doku).
- Nach einem GPU-Reset überschriebene Ansage (aus M1) – in Spur B mitnehmen, falls Zeit.
