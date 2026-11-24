# Roadmap – MVP (NoMorePaper 1.0)

Grundprinzip: **Risiko zuerst.** Die unsicherste Technik wird früh als Durchstich bewiesen,
bevor Features darauf aufbauen. Jeder Meilenstein endet mit etwas, das man **starten und
vorführen** kann.

## Übersicht

| # | Meilenstein | Ergebnis (Demo) | Größe | Status |
|---|---|---|---|---|
| M0 | [Fundament](m0-fundament.md) | Solution mit Schichten, Architekturtests, Zeilenprüfung, CI, Docker Compose (Server, Worker, Postgres) startet leer | S | erledigt 2026-11-18 – CI auf GitHub nach Push prüfen |
| M1 | [Durchstich, Barrierefreiheit + iCloud-Spike](m1-durchstich.md) | PDF hochladen → Worker → OCR → PDF/A → Volltextsuche; dunkle Kontrast-App, Viewer mit Shader „intelligent invertiert“, scharfer Zoom, Screenreader (ADR-0010/0011); Spike entscheidet, ob iCloud in .NET machbar ist (ADR-0005) | L+ | in develop 2026-11-18 – NVDA-Prüfung, iCloud-Test, OCR-Zeit auf dem Server offen |
| M2 | [Konten & Haushalt](m2-konten.md) | **Schritt 0: Anwendungsfälle (Annahme, Verarbeitung, Suche) nach Application verschieben, Server/Worker dünn (M1-Review, AGENTS.md §2.2)**; Anmeldung, Haushalt mit mehreren Konten, „privat“-Markierung, Betrieb über Tailscale, DE/EN | M | in develop 2026-11-19 – NVDA-Prüfung, iPhone über tailscale serve offen |
| M3 | [Erkennung & Ordnung](m3-erkennung.md) | Ollama ordnet Absender, Art, Datum, Betrag, Tags zu; Korrektur in der UI; Filter; Duplikat-Erkennung | L | in develop 2026-11-19 – NVDA-Prüfung, Erkennungszeit auf dem Server, erster Start mit Modell offen |
| M4 | [Eingänge](m4-eingaenge.md) | PWA-Kamera mit Kantenerkennung, Ansagen und Auto-Auslöser; Scanner-Ordner (Samba); Mail mit Spam-Prüfung durch das LLM; iCloud-Sync (nach Nutzertest, ADR-0018); Seite „Eingänge“ (ADR-0024) | L | in develop 2026-11-19 (ohne iCloud) – iPhone-/NVDA-Prüfung, iCloud-Test und Spur D offen |
| M4u | [Neue Oberfläche (Stitch)](m4u-oberflaeche.md) | Theme, Seitenleiste mit 5 Hauptpunkten, Seiten Anmelden, Dokumente, Dokument ansehen (Reiter „Erkannter Text“), Eingänge nach den [Stitch-Entwürfen](../ui/stitch/README.md); restliche Seiten angleichen | M | in develop 2026-11-23 (CI grün) – NVDA- und iPhone-Prüfung durch den Nutzer offen |
| M4b | [Scan-Bearbeitung](m4b-scan-bearbeitung.md) | Seiten drehen/löschen/umsortieren/zuschneiden, zusammenfügen/teilen, Stapel-Trennung als Vorschlag, Versionen + Papierkorb (ADR-0013); Umsetzung ADR-0025 | L | Integrationsbranch fertig 2026-11-24 (P6 Review + Abnahme-Bilder) – NVDA-Prüfung durch den Nutzer, Messung Trennung (12 Stapel), Merge nach develop offen |
| M5 | Fristen, Kalender & Home Assistant | Fristen erkennen und auflisten, Fristarten mit Übernahme-/Vorlauf-Einstellung, iCloud-Kalender per CalDAV (ADR-0012), API-Token, HA-Integration (HACS) mit Sensoren und Erinnerungen | L | offen |
| M6 | Verträge & Kosten | Verträge mit LLM-Vorschlag, Kostenübersicht nach Kategorie und Jahr, Kündigungsfrist → Frist, Preiserhöhung erkennen (ADR-0014) | M | offen |
| M7 | Backup, Steuer-Export & Release 1.0 | Verschlüsseltes Backup mit getestetem Restore, Steuer-Export, Doku, Tag `v1.0.0` | M | offen |

Größen: **S** ≈ wenige Tage · **M** ≈ 1–2 Wochen · **L** ≈ 2–4 Wochen (grob).

## Abhängigkeiten

```
M0 ─► M1 ─► M2 ─┬─► M3 ─┬─► M5 ─► M6 ─┐
                └─► M4 ─┼─► M4u ─┐      │
                        └─► M4b ─┴──────┴─► M7
```
- **Barrierefreiheit zuerst:** Ab M1 ist jede Oberfläche nutzbar für den sehbehinderten
  Hauptnutzer; ein Shader-Spike in M1 legt Renderer, Schwellen und Foto-Erkennung fest.
- **Risiko zuerst:** Der iCloud-Spike läuft in M1 parallel zum Durchstich. Scheitert er, wird
  vor M4 per ADR auf einen Fallback umgestellt.
- M3 und M4 können parallel laufen; M4 nutzt die Erkennung aus M3 (Spam-Prüfung der Mail).
- M4b nach M4: Stapel-Trennung betrifft vor allem den Scanner-Ordner.
- M4u und M4b laufen gleichzeitig: Das Backend von M4b (Schritt 0, Spuren A, C-Server, D) braucht
  M4u nicht; die Oberfläche von M4b (Spur B, `/papierkorb`) startet erst nach M4u Spur C, damit sie
  gleich im neuen Design entsteht.
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
