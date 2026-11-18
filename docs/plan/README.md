# Roadmap – MVP (NoMorePaper 1.0)

Grundprinzip: **Risiko zuerst.** Die unsicherste Technik wird früh als Durchstich bewiesen,
bevor Features darauf aufbauen. Jeder Meilenstein endet mit etwas, das man **starten und
vorführen** kann.

## Übersicht

| # | Meilenstein | Ergebnis (Demo) | Größe | Status |
|---|---|---|---|---|
| M0 | [Fundament](m0-fundament.md) | Solution mit Schichten, Architekturtests, Zeilenprüfung, CI, Docker Compose (Server, Worker, Postgres) startet leer | S | erledigt 2026-11-18 – CI auf GitHub nach Push prüfen |
| M1 | Durchstich, Barrierefreiheit + iCloud-Spike | PDF hochladen → Worker → OCR → PDF/A → Volltextsuche; dunkle Kontrast-App, Viewer mit Shader „intelligent invertiert“, scharfer Zoom, Screenreader (ADR-0010/0011); Spike entscheidet, ob iCloud in .NET machbar ist (ADR-0005) | L+ | offen |
| M2 | Konten & Haushalt | Anmeldung, Haushalt mit mehreren Konten, „privat“-Markierung, Betrieb über Tailscale, DE/EN | M | offen |
| M3 | Erkennung & Ordnung | Ollama ordnet Absender, Art, Datum, Betrag, Tags zu; Korrektur in der UI; Filter; Duplikat-Erkennung | L | offen |
| M4 | Eingänge & Scan-Bearbeitung | **vorher: HTTPS-Zugang entscheiden, Schlüsselring verschlüsseln (ADR-0015)**; PWA-Kamera mit Kantenerkennung, Scanner-Ordner, IMAP, iCloud-Sync; Seiten drehen/löschen/umsortieren/zuschneiden, zusammenfügen/teilen, Stapel-Trennung als Vorschlag, Versionen + Papierkorb (ADR-0013) | L+ | offen |
| M5 | Fristen, Kalender & Home Assistant | Fristen erkennen und auflisten, Fristarten mit Übernahme-/Vorlauf-Einstellung, iCloud-Kalender per CalDAV (ADR-0012), API-Token, HA-Integration (HACS) mit Sensoren und Erinnerungen | L | offen |
| M6 | Verträge & Kosten | Verträge mit LLM-Vorschlag, Kostenübersicht nach Kategorie und Jahr, Kündigungsfrist → Frist, Preiserhöhung erkennen (ADR-0014) | M | offen |
| M7 | Backup, Steuer-Export & Release 1.0 | Verschlüsseltes Backup mit getestetem Restore, Steuer-Export, Doku, Tag `v1.0.0` | M | offen |

Größen: **S** ≈ wenige Tage · **M** ≈ 1–2 Wochen · **L** ≈ 2–4 Wochen (grob).

## Abhängigkeiten

```
M0 ─► M1 ─► M2 ─┬─► M3 ─┬─► M5 ─► M6 ─┐
                └─► M4 ─┴─────────────┴─► M7
```
- **Barrierefreiheit zuerst:** Ab M1 ist jede Oberfläche nutzbar für den sehbehinderten
  Hauptnutzer; ein Shader-Spike in M1 legt Renderer, Schwellen und Foto-Erkennung fest.
- **Risiko zuerst:** Der iCloud-Spike läuft in M1 parallel zum Durchstich. Scheitert er, wird
  vor M4 per ADR auf einen Fallback umgestellt.
- M3 und M4 können parallel laufen (gemeinsames Eingangs-Interface aus M1).
- M5 braucht die Fristen-Felder aus M3.
- M6 braucht Fristen und Kalender aus M5 (Kündigungsfristen laufen als normale Fristen).

## Querschnittsregeln für alle Meilensteine
- Jede Aufgabe hält die Regeln aus [AGENTS.md](../../AGENTS.md) ein.
- Jeder Meilenstein schließt mit: Akzeptanzkriterien erfüllt, KB aktualisiert,
  Status hier auf **erledigt** gesetzt.
- Neue Erkenntnisse, die einen späteren Plan ändern, werden **sofort** in dessen Datei
  eingetragen (Abschnitt „Offene Fragen / Risiken“).
- Jeder Plan hat einen Abschnitt **„Parallelisierung“** (Spuren, Schritt 0, Exklusiv-Dateien,
  Hotspots, Merge-Reihenfolge); umgesetzt wird nach [AGENTS.md §10](../../AGENTS.md).
- Vor der Umsetzung wird jeder Aufgabenpunkt eingeschätzt und in einen **Detailplan** mit Dateien,
  Modell und Effort zerlegt ([Vorlage](detailplan-vorlage.md), [AGENTS.md §12](../../AGENTS.md)).

## Nach dem MVP (nicht geplant, nur gesammelt)
Siehe [../kb/produkt-und-scope.md](../kb/produkt-und-scope.md#nach-dem-mvp-ideen).
