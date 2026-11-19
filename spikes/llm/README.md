# LLM-Spike (M3, Paket P1)

Wegwerf-Prototyp zu ADR-0004. Er beantwortet eine Frage: **Welches lokale 7–8B-Modell ordnet
Dokumente auf einer CPU mit 8 GB gut und schnell genug ein?** Ergebnis:
[ADR-0022](../../docs/adr/0022-llm-modell.md), Details in [erkennung.md](../../docs/kb/erkennung.md),
Messwerte in [results.md](results.md).

Das Programm gehört **nicht** zur Solution `NoMorePaper.slnx` und wird nicht ausgeliefert. Es baut
unter der `Directory.Build.props` des Repos (Warnungen sind Fehler) und nutzt kein NuGet-Paket.

## Inhalt

| Pfad | Inhalt |
|---|---|
| `samples/*.txt` | 22 selbst geschriebene Dokumenttexte wie nach der OCR (20 deutsch, 2 englisch), mit typischen Erkennungsfehlern. Alle Namen, Firmen, Nummern und Beträge sind erfunden. |
| `samples/expected.json` | Listen eines kleinen Haushalts (Absender, Dokumentarten, Schlagwörter, frühere Dokumente) und die Sollwerte je Dokument |
| `Prompting/` | Systemtext, JSON-Schema, Auswahl der Beispiele |
| `Expected/` | Typen zu `expected.json` |
| `Text/` | Kürzen (Kopf + Fuß), Normalisieren von Namen |
| `Scoring/` | Regeln für die Bewertung je Feld |
| `measurements/*.json` | Rohantworten und Zeiten jedes Laufs; daraus entsteht `results.md` |
| `notes.md` | Notizen aus der Sichtprüfung; werden an `results.md` angehängt |

## Messen

Ollama läuft im Container **ohne GPU**, mit 8 GB Speicher und 4 CPU-Kernen. Der Port ist nur an
`127.0.0.1` gebunden; `11435`, weil auf dem Entwicklungs-PC schon ein Ollama mit GPU auf `11434` läuft.
Die Modelle liegen in einem Docker-Volume, nicht im Repo.

```powershell
docker run -d --name nmp-llm-spike --memory 8g --memory-swap 8g --cpus 4 `
  -e OLLAMA_NUM_PARALLEL=1 -e OLLAMA_MAX_LOADED_MODELS=1 -e LLAMA_ARG_CACHE_RAM=0 `
  -p 127.0.0.1:11435:11434 -v nmp-llm-spike-models:/root/.ollama ollama/ollama:0.40.16

docker exec nmp-llm-spike ollama pull ministral-3:8b-instruct-2512-q4_K_M
docker exec nmp-llm-spike ollama show ministral-3:8b-instruct-2512-q4_K_M --license   # Lizenz prüfen

dotnet run --project spikes/llm -- run --model ministral-3:8b-instruct-2512-q4_K_M --label main-ministral-3
dotnet run --project spikes/llm -- report                          # schreibt results.md
dotnet run --project spikes/llm -- show --label main-ministral-3   # alle Antworten zum Durchlesen
```

- `LLAMA_ARG_CACHE_RAM=0` schaltet den Prompt-Cache von `llama-server` aus. Ohne die Variable wächst der
  Speicher und frühere Dokumente verfälschen die Zeiten (siehe `notes.md`). Nur `main-ministral-3` und
  `ex3-19-ministral-3` liefen so; alle anderen Läufe liefen ohne die Variable.
- Vor einem Vergleichslauf das Modell entladen:
  `curl http://127.0.0.1:11435/api/generate -d '{"model":"<modell>","keep_alive":0}'`.
- Läufe, deren Name mit `main` beginnt, erscheinen in `results.md` mit ihren Abweichungen je Dokument.

Schalter von `run`:

| Schalter | Vorgabe | Bedeutung |
|---|---|---|
| `--head`, `--tail` | 4500, 1500 | Zeichen vom Anfang und vom Ende; `--head 0` schickt den ganzen Text. Die Vorgabe ist die der Hauptläufe; empfohlen wird 3000 + 1000 |
| `--num-ctx` | 4096 | Kontextlänge in Tokens |
| `--threads` | 4 | `num_thread` für Ollama; passend zu `--cpus` |
| `--examples` | 0 | höchstens so viele frühere Dokumente, deren Absender im Text vorkommt |
| `--cold-prefix` | aus | ändert die erste Zeile je Dokument und schaltet so den Präfix-Cache aus |
| `--only` | alle | Auswahl nach Nummer, z. B. `--only 03,04,06` |

## Aufräumen

```powershell
docker rm -f nmp-llm-spike
docker volume rm nmp-llm-spike-models     # drei Modelle, gibt etwa 16 GB frei
docker rmi ollama/ollama:0.40.16          # 8,5 GB; wird für den Worker wieder gebraucht
```

## Datenschutz

Das Programm spricht nur mit `127.0.0.1`. Die Testdokumente sind erfunden; echte Dokumente gehören
nicht in diesen Ordner (AGENTS.md 2.4).
