# LLM-Spike: Messergebnisse

Erzeugt mit `dotnet run --project spikes/llm -- report` aus den Rohantworten in `measurements/*.json`.
Nicht von Hand ändern; Notizen stehen in `notes.md` und werden unten angehängt.

## Läufe

| Lauf | Modell | Kontext | Threads | Kürzung Kopf+Fuß | Beispiele | ohne Präfix-Cache | Speicher laut Ollama |
|---|---|---|---|---|---|---|---|
| cache-ministral-3 | `ministral-3:8b-instruct-2512-q4_K_M` | 4096 | 4 | 4500+1500 | 0 | nein | 6,0 GB |
| cold-ministral-3 | `ministral-3:8b-instruct-2512-q4_K_M` | 4096 | 4 | 4500+1500 | 0 | ja | 6,0 GB |
| cpu2-ministral-3 | `ministral-3:8b-instruct-2512-q4_K_M` | 4096 | 2 | 4500+1500 | 0 | nein | 6,0 GB |
| cpu8-ministral-3 | `ministral-3:8b-instruct-2512-q4_K_M` | 4096 | 8 | 4500+1500 | 0 | nein | 6,0 GB |
| cut-3000+1000-ministral-3 | `ministral-3:8b-instruct-2512-q4_K_M` | 4096 | 4 | 3000+1000 | 0 | nein | 6,0 GB |
| cut-full-ministral-3 | `ministral-3:8b-instruct-2512-q4_K_M` | 8192 | 4 | keine | 0 | nein | 6,7 GB |
| cut-head4000-ministral-3 | `ministral-3:8b-instruct-2512-q4_K_M` | 4096 | 4 | 4000+0 | 0 | nein | 6,0 GB |
| ex3-19-ministral-3 | `ministral-3:8b-instruct-2512-q4_K_M` | 4096 | 4 | 4500+1500 | 3 | nein | 6,0 GB |
| ex3-ministral-3 | `ministral-3:8b-instruct-2512-q4_K_M` | 4096 | 4 | 4500+1500 | 3 | nein | 6,0 GB |
| main-granite3.3 | `granite3.3:8b` | 4096 | 4 | 4500+1500 | 0 | nein | 5,7 GB |
| main-ministral-3 | `ministral-3:8b-instruct-2512-q4_K_M` | 4096 | 4 | 4500+1500 | 0 | nein | 6,0 GB |
| main-qwen2.5 | `qwen2.5:7b-instruct-q4_K_M` | 4096 | 4 | 4500+1500 | 0 | nein | 5,1 GB |
| v1-qwen2.5 | `qwen2.5:7b-instruct-q4_K_M` | 4096 | 4 | 4500+1500 | 0 | nein | 5,1 GB |
| v2-qwen2.5 | `qwen2.5:7b-instruct-q4_K_M` | 4096 | 4 | 4500+1500 | 0 | nein | 5,1 GB |
| v3-qwen2.5-subset | `qwen2.5:7b-instruct-q4_K_M` | 4096 | 4 | 4500+1500 | 0 | nein | 5,1 GB |

## Trefferquote je Feld

„Absender“ und „Art“: normalisierter Vergleich mit Sollwert oder zugelassenem Alias. „Betrag“: Zahl und
Währung. „Titel“ und „Zusammenfassung“: einfache Regeln (Länge, Stichwort, deutsch), siehe `Scoring/`.

| Lauf | n | Absender | Art | Datum | Betrag | Tags ≥ 1 | Titel | Zusammenfassung |
|---|---|---|---|---|---|---|---|---|
| cache-ministral-3 | 22 | 21/22 (95 %) | 19/22 (86 %) | 21/22 (95 %) | 22/22 (100 %) | 21/22 (95 %) | 19/22 (86 %) | 22/22 (100 %) |
| cold-ministral-3 | 6 | 6/6 (100 %) | 6/6 (100 %) | 6/6 (100 %) | 6/6 (100 %) | 6/6 (100 %) | 6/6 (100 %) | 6/6 (100 %) |
| cpu2-ministral-3 | 2 | 2/2 (100 %) | 2/2 (100 %) | 2/2 (100 %) | 2/2 (100 %) | 2/2 (100 %) | 1/2 (50 %) | 2/2 (100 %) |
| cpu8-ministral-3 | 4 | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) |
| cut-3000+1000-ministral-3 | 4 | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) |
| cut-full-ministral-3 | 4 | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) |
| cut-head4000-ministral-3 | 4 | 4/4 (100 %) | 4/4 (100 %) | 3/4 (75 %) | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 4/4 (100 %) |
| ex3-19-ministral-3 | 1 | 1/1 (100 %) | 1/1 (100 %) | 1/1 (100 %) | 1/1 (100 %) | 1/1 (100 %) | 1/1 (100 %) | 1/1 (100 %) |
| ex3-ministral-3 | 11 | 11/11 (100 %) | 11/11 (100 %) | 11/11 (100 %) | 11/11 (100 %) | 10/11 (91 %) | 11/11 (100 %) | 11/11 (100 %) |
| main-granite3.3 | 22 | 22/22 (100 %) | 20/22 (91 %) | 21/22 (95 %) | 21/22 (95 %) | 21/22 (95 %) | 21/22 (95 %) | 18/22 (82 %) |
| main-ministral-3 | 22 | 21/22 (95 %) | 19/22 (86 %) | 21/22 (95 %) | 22/22 (100 %) | 21/22 (95 %) | 19/22 (86 %) | 22/22 (100 %) |
| main-qwen2.5 | 22 | 20/22 (91 %) | 18/22 (82 %) | 21/22 (95 %) | 22/22 (100 %) | 20/22 (91 %) | 18/22 (82 %) | 19/22 (86 %) |
| v1-qwen2.5 | 22 | 22/22 (100 %) | 18/22 (82 %) | 19/22 (86 %) | 20/22 (91 %) | 21/22 (95 %) | 19/22 (86 %) | 22/22 (100 %) |
| v2-qwen2.5 | 22 | 20/22 (91 %) | 19/22 (86 %) | 22/22 (100 %) | 22/22 (100 %) | 22/22 (100 %) | 20/22 (91 %) | 21/22 (95 %) |
| v3-qwen2.5-subset | 7 | 6/7 (86 %) | 6/7 (86 %) | 7/7 (100 %) | 7/7 (100 %) | 4/7 (57 %) | 6/7 (86 %) | 7/7 (100 %) |

## Listen und Schlagwörter

„aus Liste“: Der Sollwert steht in der Liste des Haushalts und das Modell liefert genau diese Schreibweise.
„Tag-Abdeckung“: Anteil der erwarteten Schlagwörter, die geliefert wurden. „Tag-Genauigkeit“: Anteil der
gelieferten Schlagwörter, die erwartet oder zugelassen sind. „vorhandene Tags“: Anteil aus der Liste.

| Lauf | Absender aus Liste | Art aus Liste | Betrag (nur Zahl) | Tag-Abdeckung | Tag-Genauigkeit | vorhandene Tags | gültiges JSON |
|---|---|---|---|---|---|---|---|
| cache-ministral-3 | 11/11 (100 %) | 16/17 (94 %) | 22/22 (100 %) | 95 % | 68 % | 77 % | 22/22 (100 %) |
| cold-ministral-3 | 4/4 (100 %) | 6/6 (100 %) | 6/6 (100 %) | 100 % | 83 % | 67 % | 6/6 (100 %) |
| cpu2-ministral-3 | 2/2 (100 %) | 2/2 (100 %) | 2/2 (100 %) | 100 % | 100 % | 75 % | 2/2 (100 %) |
| cpu8-ministral-3 | 2/2 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 100 % | 88 % | 75 % | 4/4 (100 %) |
| cut-3000+1000-ministral-3 | 3/3 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 100 % | 88 % | 100 % | 4/4 (100 %) |
| cut-full-ministral-3 | 3/3 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 100 % | 88 % | 100 % | 4/4 (100 %) |
| cut-head4000-ministral-3 | 3/3 (100 %) | 4/4 (100 %) | 4/4 (100 %) | 100 % | 88 % | 100 % | 4/4 (100 %) |
| ex3-19-ministral-3 | – | 1/1 (100 %) | 1/1 (100 %) | 100 % | 100 % | 50 % | 1/1 (100 %) |
| ex3-ministral-3 | 10/10 (100 %) | 11/11 (100 %) | 11/11 (100 %) | 91 % | 86 % | 95 % | 11/11 (100 %) |
| main-granite3.3 | 5/11 (45 %) | 13/17 (76 %) | 21/22 (95 %) | 73 % | 70 % | 45 % | 22/22 (100 %) |
| main-ministral-3 | 11/11 (100 %) | 16/17 (94 %) | 22/22 (100 %) | 95 % | 68 % | 77 % | 22/22 (100 %) |
| main-qwen2.5 | 9/11 (82 %) | 15/17 (88 %) | 22/22 (100 %) | 66 % | 64 % | 59 % | 22/22 (100 %) |
| v1-qwen2.5 | 11/11 (100 %) | 15/17 (88 %) | 20/22 (91 %) | 86 % | 55 % | 50 % | 22/22 (100 %) |
| v2-qwen2.5 | 3/11 (27 %) | 15/17 (88 %) | 22/22 (100 %) | 75 % | 44 % | 33 % | 22/22 (100 %) |
| v3-qwen2.5-subset | 0/4 (0 %) | 4/5 (80 %) | 7/7 (100 %) | 57 % | 31 % | 82 % | 7/7 (100 %) |

## Dauer je Dokument

Sekunden je Anfrage ohne das Laden des Modells. „Laden“ ist die Ladezeit vor dem ersten Dokument.

| Lauf | Median | Mittel | Max | ≤ 60 s | Prompt-Tokens (Mittel/Max) | Antwort-Tokens | Prompt tok/s | Antwort tok/s | Laden |
|---|---|---|---|---|---|---|---|---|---|
| cache-ministral-3 | 45,9 s | 51,6 s | 106,4 s | 18/22 (82 %) | 1544 / 2798 | 164 | – | 5,3 | 8,1 s |
| cold-ministral-3 | 62,7 s | 63,5 s | 69,8 s | 1/6 (17 %) | 1417 / 1535 | 160 | 38,7 | 6,0 | 7,3 s |
| cpu2-ministral-3 | 100,6 s | 82,9 s | 100,6 s | 0/2 (0 %) | 1400 / 1428 | 159 | – | 4,3 | 7,6 s |
| cpu8-ministral-3 | 45,2 s | 44,8 s | 67,9 s | 3/4 (75 %) | 1743 / 2798 | 163 | – | 6,5 | 7,6 s |
| cut-3000+1000-ministral-3 | 71,2 s | 66,8 s | 73,1 s | 1/4 (25 %) | 1928 / 2228 | 172 | – | 5,5 | 7,1 s |
| cut-full-ministral-3 | 92,4 s | 79,7 s | 105,3 s | 1/4 (25 %) | 2362 / 3182 | 166 | – | 5,2 | 7,6 s |
| cut-head4000-ministral-3 | 66,3 s | 64,0 s | 71,3 s | 1/4 (25 %) | 1920 / 2197 | 167 | – | 5,5 | 7,3 s |
| ex3-19-ministral-3 | 44,7 s | 44,7 s | 44,7 s | 1/1 (100 %) | 1519 / 1519 | 159 | – | 5,8 | 0,0 s |
| ex3-ministral-3 | 50,0 s | 59,8 s | 94,4 s | 6/11 (55 %) | 1846 / 2875 | 160 | – | 5,5 | 7,9 s |
| main-granite3.3 | 50,0 s | 58,4 s | 141,0 s | 18/22 (82 %) | 1922 / 3642 | 168 | – | 5,1 | 6,2 s |
| main-ministral-3 | 43,2 s | 47,3 s | 92,3 s | 19/22 (86 %) | 1544 / 2798 | 164 | – | 5,9 | 6,1 s |
| main-qwen2.5 | 32,4 s | 36,0 s | 75,2 s | 20/22 (91 %) | 1671 / 3104 | 128 | – | 7,2 | 5,4 s |
| v1-qwen2.5 | 36,1 s | 41,7 s | 95,3 s | 18/22 (82 %) | 1494 / 2927 | 138 | – | 6,0 | 0,0 s |
| v2-qwen2.5 | 35,5 s | 39,9 s | 75,5 s | 20/22 (91 %) | 1638 / 3071 | 142 | – | 6,6 | 0,0 s |
| v3-qwen2.5-subset | 47,3 s | 48,5 s | 56,8 s | 7/7 (100 %) | 1535 / 1762 | 145 | – | 5,5 | 0,0 s |

## Abweichungen je Dokument

### main-granite3.3

- 01-stromrechnung (89 s): Zusammenfassung: „Die Stadtwerke Lindental rechnen für den Zeitraum vom 1. Oktober 2025 bis 30. September 2026 den Stromverbrauch der Familie Beispiel ab. Der zu zahlende Betrag beträgt 84,20 EUR.“
- 03-mietvertrag (141 s): Zusammenfassung: „Dies ist ein Mietvertrag zwischen der Brückner Hausverwaltung GmbH und Frau Erika Beispiel sowie Herrn Jonas Beispiel für eine Wohnung in Lindental. Der Vertrag beginnt am 01.05.2026 und läuft auf unbestimmte Zeit. Die monatliche Miete beträgt 1.150,00 EUR, die aus der Grundmiete, dem Stellplatz, Vorauszahlungen für Betriebskosten und Heizkosten besteht.“
- 05-beitragsanpassung (47 s): Art: „Bescheid“ statt „Schreiben“
- 06-steuerbescheid (76 s): Zusammenfassung: „Dies ist ein Steuerbescheid für das Jahr 2025 für Herrn und Frau Beispiel, der Einkommensteuer und Solidaritätszuschlag festsetzt.“
- 07-arztbrief (57 s): Zusammenfassung: „Der Patient Jonas, geboren am 14.02.1979, wurde wegen gelegentlichem Herzstolpern untersucht. Diagnosen: Arterielle Hypertonie. Keine Anzeichen einer koronaren Herzerkrankung. Empfohlene Therapie: Ramipril 5 mg einmal täglich.“
- 08-werbung (41 s): Datum: „2023-11-16“; Betrag: „799.00 EUR“
- 12-mahnung (44 s): Tags: „Zahlung, Rechnung“ statt „Einkauf“
- 14-behoerdenschreiben (45 s): Art: „Bescheid“ statt „Schreiben“
- 15-kassenbon (45 s): Titel: „Rechnung 0218 - 07.11.2026“ statt „Kassenbon Baumarkt Holzwurm 07.11.2026“

### main-ministral-3

- 05-beitragsanpassung (43 s): Art: „Versicherungsschein“ statt „Schreiben“
- 07-arztbrief (42 s): Titel: „Herzuntersuchung Jonas Beispiel 01.09.2026“ statt „Arztbrief Kardiologie Jonas Beispiel“
- 08-werbung (38 s): Datum: „2023-11-22“
- 10-kuendigungsbestaetigung (33 s): Tags: „Gesundheit, Wohnung“ statt „Fitness|Sport|Fitnessstudio“; Titel: „Mitgliedschaftsende FitPark Lindental 2026“ statt „Kündigungsbestätigung FitPark zum 31.12.2026“
- 15-kassenbon (37 s): Titel: „Baumaterialien 07.11.2026“ statt „Kassenbon Baumarkt Holzwurm 07.11.2026“
- 17-english-booking (37 s): Art: „Schreiben“ statt „Buchungsbestätigung“
- 19-zahnarztrechnung (43 s): Absender: „Dr. Petra Sommerfeld“ statt „Zahnarztpraxis Dr. Keller“
- 22-spendenbescheinigung (42 s): Art: „Schreiben“ statt „Spendenbescheinigung“

### main-qwen2.5

- 03-mietvertrag (75 s): Titel: „Wohnraummieterschließen“ statt „Mietvertrag Wohnung Ahornweg 12“
- 04-versicherungspolice (71 s): Titel: „Versicherungsschein Jonas Beispiel 2026“ statt „Versicherungsschein Privat-Haftpflicht“; Zusammenfassung: „Versicherungsschutz für Jonas Beispiel mit Jahresbeitrag von 78,54 EUR.“
- 05-beitragsanpassung (32 s): Art: „Versicherungsschein“ statt „Schreiben“; Titel: „Versicherungsschein LT-EB 412“ statt „Kfz-Versicherung Beitragsanpassung 2027“
- 06-steuerbescheid (47 s): Titel: „Bescheid 2025 Steuern“ statt „Einkommensteuerbescheid 2025“
- 08-werbung (27 s): Datum: „2023-11-22“
- 09-kontoauszug (37 s): Tags: „Miete, Versicherung“ statt „Bank“; Zusammenfassung: „Kontoauszug vom 31.10.2026 zeigt Miete, Versicherung, Telefonrechnung und Einkäufe.“
- 10-kuendigungsbestaetigung (23 s): Art: „Schreiben“ statt „Kündigung“
- 13-garantieschein (27 s): Art: „Versicherungsschein“ statt „Garantieschein“
- 14-behoerdenschreiben (29 s): Absender: „Stadtwerke Lindental“ statt „Stadt Lindental“
- 17-english-booking (25 s): Art: „Bescheid“ statt „Buchungsbestätigung“; Zusammenfassung: „Bestätigung der Buchung mit Preis von 264 GBP für 3 Nächte.“
- 19-zahnarztrechnung (32 s): Absender: „Dr. Petra Sommerfeld“ statt „Zahnarztpraxis Dr. Keller“
- 20-angebot-maler (34 s): Tags: „Miete, Arbeit“ statt „Wohnung“

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
