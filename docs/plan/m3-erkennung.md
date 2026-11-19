# M3 – Erkennung & Ordnung

**Ziel:** Jedes Dokument wird nach der OCR von einem lokalen LLM eingeordnet: Absender, Dokumentart,
Datum, Betrag, Tags, Titel und eine kurze Zusammenfassung. Ordnung über gepflegte Listen, Filter und
eine Liste „Zu prüfen“. Doppelte Dokumente werden erkannt.
**Demo:** Rechnung hochladen → nach der OCR erscheinen „Stadtwerke München · Rechnung · 01.11.2026 ·
84,20 € · Strom“, Titel und Zusammenfassung → Eintrag in „Zu prüfen“ → per Tastatur bestätigen oder
korrigieren → Dokumentliste nach Absender und Zeitraum filtern → derselbe Brief nochmal hochgeladen:
abgelehnt mit Link; als Scan statt PDF: „mögliches Duplikat“.
**Voraussetzungen:** M2. **Größe:** L.

Entschieden (Grilling 2026-11-19):
- LLM über **Ollama auf CPU, ca. 8 GB RAM** → 7–8B-Modell; welches, entscheidet ein **Spike**
- Felder: **Absender, Dokumentart, Dokumentdatum, Betrag (+ Währung), Tags, Titel-Vorschlag,
  Zusammenfassung (1–2 Sätze)**
- **Gepflegte Listen** für Absender und Dokumentarten; das LLM wählt einen vorhandenen Eintrag oder
  schlägt einen neuen vor; Tags frei, vorhandene bevorzugt
- **Automatisch übernehmen**, markiert als „automatisch erkannt“; Liste **„Zu prüfen“** mit
  Bestätigen/Korrigieren per Tastatur
- **Duplikate:** gleiche Datei (SHA-256) wird abgelehnt mit Link zum vorhandenen Dokument;
  inhaltlich gleiche → „mögliches Duplikat“ in „Zu prüfen“
- **Filter:** Absender, Dokumentart, Zeitraum, Tags, Betrag, privat/geteilt
- **Lernen über Beispiele:** bestätigte Dokumente desselben Absenders gehen als Beispiele in die Anfrage
- **Bestand** wird einmalig nachträglich erkannt (niedrige Priorität, neue Uploads zuerst)
- **Listeneinträge aus privaten Dokumenten bleiben privat:** ein Eintrag, der nur in privaten
  Dokumenten vorkommt, sieht nur deren Besitzer; sobald er in einem geteilten Dokument steht, sehen
  ihn alle
- **Bearbeitungsrecht je Dokument:** der Besitzer darf die Angaben ändern; er kann **einzelne
  Mitglieder** als Bearbeiter auswählen
- **Listen umbenennen/zusammenführen** darf nur, wer **alle betroffenen Dokumente** bearbeiten darf

Daraus abgeleitet (Datenschutz, ADR-0002/M2):
- Duplikatprüfung und Lernbeispiele nutzen **nur Dokumente, die der jeweilige Besitzer sehen darf** –
  sonst würde „schon vorhanden“ oder ein Beispiel ein fremdes privates Dokument verraten
- „Zu prüfen“ zeigt nur Dokumente, die man bearbeiten darf

## Aufgaben

### 0. Modell und Schema (Schritt 0)
- [x] Domain: `Correspondent`, `DocumentType`, `Tag` (Id, Name, normalisierter Name); `Document` erhält
      `CorrespondentId?`, `DocumentTypeId?`, `DocumentDate?`, `Amount?` (Dezimal + ISO-Währung),
      `Summary?`, Tags, `Recognition` (`None`/`Automatic`/`Confirmed`), `PossibleDuplicateOf?`,
      Bearbeiter (`EditorIds`) mit `CanEdit(Guid)` (Besitzer oder Bearbeiter)
- [x] Schema: Tabellen `correspondents`, `document_types`, `tags`, `document_tags`, `document_editors`,
      Spalten an `documents`, Index auf `document_files.sha256` (Original)
- [x] Sichtbarkeit von Listeneinträgen als SQL-Regel: sichtbar, wenn in einem für den Betrachter
      sichtbaren Dokument verwendet **oder** unbenutzt und vom Betrachter angelegt
- [x] Ports: `IRecognizer` (Text + Kontext → Vorschlag), `IClassificationStore` (Listen, Zuordnung)
- [x] Contracts: DTOs für Felder, Listen, Filter, „Zu prüfen“

### 1. LLM-Spike (2 Tage, zuerst)
- [x] Zwei bis drei 7–8B-Modelle aus der Ollama-Bibliothek mit **offener Lizenz** (Apache-2.0, MIT o. ä.;
      Lizenz je Modell prüfen) auf 20 **selbst erzeugten** deutschen Testdokumenten (Rechnung, Vertrag,
      Bescheid, Werbung, Arztbrief, englische Rechnung …) mit bekannten Sollwerten
- [x] Strukturierte Ausgabe über Ollamas JSON-Schema (`format`), Temperatur 0
- [x] Messen: Trefferquote je Feld, Dauer je Dokument auf CPU (Docker, RAM-Grenze 8 GB), Speicher
- [x] Ergebnis: Modell, Prompt, Kontextlänge (gekürzter Text), Grenzen → ADR-0022 + `docs/kb/erkennung.md`

### 2. Erkennung im Worker
- [x] Nach der OCR: Schritt „Erkennung“ in derselben Warteschlange (eigene Stufe, eigene Wiederholungen;
      ein Fehler der Erkennung lässt das Dokument „Done“ mit `Recognition = None`)
- [x] Ollama-Client (Infrastructure), Zeitlimit, Modellname aus Konfiguration
- [x] Anfrage: gekürzter OCR-Text, sichtbare Listen des **Besitzers**, bis zu 3 bestätigte Beispiele
      desselben Absenders aus für den Besitzer sichtbaren Dokumenten
- [x] Zuordnung: Absender/Art an vorhandene Einträge anlehnen (normalisierter Name, Ähnlichkeit),
      sonst neuer Eintrag; Titel ersetzt den Dateinamen-Titel; Werte als „automatisch“ markieren
- [x] Nachträgliche Erkennung des Bestands mit niedriger Priorität
- [x] Inhaltliche Duplikate: nach der OCR Ähnlichkeit (Trigramme) gegen für den Besitzer sichtbare
      Dokumente; über Schwelle → `PossibleDuplicateOf`
- [x] `docker-compose.yml`: Dienst `ollama` (nur im Compose-Netz, Volume für Modelle, RAM-Grenze 8 GB),
      Modell wird beim ersten Start geladen

### 3. API und Anwendungsfälle
- [x] Felder bearbeiten (nur mit Bearbeitungsrecht), Bestätigen (`Confirmed`), Bearbeiter setzen
      (nur Besitzer), Duplikat-Hinweis auflösen (behalten / Dokument löschen)
- [x] Listen: anzeigen (Sichtbarkeitsregel), anlegen, umbenennen, zusammenführen (Rechte-Regel)
- [x] Filter in Liste und Suche: Absender, Art, Zeitraum, Tags (alle/irgendeiner), Betrag von–bis,
      privat/geteilt; „Zu prüfen“ (automatisch erkannt oder mögliches Duplikat, nur bearbeitbare)
- [x] Upload: gleiche Datei (SHA-256 des Originals) unter den für den Hochladenden sichtbaren
      Dokumenten → 409 mit Link; sonst normal

### 4. Oberfläche
- [x] Viewer: Bereich „Angaben“ mit Zusammenfassung zuerst (für Screenreader), Feldern, Bearbeiten
      (Kombinationsfeld mit „neu anlegen“ für Absender/Art, Tags, Datum, Betrag), „Bestätigen“,
      Bearbeiter auswählen, Kennzeichnung „automatisch erkannt“ als Text
- [x] Seite „Zu prüfen“: Liste mit Zusammenfassung, Bestätigen/Korrigieren ohne Viewer möglich,
      mögliche Duplikate mit Vergleich (Titel, Datum, Absender) und „Behalten“/„Löschen“
- [x] Dokumentliste: Filterleiste (Tastatur, Ergebnisanzahl per Live-Region), Spalten Absender, Art,
      Datum, Betrag
- [x] Seite „Listen“: Absender, Arten, Tags umbenennen und zusammenführen (mit Bestätigung)
- [x] Upload: Hinweis „schon vorhanden“ mit Link

### 5. Tests und Doku
- [x] Tests mit Fake-`IRecognizer` (kein Ollama in Unit/Integration); ein Integrationstest mit echtem
      Ollama nur in CI-optionalem Lauf (übersprungen, wenn nicht erreichbar)
- [x] Datenschutz-Tests: privater Absender nicht in fremder Liste, keine fremden privaten Beispiele,
      keine Duplikat-Meldung über fremde private Dokumente, Zusammenführen ohne Recht → 403
- [x] UI: axe DE/EN auf allen neuen Seiten/Bereichen, Tastaturtests für Bearbeiten, Bestätigen, Filter
- [x] KB `erkennung.md`, ADR-0022 (Modell), ADR-0023 (Listen, Sichtbarkeit, Bearbeitungsrecht), Katalog

## Akzeptanzkriterien
- [x] Testsatz: Absender und Art in ≥ 85 % richtig, Datum und Betrag in ≥ 90 % – erreicht an 22 Testtexten (95 / 86 / 95 / 100 %, ADR-0022); echte Scans noch offen
- [ ] Erkennung ≤ 60 s je Dokument auf dem Server (CPU) ⚠️ messen
- [x] Keine Information aus fremden privaten Dokumenten in Listen, Beispielen oder Duplikat-Meldungen
- [ ] axe 0 Verstöße; NVDA-Prüfung „Angaben“ und „Zu prüfen“ durch den Nutzer
- [x] CI grün (Run 18, c0dfcf1)

## Parallelisierung

Integrationsbranch `feat/m03-erkennung` von `develop`.

```
Spur S (LLM-Spike) ──────────────────────────────┐
Schritt 0 ─┬─► Spur A (2 Worker) ──────────────────┼─► Merge ─► Spur D (5 Doku) ─► Review
           ├─► Spur B (3 API) ─┐                  │
           │                   └─► Spur C (4 UI) ─┘
```

| Spur | Inhalt | Branch | wartet auf | EventIds |
|---|---|---|---|---|
| S | LLM-Spike | `feat/m03-s-llm-spike` | – | – |
| 0 | Modell, Schema, Ports, DTOs | `feat/m03-erkennung` | – | Infrastructure 3500–3519 |
| A | Erkennung im Worker, Ollama, Bestand, inhaltliche Duplikate | `feat/m03-a-worker` | 0 (Modellname aus S, vorher Konfiguration) | Worker 2200–2299, Infrastructure 3520–3599, Application 5200–5249 |
| B | Anwendungsfälle, API, Filter, exakte Duplikate | `feat/m03-b-api` | 0 | Server 1500–1599, Application 5250–5299 |
| C | Oberfläche | `feat/m03-c-ui` | 0 (gegen den Vertrag von B, mit Mocks) | Web.Client 4500–4599 |
| D | KB, ADRs, Katalog | `feat/m03-erkennung` | alle | – |

- **Exklusiv-Dateien:** S → `spikes/llm/`; A → `Worker/Recognition/`, `Infrastructure/Recognition/`,
  `Application/Recognition/`; B → `Application/Classification/`, `Server/Classification/`,
  `Infrastructure/Classification/`; C → `Web.Client/Classification/`, neue Seiten
- **Hotspots:** `docker-compose.yml`, Kompositionen, `Contracts/`, `Strings*.resx`, `MainLayout.razor`,
  `ServerFixture.cs`, `docs/kb/telemetrie.md`
- **Merge-Reihenfolge:** A → B → C

## Detailplan

**Erstellt von:** Opus (Stufe 3) · **Datum:** 2026-11-19 · **Offene Designfragen:** keine
(Modell und Prompt sind ausdrücklich Spike-Ergebnis)

### Einschätzung

| Aufgabenpunkt | Umfang | Risiko | Neuheit | Klarheit | Prüfbarkeit | Stufe |
|---|---|---|---|---|---|---|
| 0 Modell, Schema | ~25 Dateien | hoch (Sichtbarkeit, Migration) | neu | entschieden | Tests | 3 |
| 1 LLM-Spike | Prototyp | offen | neu | offen | Messung | 4 |
| 2 Worker-Erkennung | ~20 Dateien | hoch (externer Dienst, Datenschutz der Beispiele) | neu | entschieden | Fake + Integration | 3 |
| 3 API, Filter | ~25 Dateien | hoch (Rechte) | Muster M2 | entschieden | Server.Tests | 3 |
| 4 Oberfläche | ~20 Dateien | mittel (Barrierefreiheit, Kombinationsfelder) | Muster M1/M2 | entschieden | axe + Tastatur | 2 |
| 5 Doku | ~6 Dateien | keins | Muster | entschieden | Lesen | 1 |

### Pakete

| Paket | Inhalt | Modell | Effort |
|---|---|---|---|
| P0 | Schritt 0 | **Opus** | `high` |
| P1 | LLM-Spike | **Fable** | `high` |
| P2 | Spur A Worker | **Opus** | `high` |
| P3 | Spur B API | **Opus** | `high` |
| P4 | Spur C UI | **Sonnet** | `medium` |
| P5 | Doku | **Haiku** | `low` |

Grundlage für P2–P4: Typen und Ports aus P0 (`4eb2172`): `Document.Classify/Edit/Confirm/SetEditors/
CanEdit/MarkPossibleDuplicate`, `DocumentFields`, `Money`, `ClassificationKind`, `ClassificationName`,
`IClassificationStore`, `IRecognizer`/`RecognitionRequest`/`RecognitionResult`, `VisibilitySql.Entries`,
DTOs in `Contracts/Classification`.

**API-Vertrag B ↔ C** (B baut, C nutzt; Fehler als `ProblemDetails`; alle Routen brauchen Anmeldung):

| Route | Antwort |
|---|---|
| `GET /api/documents?…&correspondentId&documentTypeId&from&to&tagIds=a,b&allTags&amountFrom&amountTo&isPrivate` | wie bisher; `DocumentSummaryDto` erhält optional `Correspondent`, `DocumentType` (`EntryDto?`), `DocumentDate`, `Amount` (`MoneyDto?`), `Recognition` |
| `GET /api/search?q=&…` (gleiche Filter) | `SearchHitDto` erhält dieselben optionalen Felder |
| `GET /api/documents/{id}/fields` | `DocumentFieldsDto`; 404 wenn nicht sichtbar; `PossibleDuplicateOf` nur, wenn der Betrachter das andere Dokument sehen darf |
| `PUT /api/documents/{id}/fields` `EditFieldsRequestDto` (mit gesehenem `Recognition`) | 204; 403 ohne Bearbeitungsrecht; 400 ungültig; 409 inzwischen erkannt/bestätigt |
| `POST /api/documents/{id}/confirm` `ConfirmRequestDto(Recognition)` | 204; 403; 409 inzwischen erkannt/bestätigt |
| `PUT /api/documents/{id}/editors` `SetEditorsRequestDto` | 204; 403 nicht Besitzer; 400 unbekanntes Mitglied |
| `POST /api/documents/{id}/duplicate/keep` | 204 (Hinweis entfernen); 403 |
| `DELETE /api/documents/{id}` | 204; nur Besitzer und nur, solange „mögliches Duplikat“ gesetzt ist (Papierkorb kommt in M4); 403/409 sonst |
| `GET /api/review` | `ReviewItemDto[]` (nur bearbeitbare, automatisch erkannt oder mögliches Duplikat, neueste zuerst) |
| `GET /api/review/count` | Anzahl wie `GET /api/review` (für die Navigation) |
| `GET /api/classification/{correspondents\|document-types\|tags}` | `EntryDto[]` (Sichtbarkeitsregel) |
| `POST /api/classification/{kind}` `{ name }` | 200 `EntryDto` (vorhandener bei gleichem normalisiertem Namen, sonst neu) |
| `PUT /api/classification/{kind}/{id}` `RenameEntryDto` | 204; 403 ohne Recht an allen betroffenen Dokumenten; 404 |
| `POST /api/classification/{kind}/merge` `MergeEntriesDto` | 204; 403; 404 |
| `GET /api/household` | `[{ id, displayName }]` aller Mitglieder (für die Bearbeiter-Auswahl; für alle Angemeldeten) |
| `POST /api/documents` (Upload) | zusätzlich 409 mit `existingId` (Erweiterung im `ProblemDetails`), wenn dieselbe Datei unter den für den Hochladenden sichtbaren Dokumenten liegt |

### Paket P2 – Erkennung im Worker (Spur A) · Modell: **Opus** · Effort: `high`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `processing_jobs` + Migration | ändern | Spalten `stage` (`Ocr`/`Recognition`, Werk `Ocr`) und `priority` (int, Werk 0; Bestand 10); Claim sortiert nach `priority`, dann `next_attempt_at` | Infrastructure.Tests | 40 |
| `Application/Processing/ProcessNextDocument.cs` (+ `ProcessingAttempt`) | ändern | nach erfolgreicher OCR den Job **nicht löschen**, sondern auf `stage = Recognition`, `attempt = 0`, `next_attempt_at = now` setzen (gleiche Transaktion wie `Complete`); Stufe `Recognition` ruft `RecognizeDocument` | Worker.Tests | 60 |
| `Application/Recognition/RecognizeDocument.cs` | neu | Dokument laden; `Recognition == Confirmed` → nur Job beenden; Anfrage bauen: Text gekürzt nach `erkennung.md` (Spike), sichtbare Listen des **Besitzers**, Beispiele: bis zu 3 `Confirmed`-Dokumente des Absenders, dessen normalisierter Name im Text vorkommt, nur aus für den Besitzer sichtbaren Dokumenten; Ergebnis über `IClassificationStore.FindOrCreateAsync(owner, …)` zuordnen (Absender/Art/Tags), Titel übernehmen, `Document.Classify`; Fehler: Wiederholung wie OCR (1/5/30 min), nach 4 Versuchen Job löschen, Dokument bleibt `Done` mit `Recognition = None` | Application.Tests | 180 |
| `Application/Recognition/FindContentDuplicate.cs` + Port `IDuplicateFinder` | neu | nach der OCR: ähnlichstes **für den Besitzer sichtbares** anderes Dokument mit Trigramm-Ähnlichkeit ≥ 0,9 (Umsetzung in Infrastructure mit `SET LOCAL pg_trgm.similarity_threshold`, `%`-Operator, Index) → `MarkPossibleDuplicate` | Application/Infrastructure.Tests | 100 |
| `Application/Recognition/EnqueueBacklog.cs` | neu | beim Worker-Start einmalig: Jobs `stage = Recognition`, `priority = 10` für alle `Done`-Dokumente mit `Recognition = None` ohne Job | Worker.Tests | 60 |
| `Infrastructure/Recognition/OllamaRecognizer.cs`, `OllamaSchema.cs`, `RecognitionPrompt.cs` | neu | setzt `IRecognizer` um: `/api/chat`, JSON-Schema, Temperatur 0, Prompt und Schema aus dem Spike (`spikes/llm`), Zeitlimit und Modell aus Konfiguration `NoMorePaper:Recognition:{Endpoint,Model,Timeout}` | Infrastructure.Tests mit Fake-HTTP | 250 |
| `Infrastructure/Classification/ClassificationStore.cs` | neu | **nur** `VisibleEntriesAsync` und `FindOrCreateAsync` (Abgleich über `NormalizedName` unter den für den Akteur sichtbaren Einträgen, sonst neu anlegen); `Rename`/`Merge` baut Spur B in derselben Klasse → Merge-Hotspot, A legt die Datei an | Infrastructure.Tests | 120 |
| `docker-compose.yml`, `.env.example` | ändern | Dienst `ollama` (gepinnte Version aus dem Spike, kein veröffentlichter Port, `mem_limit: 8g`, Volume `ollama`), Worker-Umgebung `NoMorePaper__Recognition__Endpoint=http://ollama:11434`; Modell wird beim Worker-Start per `/api/pull` geholt, falls es fehlt (Ereignis loggen) | Compose-Lauf | 40 |
| Telemetrie | neu | Worker 2200–2299, Infrastructure 3520–3599, Application 5200–5249 (nie Dokumenttext, Namen oder Zusammenfassung loggen) | – | 60 |
- **Akzeptanz:** Tests grün mit Fake-Recognizer; Compose: Upload → OCR → Erkennung setzt Felder; Bestand wird nachgeholt; mögliches Duplikat wird markiert

### Paket P3 – API, Filter, exakte Duplikate (Spur B) · Modell: **Opus** · Effort: `high`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Application/Classification/*` | neu | Anwendungsfälle: `GetDocumentFields`, `EditDocumentFields` (Rechteprüfung `CanEdit`, Einträge per Id müssen für den Akteur sichtbar sein), `ConfirmDocument`, `SetDocumentEditors` (nur Mitglieder des Haushalts), `KeepDuplicate`, `DeleteDuplicate`, `ListReview`, `ListEntries`, `CreateEntry`, `RenameEntry`, `MergeEntries` | Application.Tests | je < 120 |
| `Infrastructure/Classification/ClassificationStore.cs` | ergänzen | `RenameAsync`, `MergeAsync`: erlaubt nur, wenn der Akteur **jedes** Dokument mit diesem Eintrag bearbeiten darf (eine SQL-Prüfung); Zusammenführen hängt Dokumente um und löscht die Quellen in einer Transaktion | Infrastructure.Tests | 120 |
| `IDocumentRepository`/`ISearchIndex` + Umsetzungen | ändern | Filter laut Vertrag (parametrisiert), Felder in Liste/Suche, exakter Duplikat-Check `FindVisibleBySha256(viewer, sha)` | Infrastructure.Tests | 150 |
| `Application/Documents/ReceiveDocument.cs` | ändern | vor dem Speichern Duplikat-Check (Hash beim Lesen des Streams berechnen; bei Treffer Eingang verwerfen) → Ergebnis „Duplikat von X“ | Application.Tests | 40 |
| `Server/Classification/*`, `Server/Documents/*` | neu/ändern | Routen laut Vertrag, 409 mit `existingId` | Server.Tests | ~300 |
| `Contracts` | ändern | Filter-/Feld-Erweiterungen laut Vertrag | – | 40 |
| Tests | neu | Rechte (Bearbeiter, nicht Besitzer, privat), Zusammenführen ohne Recht → 403, Sichtbarkeit in Listen/Review/Duplikaten, Filter-Kombinationen, 409 nur bei sichtbarem Original | – | ~600 |
- **Telemetrie:** Server 1500–1599, Application 5250–5299
- **Akzeptanz:** alle Tests grün; Datenschutz-Tests aus Aufgabe 5 grün

### Paket P4 – Oberfläche (Spur C) · Modell: **Sonnet** · Effort: `medium`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Web.Client/Classification/ClassificationApi.cs` | neu | Client für alle neuen Routen | UI.Tests | 150 |
| `Classification/EntryCombobox.razor` | neu | barrierefreies Kombinationsfeld (ARIA 1.2 Combobox mit Listbox, Pfeiltasten, Enter, Esc, „„X“ neu anlegen“ als letzte Option, Treffer-Anzahl per Live-Region) für Absender und Art | UI.Tests | 200 |
| `Classification/TagPicker.razor` | neu | Tags als Liste von Schaltern mit Entfernen, Hinzufügen über `EntryCombobox` | UI.Tests | 120 |
| `Viewer/DocumentFieldsPanel.razor` | neu | Bereich „Angaben“ im Viewer: Zusammenfassung als erstes, Felder als Beschreibungsliste, „automatisch erkannt“ als Text, „Bearbeiten“-Formular, „Bestätigen“, Bearbeiter-Auswahl (Checkboxen der Haushaltsmitglieder, nur Besitzer) | UI.Tests | 250 |
| `Pages/Review.razor` (`/zu-pruefen`) | neu | Liste mit Zusammenfassung und Feldern, „Bestätigen“/„Bearbeiten“ je Eintrag, mögliche Duplikate mit Vergleich und „Behalten“/„Löschen“ (Bestätigungsdialog wie Mitglieder-Seite) | UI.Tests | 220 |
| `Pages/Documents.razor` + `Classification/DocumentFilterBar.razor` | ändern/neu | Filterleiste (Absender, Art, Zeitraum, Tags, Betrag, privat/geteilt), Ergebnisanzahl per Live-Region, Spalten Absender/Art/Datum/Betrag | UI.Tests | 200 |
| `Pages/Lists.razor` (`/listen`) | neu | Absender, Arten, Tags: umbenennen und zusammenführen (Auswahl + Ziel + Bestätigung); 403 verständlich erklären | UI.Tests | 200 |
| `Pages/Upload.razor` | ändern | 409 → „Diese Datei ist schon vorhanden“ mit Link | UI.Tests | 20 |
| `MainLayout.razor`, `Strings*.resx` | ändern | Navigation „Zu prüfen“ (mit Anzahl), „Listen“; Texte DE/EN | – | 150 |
| `tests/NoMorePaper.UI.Tests/Classification/*` | neu | axe DE/EN auf allen neuen Seiten/Bereichen; Tastatur: Combobox, Bearbeiten/Bestätigen, Filter, Zusammenführen mit Bestätigung; Mocks nach Muster `ApiMocks` | selbst | ~600 |
- **Telemetrie:** Web.Client 4500–4599
- **Akzeptanz:** alle UI-Tests grün (gegen Mocks; echte API nach dem Merge)

### Paket P0 – Modell und Schema · Modell: **Opus** · Effort: `high`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/NoMorePaper.Domain/Classification/Correspondent.cs`, `DocumentType.cs`, `Tag.cs`, `ClassificationName.cs` | neu | Einträge mit `Id` (Guid v7), `Name` (≤ 200), `NormalizedName` (klein, ohne Akzente/Satzzeichen/Rechtsform-Zusätze wie „GmbH“, „AG“ – Regel in `ClassificationName.Normalize`), `CreatedBy` | Domain.Tests | 150 |
| `src/NoMorePaper.Domain/Documents/Document.cs` (+ ggf. Aufteilen in `DocumentClassification.cs`) | ändern | Felder aus Aufgabe 0; `Classify(...)` (setzt `Automatic`), `Confirm(Guid actor)`, `Edit(Guid actor, …)` (setzt `Confirmed`), `SetEditors(Guid actor, IEnumerable<Guid>)` (nur Besitzer), `CanEdit(Guid)`, `MarkPossibleDuplicate(Guid)`, `ClearPossibleDuplicate(Guid actor)`; Betrag als `Money(decimal, string currency)` | Domain.Tests | 150 |
| `src/NoMorePaper.Domain/Documents/Money.cs`, `RecognitionState.cs` | neu | Wertobjekt/Enum | Domain.Tests | 50 |
| `src/NoMorePaper.Application/Classification/IClassificationStore.cs` | neu | `VisibleEntriesAsync(Guid viewer, kind)`, `FindOrCreateAsync(Guid actor, kind, string name)`, `RenameAsync`, `MergeAsync` (Signaturen, Umsetzung in Spur B) | – | 40 |
| `src/NoMorePaper.Application/Recognition/IRecognizer.cs`, `RecognitionRequest.cs`, `RecognitionResult.cs` | neu | Anfrage: Text, Listen-Namen, Beispiele; Ergebnis: Absender-Name, Art-Name, Datum, Betrag+Währung, Tags, Titel, Zusammenfassung (alle optional) | – | 60 |
| `src/NoMorePaper.Infrastructure/Classification/*Configuration.cs`, Migration `Classification` | neu | Tabellen aus Aufgabe 0 (snake_case), FK `document_tags`/`document_editors` mit Cascade, Index `document_files(sha256) WHERE kind='Original'`, Index `(normalized_name)` je Liste | MigrationTests | 200 |
| `src/NoMorePaper.Infrastructure/Classification/VisibilitySql.cs` | neu | eine Stelle für die Sichtbarkeitsregel der Einträge (parametrisiertes SQL-Fragment) | Infrastructure.Tests | 60 |
| `src/NoMorePaper.Contracts/Classification/*` | neu | `EntryDto(Id, Name)`, `DocumentFieldsDto(...)`, `DocumentFilterDto(...)`, `ReviewItemDto(...)`, `EditFieldsRequestDto`, `SetEditorsRequestDto`, `RenameEntryDto`, `MergeEntriesDto` | – | 100 |
| Tests | neu | Domain: Rechte (`CanEdit`, nur Besitzer setzt Bearbeiter), Normalisierung; Infrastructure: Sichtbarkeitsregel (privater Eintrag unsichtbar für andere, sichtbar nach Nutzung in geteiltem Dokument), Migration | – | 300 |
- **Akzeptanz:** Build ohne Warnungen, alle Tests grün, Dateien ≤ 300 Zeilen

### Paket P1 – LLM-Spike · Modell: **Fable** · Effort: `high`
| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `spikes/llm/samples/*.txt` + `expected.json` | neu | 20 selbst erzeugte Dokumenttexte (wie nach OCR, mit typischen OCR-Fehlern) und Sollwerte | – | – |
| `spikes/llm/` (.NET-Konsole, nicht in der Solution) | neu | ruft Ollama (`docker run` mit `--memory 8g`) für jedes Modell × Dokument mit JSON-Schema, misst Dauer und Trefferquote je Feld, schreibt `results.md` | manuell | je < 300 |
| `docs/kb/erkennung.md` | neu | Ergebnis, Prompt, Kürzungsregel für lange Texte, Empfehlung | – | 120 |
| `docs/adr/0022-llm-modell.md` | neu | gewähltes Modell mit Lizenz, Messwerten, Alternativen | – | 50 |
- **Akzeptanz:** Messtabelle für mindestens zwei Modelle; Empfehlung mit Begründung; keine echten Dokumente

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|
| P0 Modell und Schema | Opus | ja | Schema und Sichtbarkeitsregel trugen alle Spuren; keine Nacharbeit am Modell |
| P1 LLM-Spike | Fable | eher zu hoch | offener Ausgang rechtfertigte die Stufe, die Arbeit selbst (Messläufe, Auswertung) hätte Opus geschafft |
| P2 Erkennung im Worker | Opus | ja | Nebenläufigkeit (Zeilensperren, Rückgabe bei geteiltem Dokument) brauchte die Stufe; Review fand trotzdem zwei Lücken |
| P3 API, Filter, Duplikate | Opus | ja | Datenschutzregeln der Listen waren der schwierigste Teil; Review-Befunde zu privaten Einträgen |
| P4 Oberfläche | Sonnet | passend | nach Muster umgesetzt; 409-Behandlung und Fokus nach dem Laden kamen erst im Review dazu |

Lehre: Datenschutz- und Sperrregeln quer über Spuren gehören in Schritt 0 als gemeinsame Bausteine (eine Stelle für „sichtbar für den Haushalt“), nicht in jede Spur einzeln.

## Offene Fragen / Risiken
- Modelllizenzen: nur Modelle, deren Lizenz die private und Open-Source-Nutzung erlaubt (im Spike prüfen)
- CPU-Dauer kann bei langen Dokumenten steigen → Text kürzen (Kopf + Fuß des Dokuments)
- Ollama-Image ist groß (> 1 GB) und das Modell ~5 GB → Speicherplatz auf dem Server prüfen
- Aus M2 offen: NVDA-Prüfung, iPhone über `tailscale serve`; aus M1: iCloud-Test
