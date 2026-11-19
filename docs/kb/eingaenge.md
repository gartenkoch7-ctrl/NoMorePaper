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

## Seite „Eingänge“, Einstellungen, Besitzer prüfen (Spur E)
- `GET /api/inputs` liefert `InputStatusDto[]` aus `ListInputStatus` (Sichtbarkeit siehe oben); Mitglieder
  sehen nie fremde Kontonamen oder iCloud-Zeilen.
- Seite `/eingaenge` (Navigation „Eingänge“): je Eingang ein Abschnitt (h2) mit Zustand als Satz und Symbol
  (`Läuft ohne Probleme`, `Noch nicht gelaufen`, `Problem: …` mit „Das braucht deine Aufmerksamkeit“), letzter
  Abruf und letzter Erfolg relativ und absolut. Die Seite fragt jede Minute neu (`PeriodicTimer`); eine
  Änderung von Zustand oder Problem sagt die Live-Region an, „Jetzt aktualisieren“ antwortet immer. Der
  Knopf bleibt aktiv (ein deaktivierter Knopf verliert den Tastaturfokus). Ein Ladefehler ersetzt die Zeilen
  durch eine Fehlermeldung mit „Erneut versuchen“. Probleme unbekannter Art (neuerer Server) zeigen
  „Unbekanntes Problem“.
- Links: Mail-Einstellungen (`eingaenge/mail`, nur Verwalter), iCloud (`eingaenge/icloud`, „Code erneuern“
  nur bei der eigenen Zeile mit `code-needed`; die Zeile trägt nur den Anzeigenamen, verglichen wird damit).
  Beide Seiten bauen die Spuren C und D.
- Konto-Seite (`Accounts/PreferencesSection`): „Neue Dokumente privat ablegen“ und die Absender-Liste
  speichern jede Änderung sofort (`PUT /api/account/preferences` mit dem ganzen Stand) und sagen sie an;
  eine abgelehnte Adresse bleibt im Feld und bekommt den Fokus. Upload-Seite: Privat-Schalter aus der
  Einstellung; ist sie nicht ladbar, steht das als Text da und der Schalter bleibt aus.
- Besitzer prüfen (`Classification/OwnerCheckSection`) im Viewer und in „Zu prüfen“: „Passt so: <Titel>“
  und „Weitergeben an“ (Auswahl der anderen Konten aus `GET /api/household`) mit Knopf „Weitergeben: <Titel>“.
  Ohne Wahl: Fehlertext und Fokus auf die Auswahl. Nach der Weitergabe sagt der Viewer, wohin das Dokument
  ging (es ist nicht mehr sichtbar); die Liste lädt neu und nennt, was übrig ist.

## Offen (Spuren)
- A Kamera, B Scanner-Ordner, C Mail, D iCloud (nach Test mit echter Apple-ID): siehe
  [Detailplan](../plan/features/m4-eingaenge-detail.md).
