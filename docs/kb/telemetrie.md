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
  (`NoMorePaper.Server`, `NoMorePaper.Worker`, `NoMorePaper.Application`, `NoMorePaper.Infrastructure`), definiert in
  `<Projekt>/Telemetry/<Projekt>Telemetry.cs`.
- **Export nur auf Wunsch**: Ohne `OTEL_EXPORTER_OTLP_ENDPOINT` wird OpenTelemetry gar nicht
  registriert – es gibt keinen Export und keine Kosten. Ist die Variable gesetzt, gehen Traces,
  Metriken und Logs per OTLP an diesen Collector; er muss im eigenen Netz stehen.
- Aufrufe von `/health` erzeugen im Server keine Traces (Filter in der ASP.NET-Core-Instrumentierung),
  ebenso Pfade mit Einladungs- oder Rücksetz-Token (`/einladung/…`, `/passwort/…`, `/api/invitations/…`,
  `/api/password-reset/…`), damit kein Token als `url.path` beim Collector landet.
- **Konten:** Logs nennen nur Konto-Ids – nie Benutzernamen, Anzeigenamen, Passwörter, Cookies, Tokens oder
  Notfall-Codes. Tokens und Notfall-Codes werden nur als SHA-256 gespeichert.

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
| `NoMorePaper.Application` | 5000–5999 |

Innerhalb eines Bereichs bekommt jedes Feature einen eigenen Hunderterblock (z. B. Server-Start
1000–1099). EventIds sind solutionweit eindeutig. Ausnahme seit M2 (Schritt 0): Verarbeitung (2101–2111)
und Annahme (3200–3204) sind nach Application umgezogen und haben ihre Nummern behalten.

## Katalog

### Log-Ereignisse

| EventId | Level | Klasse | Bedeutung |
|---|---|---|---|
| 1000 | Information | `Server/Startup/StartupLog` | Start-Aufgaben (Migration) fertig, Dauer in ms |
| 1001 | Critical | `Server/Startup/StartupLog` | Start-Aufgaben fehlgeschlagen, der Host beendet sich |
| 1100 | Information | `Server/Documents/DocumentsLog` | Upload angenommen: Dokument-Id, Größe, Medientyp (ohne Dateinamen) |
| 1101 | Warning | `Server/Documents/DocumentsLog` | Upload abgelehnt: Grund (`too-large`, `empty`, `type-not-allowed`, `not-multipart`, `malformed`) und Größe |
| 1200 | Warning | `Server/Security/SecurityLog` | Ändernde API-Anfrage abgelehnt, mögliche fremde Seite: `Sec-Fetch-Site` als festes Wort (`cross-site`, `same-site`, `none`, `absent`, `other`) |
| 1300 | Information | `Server/Accounts/AccountsLog` | Konto angemeldet (Anmeldung, Einrichtung, Einladung angenommen, Passwort geändert): nur Konto-Id |
| 1301 | Warning | `Server/Accounts/AccountsLog` | Anmeldung fehlgeschlagen, falsches Passwort: Konto-Id |
| 1302 | Warning | `Server/Accounts/AccountsLog` | Anmeldung fehlgeschlagen, unbekanntes Konto (ohne Benutzername) |
| 1303 | Warning | `Server/Accounts/AccountsLog` | Anmeldung abgelehnt, Konto gesperrt: Konto-Id, Restsperre in s (`Retry-After`) |
| 1304 | Information | `Server/Accounts/AccountsLog` | Konto abgemeldet: Konto-Id |
| 1305 | Information | `Server/Accounts/AccountsLog` | Sitzung beendet, weil das Konto entfernt oder sein Security-Stamp erneuert wurde: Konto-Id |
| 2000 | Information | `Worker/Heartbeat/HeartbeatLog` | Heartbeat gestartet, Intervall in s |
| 2001 | Warning | `Worker/Heartbeat/HeartbeatLog` | Heartbeat nicht geschrieben (nur der erste Fehler eines Ausfalls) |
| 2002 | Information | `Worker/Heartbeat/HeartbeatLog` | Heartbeat nach einem Ausfall wieder geschrieben |
| 2100 | Information | `Worker/Processing/ProcessingLog` | Verarbeitung gestartet, Abfrageintervall in s |
| 2101 | Information | `Application/Processing/ProcessingLog` | Dokument-Id wird verarbeitet, Versuch Nr. |
| 2102 | Information | `Application/Processing/ProcessingLog` | Dokument fertig: Dauer in ms, Seiten, Anzahl erkannter Zeichen |
| 2103 | Warning | `Application/Processing/ProcessingLog` | Versuch fehlgeschlagen: technischer Grund (Ausnahmetyp, ggf. Werkzeug + Exit-Code), Zeitpunkt des nächsten Versuchs |
| 2104 | Error | `Application/Processing/ProcessingLog` | Dokument endgültig `Failed` nach dem letzten Versuch oder einem dauerhaften Fehler, technischer Grund |
| 2105 | Warning | `Application/Processing/ProcessingLog` | Job konnte nicht geholt werden (nur Ausnahmetyp; nur der erste Fehler eines Ausfalls) |
| 2106 | Information | `Application/Processing/ProcessingLog` | Jobs holen klappt nach einem Ausfall wieder |
| 2107 | Error | `Application/Processing/ProcessingLog` | Fehlschlag konnte nicht gespeichert werden; der Job läuft nach Ablauf seiner Sperre erneut |
| 2108 | Information | `Application/Processing/ProcessingLog` | Verarbeitung durch Herunterfahren abgebrochen (auch wenn der Versuch dabei mit einem Fehler endete); der Job ist zurückgegeben und läuft beim nächsten Start ohne Versuchszählung |
| 2109 | Warning | `Application/Processing/ProcessingLog` | Arbeitsordner des Versuchs nicht gelöscht (nur Ausnahmetyp); ein Worker-Start nach einem Tag räumt ihn weg |
| 2110 | Warning | `Application/Processing/ProcessingLog` | Versuch hat seinen Job an einen anderen Worker verloren (Sperre abgelaufen); Ergebnis bzw. Fehlschlag wird verworfen |
| 2111 | Warning | `Application/Processing/ProcessingLog` | Job beim Herunterfahren nicht zurückgegeben (nur Ausnahmetyp); er läuft nach Ablauf seiner Sperre erneut |
| 2112 | Information | `Worker/Processing/ProcessingLog` | Beim Start Reste älter als einen Tag aus dem Arbeitsordner `/data/tmp` entfernt: Anzahl |
| 2113 | Warning | `Worker/Processing/ProcessingLog` | Rest im Arbeitsordner nicht entfernt (nur Ausnahmetyp); nur Platzverlust |
| 3000 | Information | `Infrastructure/Persistence/PersistenceLog` | Migration beginnt, Anzahl bekannter Migrationen |
| 3001 | Information | `Infrastructure/Persistence/PersistenceLog` | Schema aktuell, Dauer in ms |
| 3002 | Error | `Infrastructure/Persistence/PersistenceLog` | Migration fehlgeschlagen |
| 3100 | Information | `Infrastructure/Documents/DocumentStorageLog` | Datei gespeichert: Art, Dokument-Id, Bereich (`inbox`/`archive`), Größe in Bytes |
| 3101 | Warning | `Infrastructure/Documents/DocumentStorageLog` | Speichern fehlgeschlagen (nur Ausnahmetyp, keine Meldung mit Pfad); die halbe Datei wurde verworfen |
| 3102 | Information | `Infrastructure/Documents/DocumentStorageLog` | Original vom Eingang ins Archiv verschoben |
| 3103 | Warning | `Infrastructure/Documents/DocumentStorageLog` | Aufräumen (temporäre Datei, alter Eingangsordner) fehlgeschlagen; nur Platzverlust |
| 3200 | Information | `Application/Documents/DocumentIntakeLog` | Dokument angenommen und eingereiht: Id, Größe, Medientyp |
| 3201 | Error | `Application/Documents/DocumentIntakeLog` | Annahme fehlgeschlagen (nur Ausnahmetyp); die Eingangsdatei wird verworfen, außer 3203/3204 folgen |
| 3202 | Warning | `Application/Documents/DocumentIntakeLog` | Eingangsdatei nach einem Fehler nicht entfernbar; nur Platzverlust |
| 3203 | Warning | `Application/Documents/DocumentIntakeLog` | Speichern meldete einen Fehler, das Dokument steht aber in der Datenbank; die Eingangsdatei bleibt |
| 3204 | Warning | `Application/Documents/DocumentIntakeLog` | Nach einem Fehler nicht prüfbar, ob das Dokument gespeichert ist (nur Ausnahmetyp); die Eingangsdatei bleibt |
| 3210 | Information | `Infrastructure/Search/SearchLog` | Suche beantwortet: Trefferzahl, Dauer in ms, ob der Ähnlichkeits-Rückfall nötig war (nie die Anfrage) |
| 3300 | Debug | `Infrastructure/Ocr/OcrLog` | Werkzeug (`ocrmypdf`, `img2pdf`, `heif-convert`, `pdftotext`) beendet: Exit-Code, Dauer in ms |
| 3301 | Warning | `Infrastructure/Ocr/OcrLog` | Werkzeug hat sein Zeitlimit überschritten und wurde samt Kindprozessen beendet |
| 3302 | Information | `Infrastructure/Ocr/OcrLog` | Werkzeug wegen Abbruch (Herunterfahren) beendet |
| 3303 | Information | `Infrastructure/Ocr/OcrLog` | Erkannter Text auf die Höchstlänge (Zeichen) gekürzt |
| 3304 | Warning | `Infrastructure/Ocr/OcrLog` | OCR-Arbeitsordner nicht gelöscht (nur Ausnahmetyp); nur Platzverlust |
| 3400 | Information | `Infrastructure/Composition/KeyRingCertificateGuard` | Schlüsselring mit Zertifikat verschlüsselt, Fingerabdruck (kein Geheimnis) |
| 3401 | Warning | `Infrastructure/Composition/KeyRingCertificateGuard` | Kein Zertifikat konfiguriert, Schlüsselring unverschlüsselt (nur Development) |
| 4000 | Information | `Web.Client/Telemetry/WebClientLog` | Kultur (Sprache) angewendet |
| 4001 | Warning | `Web.Client/Telemetry/WebClientLog` | Gespeicherte Sprache wird nicht unterstützt, Standard wird genommen |
| 4002 | Warning | `Web.Client/Telemetry/WebClientLog` | Sprachpräferenz nicht lesbar oder nicht speicherbar |
| 4003 | Information | `Web.Client/Telemetry/WebClientLog` | Sprachwechsel angefordert |
| 4100 | Information | `Web.Client/Documents/DocumentsLog` | Upload gestartet (Größe in Bytes) |
| 4101 | Information | `Web.Client/Documents/DocumentsLog` | Upload vom Server angenommen |
| 4102 | Warning | `Web.Client/Documents/DocumentsLog` | Upload abgelehnt (Grund: `empty`, `too-large`) |
| 4103 | Warning | `Web.Client/Documents/DocumentsLog` | API-Aufruf mit HTTP-Fehlercode beantwortet (Vorgang, Statuscode) |
| 4104 | Warning | `Web.Client/Documents/DocumentsLog` | API nicht erreichbar oder unlesbare Antwort (Vorgang, Ausnahmetyp) |
| 4105 | Debug | `Web.Client/Documents/DocumentsLog` | Dokumentliste geladen (Anzahl) |
| 4106 | Information | `Web.Client/Documents/DocumentsLog` | Dokument hat den Verarbeitungsstatus gewechselt (neuer Status) |
| 4107 | Information | `Web.Client/Documents/DocumentsLog` | Suche beendet (Trefferzahl, ohne Suchbegriff) |
| 4108 | Debug | `Web.Client/Documents/DocumentsLog` | Liste fragt alle 3 s nach, solange Dokumente in Arbeit sind |
| 4109 | Information | `Web.Client/Documents/DocumentsLog` | Dokument privat gemacht oder wieder freigegeben (`true`/`false`) |
| 4200 | Information | `Web.Client/Viewer/ViewerLog` | Viewer hat ein Dokument geöffnet (Seitenzahl, Renderer `webgl2`/`fallback`) |
| 4201 | Warning | `Web.Client/Viewer/ViewerLog` | WebGL2 nicht nutzbar, Rückfall auf den CSS-Filter (Grund: `no-webgl2`, `shader-error`, `context-lost`, `restore-failed`) |
| 4202 | Debug | `Web.Client/Viewer/ViewerLog` | sichtbarer Ausschnitt einer Seite gerendert (Dauer in ms, gerundet) |
| 4203 | Warning | `Web.Client/Viewer/ViewerLog` | Viewer konnte das Dokument nicht öffnen (technischer Fehlername von PDF.js, z. B. `InvalidPDFException`) |
| 4204 | Warning | `Web.Client/Viewer/ViewerLog` | Seite konnte nicht gerendert werden (technischer Fehlername) |
| 4205 | Information | `Web.Client/Viewer/ViewerLog` | Dokument hat noch kein PDF zum Anzeigen (Verarbeitungsstatus) |
| 4206 | Information | `Web.Client/Viewer/ViewerLog` | angezeigte Seite hat keine Textebene |
| 4207 | Information | `Web.Client/Viewer/ViewerLog` | Schalter „Originalfarben“ umgestellt (`on`/`off`) |
| 4208 | Warning | `Web.Client/Viewer/ViewerLog` | Browserspeicher für den Schalter „Originalfarben“ nicht nutzbar |
| 4209 | Warning | `Web.Client/Viewer/ViewerLog` | Vorschaubild konnte nicht gerendert werden (technischer Fehlername oder `interop`) |
| 4210 | Warning | `Web.Client/Viewer/ViewerLog` | WebGL-Kontext verloren (z. B. GPU-Reset); Viewer versucht den Neuaufbau |
| 4211 | Information | `Web.Client/Viewer/ViewerLog` | WebGL-Kontext wieder da, Seite neu gezeichnet |
| 4400 | Information | `Web.Client/Accounts/AccountsLog` | Sitzungszustand geladen (`SignedIn`, `SignedOut`, `SetupNeeded`, `Unavailable`) |
| 4401 | Information | `Web.Client/Accounts/AccountsLog` | Server antwortete 401, Sitzung vorbei |
| 4402 | Information | `Web.Client/Accounts/AccountsLog` | angemeldet |
| 4403 | Information | `Web.Client/Accounts/AccountsLog` | abgemeldet (überall: ja/nein) |
| 4404 | Warning | `Web.Client/Accounts/AccountsLog` | Konto-Aufruf mit HTTP-Fehlercode beantwortet (Vorgang, Statuscode) |
| 4405 | Warning | `Web.Client/Accounts/AccountsLog` | Konto-Aufruf nicht erreichbar oder unlesbare Antwort (Vorgang, Ausnahmetyp) |
| 4406 | Information | `Web.Client/Accounts/AccountsLog` | Haushalt eingerichtet |
| 4407 | Information | `Web.Client/Accounts/AccountsLog` | Einladungslink erzeugt (nie der Link) |
| 4408 | Information | `Web.Client/Accounts/AccountsLog` | Einladung angenommen |
| 4409 | Information | `Web.Client/Accounts/AccountsLog` | Rücksetzlink erzeugt (nie der Link) |
| 4410 | Information | `Web.Client/Accounts/AccountsLog` | Mitglied entfernt |
| 4411 | Information | `Web.Client/Accounts/AccountsLog` | Passwort geändert (Weg: `account`, `reset-link`, `emergency-code`) |
| 4412 | Debug | `Web.Client/Accounts/AccountsLog` | nicht angemeldet, Weiterleitung zur Anmeldung |
| 4413 | Warning | `Web.Client/Accounts/AccountsLog` | Kopieren in die Zwischenablage fehlgeschlagen (Ausnahmetyp) |
| 5100 | Information | `Application/Accounts/AccountsLog` | Haushalt eingerichtet: Verwalter-Id, Anzahl übernommener Dokumente ohne Besitzer (M1) |
| 5101 | Warning | `Application/Accounts/AccountsLog` | Einrichtung abgelehnt, Haushalt schon eingerichtet |
| 5102 | Information | `Application/Accounts/AccountsLog` | Einladung erzeugt: Konto-Id des Verwalters, Ablaufzeit (nie der Link) |
| 5103 | Information | `Application/Accounts/AccountsLog` | Einladung angenommen: Id des neuen Mitglieds |
| 5104 | Information | `Application/Accounts/AccountsLog` | Mitglied entfernt: Konto-Id des Verwalters, Id des Mitglieds |
| 5105 | Information | `Application/Accounts/AccountsLog` | Rücksetzlink erzeugt: Konto-Id des Verwalters, Id des Mitglieds, Ablaufzeit |
| 5106 | Information | `Application/Accounts/AccountsLog` | Passwort per Rücksetzlink neu gesetzt: Konto-Id |
| 5107 | Warning | `Application/Accounts/AccountsLog` | Passwort per Notfall-Code neu gesetzt, neuer Code ausgegeben: Konto-Id |
| 5108 | Warning | `Application/Accounts/AccountsLog` | Notfall-Reset abgelehnt (unbekanntes Konto oder falscher Code) |
| 5109 | Information | `Application/Accounts/AccountsLog` | Passwort geändert: Konto-Id |
| 5110 | Information | `Application/Accounts/AccountsLog` | „Auf allen Geräten abmelden“: Konto-Id |
| 5111 | Warning | `Application/Accounts/AccountsLog` | Verwalter-Aktion eines Mitglieds abgelehnt (403): Konto-Id, Aktion (`invite`, `list-members`, `remove-member`, `reset-link`) |
| 5120 | Information | `Application/Documents/DocumentPrivacyLog` | Besitzer hat „privat“ umgestellt: Dokument-Id, neuer Wert, Konto-Id |

Web.Client: Die Ereignisse erscheinen nur in der Browser-Konsole und enthalten nur Sprachkürzel,
Größen, Anzahlen, Statuscodes, Dauern und technische Fehlernamen – nie Dateinamen, Titel,
Suchbegriffe oder Dokumentinhalte.
In WASM gibt es keine `ActivitySource` und kein `Meter` (kein Exporter).

### Activities

| Name | Quelle | Ablauf |
|---|---|---|
| `nomorepaper.server.startup` | `NoMorePaper.Server` | Start-Aufgaben des Servers vor dem ersten Request |
| `nomorepaper.database.migrate` | `NoMorePaper.Infrastructure` | EF-Core-Migrationen anwenden (Kind von `server.startup`) |
| `nomorepaper.storage.write` | `NoMorePaper.Infrastructure` | eine Dokumentdatei atomar schreiben (Tag `nomorepaper.storage.kind`) |
| `nomorepaper.storage.archive_original` | `NoMorePaper.Infrastructure` | Original vom Eingang ins Archiv verschieben |
| `nomorepaper.documents.receive` | `NoMorePaper.Application` | ein Dokument annehmen: Eingang speichern, Dokument und Auftrag eintragen |
| `nomorepaper.search.query` | `NoMorePaper.Infrastructure` | eine Suche ausführen (Stämme, ggf. Ähnlichkeits-Rückfall) |
| `nomorepaper.worker.heartbeat` | `NoMorePaper.Worker` | ein Heartbeat in `worker_heartbeats` schreiben |
| `nomorepaper.processing.document` | `NoMorePaper.Application` | ein Verarbeitungsversuch eines Dokuments (OCR, Archiv speichern, abschließen); Tag `nomorepaper.processing.attempt` |

### Messwerte

| Name | Art | Meter | Bedeutung |
|---|---|---|---|
| `nomorepaper.worker.heartbeat_failures` | Counter | `NoMorePaper.Worker` | nicht geschriebene Heartbeats |
| `nomorepaper.upload.rejected` | Counter | `NoMorePaper.Server` | abgelehnte Uploads, Tag `nomorepaper.upload.reason` (siehe Ereignis 1101) |
| `nomorepaper.request_guard.rejected` | Counter | `NoMorePaper.Server` | abgelehnte ändernde API-Anfragen, Tag `nomorepaper.request_guard.fetch_site` (siehe Ereignis 1200) |
| `nomorepaper.auth.sign_ins` | Counter | `NoMorePaper.Server` | Anmeldeversuche, Tag `nomorepaper.auth.outcome` (`succeeded`/`failed`/`locked`) |
| `nomorepaper.documents.received` | Counter | `NoMorePaper.Application` | angenommene Dokumente |
| `nomorepaper.search.duration` | Histogramm (ms) | `NoMorePaper.Infrastructure` | Dauer einer Suche |
| `nomorepaper.storage.stored_bytes` | Counter (By) | `NoMorePaper.Infrastructure` | in die Dokumentablage geschriebene Bytes, Tag `nomorepaper.storage.kind` (`Original`/`Archive`) |
| `nomorepaper.processing.completed` | Counter | `NoMorePaper.Application` | erfolgreich verarbeitete Dokumente |
| `nomorepaper.processing.failed` | Counter | `NoMorePaper.Application` | fehlgeschlagene Versuche, Tag `nomorepaper.processing.outcome` (`retry`/`failed` = aufgegeben) |
| `nomorepaper.processing.duration` | Histogram (s) | `NoMorePaper.Application` | Dauer eines Versuchs, Tag `nomorepaper.processing.outcome` (`done`/`retry`/`failed`) |

## Health
- **Server** `/health`: 200, wenn die Datenbank erreichbar ist, sonst 503.
- **Worker** `/health` (interner Port 8080, nicht veröffentlicht): 200, wenn der letzte Heartbeat
  in der Datenbank höchstens 60 s alt ist (Intervall 15 s). Prüft so Schleife und Datenbank.
- Docker-Healthchecks rufen beide Endpunkte im Container auf (`docker-compose.yml`): der Server
  (Alpine) per `wget`, der Worker (Ubuntu-Image wegen OCRmyPDF) per `curl`.
