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
- [ ] Zwei bis drei 7–8B-Modelle aus der Ollama-Bibliothek mit **offener Lizenz** (Apache-2.0, MIT o. ä.;
      Lizenz je Modell prüfen) auf 20 **selbst erzeugten** deutschen Testdokumenten (Rechnung, Vertrag,
      Bescheid, Werbung, Arztbrief, englische Rechnung …) mit bekannten Sollwerten
- [ ] Strukturierte Ausgabe über Ollamas JSON-Schema (`format`), Temperatur 0
- [ ] Messen: Trefferquote je Feld, Dauer je Dokument auf CPU (Docker, RAM-Grenze 8 GB), Speicher
- [ ] Ergebnis: Modell, Prompt, Kontextlänge (gekürzter Text), Grenzen → ADR-0022 + `docs/kb/erkennung.md`

### 2. Erkennung im Worker
- [ ] Nach der OCR: Schritt „Erkennung“ in derselben Warteschlange (eigene Stufe, eigene Wiederholungen;
      ein Fehler der Erkennung lässt das Dokument „Done“ mit `Recognition = None`)
- [ ] Ollama-Client (Infrastructure), Zeitlimit, Modellname aus Konfiguration
- [ ] Anfrage: gekürzter OCR-Text, sichtbare Listen des **Besitzers**, bis zu 3 bestätigte Beispiele
      desselben Absenders aus für den Besitzer sichtbaren Dokumenten
- [ ] Zuordnung: Absender/Art an vorhandene Einträge anlehnen (normalisierter Name, Ähnlichkeit),
      sonst neuer Eintrag; Titel ersetzt den Dateinamen-Titel; Werte als „automatisch“ markieren
- [ ] Nachträgliche Erkennung des Bestands mit niedriger Priorität
- [ ] Inhaltliche Duplikate: nach der OCR Ähnlichkeit (Trigramme) gegen für den Besitzer sichtbare
      Dokumente; über Schwelle → `PossibleDuplicateOf`
- [ ] `docker-compose.yml`: Dienst `ollama` (nur im Compose-Netz, Volume für Modelle, RAM-Grenze 8 GB),
      Modell wird beim ersten Start geladen

### 3. API und Anwendungsfälle
- [ ] Felder bearbeiten (nur mit Bearbeitungsrecht), Bestätigen (`Confirmed`), Bearbeiter setzen
      (nur Besitzer), Duplikat-Hinweis auflösen (behalten / Dokument löschen)
- [ ] Listen: anzeigen (Sichtbarkeitsregel), anlegen, umbenennen, zusammenführen (Rechte-Regel)
- [ ] Filter in Liste und Suche: Absender, Art, Zeitraum, Tags (alle/irgendeiner), Betrag von–bis,
      privat/geteilt; „Zu prüfen“ (automatisch erkannt oder mögliches Duplikat, nur bearbeitbare)
- [ ] Upload: gleiche Datei (SHA-256 des Originals) unter den für den Hochladenden sichtbaren
      Dokumenten → 409 mit Link; sonst normal

### 4. Oberfläche
- [ ] Viewer: Bereich „Angaben“ mit Zusammenfassung zuerst (für Screenreader), Feldern, Bearbeiten
      (Kombinationsfeld mit „neu anlegen“ für Absender/Art, Tags, Datum, Betrag), „Bestätigen“,
      Bearbeiter auswählen, Kennzeichnung „automatisch erkannt“ als Text
- [ ] Seite „Zu prüfen“: Liste mit Zusammenfassung, Bestätigen/Korrigieren ohne Viewer möglich,
      mögliche Duplikate mit Vergleich (Titel, Datum, Absender) und „Behalten“/„Löschen“
- [ ] Dokumentliste: Filterleiste (Tastatur, Ergebnisanzahl per Live-Region), Spalten Absender, Art,
      Datum, Betrag
- [ ] Seite „Listen“: Absender, Arten, Tags umbenennen und zusammenführen (mit Bestätigung)
- [ ] Upload: Hinweis „schon vorhanden“ mit Link

### 5. Tests und Doku
- [ ] Tests mit Fake-`IRecognizer` (kein Ollama in Unit/Integration); ein Integrationstest mit echtem
      Ollama nur in CI-optionalem Lauf (übersprungen, wenn nicht erreichbar)
- [ ] Datenschutz-Tests: privater Absender nicht in fremder Liste, keine fremden privaten Beispiele,
      keine Duplikat-Meldung über fremde private Dokumente, Zusammenführen ohne Recht → 403
- [ ] UI: axe DE/EN auf allen neuen Seiten/Bereichen, Tastaturtests für Bearbeiten, Bestätigen, Filter
- [ ] KB `erkennung.md`, ADR-0022 (Modell), ADR-0023 (Listen, Sichtbarkeit, Bearbeitungsrecht), Katalog

## Akzeptanzkriterien
- [ ] Testsatz: Absender und Art in ≥ 85 % richtig, Datum und Betrag in ≥ 90 % (Spike-Werte, ⚠️ werden im Spike bestätigt oder angepasst)
- [ ] Erkennung ≤ 60 s je Dokument auf dem Server (CPU) ⚠️ messen
- [ ] Keine Information aus fremden privaten Dokumenten in Listen, Beispielen oder Duplikat-Meldungen
- [ ] axe 0 Verstöße; NVDA-Prüfung „Angaben“ und „Zu prüfen“ durch den Nutzer
- [ ] CI grün

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

Dateitabellen für P2–P4 folgen nach P0 (sie hängen an dessen Typen).

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

## Offene Fragen / Risiken
- Modelllizenzen: nur Modelle, deren Lizenz die private und Open-Source-Nutzung erlaubt (im Spike prüfen)
- CPU-Dauer kann bei langen Dokumenten steigen → Text kürzen (Kopf + Fuß des Dokuments)
- Ollama-Image ist groß (> 1 GB) und das Modell ~5 GB → Speicherplatz auf dem Server prüfen
- Aus M2 offen: NVDA-Prüfung, iPhone über `tailscale serve`; aus M1: iCloud-Test
