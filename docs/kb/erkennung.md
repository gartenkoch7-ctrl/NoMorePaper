# Erkennung: lokales LLM ordnet Dokumente ein

Ergebnis des LLM-Spikes (M3, Paket P1, 2026-11-19) zu [ADR-0004](../adr/0004-ocr-und-lokales-llm.md).
Entscheidung: [ADR-0022](../adr/0022-llm-modell.md). Prototyp: [spikes/llm/](../../spikes/llm/README.md),
alle Messwerte: [spikes/llm/results.md](../../spikes/llm/results.md), Rohantworten in `spikes/llm/measurements/`.
Gemessen an **22 selbst geschriebenen Testdokumenten** (20 deutsch, 2 englisch, als OCR-Text mit
typischen Fehlern), auf dem Entwicklungs-PC und **nicht auf dem Server** (siehe „Grenzen“).

## Ergebnis in Kürze
- **Modell:** `ministral-3:8b-instruct-2512-q4_K_M` (Mistral AI, Apache-2.0, 6,0 GB). Ersatz für
  schwächere Server: `qwen2.5:7b-instruct-q4_K_M` (Apache-2.0, 4,7 GB).
- **Aufruf:** Ollama 0.40.16, `/api/chat` mit JSON-Schema in `format`, Temperatur 0. Jede der 175
  Antworten aller Läufe war gültiges JSON.
- **Trefferquote:** Absender 95 %, Dokumentart 86 %, Datum 95 %, Betrag mit Währung 100 %.
- **Dauer:** 43 s je Dokument (Median, 4 CPU-Kerne), lange Dokumente 71–73 s.
- **Speicher:** 6,9 GB von 8 GB. Nur mit `LLAMA_ARG_CACHE_RAM=0`; ohne diese Einstellung wurde das Modell
  einmal vom Kernel beendet (OOM).

## Messaufbau
- Entwicklungs-PC: Windows 11, Rancher Desktop (WSL 2), Intel Core i7-12700KF.
- Ollama im Container **ohne GPU**: `ollama/ollama:0.40.16`, `--memory 8g --memory-swap 8g --cpus 4`,
  `num_thread 4`. Der Server hat eine andere CPU; die Zeiten sind dort neu zu messen.
- Haushalt mit 12 Absendern, 11 Dokumentarten, 17 Schlagwörtern. Bei 11 Dokumenten steht der Absender
  in der Liste, bei 11 nicht; 5 Dokumentarten fehlen in der Liste und müssen neu vorgeschlagen werden.
- Bewertung je Feld durch Regeln (`spikes/llm/Scoring/`), Titel und Zusammenfassung zusätzlich von Hand.

## Modelle im Vergleich
Alle drei: Apache-2.0, geprüft mit `ollama show <modell> --license` und auf der Modellkarte.

| | ministral-3 8B | qwen2.5 7B | granite3.3 8B |
|---|---|---|---|
| Absender | 21/22 (95 %) | 20/22 (91 %) | 22/22 (100 %) |
| Dokumentart | 19/22 (86 %) | 18/22 (82 %) | 20/22 (91 %) |
| Datum | 21/22 (95 %) | 21/22 (95 %) | 21/22 (95 %) |
| Betrag mit Währung | 22/22 (100 %) | 22/22 (100 %) | 21/22 (95 %) |
| mindestens ein passendes Schlagwort | 21/22 (95 %) | 20/22 (91 %) | 21/22 (95 %) |
| passende unter den gelieferten Schlagwörtern | 68 % | 64 % | 70 % |
| vorhandener Absender in Listenschreibweise | 11/11 | 9/11 | 5/11 |
| vorhandene Dokumentart in Listenschreibweise | 16/17 | 15/17 | 13/17 |
| Titel brauchbar (Sichtprüfung) | 21/22 | etwa 15/22 | etwa 19/22 |
| Zusammenfassung (Sichtprüfung) | flüssig, mit Betrag und Frist; 3/22 mit kleinem Sachfehler | knapp, übernimmt OCR-Fehler | richtig, aber lang |
| Dauer Median / Maximum | 43 s / 92 s | 32 s / 75 s | 50 s / 141 s |
| Speicher | 6,9 GB | 5,2 GB | 5,7 GB (Angabe von Ollama) |

- „Absender“ und „Dokumentart“ zählen auch zugelassene Varianten („Nordkom Telefon GmbH“ für
  „Nordkom“). Die Zeile „in Listenschreibweise“ zählt nur die genaue Übernahme.
- Ein Dokument mehr oder weniger ändert jede Quote um 4,5 Prozentpunkte. Die Unterschiede zwischen den
  Modellen bei Absender, Art und Datum liegen in diesem Rauschen; deutlich sind die Unterschiede bei der
  Übernahme der Listen, bei der Sprache und bei der Dauer.

## Anfrage
`POST /api/chat` mit `stream: false`, `format` = Schema (unten), `options`: `temperature 0`, `seed 1`,
`num_ctx 4096`, `num_predict 400`, `num_thread` = Zahl der CPU-Kerne des Containers.

**Systemtext** (gleich für alle Dokumente eines Besitzers; Quelle: `spikes/llm/Prompting/PromptBuilder.cs`):

```text
Du ordnest Dokumente eines privaten Haushalts ein. Du bekommst den Text eines Dokuments, wie ihn die Texterkennung (OCR) geliefert hat. Er kann Erkennungsfehler enthalten: fehlende Umlaute, verwechselte Zeichen (l/I/1, O/0, rn/m, B/ß), zerrissene Tabellen.

Antworte mit genau einem JSON-Objekt mit diesen Feldern:
- correspondent: Absender, also wer das Dokument ausgestellt hat (Firma, Behörde, Praxis, Verein oder Person). Nie der Empfänger. Ohne Rechtsform (GmbH, AG, e.V.), ohne Abteilung, ohne Adresse.
- documentType: Dokumentart als ein Begriff. Sie beschreibt die Form des Dokuments (Rechnung, Vertrag, Bescheid, Schreiben), nicht sein Thema.
- summary: 1 bis 2 deutsche Sätze: Worum geht es, was muss man wissen oder tun (Betrag, Frist).
- title: kurzer deutscher Titel, höchstens 60 Zeichen, der das Dokument von ähnlichen unterscheidet (Gegenstand und Zeitraum oder Jahr). Ohne Adresse, ohne Namen des Empfängers.
- documentDate: Tag, an dem das Dokument ausgestellt wurde, als JJJJ-MM-TT. Fast jedes Dokument hat ein solches Datum: Briefdatum neben dem Ort, Rechnungsdatum, „Auszug vom“, „Erstellt am“, Datum bei der Unterschrift; auch ausgeschrieben wie „22. Juli 2026“. Nicht Fälligkeit, Leistungszeitraum, Vertragsbeginn oder Geburtsdatum. null nur, wenn im ganzen Text kein Ausstellungsdatum mit Jahr steht.
- amount: der wichtigste Geldbetrag als Zahl mit Punkt als Dezimaltrenner (1.234,56 EUR wird 1234.56). Das ist der Endbetrag, der zu zahlen ist oder erstattet wird (brutto, nach Abzug bereits geleisteter Zahlungen); bei Verträgen, Versicherungen und Beitragsänderungen der neue regelmäßige Gesamtbetrag (Gesamtmiete, Jahresbeitrag). Keine Zwischensummen, Nettobeträge, Steuern, Abschläge oder Einzelposten. null, wenn es keinen solchen Betrag gibt (zum Beispiel Arztbrief, Werbung, Kontoauszug, allgemeines Schreiben).
- currency: Währung von amount als ISO-Code (EUR, USD, GBP, CHF). null, wenn amount null ist.
- tags: 1 oder 2 deutsche Schlagwörter zum Thema des Dokuments. Nimm ein vorhandenes Schlagwort, wenn es das Thema wirklich trifft, sonst ein neues (ein Wort). Nicht die Dokumentart, nicht der Absender, kein Ort, kein Name.

Der Haushalt hat bereits die folgenden Einträge. Steht der Absender oder die Dokumentart in der Liste, übernimm genau diese Schreibweise. Passt kein Eintrag, schlage einen neuen, kurzen Namen vor (zum Beispiel eine neue Dokumentart wie Angebot oder Quittung).

Absender: <Namen, getrennt durch „; “>
Dokumentarten: <…>
Schlagwörter: <…>

Antworte auf Deutsch, auch wenn das Dokument in einer anderen Sprache geschrieben ist. Erfinde nichts: Was nicht im Text steht, ist null.
```

**Nachricht des Nutzers:**

```text
Früher bestätigte Dokumente, deren Absender im Text vorkommt (Vorbild für Dokumentart, Schlagwörter und Titel):
{"correspondent":"…","documentType":"…","tags":["…"],"title":"…"}        ← nur wenn es Beispiele gibt

Dokumenttext:
<<<
<gekürzter OCR-Text>
>>>
Gib jetzt das JSON aus. Übernimm für correspondent und documentType die Schreibweise eines vorhandenen Eintrags, wenn einer passt. documentDate ist das Ausstellungsdatum, amount der Endbetrag.
```

## Schema
Ollama macht aus dem Schema eine Grammatik: Die Antwort hat genau diese Felder **in dieser Reihenfolge**.
Typ-Listen mit `null`, `pattern`, `minItems`/`maxItems` und `enum` wurden von Ollama 0.40.16 angenommen.

```json
{
  "type": "object",
  "properties": {
    "correspondent": { "type": ["string", "null"] },
    "documentType":  { "type": ["string", "null"] },
    "summary":       { "type": "string" },
    "title":         { "type": "string" },
    "documentDate":  { "type": ["string", "null"], "pattern": "^[0-9]{4}-[0-9]{2}-[0-9]{2}$" },
    "amount":        { "type": ["number", "null"] },
    "currency":      { "type": ["string", "null"], "pattern": "^[A-Z]{3}$" },
    "tags":          { "type": "array", "items": { "type": "string" }, "minItems": 1, "maxItems": 2 }
  },
  "required": ["correspondent", "documentType", "summary", "title", "documentDate", "amount", "currency", "tags"],
  "additionalProperties": false
}
```

**Die Reihenfolge der Felder entscheidet mit** (gemessen mit qwen2.5, Läufe `v1`, `v2`, `main`):
- Absender und Art zuerst: Das Modell übernimmt die Schreibweise der Liste (11/11 in Version 1, 9/11 im
  Endstand). Steht die Zusammenfassung davor, schreibt es den Namen aus dem Briefkopf ab (nur 3/11).
- Zusammenfassung vor Datum und Betrag: Datum 19 → 21 von 22, Betrag 20 → 22 von 22. Ohne sie davor
  antwortete das Modell `"amount": null` und nannte den Betrag danach in der Zusammenfassung.
- Arrays füllen die Modelle bis zur Obergrenze. Mit 3 Schlagwörtern war das dritte meist geraten, deshalb
  `maxItems: 2`. Ein `enum` der vorhandenen Schlagwörter machte es schlechter (Lauf `v3`).

## Kürzungsregel
1. **Leerraum zusammenziehen:** drei oder mehr Leerzeichen oder Tabs → zwei Leerzeichen; Linien aus sechs
   oder mehr `-`, `_`, `=`, `.` → `---`; drei oder mehr Zeilenumbrüche → zwei. Das spart bei Tabellen
   ein Viertel bis ein Drittel der Zeichen, bei Fließtext 6–8 %.
2. **Ist der Text danach länger als 4 000 Zeichen:** die ersten 3 000 und die letzten 1 000 Zeichen
   behalten, an Zeilenenden geschnitten, dazwischen die Zeile `[… Mittelteil gekürzt …]`.

| Regel (ministral-3) | Mietvertrag | Versicherungsschein | Felder |
|---|---|---|---|
| ganzer Text (7 411 / 6 935 Zeichen), `num_ctx 8192` | 105 s | 92 s | alle richtig |
| Kopf 4500 + Fuß 1500 | 106 s | 93 s | alle richtig |
| **Kopf 3000 + Fuß 1000** | **73 s** | **71 s** | alle richtig |
| nur Kopf 4000 | 71 s | 66 s | Mietvertrag: Datum falsch |

- Der Fuß ist nötig: Beim Mietvertrag steht das Datum bei der Unterschrift. Ohne Fuß nimmt das Modell
  den Mietbeginn.
- 1 000 Zeichen Fließtext sind etwa 310 Tokens und kosten auf 4 Kernen 8 s; Tabellen mit Zahlen bis 530
  Tokens und 14 s. Der ganze Text lohnt nicht und passt mit `num_ctx 8192` nicht in 8 GB (8,4 GB).
- Nicht gemessen: Dokumente über 10 000 Zeichen und Dokumente, deren Betrag nur im Mittelteil steht.

## Beispiele
- Auswahl ohne LLM: Namen der Absenderliste normalisiert im OCR-Text suchen (klein, ohne Akzente,
  Satzzeichen und Rechtsform). Zu den gefundenen Absendern bis zu **3** bestätigte Dokumente, der im
  Text am frühesten genannte Absender zuerst.
- Form: je Beispiel eine JSON-Zeile mit Absender, Art, Schlagwörtern, Titel. **Kein Dokumenttext.** Etwa
  58 Tokens, also 1,5 s je Beispiel.
- Wirkung auf den 11 Dokumenten mit Treffer: Dokumentart 11/11 statt 10/11, passende Schlagwörter 86 %
  statt 73 %, Titel im Muster des Haushalts.
- Nebenwirkung: Schlagwörter des Vorbilds wandern mit, auch wenn das Thema anders ist.

## Zeiten (ministral-3, Sekunden je Dokument, ohne Laden des Modells)

| Lage | 4 Kerne | 2 Kerne | 8 Kerne |
|---|---|---|---|
| eine Seite, Systemtext wiederverwendet | 32–51 (Median aller 22 Dokumente: 43) | 65 | 32–34 |
| eine Seite, Systemtext neu (erste Anfrage, geänderte Listen) | 58–70 | 101 | 45 |
| lang, Kopf 3000 + Fuß 1000 | 71–73 | nicht gemessen | nicht gemessen |
| lang, Kopf 4500 + Fuß 1500 | 85–106 | nicht gemessen | 68 |

- Rund 60 % der Zeit ist die Antwort: 132–190 Tokens bei 5,9 Tokens/s. Der Prompt läuft mit
  38,7 Tokens/s.
- 2 und 8 Kerne: je zwei bis vier Dokumente, mit Prompt-Cache gemessen.
- Laden des Modells: 6–8 s, wenn die Datei im Seiten-Cache liegt. Von der Platte nicht gemessen.
- Ollama verwendet den gleichen Anfang zweier Anfragen wieder. Systemtext und Listen (etwa 900 Tokens,
  gut 20 s) werden so nur einmal gerechnet, solange das Modell geladen bleibt und die Listen gleich sind.

## Speicher
- ministral-3 mit `num_ctx 4096`: **6,93 GB**, über 23 Anfragen gleichbleibend, wenn der Container
  `LLAMA_ARG_CACHE_RAM=0` setzt.
- Ohne diese Variable hebt `llama-server` (der Prozess, den Ollama startet) frühere Prompts im
  Arbeitsspeicher auf. Der Verbrauch stieg auf 7,95 GB und der Kernel beendete den Prozess einmal;
  Ollama antwortet dann mit 500 „model runner has unexpectedly stopped“.
- Platte: Image 8,5 GB, Modell 6,0 GB.

## Grenzen
- **Kleiner, selbst geschriebener Testsatz.** Der Prompt wurde an denselben 22 Dokumenten entwickelt;
  mit echten Scans sind schlechtere Quoten zu erwarten. Keine Handschrift, keine mehrspaltigen Seiten.
- **Falscher Listeneintrag:** Zahnarztrechnung → vorhandener Eintrag einer anderen Ärztin. Das ist
  schlimmer als ein neuer Name, weil es richtig aussieht.
- **Erfundenes Datum:** Werbung ohne Jahr → `2023-11-22` (alle drei Modelle).
- **Dokumentart:** Beitragsanpassung → „Versicherungsschein“; für fehlende Arten nimmt das Modell
  lieber „Schreiben“, als eine neue vorzuschlagen (Spendenbescheinigung, Buchungsbestätigung).
- **Schlagwörter:** Nur etwa zwei Drittel passen. Schwächstes Feld.
- **Zusammenfassung:** 3 von 22 mit kleinem Sachfehler, 5 von 22 mit drei Sätzen statt zwei.
- **Nicht festgelegt:** Betrag beim Kontoauszug (Kontostand oder leer), beim Garantieschein (Kaufpreis
  oder leer), Vorzeichen bei Erstattungen. Die Bewertung lässt jeweils beides gelten.
- **Text im Dokument als Anweisung** („Ignoriere alle Regeln …“) wurde nicht geprüft. Das Schema begrenzt
  die Form, nicht den Inhalt; der Worker behandelt alle Werte als ungeprüfte Daten.

## Vorgaben für den Worker (Spur A)

| Punkt | Wert |
|---|---|
| Modell | `ministral-3:8b-instruct-2512-q4_K_M`, Name aus der Konfiguration |
| Container | `ollama/ollama:0.40.16`, 8 GB, `LLAMA_ARG_CACHE_RAM=0`, `OLLAMA_NUM_PARALLEL=1`, `OLLAMA_MAX_LOADED_MODELS=1`, kein veröffentlichter Port |
| Kontext | `num_ctx 4096`. Größte gemessene Anfrage mit der Kürzungsregel: 2 228 Tokens, dazu bis 200 für die Antwort. Mit Tabellen und Beispielen gerechnet bis etwa 3 400 |
| Weitere Optionen | `temperature 0`, `seed 1`, `num_predict 400`, `num_thread` = CPU-Kerne des Containers, `keep_alive 30m` |
| Zeitlimit | **300 s** je Anfrage. Deckt 2 Kerne, langes Dokument und Laden ab |
| Fehler | HTTP 500, Zeitlimit oder `done_reason` ≠ `stop`: neuer Versuch der Stufe; das Dokument bleibt „Done“ |
| Text | Kürzungsregel oben |
| Listen | alle Dokumentarten; Absender und Schlagwörter zusammen höchstens etwa 150 Namen (⚠️ gerechnet aus dem freien Kontext, nicht gemessen), sonst die meistbenutzten und die im Text gefundenen |
| Beispiele | höchstens 3, Auswahl und Form wie oben |

**Prüfungen nach der Antwort** (jede hätte einen der beobachteten Fehler abgefangen):
- **Absender, der schon in der Liste steht:** Jedes Wort des Namens mit vier oder mehr Buchstaben muss
  normalisiert im OCR-Text vorkommen. Sonst verwerfen. Fängt beide falschen Listeneinträge, lässt alle
  richtigen durch.
- **Datum:** Die Jahreszahl muss im OCR-Text stehen, das Datum darf nicht in der Zukunft liegen. Sonst
  leer. Fängt das erfundene Datum der Werbung.
- **Betrag:** Die Zahl muss im OCR-Text stehen (`84,20` oder `84.20`, auch mit Tausenderzeichen wie
  `1.150,00`), sonst leer. Währung ohne Angabe: `EUR`.
- **Zuordnung zu Listen:** über den normalisierten Namen (ohne Rechtsform und Titel wie „Dr. med.“);
  genaue Schreibweise ist bei qwen2.5 und granite3.3 nicht verlässlich.
- **Schlagwörter:** verwerfen, wenn sie normalisiert der Dokumentart oder dem Absender gleichen.
- **Längen:** Titel höchstens 80 Zeichen, Zusammenfassung höchstens 500, sonst abschneiden.

## Umsetzung im Worker (Spur A, M3)
- **Ablauf:** Nach der OCR geht der Job in die Stufe `Recognition` über (gleiche Zeile in
  `processing_jobs`, Spalten `stage` und `priority`). Direkt nach der OCR prüft der Worker auf
  inhaltliche Duplikate; die Erkennung läuft danach als eigener Job-Durchlauf.
- **Erkennung** (`Application/Recognition/RecognizeDocument`, `RecognitionAttempt`): Listen und
  Beispiele immer aus Sicht des **Besitzers**; Kürzungsregel und Prüfungen wie oben; Namen werden
  über `IClassificationStore.FindOrCreateAsync(Besitzer, …)` zu Einträgen (gleicher normalisierter
  Name unter den sichtbaren, sonst neu); der Titel ersetzt den Dateinamen; `Document.Classify` setzt
  `Recognition = Automatic`.
- **Übersprungen** (Job entfernt, ohne LLM): bestätigte Dokumente, Dokumente ohne Besitzer (M1 vor
  der Einrichtung) und Dokumente ohne Text. Die beiden letzten bekommen schon beim Abschluss der OCR
  keinen Erkennungs-Job.
- **Zeitlimits beim Start:** `/api/show` 30 s, `/api/pull` 2 h; danach neuer Versuch des Worker-Starts.
- **Prüfungen, genauer als oben:** Ein Absendername ohne Wort mit vier Buchstaben (`AOK`) muss als
  ganzes Wort im Text stehen. Die Jahreszahl zählt auch als kurzes Datum (`19.11.26`). Ein Datum darf
  höchstens einen Tag nach dem heutigen UTC-Datum liegen (Zeitzonen). Ein ganzer Betrag ohne
  Nachkommastellen (`1150`) zählt nur neben einer Währung (`1.150 €`, `EUR 1150`), sonst wären
  Hausnummern und Jahreszahlen ein Beleg.
- **Modell bereit:** Erkennungs-Jobs werden erst geholt, wenn der Worker-Start das Modell bestätigt
  hat (`RecognitionReadiness`); bis dahin läuft nur die OCR. So verbrauchen neue Uploads ihre Versuche
  nicht, während das Modell beim ersten Start lädt.
- **Listen im Prompt stabil:** Passen alle Absender in die Grenze, bleibt die Reihenfolge alphabetisch,
  damit der Systemtext gleich bleibt und Ollama ihn wiederverwendet.
- **Bestätigung während der Erkennung:** Nach der Antwort des Modells liest der Worker das Dokument
  neu; ist es inzwischen bestätigt, wird die Antwort verworfen und es entstehen keine Listeneinträge
  (Ereignis 5205). Nur eine Änderung in den Millisekunden bis zum Abschluss verwirft die Antwort,
  nachdem ihre neuen Einträge angelegt sind; sie bleiben unbenutzt und nur für den Besitzer sichtbar. Der Abschluss sperrt danach die Dokumentzeile (vor dem Job, gleiche Reihenfolge wie
  beim Löschen) und übernimmt die Angaben nur, wenn das Dokument noch nicht bestätigt ist.
  ⚠️ Bestätigen und Bearbeiten (Spur B) laden das Dokument ohne Sperre: Fällt ein Bestätigen genau in
  die Millisekunden des Abschlusses, kann ein Dokument als bestätigt gelten, dessen Angaben vom
  Modell stammen. Abhilfe bei der Integration: Zeile in Bestätigen/Bearbeiten mit `FOR UPDATE` laden
  oder ein Concurrency-Token (`xmin`).
- **Fehler:** Zeitlimit, HTTP-Fehler, nicht erreichbarer Dienst oder `done_reason` ≠ `stop` → neuer
  Versuch nach 1, 5 und 30 Minuten; nach 4 Versuchen wird nur der Job gelöscht, das Dokument bleibt
  „Done“ mit `Recognition = None`. Als Grund wird nur Ausnahmetyp bzw. HTTP-Status gespeichert.
- **Listen im Prompt:** alle Dokumentarten; höchstens 100 Absender (die im Text genannten zuerst)
  und 50 Schlagwörter. Abweichung von „die meistbenutzten“: Der Store liefert keine Nutzungszahlen,
  aufgefüllt wird alphabetisch.
- **Inhaltliche Duplikate** (`FindContentDuplicate`, `PostgresDuplicateFinder`): `pg_trgm`-Ähnlichkeit
  ≥ 0,9 per `%`-Operator (`set_config('pg_trgm.similarity_threshold', …, true)` = `SET LOCAL`) gegen
  fertige Dokumente, die der Besitzer sehen darf; das ähnlichste, bei Gleichstand das älteste; nie ein
  Dokument, das schon als Duplikat dieses Dokuments markiert ist (der Hinweis zeigt nur in eine Richtung). Texte
  unter **50 Zeichen** werden nicht verglichen (Fotos mit wenigen Wörtern). Läuft nur nach der OCR,
  nicht für den Bestand.
- **Bestand:** Beim Worker-Start erst das Modell sicherstellen (`/api/show`, fehlt es: `/api/pull`),
  dann `EnqueueBacklog` (Priorität 10) für alle fertigen Dokumente mit Text, Besitzer und
  `Recognition = None` ohne Job. Beide Schritte werden wiederholt (30 s, verdoppelt bis 10 min), bis
  sie klappen. So verbraucht der Bestand seine Versuche nicht, während das Modell noch lädt. Dokumente,
  deren Erkennung aufgegeben wurde, kommen beim nächsten Worker-Start wieder dran.
- **Konfiguration** (`NoMorePaper:Recognition:…`, Umgebung `NoMorePaper__Recognition__…`):
  `Endpoint` (Standard `http://ollama:11434`), `Model` (Standard ministral-3, siehe oben), `Timeout`
  (Sekunden oder `hh:mm:ss`, Standard 300 s, höchstens 10 min wegen der Job-Sperre von 15 min),
  `NumThread` (Standard 4, 0 = Ollama wählt). In
  `docker-compose.yml` über `.env`: `NOMOREPAPER_LLM_MODEL`, `NOMOREPAPER_LLM_THREADS`.
- **Compose:** Dienst `ollama` (`ollama/ollama:0.40.16`, `mem_limit`/`memswap_limit` 8 GB,
  `LLAMA_ARG_CACHE_RAM=0`, `OLLAMA_NUM_PARALLEL=1`, `OLLAMA_MAX_LOADED_MODELS=1`, Volume `ollama`,
  kein veröffentlichter Port). Für den Download braucht der Ollama-Container einmal Internet; es
  gehen dabei keine Dokumentdaten hinaus.
- **Tests:** ohne echtes Ollama (Fake-`IRecognizer`, Fake-HTTP-Handler für `OllamaRecognizer`).
  `OllamaLiveTests` läuft nur, wenn `NOMOREPAPER_TEST_OLLAMA` auf ein Ollama mit geladenem Modell
  zeigt (z. B. ein eigener Container auf `127.0.0.1:11500` mit dem Volume des Spikes); 2026-11-19
  lokal grün, 55 s mit Laden des Modells.

## Offene Punkte
- Inhaltliche Duplikate: Dauer der Trigramm-Suche mit sehr langen Texten (Verträge mit vielen Seiten)
  und großem Archiv nicht gemessen; ggf. nur Anfang des Textes vergleichen. Monatliche Rechnungen
  desselben Absenders können über 0,9 ähnlich sein (Schwelle am echten Bestand prüfen).
- Ein Ausfall von Ollama länger als 36 Minuten verbraucht alle Versuche der wartenden Dokumente; sie
  kommen erst beim nächsten Worker-Start über den Bestand wieder dran. Dokumente, deren Erkennung immer
  scheitert, laufen bei jedem Worker-Start erneut vier Versuche.
- Zeiten und Speicher auf dem Server messen; danach Zeitlimit und Kriterium „≤ 60 s“ festlegen.
- Kürzungsregel, Beispiele und Prüfungen wurden mit Prompt-Cache gemessen bzw. nur nachgerechnet, nicht
  als Code erprobt.
- Zusammenfassung hart auf zwei Sätze begrenzen (geschätzt 5 s weniger) wurde nicht mehr gemessen.
- `num_thread` weglassen wurde nicht gemessen.
