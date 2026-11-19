# ADR-0022: LLM-Modell für die Erkennung: Ministral 3 8B über Ollama

- **Status:** Angenommen
- **Datum:** 2026-11-19
- **Ergänzt:** ADR-0004 (bleibt gültig)

## Kontext
ADR-0004 legt fest, dass ein lokales LLM über Ollama auf der CPU die Dokumente einordnet, und überlässt
die Wahl des Modells einem Spike. Randbedingungen aus dem Plan M3: etwa 8 GB Arbeitsspeicher für das
LLM, also ein Modell mit 7 bis 8 Milliarden Parametern; offene Lizenz; gutes Deutsch; kein Dokument
verlässt das eigene Netz (ADR-0002).

Der Spike (`spikes/llm/`) hat drei Modelle an 22 selbst geschriebenen Testdokumenten gemessen. Details,
Prompt, Schema und alle Zahlen: [erkennung.md](../kb/erkennung.md).

## Entscheidung
- **Modell:** `ministral-3:8b-instruct-2512-q4_K_M` aus der Ollama-Bibliothek (Mistral AI, Ministral 3
  8B Instruct, 4-Bit-Quantisierung, 6,0 GB). Der Modellname steht in der Konfiguration.
- **Lizenz:** Apache-2.0. Geprüft am 2026-11-19 mit `ollama show --license` und auf der Modellkarte
  `mistralai/Ministral-3-8B-Instruct-2512`. Sie erlaubt die private und die Open-Source-Nutzung.
- **Ollama:** Image `ollama/ollama:0.40.16` (fest), Speichergrenze 8 GB, `LLAMA_ARG_CACHE_RAM=0`,
  `OLLAMA_NUM_PARALLEL=1`, `OLLAMA_MAX_LOADED_MODELS=1`.
- **Aufruf:** `/api/chat` mit JSON-Schema in `format`, `temperature 0`, `num_ctx 4096`. Prompt, Schema
  mit fester Feldreihenfolge und Kürzungsregel (Leerraum zusammenziehen, dann Kopf 3 000 + Fuß 1 000
  Zeichen) wie in erkennung.md.
- **Ersatzmodell** für Server mit weniger Speicher oder langsamer CPU:
  `qwen2.5:7b-instruct-q4_K_M` (Apache-2.0). Der Wechsel ist nur eine Einstellung.
- Der Worker **prüft jede Antwort gegen den OCR-Text** (Absender aus der Liste, Jahr, Betrag), bevor er
  sie übernimmt.

## Messwerte
Entwicklungs-PC (Intel Core i7-12700KF), Ollama im Container ohne GPU, 4 CPU-Kerne, 8 GB. Der Server hat
eine andere CPU; dort wird neu gemessen.

| | ministral-3 8B | qwen2.5 7B | granite3.3 8B |
|---|---|---|---|
| Absender richtig | 95 % | 91 % | 100 % |
| Dokumentart richtig | 86 % | 82 % | 91 % |
| Datum richtig | 95 % | 95 % | 95 % |
| Betrag und Währung richtig | 100 % | 100 % | 95 % |
| vorhandener Absender in Listenschreibweise | 11/11 | 9/11 | 5/11 |
| Dauer je Dokument, Median / Maximum | 43 s / 92 s | 32 s / 75 s | 50 s / 141 s |
| Speicher | 6,9 GB | 5,2 GB | 5,7 GB |

- Mit der Kürzungsregel dauern lange Dokumente 71–73 s. Ohne wiederverwendeten Systemtext (erste
  Anfrage) dauert eine Seite 58–70 s, auf 2 Kernen 101 s, auf 8 Kernen 45 s.
- Jede der 175 Antworten war gültiges JSON.
- 22 Dokumente: Ein Dokument entspricht 4,5 Prozentpunkten.

## Begründung
- **Ministral 3** übernimmt als einziges Modell alle vorhandenen Absender in der Schreibweise der Liste
  und schreibt die besten Titel und Zusammenfassungen (flüssiges Deutsch, Betrag und Frist genannt). Die
  Zusammenfassung ist das Erste, was der Screenreader vorliest (ADR-0010); ihre Qualität wiegt hier mehr
  als 11 Sekunden.
- **Verworfen: qwen2.5 7B** als Standard. Am schnellsten und sparsamsten, aber schwächeres Deutsch
  (übernimmt OCR-Fehler, erfundene Wörter), Dokumentart unter dem Ziel von 85 %, zwei falsche
  Listeneinträge. Bleibt Ersatzmodell.
- **Verworfen: granite3.3 8B.** Nach den Regeln die meisten Treffer bei Absender und Art, aber nur mit
  zugelassenen Varianten: Es schreibt Namen aus dem Briefkopf ab statt aus der Liste (5/11). Dazu am
  langsamsten (langes Dokument 141 s) und erfindet bei der Werbung Datum und Betrag.
- **Nicht gemessen:** `llama3.1:8b`, `gemma3`, `gemma4`, `aya-expanse:8b` (Lizenz nicht offen);
  größere Modelle (schon 8,9 Milliarden Parameter brauchen 6,9 von 8 GB); `mistral:7b-instruct-v0.3`,
  `qwen3:8b` und andere Modelle mit offener Lizenz aus Zeitgründen.
- **`LLAMA_ARG_CACHE_RAM=0`:** Ohne die Einstellung hebt Ollamas `llama-server` frühere Prompts im
  Arbeitsspeicher auf. Der Verbrauch stieg von 6,8 auf 7,95 GB, der Kernel beendete den Prozess einmal.
  Mit der Einstellung blieb er über 23 Anfragen bei 6,93 GB, und die Anfragen waren 8 % schneller.

## Folgen
- **Akzeptanzkriterien M3:** Absender (≥ 85 %), Datum und Betrag (≥ 90 %) sind am Testsatz erreicht,
  Dokumentart mit 86 % nur knapp. „≤ 60 s je Dokument“ gilt auf 4 schnellen Kernen für einseitige
  Dokumente, nicht für lange (71–73 s) und nicht für die erste Anfrage. Vorschlag: Median ≤ 60 s,
  Maximum ≤ 120 s, gemessen auf dem Server; Zeitlimit des Workers 300 s.
- Die Quoten stammen von einem kleinen, selbst geschriebenen Testsatz, an dem auch der Prompt entwickelt
  wurde. Mit echten Scans sind sie niedriger. „Zu prüfen“ bleibt nötig.
- Bekannte Fehler: falscher vorhandener Absender, erfundenes Datum, „Schreiben“ statt einer neuen
  Dokumentart, geratene Schlagwörter, kleine Sachfehler in Zusammenfassungen. Die Prüfungen des Workers
  fangen die ersten beiden.
- Platz auf dem Server: Image 8,5 GB, Modell 6,0 GB.
- Der Speicher ist knapp (6,9 von 8 GB). `num_ctx 8192` passt nicht (8,4 GB).
- Ein neues Modell oder eine neue Ollama-Version wird mit `spikes/llm/` an denselben Dokumenten
  nachgemessen, bevor es Standard wird.
