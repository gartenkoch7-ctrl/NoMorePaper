# AGENTS.md – NoMorePaper

> **NoMorePaper** ist ein selbst gehostetes, quelloffenes Dokumentenarchiv (MIT) für den
> Haushalt: Papier, Mails und Dateien rein – per OCR durchsuchbar, von einem **lokalen** LLM
> automatisch zugeordnet (Absender, Art, Datum, Betrag, Fristen). Kein Dokument verlässt das
> eigene Netz.

Diese Datei ist die **einzige Quelle der Wahrheit** für alle Agents (Claude Code, Codex,
Cursor, …) und Menschen. `CLAUDE.md` importiert sie nur.

---

## 1. Knowledgebase – vor jeder Aufgabe lesen

Lade nur die Dateien, die deine Aufgabe betreffen. Index: [docs/kb/README.md](docs/kb/README.md)

| Thema | Datei | Lesen, wenn … |
|---|---|---|
| Produkt, Scope, MVP | [docs/kb/produkt-und-scope.md](docs/kb/produkt-und-scope.md) | du Features planst |
| Glossar | [docs/kb/glossar.md](docs/kb/glossar.md) | ein Begriff unklar ist |
| Architektur & Schichten | [docs/kb/architektur.md](docs/kb/architektur.md) | du Code anlegst/verschiebst |
| Build & Tooling | [docs/kb/build-und-tooling.md](docs/kb/build-und-tooling.md) | Build oder Tests nicht laufen |
| Testumgebung | [docs/kb/testumgebung.md](docs/kb/testumgebung.md) | du testest oder misst |
| Telemetrie (Katalog) | [docs/kb/telemetrie.md](docs/kb/telemetrie.md) | du ein Feature baust – jedes bringt Logs/Abläufe mit |
| **UI-Prozess (Stitch → Blazor)** | [docs/kb/ui-prozess.md](docs/kb/ui-prozess.md) | du eine Seite entwirfst, überarbeitest oder umsetzt |
| Architekturentscheidungen | [docs/adr/](docs/adr/) | du eine Entscheidung infrage stellst |
| **Roadmap & Meilensteinpläne** | [docs/plan/README.md](docs/plan/README.md) | du eine Aufgabe beginnst – arbeite im aktuellen Meilenstein, hake erledigte Punkte ab |
| Detailplan-Vorlage | [docs/plan/detailplan-vorlage.md](docs/plan/detailplan-vorlage.md) | du einen Plan in Pakete zerlegst und Modelle zuweist (§13) |

**Pflege:** Wer etwas Neues lernt (API-Eigenheit, Messwert, Verhaltensbeobachtung),
schreibt es in die passende KB-Datei. Neue Grundsatzentscheidungen → neues ADR
(`docs/adr/NNNN-titel.md`, Vorlage [0000-vorlage.md](docs/adr/0000-vorlage.md)). Bestehende
ADRs werden nicht umgeschrieben, sondern ersetzt (`Status: Ersetzt durch NNNN`).

---

## 2. Harte Architekturregeln (nicht verhandelbar)

### 2.1 Dateigröße
- **Max. 300 Zeilen pro Datei** für Code und Tests.
- Ausgenommen: generierte Dateien, Build-Ausgaben, Lockfiles.
- Nähert sich eine Datei 250 Zeilen: **jetzt** aufteilen, nicht später. Aufteilen nach
  Verantwortung, nicht mechanisch.
- Ein öffentlicher Typ pro Datei, Dateiname = Typname.
- Geprüft durch `scripts/check-file-length.ps1` – lokal im Pre-Commit-Hook und in CI (entsteht in M0).

### 2.2 Schichten & Abhängigkeiten (ADR-0001)
```
NoMorePaper.Server ─┐                       ┌─► NoMorePaper.Contracts ◄── NoMorePaper.Web.Client
NoMorePaper.Worker ─┴─► Infrastructure ─► Application ─► Domain
```
- **Domain** kennt niemanden: keine EF-, ASP.NET-, IO- oder HTTP-Abhängigkeiten. Reine Logik
  (Dokument, Felder, Tags, Fristen, Duplikat-Regeln).
- **Application** enthält die Anwendungsfälle und definiert Interfaces (Speicher, OCR, LLM,
  Eingangsquellen), kennt nur Domain.
- **Infrastructure** implementiert diese Interfaces (EF Core/Postgres, Dateiablage, OCR,
  Ollama, IMAP, iCloud, Ordner).
- **Server** (API, Composition Root) und **Worker** (Verarbeitung, Abholen) sind dünn.
- **Web.Client** (Blazor WASM) kennt nur **Contracts** (DTOs), nie Domain oder Infrastructure.
- Erzwungen durch `tests/NoMorePaper.Architecture.Tests`.
- Innerhalb eines Projekts: Ordner nach **Feature**, nicht nach Typart (`Services/`,
  `Helpers/` sind verboten).
- Schichtregeln werden durch Architekturtests erzwungen.

### 2.3 Telemetrie
- **Jedes Feature** bringt seine Logs/Abläufe/Messwerte mit und trägt sie in den Katalog ein.
- **Keine Inhalte des Nutzers** (Dokumentinhalte, Namen, volle Pfade) in Logs.

### 2.4 Datenschutz (ADR-0002)
- **Dokumente, OCR-Text und Metadaten verlassen nie das eigene Netz.** Keine Cloud-OCR,
  keine Cloud-LLMs, keine externen Analyse- oder Telemetriedienste.
- Einzige Ausnahme: Kalendertermine im iCloud-Kalender mit **nur Titel und Link** (ADR-0012).
- Zugangsdaten (IMAP, Apple-ID-Sitzung) nur verschlüsselt speichern (Data Protection), nie im
  Klartext in Logs, Settings-Dateien oder dem Repo.
- Der Server ist nicht öffentlich erreichbar; Zugriff von außen nur über Tailscale.

### 2.5 Barrierefreiheit (ADR-0010, ADR-0011, ADR-0026)
Der Hauptnutzer ist blind bzw. stark sehbehindert. Barrierefreiheit ist Pflicht, kein Extra.
- **Jede Oberfläche** ist dunkel und vollständig per Tastatur und Screenreader (VoiceOver, NVDA)
  bedienbar. Die Optik (Farben, Schrift, Fokus) folgt dem vom Nutzer freigegebenen
  Stitch-Design-System ([KB oberflaeche.md](docs/kb/oberflaeche.md)); kein eigener Fokusrahmen
  (die Bildschirmlupe des Nutzers zeichnet ihn), Browser-Zoom nie sperren.
- Dokumente werden **überall** intelligent invertiert dargestellt: schwarzer Hintergrund, weiße
  Schrift, Farbton erhalten, Fotos original. Download und Export liefern immer das Original.
- Der Viewer zoomt stufenlos und scharf (neu rendern, nicht Pixel strecken).
- Nie Information nur über Farbe vermitteln; jedes Bedienelement hat einen zugänglichen Namen.
- **Neue oder überarbeitete Seiten** folgen dem [UI-Prozess](docs/kb/ui-prozess.md): Entwurf im
  Stitch-Projekt „NoMorePaper“ → abholen → prüfen → Freigabe durch den Nutzer → umsetzen → abnehmen.
  Stitch-HTML ist nur Vorlage, nie Produktcode; Funktionen aus Entwürfen nur, wenn sie im Plan stehen.
  **Jede** UI-Änderung, auch eine kleine, wird zuerst im Stitch-Screen gemacht (Stitch-MCP), dann im Code.

### 2.6 Statische Klassen
- `static` nur für Code **ohne Nebenwirkungen** (reine Logik, Konstanten).
- Alles mit Dateien, Netz, Prozessen, Umgebung, Uhr oder Sprache ist ein **Dienst**
  (Interface + Implementierung, registriert in der Composition Root).

---

## 3. Tech-Stack

| Bereich | Wahl |
|---|---|
| Sprache/Runtime | C#, **.NET 10 (LTS)** |
| Backend | ASP.NET Core (API) + eigener Worker-Prozess (ADR-0006) |
| UI | Blazor WebAssembly + MudBlazor als **PWA**, Deutsch/Englisch, dunkles Kontrast-Theme |
| PDF-Darstellung | Rendern im Browser + WebGL-Shader „intelligent invertiert“ (ADR-0011) |
| Kalender | iCloud-Kalender per CalDAV, einseitig (ADR-0012) |
| Datenbank | PostgreSQL mit EF Core, Volltextsuche in Postgres |
| Ablage | Original + PDF/A im Dateisystem-Volume, Versionen + Papierkorb (ADR-0003, ADR-0013) |
| Erkennung | OCR mit PDF/A-Ausgabe + lokales LLM über Ollama (CPU) (ADR-0004) |
| Eingänge | Upload, PWA-Kamera, Scanner-Ordner (SMB), IMAP, iCloud Drive (ADR-0005) |
| Home Assistant | eigene Integration (Python, HACS) über die NoMorePaper-API (ADR-0007) |
| Betrieb | Docker Compose auf Proxmox, Zugriff nur über Tailscale (ADR-0002) |
| Tests | xUnit, Architekturtests |
| Lizenz | MIT |

Neue Pakete nur mit kurzer Begründung im PR; keine Pakete mit Copyleft-Lizenz.

---

## 4. Code-Konventionen

- Code, Bezeichner, Kommentare, Commit-Messages: **Englisch**. Doku/KB: **Deutsch**.
- Warnungen sind Fehler; Nullability/strikte Typprüfung an.
- Kommentare erklären das **Warum**, nicht das Was.
- Commits: Conventional Commits (`feat:`, `fix:`, `perf:`, `docs:`, `refactor:`, `test:`).

---

## 5. Tests

- **Kernlogik:** Unit-Tests für jede Logik – Ziel ≥ 90 %.
- **Architektur:** Schichtregeln aus 2.2 als Tests.
- Plattform-/UI-Code dünn halten, Logik in den Kern ziehen, damit sie testbar ist.
- Testdateien unterliegen ebenfalls der 300-Zeilen-Regel.

---

## 6. Befehle

```powershell
dotnet build NoMorePaper.slnx                 # Build
dotnet test --solution NoMorePaper.slnx       # alle Tests (Docker muss laufen)
powershell -NoProfile -ExecutionPolicy Bypass -File tests/NoMorePaper.UI.Tests/bin/Debug/net10.0/playwright.ps1 install chromium   # einmal nach dem ersten Build
cp .env.example .env                          # POSTGRES_PASSWORD setzen (nur Buchstaben und Ziffern)
docker compose up -d --build                  # Server, Worker, Postgres starten
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-file-length.ps1   # 300-Zeilen-Regel prüfen
git config core.hooksPath .githooks           # Pre-Commit-Hook aktivieren (einmalig)
```

---

## 7. Definition of Done

- [ ] Build ohne Warnungen, Tests grün
- [ ] Zeilenprüfung grün (keine Datei > 300 Zeilen)
- [ ] Schichtregeln eingehalten (Architekturtests grün)
- [ ] Telemetrie mitgeliefert und im Katalog
- [ ] UI geändert? → Design-System eingehalten, per Tastatur und Screenreader bedienbar (§2.5);
      Entwurf „freigegeben“ und Abnahme nach [UI-Prozess](docs/kb/ui-prozess.md) Schritt 8
- [ ] Neues Wissen in `docs/kb/`, neue Grundsatzentscheidung als ADR
- [ ] Code-Review-Schleife nach Abschnitt 8 ohne offene Befunde durchlaufen

---

## 8. Review, Commit, Push, Merge

1. **Vor jedem Commit ein Code-Review** der gesamten Änderung (z. B. `/code-review`).
2. **Führt das Review zu Änderungen**, wird danach wieder der **komplette** geänderte Code
   reviewt – nicht nur die Korrekturen.
3. Schritt 2 wiederholen, **bis ein Review keine Änderungen mehr ergibt**. Erst dann committen
   (Build, Tests und Zeilenprüfung vorher grün, siehe Abschnitt 7).
4. **Push** auf den eigenen Feature-Branch ist danach ohne Rückfrage erlaubt.
5. **Mergen** (in `develop`, `main` oder per PR) nur mit ausdrücklicher Genehmigung des Nutzers.

---

## 9. Selbstständig arbeiten

- **Probleme so weit wie möglich selbst lösen:** reproduzieren, Daten beschaffen (Logs,
  Settings), Ursache finden, beheben, testen, prüfen – ohne den Nutzer für Zwischenschritte zu
  brauchen. Nachfragen nur bei echten Entscheidungen (Produkt, Datenschutz, Grundsatzfragen),
  nicht für Arbeit, die ein Agent selbst erledigen kann.
- **Werkzeuge nutzen:** Dateien über das Terminal-Panel in `TestResults/` kopieren; Oberflächen
  per Computer-Use oder Browser bedienen, wenn der Nutzer es erlaubt hat.
- **Adminrechte:** Braucht ein Schritt Administratorrechte, zeigt der Agent den Schritt mit
  Begründung an, der Nutzer bestätigt ihn. Nicht darum herumarbeiten und nicht ohne Bestätigung
  erhöhen.

---

## 10. Parallel arbeiten (Zeit sparen)

Bei **jedem** Plan und vor jeder Umsetzung prüfen: Was davon kann gleichzeitig laufen?

### Im Plan
- Jede Meilensteindatei hat einen Abschnitt **„Parallelisierung“** mit:
  - **Spuren:** je Spur Inhalt, Branch, Start, worauf sie wartet
  - einem kleinen Ablaufbild
  - **Schritt 0:** gemeinsame Bausteine, die mehrere Spuren brauchen. Sie kommen zuerst und
    nacheinander.
  - **Exklusiv-Dateien:** gehören genau einer Spur
  - **Hotspots:** Dateien, die alle Spuren nur ergänzend ändern (Einstellungen, Texte,
    Telemetrie-Katalog)
  - **Merge-Reihenfolge**
- Zwei Spuren, die dieselbe Exklusiv-Datei ändern müssen, werden **eine** Spur, die nacheinander
  arbeitet.

### In der Umsetzung
1. **Integrationsbranch** je Meilenstein (`feat/mNN-…` von `develop`). Die steuernde Sitzung
   setzt dort Schritt 0 um, mit Review-Schleife nach §8.
2. **Spuren** laufen als Subagenten mit eigenem Worktree (`isolation: worktree`) oder als eigene
   Sitzungen. Jede Spur:
   - hat einen eigenen Branch vom Integrationsbranch (`feat/mNN-<id>-…`)
   - hält Build, Tests, Zeilenprüfung und ihre eigene Review-Schleife (§8) ein
   - committet und pusht nur auf ihren Branch
   - hakt nur ihre eigene Feature-Datei ab
   - läuft mit dem Modell und Effort aus dem Detailplan (§12)
3. **Zusammenführen an einem Strang:** Die steuernde Sitzung mergt die Spuren in der
   Merge-Reihenfolge in den Integrationsbranch. Nach jedem Merge laufen Build und Tests, Konflikte
   in Hotspots löst sie dort.
4. Danach **Gesamt-Review** des ganzen Meilensteins (§8) und Akzeptanzprüfung. Der Merge nach
   `develop` braucht die Freigabe des Nutzers (§8.5).
5. Gleichzeitig laufen nur Spuren mit **getrennten Exklusiv-Dateien**. Im Zweifel nacheinander:
   Ein Merge-Konflikt kostet mehr als die gesparte Zeit.
6. Spuren des **nächsten** Meilensteins dürfen früher starten, wenn ihre Exklusiv-Dateien frei
   sind. Gemergt wird trotzdem erst in dessen Integrationsbranch.

---

## 11. Knapp antworten („Caveman“-Stil)

Spart Lesezeit und Tokens. Gilt für **Chat-Antworten** und **Berichte** von Nebensitzungen,
Spuren und Subagenten.

- Stichworte statt Sätze. Keine Füllwörter, keine Höflichkeitsfloskeln, keine Wiederholung der
  Aufgabe, kein „Ich werde jetzt …“.
- Ergebnis zuerst, dann nur, was der Nutzer wissen oder entscheiden muss: Branch, Commit,
  Testzahlen, offene Punkte, Risiken.
- Fachbegriffe, Datei- und Branchnamen, Zahlen und Befehle **exakt** lassen – Kürze nie auf Kosten
  von Genauigkeit.
- **Nicht** knapp: Code, Kommentare, Commit-Messages, Doku/KB/ADRs und Pläne folgen weiter
  Abschnitt 4. Warnungen (Datenverlust, Adminrechte, Merge) bleiben vollständige, eindeutige Sätze.
- **Fragen nie im Caveman-Stil:** Jede Frage an den Nutzer (Rückfrage, Auswahl, Freigabe) samt
  ihren Antwortoptionen und Erklärungen wird in ganzen, verständlichen Sätzen gestellt.

---

## 12. Detailplan und Modellwahl (Zeit und Tokens sparen)

Vor der Umsetzung eines Meilenstein- oder Feature-Plans wird jeder Punkt **eingeschätzt** und
in einen **Detailplan** zerlegt. Aus dem Detailplan folgt, **welches Modell** ihn umsetzt.
Ziel: Teure Modelle denken und prüfen, günstige Modelle führen genau beschriebene Arbeit aus.
Vorlage: [docs/plan/detailplan-vorlage.md](docs/plan/detailplan-vorlage.md).

### 12.1 Einschätzen
Die steuernde Sitzung bewertet jeden Aufgabenpunkt (Checkbox der Feature-Datei) nach:

| Kriterium | Frage |
|---|---|
| **Umfang** | Wie viele Dateien und Zeilen ändern sich ungefähr? |
| **Risiko** | Nebenläufigkeit, Interop, Sicherheit, Datenverlust, Migration? |
| **Neuheit** | Gibt es ein Muster im Code, dem man folgen kann, oder ist es neues Terrain? |
| **Klarheit** | Ist alles entschieden, oder sind noch Designfragen offen? |
| **Prüfbarkeit** | Belegen Build und Tests das Ergebnis, oder braucht es Messung bzw. Sichtprüfung? |

Ergebnis je Punkt: **Stufe 1–4** (siehe 12.3). Offene Designfragen werden **vor** dem Detailplan
geklärt (Grilling, Nutzer), nie vom ausführenden Modell nebenbei entschieden.

### 12.2 Detailplan
- Steht in der Feature-Datei im Abschnitt **„Detailplan“** (bei mehr als etwa 15 Dateien als
  eigene Datei `docs/plan/features/<id>-detail.md`).
- Je Arbeitspaket eine Tabelle mit **jeder** Datei:
  `Datei | neu/ändern | Was genau | Tests | ca. Zeilen`.
  „Was genau“ nennt Typen, Methoden mit Signatur, betroffene Stellen und das Muster, dem
  gefolgt wird.
- Dazu je Paket: Reihenfolge, Telemetrie-Namen, Akzeptanz (welcher Test, welcher Befehl beweist
  es), **Modell** und **Effort**.
- Der Detailplan ist so genau, dass ein Modell der zugewiesenen Stufe **ohne eigene
  Designentscheidung und ohne breite Codesuche** umsetzen kann. Das spart die meisten Tokens:
  Der Planer liest den Code einmal, die Ausführenden lesen nur ihre Dateien.
- Pakete gleicher Stufe, die dieselben Dateien berühren, werden **ein** Paket. Mehrere kleine
  Pakete gleicher Stufe kommen in **einen** Subagenten (jeder Start kostet Kontext).
- Arbeit, die nur wenige Werkzeugaufrufe braucht, erledigt die steuernde Sitzung selbst.

### 12.3 Modellwahl
Stand 2026-11, Kosten grob relativ: Haiku ≈ 1/20 Sonnet, Sonnet ≈ ½ Opus, Opus ≈ ½ Fable.

| Stufe | Modell | Effort | Typische Arbeit |
|---|---|---|---|
| 1 – mechanisch | **Haiku** | `low` | Code suchen und zusammenfassen (Explore), Texte, Telemetrie-Katalog, KB- und Plan-Häkchen, Umbenennen, Datei aufteilen nach genauer Vorgabe |
| 2 – Standard nach Plan | **Sonnet** | `medium` | Kernlogik mit Tests nach Detailplan, Settings/Mapping, UI nach bestehendem Muster, Build- und Testfehler beheben |
| 3 – anspruchsvoll | **Opus** | `high` | Detailplan erstellen, Interop, Nebenläufigkeit, Sicherheit, unklare Fehlersuche, Integration und Merge-Konflikte, **jedes Review nach §8** |
| 4 – Grenzfall | **Fable** | `high` | Spikes mit offenem Ausgang, ADR-Entwürfe mit großer Tragweite, Probleme, an denen Opus gescheitert ist |

- **Steuernde Sitzung** und **Reviews** laufen mindestens auf Opus.
- Subagenten bekommen `model` und `effort` aus dem Detailplan (Agent-Tool: `model`, `effort`);
  Spuren nach §10 übernehmen das Modell ihres anspruchsvollsten Pakets oder werden in Pakete
  verschiedener Stufen geteilt.
- **Eskalation:** Scheitert ein Modell zweimal am selben Paket (Build oder Tests rot) oder stößt es
  auf eine Entscheidung, die der Plan nicht trifft, hört es auf und meldet das. Die steuernde
  Sitzung ergänzt den Plan oder vergibt das Paket eine Stufe höher. Kein Raten, kein Umgehen.
- **Rückblick:** Nach dem Meilenstein trägt die steuernde Sitzung in der Meilensteindatei ein,
  welche Einstufungen zu niedrig oder zu hoch waren, damit spätere Pläne besser schätzen.
