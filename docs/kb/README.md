# Knowledgebase – Index

Thematisch aufgeteilt, damit Agents nur laden, was sie brauchen. Jede Datei bleibt kurz.

| Datei | Inhalt |
|---|---|
| [produkt-und-scope.md](produkt-und-scope.md) | Vision, Zielgruppe, MVP-Umfang, Roadmap |
| [glossar.md](glossar.md) | Begriffe aus der Fachdomäne und Technik |
| [architektur.md](architektur.md) | Projekte, Schichten, Ordnerstruktur, Datenfluss |
| [testumgebung.md](testumgebung.md) | Vorhandene Hardware, Teststrategie, was ungetestet bleibt |
| [build-und-tooling.md](build-und-tooling.md) | Build, Tests, Analyzer, Werkzeuge |
| [telemetrie.md](telemetrie.md) | Logs, Abläufe, Messwerte: EventId-Bereiche, Katalog |
| [oberflaeche.md](oberflaeche.md) | Design-Werte aus Stitch (Farben, Schrift, Abstände), Seitenrahmen, Bausteine, Fokus |
| [ui-prozess.md](ui-prozess.md) | Ablauf Stitch-Entwurf → Prüfung → Freigabe → Blazor-Seite → Abnahme; Prüfliste, wiederkehrende Stitch-Fehler |
| [darstellung.md](darstellung.md) | PDF intelligent invertiert: Farbraum, Formeln, Foto-Erkennung, Schwellen, Messwerte, Empfehlungen für den Viewer |
| [betrieb.md](betrieb.md) | Installation, Schlüsselring-Zertifikat, Tailscale-HTTPS, vertrauenswürdige Proxys, Prüfliste |
| [konten.md](konten.md) | Konten und Haushalt: Rollen, Anmeldung, Sitzung, Tokens, Notfall-Code, private Dokumente |
| [verarbeitung.md](verarbeitung.md) | Pipeline vom Upload bis Done: Warteschlange, Werkzeuge, Zeitlimits, gemessene Zeiten, bekannte Lücken |
| [erkennung.md](erkennung.md) | Lokales LLM ordnet Dokumente ein: Modell, Prompt, JSON-Schema, Kürzungsregel, Trefferquoten, Zeiten, Speicher, Vorgaben für den Worker |
| [scan-bearbeitung.md](scan-bearbeitung.md) | Scan-Bearbeitung (M4b): Seiten bearbeiten, Zusammenfügen und Teilen, Fassungen, Papierkorb (30 Tage), Stapel-Trennung, Werkzeuge, Messwerte |
| [eingaenge.md](eingaenge.md) | Eingänge (M4): Annahme mit Besitzer, Privat-Einstellung, Absender-Adressen, Herkunft unklar, Takt der Hintergrund-Eingänge, Geheimnisse, PDF, LLM-Zugang |

Architekturentscheidungen: [../adr/](../adr/) · Roadmap & Meilensteine: [../plan/](../plan/README.md)

## Pflege-Regeln
- Fakten mit Quelle (Doku-Link, Messung mit Hardware, Beobachtung mit Version).
- Unsichere Aussagen markieren: `⚠️ unverifiziert`.
- Datei > 300 Zeilen → nach Unterthema aufteilen und diesen Index aktualisieren.
