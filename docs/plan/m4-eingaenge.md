# M4 – Eingänge

**Ziel:** Dokumente kommen nicht nur über den Upload im Browser, sondern auch über die Kamera der PWA,
den Scanner-Ordner, eine eigene Mail-Adresse und iCloud Drive herein – jedes Dokument mit klarem
Besitzer und der Privat-Einstellung seines Kontos. Eine Seite „Eingänge“ zeigt, ob jeder Eingang läuft.
**Demo:** Mit dem iPhone (über `tailscale serve`) die Kamera öffnen → die App sagt „weiter nach links“,
dann „Seite 1 aufgenommen“, zweite Seite, „Fertig“ → ein Dokument mit zwei Seiten. Scanner legt ein PDF
in `\\nomorepaper\scan\anna` → wenig später erscheint es bei Anna, die Datei im Ordner ist weg. Eine
Rechnung an die Dokumenten-Adresse → Dokument mit „Aus der Mail: Zahlung bis 30.11. per Lastschrift“;
eine Werbemail → landet im Postfach-Ordner „NoMorePaper-Spam“. Seite „Eingänge“ sagt für jeden Eingang
„läuft, letzter Abruf vor 3 Minuten“.
**Voraussetzungen:** M2, M3 (Erkennung, Ollama). **Größe:** L.
**Nicht in M4:** Scan-Bearbeitung, Versionen, Papierkorb und Stapel-Trennung → **M4b** (ADR-0013).

Entschieden (Grilling 2026-11-19):
- **Schnitt:** M4 nur Eingänge; Scan-Bearbeitung wird M4b
- **Schlüsselring:** ADR-0015 ist schon umgesetzt (Zertifikat, Wächter); M4 speichert als Erstes
  Zugangsdaten damit
- **Besitzer:** jedes Konto hat seinen eigenen Eingang – Unterordner im Scanner-Ordner, hinterlegte
  Absender-Adressen für Mail, eigene Apple-ID für iCloud. Nicht zuordenbare Dokumente gehören dem
  **Verwalter** und stehen in „Zu prüfen“
- **Privat/geteilt:** Einstellung je Konto für alle Eingänge, ab Werk **geteilt**; beim Upload im
  Browser weiter je Datei änderbar
- **Kamera:** Ansagen (selten, über Live-Region), **Töne wie eine Einparkhilfe**, Vibration (nur
  Android), **Auto-Auslöser** bei vier erkannten Kanten und ruhigem Bild; Kanten und Entzerrung mit
  **OpenCV.js** im Browser; gespeichert wird **nur die zugeschnittene Seite**; mehrere Seiten werden
  ein Dokument
- **Scanner-Ordner:** **Samba-Container** in Compose, ein Unterordner je Konto; nach dem Einlesen wird
  die Datei **gelöscht**, nicht lesbare Dateien wandern nach `Fehler/`
- **Mail:** eigenes Postfach (ADR-0009), vom **Verwalter in der App** eingerichtet (Passwort
  verschlüsselt), Abruf **alle 5 Minuten**. Das **LLM prüft jede Mail auf Spam**; Spam wird nicht
  abgelegt und wandert in den Ordner **„NoMorePaper-Spam“**; schiebt man sie zurück, wird sie ohne
  Spam-Prüfung eingelesen. Mails von hinterlegten Absendern der Konten gelten nie als Spam
- **Anhänge** (PDF, Bilder) werden Dokumente mit dem Feld **„Aus der Mail“** (Absender-Adresse,
  Betreff, 1–2 Sätze Wichtiges aus dem Mailtext, leer wenn nichts Nützliches)
- **Mail ohne Anhang:** das LLM entscheidet, ob es ein echtes Dokument ist; dann wird der Mailtext als
  **Text-PDF** (Absender, Datum, Betreff, Text) abgelegt, sonst Ordner „Verarbeitet“ ohne Dokument
- **iCloud:** je Konto mit eigener Apple-ID, ein gewählter Ordner, **nur lesen**, alle **15 Minuten**;
  NoMorePaper merkt sich, was eingelesen ist. **Die Spur startet erst nach dem Test des Prototyps mit
  echter Apple-ID** (ADR-0018); scheitert er, Fallback iOS-Kurzbefehl per neuem ADR
- **Seite „Eingänge“:** Zustand je Eingang (läuft, letzter Abruf, Fehler in Klartext, „iCloud braucht
  einen neuen Code“)
- **Herkunft unklar** (Nachfrage nach dem Plan-Review): solche Dokumente sind **privat beim Verwalter**,
  bis er sie geprüft hat; er kann „Passt so“ wählen oder sie an ein anderes Konto **weitergeben** – dann
  gilt dessen Privat-Einstellung

Daraus abgeleitet:
- Eine Datei, die ihr Besitzer **selbst** schon hat (SHA-256), wird bei Hintergrund-Eingängen **still
  verworfen** (Log ohne Dateinamen) – es gibt niemanden, dem man 409 zeigen könnte. Ein geteiltes
  Dokument eines anderen Kontos zählt nicht: es könnte später privat werden
- Ein Eingang ohne Besitzer (Konto entfernt, Scanner-Unterordner ohne Konto) liest nichts mehr ein und meldet das auf „Eingänge“; seine
  Dateien bleiben liegen. Einstellungen, Absender-Adressen und Sitzungen eines entfernten Kontos werden
  gelöscht
- Der Absender einer Mail zählt nur, wenn der eigene Mailserver SPF/DMARC als bestanden vermerkt
  (`Authentication-Results` mit der eingetragenen `authserv-id` des eigenen Servers); sonst ist er unbekannt – ein gefälschter Absender kann weder die
  Spam-Prüfung umgehen noch Dokumente einem Konto unterschieben
- Eine Mail wird nie zweimal angenommen (gemerkte Kennung vor dem Verschieben); eine Mail, die dreimal
  scheitert, wandert in `NoMorePaper-Fehler` und wird auf „Eingänge“ gemeldet
- Erkennung und Mail-Prüfung teilen sich Ollama über **einen** Zugang nacheinander; Wartezeit zählt nicht
  zum Zeitlimit; die Erkennung hat Vorrang. Fällt Ollama aus, bleiben Mails unberührt im Posteingang
- Wird ein Dokument unklarer Herkunft weitergegeben, bevor es bestätigt ist, wird es mit der Sicht des
  neuen Besitzers neu erkannt (sonst könnten private Listeneinträge des Verwalters sichtbar werden)
- Zugangsdaten und die Apple-ID-Sitzung liegen nur mit Data Protection verschlüsselt in Postgres
  (ADR-0015), nie in Logs, nie in der Antwort einer API
- Der Text einer Mail verlässt das Netz nicht; die Spam-Prüfung läuft im selben lokalen Ollama (ADR-0002)

## Aufgaben

### 0. Gemeinsame Bausteine (Schritt 0)
- [x] Domain: `DocumentSource` (`Upload`, `Camera`, `ScannerFolder`, `Mail`, `ICloud`) an `Document`;
      `MailOrigin` (Absender-Adresse, Betreff, Hinweis ≤ 500 Zeichen) als „Aus der Mail“;
      `NeedsOwnerCheck` (unbekannter Absender) macht ein Dokument für den Verwalter „zu prüfen“
- [x] Application: `IntakeDocument` – Annahme **mit ausdrücklichem Besitzer** (ohne `ICurrentAccount`)
      für Hintergrund-Eingänge; `ReceiveDocument` (Browser) nutzt sie nach der Kontoprüfung
- [x] Application: Port `IInputSource` (Art, Takt, `RunAsync`), Ergebnis `InputRunResult`;
      `InputStatus` je Eingang (letzter Abruf, Fehlerklasse, „braucht Aufmerksamkeit“)
- [x] Application: `AccountPreferences` (neue Dokumente privat ja/nein, Mail-Absender-Adressen),
      Port `IAccountPreferencesStore`; Port `ISecretProtector` (Data Protection, je Zweck)
- [x] Application: Port `IPdfComposer` – Bilder → ein PDF (Kamera), Text → PDF (Mail)
- [x] Infrastructure: Tabellen `account_preferences`, `input_connections` (verschlüsselte Zugangsdaten),
      `input_status`, `input_seen_items` (Hash der Kennung, z. B. Message-ID oder iCloud-Datei-Id);
      Spalten `source`, `mail_*`, `needs_owner_check` an `documents`; Migration `Inputs`
- [x] Infrastructure: `PdfSharpComposer` (PDFsharp, MIT) mit eingebetteter Schrift (DejaVu Sans)
- [x] Worker: `InputScheduler` (ein Hintergrunddienst, führt jede registrierte `IInputSource` in ihrem
      Takt aus, Fehler eines Eingangs stören die anderen nicht)
- [x] „Zu prüfen“: Regel um `NeedsOwnerCheck` erweitert (beim Besitzer), Zähler gleich; Anwendungsfälle
      „Passt so“ (`ConfirmOwnerCheck`) und „Weitergeben“ (`HandOverDocument`) mit Endpunkten
- [x] Gemeinsamer LLM-Zugang `LlmGate` für Erkennung und Mail-Prüfung
- [x] Einstellungen-API `GET/PUT /api/account/preferences` und Clients `PreferencesApi`, `InputsApi`
      (Kamera, Upload und Seite „Eingänge“ brauchen sie)
- [x] `FileSignature` nach Application (Server, Scanner-Ordner, Mail)
- [x] Contracts: DTOs für Einstellungen, Eingangsstatus, Mail-Einrichtung, Weitergabe, „Aus der Mail“
- [x] ADR-0024 (Eingänge: Bibliotheken, Samba, Spam-Prüfung), Katalogblöcke reservieren

### A. Kamera in der PWA
- [x] Seite `/kamera` (Navigation „Kamera“), Rückkamera per `getUserMedia`, OpenCV.js lokal unter
      `wwwroot/lib/opencv` (Apache-2.0, nur auf dieser Seite nachgeladen)
- [x] Kantenerkennung je Videobild (verkleinert, ~8 Bilder/s), Viereck → Hinweis: „weiter nach links/
      rechts/oben/unten“, „näher ran“, „weiter weg“, „ruhig halten“; Ansage höchstens alle 2,5 s
- [x] Töne (WebAudio): Tonhöhe und Takt nach Güte; Auslöse-Klick; `navigator.vibrate` wo vorhanden;
      Töne abschaltbar (Einstellung im Browser)
- [x] Auto-Auslöser: vier Kanten, Fläche ≥ 35 % des Bildes, 0,8 s ruhig → Aufnahme in voller Auflösung,
      Entzerrung (Perspektive) → JPEG; Ansage „Seite N aufgenommen“; manueller Auslöser zusätzlich
- [x] Seitenliste mit „letzte Seite verwerfen“, „Fertig“; Privat-Schalter vorbelegt aus der Konto-Einstellung
- [x] Server: `POST /api/documents/pages` (1–50 JPEGs) → `IPdfComposer` → `ReceiveDocument`, Quelle `Camera`
- [x] Ohne Kamera/Berechtigung: klare Meldung und Verweis auf „Hochladen“

### B. Scanner-Ordner
- [x] `docker-compose.yml`: Dienst `samba` (Image mit fester Version, Benutzer `scanner`, Passwort aus
      `.env`), Freigabe `scan` auf Volume `scaninbox`, Port 445 nur an `NOMOREPAPER_SMB_BIND` (LAN-/
      Tailscale-Adresse, nie `0.0.0.0` ab Werk); Worker bindet `scaninbox` ein
- [x] `ScannerFolderSource` (alle 30 s): legt je Konto einen Unterordner (Anmeldename) an; eine Datei gilt
      als fertig, wenn Größe und Änderungszeit über zwei Durchläufe gleich sind; erkannter Typ (PDF, JPG,
      PNG, HEIC), sonst `Fehler/`; nach erfolgreicher Annahme löschen
- [x] Datei im Wurzelordner → Verwalter, privat, `NeedsOwnerCheck`; Ordner eines entfernten Kontos →
      Dateien bleiben, Meldung auf „Eingänge“
- [x] Gelöscht wird erst nach erfolgreicher Annahme (Commit); Samba im Compose-Profil `scanner`

### C. Mail (IMAP)
- [x] `MailKit` (MIT): Verbindung, Ordner `INBOX`, `Verarbeitet`, `NoMorePaper-Spam` (werden angelegt)
- [x] Ablauf je Mail: Absender einem Konto zuordnen (hinterlegte Adressen, nur mit SPF/DMARC „pass“) → bei unbekanntem Absender
      LLM-Prüfung `IMailAssessor` (Spam? wichtig? Hinweis für „Aus der Mail“); Mails von Konten nur
      Hinweis, keine Spam-Prüfung; Message-ID, die schon als Spam galt, wird nie wieder geprüft
- [x] Anhänge (PDF, Bild) → je ein Dokument mit `MailOrigin`; wichtige Mail ohne Anhang → Text-PDF
- [x] Danach Mail nach `Verarbeitet` bzw. `NoMorePaper-Spam` verschieben; Fehler → Mail bleibt, nächster Lauf
- [x] Spam-/Wichtig-Prompt mit JSON-Schema, gemessen an 24 selbst geschriebenen Testmails
- [x] Einstellungsseite (nur Verwalter): Server, Port, TLS, Benutzer, Passwort (nur setzen, nie anzeigen),
      „Verbindung testen“; Konto-Seite: eigene Absender-Adressen (Konto-Seite: Spur E)
- [x] Viewer „Angaben“: „Aus der Mail“ direkt nach der Zusammenfassung

### D. iCloud Drive (startet nach bestandenem Test, ADR-0018)
- [ ] Spike-Code nach Infrastructure übernehmen (`Inputs/ICloud/`), DPAPI → `ISecretProtector`, Sitzung in
      `input_connections`; Cookie-Grenzen wie im Spike
- [ ] Einrichtung in der App (Server, interaktiv): Apple-ID + Passwort → Code → Ordner wählen; Passwort wird
      **nicht** gespeichert, nur die Sitzung
- [ ] `ICloudSource` (Worker, alle 15 min): neue Dateien im Ordner (Datei-Id + Änderung in
      `input_seen_items`) → Annahme; Fehlerklassen aus ADR-0018 → Status; nie automatisch mit Passwort;
      bei 429/503 lange Pause
- [ ] „Code erneuern“ auf der Seite „Eingänge“, voll per Screenreader

### E. Einstellungen und Seite „Eingänge“
- [x] Konto-Seite: „Neue Dokumente privat“ (Schalter), Absender-Adressen (Liste)
- [x] Seite `/eingaenge`: je Eingang Zustand als Text (nie nur Farbe), letzter Abruf, letzte Meldung,
      Aktion „einrichten“ für Mail; Live-Region bei Änderung
- [ ] iCloud-Aktionen „einrichten“ und „Code erneuern“ auf `/eingaenge`: kommen mit Spur D. Bis dahin steht
      dort nur „iCloud folgt – wartet auf den Test mit Apple-ID“ ohne Link; Spur D schaltet
      `Inputs.ICloudPageExists` auf `true`
- [x] „Herkunft unklar“ im Viewer und in „Zu prüfen“: „Passt so“ und „Weitergeben an …“ per Tastatur
- [x] API: `GET /api/inputs`

### F. Tests und Doku
- [ ] Unit: Zuordnung des Besitzers, Spam-Regeln (Konto-Absender nie Spam, bekannte Spam-ID), fertige
      Datei im Ordner, Hinweis-Logik der Kamera
- [ ] Integration: Ordner-Eingang gegen echtes Dateisystem; IMAP gegen einen IMAP-Testserver im Container
      (GreenMail, Apache-2.0); Verschlüsselung der Zugangsdaten (nie Klartext in der Tabelle)
- [ ] UI: axe DE/EN auf `/kamera`, `/eingaenge`, Mail-Einstellungen; Kamera mit Chromium-Fake-Kamera
      (Video eines Blatts) bis zum Auto-Auslöser
- [ ] KB `eingaenge.md`, ADR-0024, Katalog, `betrieb.md` (Samba, Scanner einrichten, Postfach)

## Akzeptanzkriterien
- [ ] Jeder Eingang legt Dokumente mit richtigem Besitzer und dessen Privat-Einstellung an; Unzuordenbares
      gehört dem Verwalter und steht in seinem „Zu prüfen“
- [ ] Kamera: Fake-Kamera-Test löst automatisch aus und ergibt ein Dokument mit N Seiten; Prüfung am iPhone
      mit VoiceOver durch den Nutzer
- [ ] Mail: an den 24 Testmails kein wichtiges Dokument als Spam, ≥ 90 % Spam erkannt (⚠️ messen)
- [ ] Keine Zugangsdaten, Mailtexte, Betreffs, Adressen oder Dateinamen in Logs; Passwörter nur verschlüsselt
- [ ] Seite „Eingänge“ zeigt Fehler jedes Eingangs als Text; axe 0 Verstöße; NVDA-Prüfung durch den Nutzer
- [ ] CI grün

## Parallelisierung

Integrationsbranch `feat/m04-eingaenge` von `develop`.

| Spur | Inhalt | Branch | Start | wartet auf |
|---|---|---|---|---|
| 0 | Schritt 0 | `feat/m04-eingaenge` | sofort | – |
| A | Kamera | `feat/m04-a-kamera` | nach 0 | – |
| B | Scanner-Ordner | `feat/m04-b-scanner` | nach 0 | – |
| C | Mail | `feat/m04-c-mail` | nach 0 | – |
| E | Einstellungen, „Eingänge“ | `feat/m04-e-eingaenge-seite` | nach 0 | – |
| D | iCloud | `feat/m04-d-icloud` | nach 0 **und** bestandenem Test | Nutzer |

```
0 ──┬─► B ─┐
    ├─► E ─┤
    ├─► A ─┼─► Merge B, E, A, C ─► (D nach Nutzertest) ─► Gesamt-Review
    └─► C ─┘   (C: P3 dann P4 nacheinander, Opus → Sonnet)
```
A, B, C und E laufen gleichzeitig (getrennte Exklusiv-Dateien); gemergt wird in der Reihenfolge unten.

- **Exklusiv-Dateien:** A: `Pages/Camera*`, `Camera/*`, `wwwroot/js/camera/*`, `wwwroot/lib/opencv/*`,
  `Documents/PageUploadApi.cs`, `Server/Documents/PageUpload*.cs`, `tests/NoMorePaper.Web.Client.Tests`.
  B: `Infrastructure/Inputs/ScannerFolder/*`, Dienst `samba` in Compose, `Dockerfile.worker`.
  C: `Application/Inputs/Mail/*`, `Infrastructure/Inputs/Mail/*`, `Infrastructure/Recognition/*Mail*`,
  `Server/Inputs/MailSettings*`, `Pages/MailSettings*`, `Inputs/MailSettingsApi.cs`,
  `Classification/MailOriginSection.razor`. D: `*/Inputs/ICloud/*`, `Server/Inputs/ICloud*`, `Pages/ICloud*`,
  `Inputs/ICloudApi.cs`. E: `Pages/Inputs*`, `Pages/Account.razor`, `Pages/Upload.razor`,
  `Server/Inputs/InputStatusEndpoints.cs`, `Classification/OwnerCheckSection.razor`.
- **Hotspots** (nur ergänzen): `Strings*.resx`, `MainLayout.razor` (Navigation), `Server/Program.cs`,
  `docs/kb/telemetrie.md`, `docs/kb/eingaenge.md`, `ApplicationServiceCollectionExtensions.cs`,
  `InputRegistration.cs`, `RecognitionRegistration.cs`, `WorkerComposition.cs`, `docker-compose.yml`,
  `.env.example`, `DocumentFieldsPanel.razor` und `Review.razor` (je eine Zeile zum Einbinden),
  `Directory.Packages.props`, `NoMorePaper.slnx`.
- **Merge-Reihenfolge:** B → E → A → C → D.

## Detailplan
Eigene Datei (mehr als 15 Dateien): [features/m4-eingaenge-detail.md](features/m4-eingaenge-detail.md)

## Offene Fragen / Risiken
- **iCloud** hängt am Test mit echter Apple-ID (ADR-0018); bis dahin bleibt Spur D zu. Ohne Test kann M4
  ohne iCloud abgeschlossen werden, iCloud folgt dann als eigener Punkt
- **OpenCV.js** ist groß (~8–10 MB); der Service Worker cacht bisher nichts → beim ersten Öffnen der Kamera
  ladeanzeige mit Ansage, danach HTTP-Cache. Leistung auf älteren iPhones ⚠️ messen
- **iOS:** `getUserMedia` nur über HTTPS (`tailscale serve`) und nicht in jeder installierten PWA-Version
  gleich; Vibration gibt es im Browser auf iOS nicht
- **Samba** ist GPL; es läuft als **eigener Container** (Werkzeug, nicht Teil unseres Codes, nicht
  gelinkt) – Begründung in ADR-0024. Image mit fester Version und Digest; Port 445 nie öffentlich
- **Scanner** mit SMB1 nur: wird nicht unterstützt (Sicherheitsrisiko); in `betrieb.md` dokumentieren
- **Spam-Prüfung** kostet je Mail eine LLM-Anfrage (~20–40 s CPU) und teilt sich Ollama mit der Erkennung;
  Werbung in großer Menge verzögert die Erkennung. Gegenmaßnahme: Mails von Konto-Absendern ohne Prüfung,
  höchstens 20 Mails je Lauf
- **PDFsharp unter Linux** braucht eine eingebettete Schrift (Font-Resolver); DejaVu Sans (freie
  Bitstream-Vera-Lizenz)
- Aus M3 offen: NVDA-Prüfung, Erkennungszeit auf dem Server, erster Start mit Modell; aus M2: iPhone über
  `tailscale serve` (für die Kamera jetzt Pflicht)

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|
