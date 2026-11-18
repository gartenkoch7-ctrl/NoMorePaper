# Vorlage – Detailplan

Regeln: [AGENTS.md §12](../../AGENTS.md). Diesen Abschnitt in die Feature-Datei kopieren
(Überschrift „Detailplan“) oder bei großen Features als `features/<id>-detail.md` anlegen.

## Detailplan

**Erstellt von:** Opus (Stufe 3) · **Datum:** JJJJ-MM-TT · **Offene Designfragen:** keine
(sonst erst klären, dann planen)

### Einschätzung

| Aufgabenpunkt | Umfang | Risiko | Neuheit | Klarheit | Prüfbarkeit | Stufe |
|---|---|---|---|---|---|---|
| Kern: `Xyz` | 2 Dateien, ~150 Zeilen | gering | Muster vorhanden | entschieden | Unit-Tests | 2 |
| Texte, Katalog, KB | 4 Dateien, ~40 Zeilen | keins | Muster | entschieden | Build | 1 |

### Paket P1 – <Name> · Modell: **Sonnet** · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/…/Xyz.*` | neu | Typ/Methode mit Signatur – wie `<bestehendes Muster>` | – | 40 |
| `tests/…/XyzTests.*` | neu | Fälle: … | selbst | 80 |

- **Reihenfolge:** Kern → Tests → Mapping
- **Telemetrie:** Log-/Ablaufnamen – Eintrag im Katalog
- **Akzeptanz:** Testbefehl grün, neue Tests decken alle Fälle
- **Eskalation:** zwei rote Läufe oder eine nicht geplante Entscheidung → stoppen und melden

### Paket P2 – <Name> · Modell: **Opus** · Effort: `high`
…

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|
