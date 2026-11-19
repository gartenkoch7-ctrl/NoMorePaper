# ADR-0023: Gepflegte Listen, Sichtbarkeit der Einträge und Bearbeitungsrecht

- **Status:** Angenommen
- **Datum:** 2026-11-19
- **Ergänzt:** ADR-0002, ADR-0004, ADR-0022

## Kontext
Ab M3 ordnet das lokale LLM jedes Dokument ein: Absender, Dokumentart, Datum, Betrag, Tags, Titel und
Zusammenfassung. Dafür braucht es Listen (Absender, Dokumentarten, Tags), die das LLM nutzt und die der
Haushalt pflegt. Dokumente können privat sein (M2). Ein Listeneintrag, ein Lernbeispiel oder ein
Duplikat-Hinweis darf deshalb nichts über ein fremdes privates Dokument verraten (ADR-0002). Außerdem
muss klar sein, wer die Angaben eines Dokuments ändern und wer Listeneinträge umbenennen oder
zusammenführen darf.

## Entscheidung
- **Gepflegte Listen** für Absender, Dokumentarten und Tags. Das LLM wählt einen vorhandenen Eintrag
  oder schlägt einen neuen vor. Der Abgleich läuft über den normalisierten Namen (`ClassificationName`),
  sonst entsteht ein neuer Eintrag. Tags sind frei, vorhandene werden bevorzugt.
- **Automatisch übernehmen:** Die Werte des LLMs gelten sofort, das Dokument steht auf
  `Recognition = Automatic` und erscheint unter „Zu prüfen“ (nur für Nutzer, die es bearbeiten dürfen).
  Bestätigen oder Bearbeiten setzt `Confirmed`. Die Kennzeichnung „automatisch erkannt“ steht als Text
  in der Oberfläche.
- **Sichtbarkeit der Listeneinträge** (eine Stelle: `VisibilitySql`): Ein Eintrag ist für einen Nutzer
  sichtbar, wenn ein Dokument ihn verwendet, das der Nutzer sehen darf (eigene und alle nicht
  privaten), **oder** wenn kein Dokument ihn verwendet und der Nutzer ihn angelegt hat. Ein Eintrag aus
  privaten Dokumenten bleibt damit privat; sobald ein geteiltes Dokument ihn nutzt, sehen ihn alle.
  Macht der Besitzer das Dokument privat, verschwindet der Eintrag für andere wieder.
- **Bearbeitungsrecht je Dokument** (`Document.CanEdit`): der Besitzer und die vom Besitzer einzeln
  ausgewählten Bearbeiter. Nur der Besitzer setzt die Bearbeiter. Solange das Dokument **privat** ist,
  haben Bearbeiter kein Recht; es kehrt mit dem Teilen zurück. Ein Dokument ohne Besitzer (M1) kann
  niemand bearbeiten.
- **Umbenennen und Zusammenführen** eines Listeneintrags nur für Nutzer, die **jedes** Dokument
  bearbeiten dürfen, das einen betroffenen Eintrag verwendet (`EntryRightsSql`, eine SQL-Prüfung). Sonst
  403. Die Einträge sind bis zum Ende der Transaktion gesperrt, damit zwischen Prüfung und Änderung kein
  Dokument dazukommt. Ein Eintrag, den der Nutzer nicht sehen darf, gilt als nicht gefunden (404).
- **Duplikatprüfung und Lernbeispiele nutzen nur Dokumente, die der Besitzer bzw. Hochladende sehen
  darf.** Das gilt für die Listen im Prompt, die bis zu 3 bestätigten Beispiele desselben Absenders und
  beide Duplikatprüfungen.
- **Duplikate:** Dieselbe Datei (SHA-256 des Originals) unter den für den Hochladenden sichtbaren
  Dokumenten wird beim Upload mit **409** und Link zum vorhandenen Dokument abgelehnt. Inhaltlich
  gleiche Dokumente (Trigramm-Ähnlichkeit mit `pg_trgm` ≥ 0,9, nach der OCR, nur gegen für den Besitzer
  sichtbare Dokumente) bekommen „mögliches Duplikat“ (`PossibleDuplicateOf`) und stehen unter „Zu
  prüfen“. Der Hinweis wird nur gezeigt, wenn der Betrachter das andere Dokument sehen darf.
  Auflösen: „Behalten“ (Hinweis entfernen) oder „Löschen“ (nur Besitzer, nur mit gesetztem Hinweis).

## Begründung
- **Sichtbarkeit an der Nutzung festmachen** statt an einem Besitzer pro Eintrag: Ein Eintrag gehört
  keinem, er ist so sichtbar wie die Dokumente, die ihn brauchen. Das verhindert Verrat über „Absender
  existiert schon“ und braucht keine Pflege von Hand. Der unbenutzte, selbst angelegte Eintrag bleibt
  sichtbar, damit man ihn vor dem ersten Gebrauch auswählen kann.
- **Rechte über alle betroffenen Dokumente:** Wer einen Eintrag umbenennt oder zusammenführt, ändert
  Dokumente anderer. Das darf nur, wer jedes davon ohnehin ändern dürfte; sonst könnte man fremde
  Angaben über die Liste umschreiben.
- **Duplikate nur über Sichtbares:** „Schon vorhanden“ oder ein Beispiel aus einem fremden privaten
  Brief würde dessen Existenz und Inhalt verraten.

### Verworfene Alternativen
- **Globale Listen für alle:** einfach, aber ein privater Absender (z. B. Arztpraxis) wäre für alle
  sichtbar.
- **Listen je Nutzer:** kein Verrat, aber der Haushalt hätte mehrere Schreibweisen desselben Absenders;
  Filter und Zusammenführen über den Haushalt wären unmöglich.
- **Besitzer je Eintrag mit eigener Freigabe:** zusätzliche Verwaltung, die niemand pflegt.
- **Alle Mitglieder dürfen alles bearbeiten:** verträgt sich nicht mit privaten Dokumenten und
  Verantwortung des Besitzers.
- **Erst nach Bestätigung übernehmen:** Dokumente blieben ohne Angaben, Filter und Suche wären bis zur
  Prüfung leer; für einen blinden Nutzer wäre die Prüfpflicht eine Dauerlast.
- **Duplikate über alle Dokumente prüfen:** verrät fremde private Dokumente.

## Folgen
- Die Sichtbarkeit steht in `VisibilitySql`, die Rechteregel in `EntryRightsSql`; Abfragen, die Einträge
  liefern, müssen eine davon nutzen.
- Ändert sich die Sichtbarkeit eines Dokuments (privat/geteilt), ändern sich Listen, Beispiele und
  Duplikat-Hinweise mit.
- Das Recht der Bearbeiter hängt am Teilen; wird ein Dokument privat, bleiben ihre Einträge in der
  Auswahl, sie können aber nichts mehr ändern.
- Zwei Nutzer können getrennte Einträge mit gleichem Namen haben, solange beide nur in privaten
  Dokumenten stehen; erst nach dem Teilen lassen sie sich zusammenführen.
- Datenschutz-Tests belegen die Regeln: `EntryVisibilityTests`, `ClassificationStoreTests`,
  `EntryMergeTests`, `DuplicateFinderTests`, `RecognitionVisibilityTests`, `DuplicateApiTests`,
  `ListsApiTests`.

## Nachtrag 2026-11-19: Erkennung geteilter Dokumente
**Befund:** Die Erkennung nahm Listen und Beispiele aus Sicht des Besitzers, auch für ein geteiltes
Dokument. So konnte ein Eintrag, den bisher nur private Dokumente des Besitzers verwenden (z. B. Tag
„Psychotherapie“), an ein geteiltes Dokument geraten und damit für den ganzen Haushalt sichtbar
werden. Das widerspricht der Sichtbarkeitsregel oben: Ein Eintrag wird nur dann geteilt, wenn eine
Person ihn bewusst an ein geteiltes Dokument hängt.

**Entscheidung:**
- Jede Abfrage der Listen und Beispiele nennt ihr Publikum (`EntryAudience`): `Account` (Regel oben)
  oder `Household` (nur Einträge, die ein **nicht privates** Dokument verwendet; Beispiele nur aus nicht
  privaten Dokumenten). Die Regel steht weiter an einer Stelle, `VisibilitySql.Entries`.
- Die Erkennung eines **geteilten** Dokuments nutzt `Household` für Prompt-Listen, Beispiele und
  `FindOrCreateAsync`; die eines privaten Dokuments weiter `Account`. Personen (Listenseite,
  Bearbeiten) nutzen `Account`: Sie wählen einen Eintrag bewusst.
- Passt der Name nur zu einem privaten (oder noch unbenutzten) Eintrag, legt die Erkennung einen
  **neuen** Eintrag mit demselben Namen an. Der Index auf `normalized_name` ist nicht eindeutig
  (zwei gleichnamige Einträge sind schon heute erlaubt), es gibt also keine Kollision; den Wert
  wegzulassen war nicht nötig. Der Name selbst stammt dann aus dem Text des geteilten Dokuments, nicht
  aus der privaten Liste.
- Wird ein privates Dokument während der Erkennung geteilt, wird die Antwort verworfen und der Job
  sofort neu eingereiht (Versuch zählt nicht): beim zweiten Lesen nach der Antwort des Modells und
  noch einmal beim Abschluss unter der Zeilensperre, so dass auch ein Teilen kurz vor dem Abschluss
  nichts übernimmt.

**Folgen:**
- Unbenutzte, selbst angelegte Einträge werden für geteilte Dokumente nicht wiederverwendet; ein
  Doppel lässt sich unter „Listen“ zusammenführen.
- Tests: `EntryVisibilityTests`, `ClassificationStoreTests`, `RecognitionExamplesTests`,
  `RecognitionVisibilityTests`, `DocumentChangeLockTests`.

## Nachtrag 2026-11-19: Weitergabe eines Dokuments unklarer Herkunft (ADR-0024)

**Anlass:** Der Verwalter kann ein Dokument unklarer Herkunft an ein anderes Konto weitergeben. Seine
Angaben können Einträge seiner **privaten** Listen tragen; `FieldsReader` zeigt benutzte Einträge ohne
Sichtbarkeitsprüfung an. Das neue Konto (und bei einem geteilten Dokument der ganze Haushalt) sähe sie.

**Entscheidung:**
- Nicht bestätigte Angaben werden bei der Weitergabe verworfen, die Erkennung läuft neu mit der Sicht des
  neuen Besitzers (schon in Schritt 0 von M4).
- **Bestätigte** Angaben bleiben, aber ohne jeden Absender, jede Art und jedes Tag, den die **Zielsicht**
  nicht sieht: `Account` des neuen Besitzers, wenn das Dokument privat bleibt, `Household`, wenn es geteilt
  wird – dieselbe Wahl wie bei der Erkennung. Geprüft wird vor dem Speichern, das Dokument zählt also noch
  als privates des Verwalters und macht seine eigenen Einträge nicht sichtbar.
- Datum, Betrag, Titel und Zusammenfassung bleiben; sie stammen aus dem Text, nicht aus den Listen.

**Tests:** `OwnerCheckTests`, `JobQueueHandOverTests`, `DocumentIntakeTests`.
