## Notizen zur Messung (von Hand, 2026-11-19)

### Umgebung

- Windows 11, Rancher Desktop (WSL 2), Docker 29.5.3. CPU Intel Core i7-12700KF. Der Container bekam
  `--cpus 4` (Läufe `cpu2`/`cpu8`: 2 bzw. 8), `--memory 8g --memory-swap 8g`, **keine GPU** (Ollama-Log:
  `inference compute id=cpu library=cpu`). `num_thread` war immer gleich der CPU-Zahl.
- Ollama `ollama/ollama:0.40.16` (Digest `sha256:910d0d81…4592`), Anfragen an `/api/chat` mit
  `stream: false`, `temperature 0`, `seed 1`, `num_predict 400`.
- Die Zeiten gelten für diese CPU. Der Server hat eine andere; dort nachmessen.
- Dieselbe Anfrage schwankt um etwa ±15 % (Mietvertrag mit qwen2.5: 75 s und 88 s).
- Gleiche Eingabe ergibt die gleiche Antwort: `cache-ministral-3` und `main-ministral-3` sind in allen 22
  Antworten identisch.

### Lizenzen (geprüft am 2026-11-19)

Geprüft mit `ollama show <modell> --license` im Container und auf der Modellkarte bei Hugging Face
(Feld `license`).

| Ollama-Tag | Herkunft | Lizenz | Parameter | Datei |
|---|---|---|---|---|
| `ministral-3:8b-instruct-2512-q4_K_M` | Mistral AI, `mistralai/Ministral-3-8B-Instruct-2512` | Apache-2.0 | 8,9 Mrd. | 6,0 GB |
| `qwen2.5:7b-instruct-q4_K_M` | Alibaba, `Qwen/Qwen2.5-7B-Instruct` | Apache-2.0 | 7,6 Mrd. | 4,7 GB |
| `granite3.3:8b` | IBM, `ibm-granite/granite-3.3-8b-instruct` | Apache-2.0 | 8,2 Mrd. | 4,9 GB |

Nicht gemessen, weil die Lizenz nicht offen ist: `llama3.1:8b` (Llama Community License), `gemma3`/`gemma4`
(Gemma Terms of Use), `aya-expanse:8b` (CC-BY-NC). Bei Qwen 2.5 sind nur die Größen 3B und 72B anders
lizenziert; 7B ist Apache-2.0.

### Prompt-Versionen

Der Prompt wurde mit qwen2.5 an denselben 22 Dokumenten entwickelt, an denen gemessen wird. Die Quoten
sind deshalb eher zu gut (siehe „Grenzen“ in `docs/kb/erkennung.md`).

| Version | Lauf | Änderung | Befund |
|---|---|---|---|
| 1 | `v1-qwen2.5` | Felder in der Reihenfolge Absender, Art, Datum, Betrag, Währung, Tags, Titel, Zusammenfassung; bis zu 3 Tags | Listeneinträge 11/11 übernommen. Aber 3-mal `documentDate: null` trotz Datum, 2-mal `amount: null` bei langen Dokumenten, obwohl die Zusammenfassung danach den Betrag nennt. Zeiten ungültig (Modelle wurden nebenher geladen). |
| 2 | `v2-qwen2.5` | Zusammenfassung und Titel zuerst; deutlichere Regel zum Datum; Erinnerung nach dem Dokumenttext | Datum und Betrag 22/22. Aber nur noch 3/11 Listeneinträge: Das Modell schreibt erst die Zusammenfassung mit dem Namen aus dem Briefkopf („Nordkom Telefon GmbH“) und wiederholt ihn dann. |
| 3 | `v3-qwen2.5-subset` | Listen hinter dem Dokumenttext; Tags als `enum` der vorhandenen Schlagwörter plus `newTags` | Verworfen. Listeneinträge weiter nicht übernommen; das `enum`-Feld wird immer gefüllt (Fitnessstudio → „Kfz, Versicherung“); 8 s mehr je Dokument, weil die Listen nicht mehr im wiederverwendeten Anfang stehen. |
| 4 | `main-*` | Absender und Art zuerst, dann Zusammenfassung und Titel, dann Datum, Betrag, Währung; höchstens 2 Tags; Listen wieder im Systemtext | Endstand. Listeneinträge 9/11 (qwen2.5) bzw. 11/11 (ministral-3), Datum 21/22, Betrag 22/22. |

### Sichtprüfung Titel und Zusammenfassung (alle 66 Antworten der drei Hauptläufe gelesen)

- **ministral-3:** flüssiges, richtiges Deutsch, Umlaute werden aus dem OCR-Text wiederhergestellt. Titel
  21/22 brauchbar (ein erfundenes Jahr: „Herbstsale 2023“); die Regel ist strenger (19/22), weil sie ein
  bestimmtes Stichwort verlangt. Zusammenfassungen nennen Betrag und Frist. 5/22 haben drei statt zwei
  Sätze. 3/22 enthalten einen kleinen Sachfehler, den die Regel nicht sieht: „Urlaubsanspruch 8 Tage“
  (es ist der Resturlaub), „bei der Ankunft zu zahlen“ (bei der Abreise), „für den Veranlagungszeitraum
  2022 bestätigt“ (gemeint ist der Freistellungsbescheid).
- **qwen2.5:** knapp, oft nur ein Satz ohne die wichtige Angabe. Übernimmt OCR-Fehler („Kundigung“,
  „spatestens“), erfindet Wörter („Wohnraummieterschließen“), ungelenke Sätze („zu zahlen in vier Fällen“).
  Titel etwa 15/22 brauchbar.
- **granite3.3:** richtiges, aber langes Deutsch („Dies ist ein …“, drei Sätze, auch das Geburtsdatum aus
  dem Arztbrief). Titel mit Belegnummern („Rechnung 0218 - 07.11.2026“). Schreibt Namen aus dem
  Briefkopf ab statt aus der Liste (5/11).
- Alle drei: Schlagwörter sind das schwächste Feld. Das zweite Schlagwort ist oft geraten („Garantie“ bei
  einer Werkstattrechnung, „Gesundheit, Wohnung“ beim Fitnessstudio).
- Zwei Modelle wählen bei der Zahnarztrechnung den vorhandenen Eintrag „Dr. Petra Sommerfeld“, qwen2.5
  beim Bürgeramt „Stadtwerke Lindental“. Beide Namen kommen im jeweiligen Text nicht vor.

### Kürzen (ministral-3, drei längere Dokumente, Prompt-Cache noch an, vor jedem Lauf entladen)

| Regel | Mietvertrag (7 411 Zeichen) | Versicherungsschein (6 935) | Steuerbescheid (2 921) | Felder |
|---|---|---|---|---|
| ganzer Text, `num_ctx 8192` | 105 s | 92 s | 59 s | alle richtig |
| Kopf 4500 + Fuß 1500 | 106 s | 93 s | 68 s | alle richtig |
| Kopf 3000 + Fuß 1000 | 73 s | 71 s | 58 s | alle richtig |
| nur Kopf 4000 | 71 s | 66 s | 58 s | Mietvertrag: Datum falsch (Mietbeginn statt Datum bei der Unterschrift) |

Zeichenzahlen nach dem Zusammenziehen der Leerräume (`TextTruncator.Compact`); der Mietvertrag hat roh
8 050 Zeichen. 1 000 Zeichen Fließtext sind etwa 310 Tokens (Tabellen mit Zahlen bis 530) und kosten auf
4 Kernen 8 bis 14 s.

### Beispiele (ministral-3, `ex3-*`)

Bis zu 3 frühere Dokumente, deren Absender im Text vorkommt, nur als Zeile mit Absender, Art, Tags, Titel
(etwa 58 Tokens, also rund 1,5 s je Beispiel). Auf den 11 Dokumenten mit Treffer: Art 11/11 statt 10/11
(die Beitragsanpassung wird „Schreiben“ wie das Vorbild), Tag-Genauigkeit 86 % statt 73 %, Titel folgen dem
Muster des Haushalts („Kontoauszug 10/2026“). Nebenwirkung: Beim Schreiben des Bürgeramts wandert
„Wohnung“ aus dem Vorbild (Abfallbescheid derselben Stadt) in die Tags.

### Speicher (cgroup `rss`, also ohne Seiten-Cache)

| Modell, Einstellung | Speicher |
|---|---|
| qwen2.5, `num_ctx 4096`, nach 1 Dokument | 5,2 GB |
| ministral-3, `num_ctx 4096`, `LLAMA_ARG_CACHE_RAM=0`, 23 Anfragen | 6,93–6,94 GB, gleichbleibend |
| ministral-3, `num_ctx 4096`, Prompt-Cache an (Vorgabe) | 6,8 GB nach 1 Dokument, bis 7,95 GB; **ein OOM-Kill** |
| ministral-3, `num_ctx 8192`, Prompt-Cache an | 8,44 GB, an der Grenze |

`docker stats` zeigt mehr (7,0–7,9 GiB bei allen Modellen), weil es den Seiten-Cache der Modelldatei mitzählt.

### Fallen beim Messen

- **Prompt-Cache von llama-server.** Ollama 0.40 startet `llama-server` und der hebt frühere Prompts im
  Arbeitsspeicher auf (Vorgabe 8 192 MiB, zusätzlich begrenzt auf `num_ctx` Tokens). Folgen: (1) Der
  Speicher wächst, bei ministral-3 bis zum OOM-Kill (`signal: killed`, cgroup `oom_kill 1`, Antwort 500
  „model runner has unexpectedly stopped“; siehe Dokument 19 in `ex3-ministral-3`). (2) Ein Dokument, das
  zum zweiten Mal mit gleichem Anfang kommt, ist viel schneller (Mietvertrag 96 s → 47 s). Deshalb wurde
  vor jedem Vergleichslauf das Modell entladen (`keep_alive: 0`). Mit `LLAMA_ARG_CACHE_RAM=0` am Container
  ist der Cache aus (Log: „prompt cache is disabled“); der gemeinsame Anfang (Systemtext) wird trotzdem
  wiederverwendet, und die Anfragen waren sogar etwa 8 % schneller (Mittel 47,3 s statt 51,6 s).
- `prompt_eval_count` zählt auch die wiederverwendeten Tokens. Die Rate „Prompt tok/s“ stimmt deshalb nur
  im Lauf ohne Wiederverwendung (`cold-ministral-3`: 38,7 tok/s).
- Das erste Dokument jedes Laufs bezahlt den Systemtext mit Listen (etwa 900 Tokens, gut 20 s) und steht
  so im Mittel.
- `ex3-ministral-3` enthält den Absturz bei Dokument 19; `ex3-19-ministral-3` ist dessen Wiederholung.
- Die Läufe `cut-*`, `ex3-*`, `cold-*`, `cpu*` und `cache-*` liefen noch mit Prompt-Cache.
