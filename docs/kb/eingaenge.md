# Eingänge

Wie Dokumente außer über den Upload hereinkommen (M4): Kamera, Scanner-Ordner, Mail, iCloud Drive.
Entscheidung: [ADR-0024](../adr/0024-eingaenge.md), Plan: [m4-eingaenge.md](../plan/m4-eingaenge.md).
Stand: Schritt 0 (gemeinsame Bausteine). Die Spuren A–E ergänzen ihre Abschnitte.

## Annahme: `IntakeDocument`
- **Ein Kern für alle Quellen** (`Application/Documents/IntakeDocument.cs`): speichert das Original im
  Eingang, trägt Dokument, Datei und Auftrag in **einer** Transaktion ein und räumt bei Fehlern auf wie der
  Upload seit M1 (Eingangsdatei nur löschen, wenn sicher kein Dokument darauf zeigt).
- Der Browser-Upload (`ReceiveDocument`) prüft zuerst das angemeldete Konto und ruft dann die Annahme mit
  diesem Konto als Besitzer und der Privat-Wahl der Seite. Hintergrund-Eingänge rufen sie direkt.
- **Quelle** steht am Dokument (`documents.source`: `Upload`, `Camera`, `ScannerFolder`, `Mail`, `ICloud`);
  alte Dokumente sind `Upload`.

## Besitzer
- Ausdrücklicher Besitzer (`IntakeRequest.OwnerId`): Unterordner im Scanner-Ordner, hinterlegte
  Absender-Adresse, eigene Apple-ID. Ist das Konto entfernt → `OwnerGone`, nichts wird gespeichert (Log 5301).
- **Unbekannt** (`OwnerId = null`): das Dokument gehört dem **Verwalter** (ältester Verwalter,
  `IAccountStore.FindAdministratorIdAsync`), ist **privat** – egal, was der Verwalter für neue Dokumente
  gewählt hat – und trägt `needs_owner_check`. Ohne Verwalter (Haushalt nicht eingerichtet) →
  `NoAdministrator`, nichts wird gespeichert (Log 5302).

## Privat oder geteilt
- Einstellung je Konto (`account_preferences.new_documents_private`), ab Werk **geteilt**.
- Hintergrund-Eingänge nehmen immer diese Einstellung; der Browser schickt seine Wahl je Datei mit.
- API: `GET/PUT /api/account/preferences` (`AccountPreferencesDto`).

## Absender-Adressen (Mail)
- Je Konto höchstens 20, gespeichert klein geschrieben und getrimmt (`AccountPreferences.NormalizeAddress`:
  `lokal@domain.tld`, keine Leerzeichen, höchstens 320 Zeichen).
- **Eine Adresse gehört genau einem Konto** – der Schlüssel von `account_mail_senders` ist die Adresse. Eine
  Adresse eines anderen Kontos → 409 mit `errors: { mailSenders: ["taken"] }`, ungültig → 400 `invalid`.
  Fehlt die Liste (`null`), ist das ebenfalls 400 – so verschwinden Adressen nie aus Versehen. Wird das
  Konto beim Speichern gerade entfernt (Fremdschlüssel verletzt), antwortet die API wie jeder Kontopfad mit 401.
- **Bewusst:** jedes Konto darf jede noch freie Adresse eintragen. Der Haushalt vertraut sich; Mails von
  dieser Adresse gehören danach diesem Konto.
- Entfernt der Verwalter ein Konto, löscht die Datenbank dessen Einstellungen, Adressen, Einrichtungen
  (`input_connections`), Status und gemerkte Elemente (`ON DELETE CASCADE` auf `users`). Seine Dokumente bleiben.

## Dubletten
- **Browser** (`DuplicateScope.Visible`): wie seit M3 – jede Datei, die der Hochladende sehen darf, → 409.
- **Hintergrund** (`DuplicateScope.Owned`): nur die **eigenen** Dokumente des Besitzers zählen. Die Datei
  wird still verworfen (Log 5300, ohne Dateinamen), es gibt niemanden, dem man 409 zeigen könnte. Ein
  geteiltes Dokument eines anderen Kontos zählt nicht: es könnte später privat werden.

## Herkunft unklar: „Passt so“ und „Weitergeben“
- Dokumente mit `needs_owner_check` stehen **nur beim Besitzer** (Verwalter) in „Zu prüfen“ und zählen im
  Zähler mit, auch wenn ihre Angaben schon bestätigt sind.
- „Passt so“: `POST /api/documents/{id}/owner-check` – Flag aus, Privat bleibt wie es ist.
- „Weitergeben“: `POST /api/documents/{id}/hand-over` mit `{ accountId }` – nur der Besitzer, nur mit Flag,
  nur an ein anderes bestehendes Konto. Das neue Konto wird Besitzer, **dessen** Privat-Einstellung gilt,
  Bearbeiter werden geleert.
- Waren die Angaben **nicht bestätigt**, werden Absender, Art, Tags, Zusammenfassung und der erkannte Titel
  verworfen (Titel wieder aus dem Dateinamen), `recognition = None`, und die Erkennung wird **in derselben
  Transaktion** neu eingereiht (`IJobQueue.RequeueRecognitionAsync`, Priorität 0). Sonst könnten private
  Listeneinträge des Verwalters beim neuen Besitzer landen (ADR-0023). Datum und Betrag bleiben bis zur
  neuen Erkennung stehen; sie kommen aus dem Text, nicht aus Listen.
- Waren die Angaben **bestätigt**, bleiben sie – ohne Absender, Art und Tags, die die Zielsicht nicht sieht
  (`Account` des neuen Besitzers, wenn das Dokument privat bleibt, sonst `Household`). So landen private
  Listeneinträge des Verwalters nie beim neuen Besitzer (ADR-0023, Nachtrag 2026-11-19). Log 5303 nennt die
  Zahl der verworfenen Einträge.
- Läuft gerade eine Erkennung, verliert sie ihren Auftrag (neue Claim-Id) und ihr Ergebnis wird verworfen.
  Zusätzlich prüft `CompleteRecognitionAsync` unter der Zeilensperre den Besitzer, mit dessen Listen die
  Angaben entstanden (`RecognitionCommit.OwnerChanged`, Log 5213).
- Antworten: 404 für Unsichtbares, 403 ohne Besitz, 400 für unbekanntes oder eigenes Konto, 409 ohne Flag.

## „Aus der Mail“
- `MailOrigin` (Absender-Adresse ≤ 320, Betreff ≤ 300, Hinweis ≤ 500 Zeichen; Längeres wird gekürzt,
  Steuerzeichen werden Leerzeichen) in `documents.mail_sender|mail_subject|mail_note`.
- Erscheint in `DocumentFieldsDto.Mail`; nie in Logs.

## Hintergrund-Eingänge: `IInputSource` und `InputScheduler`
- Jede Quelle meldet Art (`InputKind`), Takt und `RunAsync` → `InputRunResult(Received, Skipped, Problem)`
  für den ganzen Eingang, dazu bei Eingängen je Konto (iCloud) `Accounts` mit je einem
  `InputAccountResult(AccountId, Received, Skipped, Problem)`. Probleme sind Ergebnis, keine Ausnahmen
  (`InputProblemKind`: `NotConfigured`, `SignInFailed`, `CodeNeeded`, `Unreachable`, `LlmUnavailable`,
  `Throttled`, `ProtocolChanged`, `UnknownFolder`, `StuckItems`, `Failed`).
- Registrierung **je Art**: `services.AddInputSource<TQuelle>(InputKind.…)` in
  `InputRegistration.AddInputSources()` (nur Worker) – scoped, mit der Art als Schlüssel, plus eine
  `InputSourceRegistration`. Der Scheduler lernt die Eingänge aus diesen Einträgen, **ohne** eine Quelle zu
  bauen, und baut je Lauf nur die eine Quelle. Eine Quelle, deren Konstruktor wirft, scheitert nur selbst
  (`Failed`, Log 2302) und wird nach 1 min erneut versucht; die anderen laufen weiter, der Worker-Host bleibt.
- Der `InputScheduler` (Worker) prüft alle 10 s, welche Quelle fällig ist (Takt ab Start des letzten Laufs),
  startet jeden Lauf in **eigenem Scope** (`CreateAsyncScope`) und **neben** den anderen (`InputRunner`); ein
  laufender Eingang wird nicht doppelt gestartet. Ausnahmen landen nur als Typ im Log; kein Fehler beendet
  den Host (Lernen der Eingänge: 2305).
- Jeder Lauf schreibt `input_status` (letzter Lauf, letzter Erfolg, Problem, „braucht Aufmerksamkeit“,
  Summe angenommener Dokumente): die Zeile des ganzen Eingangs (ohne Konto) und **je Konto seine eigene**.
  Ein späterer Erfolg eines Kontos löscht dessen Problem und zählt dessen Dokumente. Scheitert das
  Schreiben (auch beim Herunterfahren), wird es protokolliert (2303), der Lauf endet ruhig.
- Stores, Geheimnisse und PDF registriert `AddInputs()` (Server und Worker). Der Worker registriert auch
  den Konto-Store (Besitzer und Verwalter für die Annahme).
- Sichtbarkeit (`ListInputStatus`): Verwalter alles mit Kontonamen; Mitglied die Zeilen von Scanner-Ordner
  und Mail ohne Konto und seine eigenen Zeilen (sein iCloud).

## Gemerkte Elemente
- `input_seen_items`: je Eingang, Konto und Liste (`Imported`, `Spam`, `Failed`) nur der **SHA-256** der
  Kennung (Message-ID, iCloud-Datei-Id), nie die Kennung selbst; `failures` zählt Fehlschläge.

## Geheimnisse
- `ISecretProtector` mit ASP.NET Data Protection und dem gemeinsamen, per Zertifikat geschützten
  Schlüsselring (ADR-0015); Zweck `NoMorePaper.Inputs.<Zweck>` (`MailPassword`, `ICloudSession`). Ein
  Geheimnis öffnet sich nur mit seinem Zweck. Nicht lesbar → `SecretUnreadableException`, Log 3901.
- In `input_connections.secret` steht nur der verschlüsselte Wert; Einstellungen ohne Geheimnis als JSON
  (`jsonb`, Postgres normalisiert die Schreibweise).

## PDF erzeugen
- `PdfSharpComposer` (PDFsharp 6.2.4, MIT). PDFsharp 6.2.4 zieht `System.Security.Cryptography.Pkcs` 8.0.1
  mit bekannten Lücken nach; die Version ist zentral auf 10.0.15 angehoben.
- Schrift: **DejaVu Sans** und **DejaVu Sans Bold** als eingebettete Ressource (Release 2.37 von
  github.com/dejavu-fonts, Lizenz `Fonts/DejaVu-LICENSE.txt`); die Linux-Container haben keine Schriften. Der
  Font-Resolver ist eine prozessweite Einstellung von PDFsharp und wird einmal gesetzt.
- **Bilder:** je JPEG eine Seite, das Bild füllt sie; die kürzere Seite ist A4-breit (595,28 pt), die
  längere folgt dem Seitenverhältnis. So sieht OCR eine sinnvolle Auflösung (ein Foto mit 2 480 px Breite ≈
  300 dpi). JPEG wird unverändert eingebettet.
- **Text:** A4, Ränder 20 mm, Überschrift fett 16 pt, Kopffelder fett, Text 11 pt; Umbruch an Wortgrenzen,
  zu lange Wörter werden geteilt (binäre Suche), neue Seite wenn voll. Der Text bleibt Text (durchsuchbar).
- Das PDF entsteht im Speicher und wird erst danach geschrieben; ein unlesbares Bild → `InvalidDataException`
  (Log 3903), am Ziel steht dann nichts.

## Gemeinsamer LLM-Zugang: `LlmGate`
- Erkennung und Mail-Prüfung teilen sich Ollama (CPU): **eine** Anfrage zur Zeit. Eine wartende Erkennung
  kommt vor jeder wartenden Mail; untereinander in Ankunftsfolge.
- Wartezeit zählt nicht zum Zeitlimit der Anfrage; das Limit beginnt erst, wenn das Gate gehalten wird.
- Die Erkennung wartet höchstens `LockFor − Timeout − 1 min` (Standard 15 − 5 − 1 = 9 min, mindestens
  4 min). Danach `LlmBusyException`: `RecognizeDocument` gibt den Auftrag zurück, **ohne** Fehlversuch zu
  zählen (Log 5214); er ist sofort wieder fällig.

## Kamera (Spur A)
Seite `/kamera` (Navigation „Kamera“). Ablauf: „Kamera starten“ → Rückkamera (`getUserMedia`,
`facingMode: environment`, ideal 3840 px) → je Videobild Kantenerkennung → Hinweise, Töne → Auto-Auslöser →
Seitenliste → „Fertig“ → `POST /api/documents/pages` → ein PDF, Quelle `Camera`.

**Dateien:** `Pages/Camera.razor(.cs)`, `Camera/*` (Interop, Ansagen, Einstellung „Töne“, Logs, Registrierung
`AddCamera()`), `Documents/PageUploadApi.cs`, `wwwroot/js/camera/*`, `wwwroot/lib/opencv/*`, Server
`Documents/PageUpload*.cs`.

**OpenCV.js**
- Offizieller Release-Build **4.12.0** (Apache-2.0), unverändert unter `wwwroot/lib/opencv/opencv.js`
  (10 872 775 Bytes), Lizenz daneben (`LICENSE`).
  Quelle: `https://docs.opencv.org/4.12.0/opencv.js`,
  SHA-256 `c7d6967284acc7eb50897eeb4186690444396bbe834aeb7caf7a565285976829`;
  Lizenz: `https://raw.githubusercontent.com/opencv/opencv/4.12.0/LICENSE`,
  SHA-256 `cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30`.
- Wird **nur auf `/kamera`** per `<script>` nachgeladen (einmal je App-Sitzung), kein CDN. Das WebAssembly
  steckt als Base64 in der Datei; es gibt keine zweite Datei.
- **Eigenheit:** Der Build ist ein altes Emscripten-Modul mit eigener `then`-Methode. Wer eine Promise mit
  diesem Objekt auflöst (auch `return cv` aus einer `async`-Funktion), ruft `then` endlos auf – die Seite
  friert ein. `edges.js` wartet deshalb auf `onRuntimeInitialized` und löscht `cv.then`, bevor es das Modul
  weitergibt. Zeitlimit 60 s nach dem Laden des Skripts.
- Jede `cv.Mat` wird im `finally` freigegeben (WebAssembly-Speicher sieht der Garbage Collector nicht).

**Kanten (`edges.js`)**: Bild auf 640 px Breite, Graustufen, Gauß 5 × 5, Canny 50/150, Dilatation 3 × 3,
äußere Konturen, `approxPolyDP` (2 % des Umfangs), größtes konvexes Viereck ab 5 % der Bildfläche; Ecken
normiert 0–1, sortiert oben links, oben rechts, unten rechts, unten links. Etwa 8 Analysen je Sekunde
(`requestVideoFrameCallback`, sonst `requestAnimationFrame`).

**Hinweise (`guidance.js`, rein, Tabellentest)** – Vorrang von oben nach unten:

| Lage | Hinweis | Text |
|---|---|---|
| kein Viereck | `search` | „Kein Blatt im Bild.“ |
| eine Ecke näher als 2 % am Rand | `farther` | „Weiter weg.“ |
| Fläche < 35 % | `closer` | „Näher ran.“ |
| Mittelpunkt > 12 % neben der Bildmitte | `left`/`right`/`up`/`down` (größere Abweichung gewinnt) | „Weiter nach links.“ … |
| sonst, noch nicht ruhig | `hold` | „Ruhig halten.“ |
| sonst, ruhig | `ready` | – (Aufnahme) |

- **Bereit** = keine Ecke am Rand, Fläche ≥ 35 %, alle Ecken bewegen sich über 800 ms weniger als 1,5 % und
  das Blatt war in jedem Bild zu sehen. Die Mitte ist **keine** Bedingung (Plan: vier Kanten, Fläche, ruhig);
  ein Blatt etwas neben der Mitte wird also aufgenommen, der Richtungshinweis ist nur eine Hilfe.
- **Nächste Seite:** Nach einer Aufnahme schweigt die Führung, bis das Blatt 0,5 s nicht zu sehen war oder
  sich um mehr als 10 % bewegt hat – sonst würde dasselbe Blatt immer wieder aufgenommen. „Letzte Seite
  verwerfen“ gibt die Aufnahme sofort wieder frei (gleiches Blatt noch einmal).
- **Güte** für die Töne: 0 ohne Blatt, 0,15 bei Ecke am Rand, sonst 0,15 + 0,75 · Größe · (0,4 + 0,6 · Mitte),
  1 kurz vor der Aufnahme.

**Ansagen** (`CameraAnnouncer`, Dienst mit `TimeProvider`): höchstens alle 2,5 s, derselbe Hinweis erst nach
6 s wieder; die Seite fragt alle 250 ms, ob einer fällig ist (so wird ein zurückgehaltener Hinweis nach der
Pause doch gesprochen). „Kamera läuft“ und „Seite N aufgenommen“ starten die Pause ebenfalls. Live-Region
`polite`, Fehler `alert`. Der aktuelle Hinweis steht zusätzlich als Text („Hinweis: …“) auf der Seite.

**Töne** (`feedback.js`): Piepen 300–900 Hz, Abstand 600–120 ms nach Güte, Stille ohne Blatt, Klick beim
Auslösen, `navigator.vibrate(60)` wo vorhanden (nicht iOS). Der Audio-Kontext entsteht beim ersten
Tippen/Klicken/Tastendruck auf der Seite (iOS verlangt eine Geste; die Seite lädt das Modul vor dem
Start-Knopf). Schalter „Töne“ je Gerät in `localStorage` (`nomorepaper.camera.tones`, ab Werk an).

**Aufnahme** (`capture.js`): volles Videobild → `warpPerspective` auf die Kantenlängen (längere Seite
höchstens 3000 px) → JPEG 0,85. Gespeichert wird nur die zugeschnittene Seite. Manuell „Auslösen“ nimmt
das zuletzt gefundene Blatt, ohne Blatt das ganze Bild.
- **PDFsharp-Eigenheit:** PDFsharp 6.2.4 lehnt ein JPEG ab („Unsupported image format“), wenn direkt nach dem
  Startmarker nicht der JFIF-Kopf (APP0) steht – etwa ein Kommentar- oder Exif-Segment. Daten **nach** dem
  Endmarker stören nicht. `capture.js` baut deshalb den Kopf jedes Bildes neu: Start, JFIF, dann nur Tabellen,
  Frame und Scan (APP1–APP15 und Kommentare fallen weg). So hängt es nicht davon ab, was Safari oder Chrome
  schreiben.
- Seiten bleiben als Blobs im JS-Modul, bis „Fertig“ sie einzeln holt; sie überleben eine Kamera, die sich
  selbst beendet (iOS beim Sperren), und werden beim Verlassen der Seite gelöscht. Höchstens 50 Seiten.

**Server `POST /api/documents/pages`** (`PageUpload`, `PageUploadForm`, `PageUploadName`)
- Formular: `private` (`true`/`false`), `takenAt`, Teile `page` in Reihenfolge. 1–50 Teile, je ≤ 15 MB,
  zusammen ≤ 150 MB (Kestrel-Grenze je Anfrage angehoben), jedes Teil JPEG nach `FileSignature`.
- Antworten wie der Upload: 202 + `UploadResultDto`; 400 (kein/leeres Teil, > 50 Teile, kein JPEG, Bild nicht
  lesbar, `private` ungültig); 413; 401 (Konto entfernt); 409 + `existingId` (dieselbe PDF-Datei – praktisch
  nie, weil PDFsharp Erzeugungszeit und Dokument-Id einbettet).
- PDF über `IPdfComposer.ComposeImagesAsync` in eine Datei unter `<DataPath>/tmp` (`FileOptions.DeleteOnClose`,
  also immer gelöscht), dann `ReceiveDocument` mit Quelle `Camera`.
- **Name:** „Kamera JJJJ-MM-TT HH-mm.pdf“ aus `takenAt` (ISO 8601 **mit** Versatz, Ortszeit des Handys bei
  der ersten Seite). Ohne Versatz, mit `Z`, unlesbar oder mehr als einen Tag neben der Serverzeit → Ortszeit
  des Servers.
- Der Server nennt den Daten-Pfad-Schlüssel `NoMorePaper:DataPath` selbst, weil er Infrastructure-Typen nur in
  der Composition Root nennen darf; die Route baut `PageUpload` je Anfrage mit `ActivatorUtilities`.

**Fehler auf der Seite** (als Text mit Symbol, Live-Region `alert`, Link „Stattdessen eine Datei hochladen“):
`denied` (Berechtigung), `no-camera`, `busy` (andere App), `unsupported` (kein HTTPS bzw. keine Kamera-API;
auch `NotSupportedError` – so antwortet headless Chromium ohne Fake-Kamera),
`opencv-failed`, `failed`, `ended` (Kamera beendet; Seiten bleiben, „Kamera starten“ erneut möglich).

**Tests:** `tests/NoMorePaper.UI.Tests/Camera/*` mit eigenem Chromium (`--use-fake-device-for-media-stream`,
`--use-file-for-fake-video-capture`). Das Video (`SheetVideo`, Y4M, 1280 × 720, ein Bild: helles, leicht
schräges Blatt rechts der Mitte auf dunklem Grund) erzeugt der Test bei jedem Lauf – keine Binärdatei im
Repo. Server: `tests/NoMorePaper.Server.Tests/Documents/PageUploadTests.cs`; Ansagen:
`tests/NoMorePaper.Web.Client.Tests` (neues Testprojekt, nur Web.Client-Referenz).

### Prüfliste iPhone mit VoiceOver (Nutzer)
Voraussetzung: NoMorePaper über die `https`-Adresse von `tailscale serve` in Safari (oder als installierte
PWA) geöffnet, angemeldet.
1. „Kamera“ in der Navigation wählen. VoiceOver liest Überschrift „Mit der Kamera aufnehmen“ und die Erklärung.
2. Schalter „Töne“ und „Privat“ sind als Schalter mit Namen und Hinweis zu hören; „Privat“ entspricht der
   Konto-Einstellung.
3. „Kamera starten“ doppeltippen. Safari fragt nach der Kamera → erlauben. Ansage „Kamera und
   Kantenerkennung werden geladen …“, danach „Die Kamera läuft …“. Wie lange hat das Laden gedauert?
4. Fokus steht auf „Auslösen“. Handy über ein Blatt auf dunklem Tisch halten: Sind Töne zu hören, werden sie
   höher und schneller, je besser das Blatt im Bild liegt? Kommen Ansagen wie „Weiter nach links“, „Näher
   ran“, „Ruhig halten“ – nicht zu oft, nicht zu selten?
5. Still halten: Klick, Ansage „Seite 1 aufgenommen …“. Wird dieselbe Seite **nicht** ein zweites Mal
   aufgenommen?
6. Zweites Blatt hinlegen → „Seite 2 aufgenommen“. „Letzte Seite verwerfen“ ausprobieren, dann neu aufnehmen.
7. „Fertig“ doppeltippen → Ansage „Das Dokument wurde … gespeichert … Seiten: 2.“ und Link zur Liste. Im
   Viewer: zwei Seiten, gerade, nur das Blatt (kein Tisch), lesbar?
8. Bildschirm sperren, während die Kamera läuft, entsperren: Meldung „Die Kamera wurde beendet.“, die Seiten
   sind noch da, „Kamera starten“ geht wieder.
9. Kamera in den iOS-Einstellungen für Safari verbieten → „Kamera starten“: Meldung „Die Kamera ist nicht
   erlaubt …“ mit Link „Stattdessen eine Datei hochladen“.
10. Rückmeldung: Wann wurde unnötig gewartet, wann zu früh ausgelöst, welche Ansage war unklar?

## Offen (Spuren)
- A Kamera, B Scanner-Ordner, C Mail, D iCloud (nach Test mit echter Apple-ID), E Seite „Eingänge“ und
  Einstellungen: siehe [Detailplan](../plan/features/m4-eingaenge-detail.md).
