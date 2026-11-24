# ADR-0027: Split-Teile ohne erneute Trennung, Anhängen behält die Fassung

- **Status:** Angenommen
- **Datum:** 2026-11-24

Ergänzt [ADR-0025](0025-scan-bearbeitung-umsetzung.md); entschieden vom Nutzer bei der Integration von M4b.

## Kontext
Zwei Lücken fielen erst beim Zusammenführen der Spuren auf:

1. **Split-Teile:** Ein Teil aus einem Split ist ein neues Dokument und läuft durch OCR. Ist es mehrseitig und
   ohne eigene Textebene, landet es wieder in der Stapel-Trennung. Wer über „Anpassen“ Seiten bewusst
   zusammengelassen hat, bekäme denselben Trennvorschlag erneut.
2. **Anhängen unter „Nur Original“:** Die Versionsregel baut das Archiv bei „Nur Original“ aus dem Original.
   Beim Anhängen kämen die Seiten des angehängten Dokuments damit nicht ins Ergebnis; das angehängte Dokument
   liegt im Papierkorb und wäre nach 30 Tagen endgültig weg.

## Entscheidung
1. Jedes Teil eines Splits merkt sich sein Ausgangsdokument (`Document.SplitFrom`, Spalte `split_from_id`).
   Nach der OCR geht ein solches Teil direkt zur Erkennung, nie in die Stufe `Separation`.
2. Ein Anhängen behält immer die bearbeitete Fassung. Steht das Dokument auf „Nur Original“, stellt das
   Anhängen es im selben Commit auf „Beides behalten“ um. Die Rückfrage zum Anhängen sagt das vorher an.

## Begründung
1. Die Seiten eines Teils hat der Nutzer schon festgelegt; ein neuer Vorschlag wäre nur Lärm. Eine
   Markierung am Dokument überlebt Neustarts und Wiederholungen, ein Flag nur am Auftrag nicht.
2. Verworfen: Anhängen bei „Nur Original“ ablehnen. Das wäre einfacher, verlangt aber einen Umweg über
   „Fassungen“. Der Nutzer wollte, dass Anhängen immer klappt und nichts verloren geht.

## Folgen
- Neue Spalte `split_from_id` (Migration `SplitParts`); Tests in Domain, Warteschlange und Split-Ablauf.
- `VersionRule` hat eine Ausnahme für `Append`; Log 5516 meldet die Umstellung.
- Offen: Der Erklärtext zu „Nur Original“ (Konto, Fassungen) nennt die Ausnahme noch nicht; das braucht zuerst
  eine Änderung in Stitch.
