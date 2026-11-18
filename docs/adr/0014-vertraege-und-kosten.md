# ADR-0014: Verträge als eigenes Objekt mit Kosten und Kündigungsfristen

- **Status:** Angenommen
- **Datum:** 2026-11-18

## Entscheidung
- Ein **Vertrag** ist ein eigenes Objekt: Anbieter, Kategorie (Versicherung, Energie, Abo …),
  Betrag mit Intervall, Laufzeit und Kündigungsfrist. Alle zugehörigen Dokumente (Police,
  Beitragsrechnungen, Kündigung) hängen daran.
- Das **LLM schlägt** „neuer Vertrag“ oder „zu Vertrag X“ **vor**, der Nutzer bestätigt.
- **Kosten:** Das LLM liest Betrag und Intervall aus dem neuesten Vertragsdokument. Änderungen
  erscheinen als Vorschlag, der Nutzer bestätigt sie und kann jeden Wert von Hand überschreiben.
- **Kostenübersicht:** monatlich und jährlich, nach Kategorie, mit Verlauf über die Jahre.
- **Kündigungsfrist:** wird aus Laufzeit und Frist berechnet und als Frist der Fristart
  „Kündigung“ angelegt. Damit gelten Kalender und HA (ADR-0012, ADR-0007).
- **Preiserhöhung:** Steigt der Betrag in einem neuen Schreiben, gibt es einen Hinweis mit altem
  und neuem Betrag sowie dem möglichen Sonderkündigungsrecht.

## Verworfen
Kosten nur als Felder am Dokument (keine Gesamtsicht), vollautomatische Übernahme, Garantien
(vorerst nicht gewünscht).

## Folgen
Die Berechnung von Fristen und Kosten ist reine Domain-Logik mit Unit-Tests. Beträge werden als
Dezimalzahl mit Währung gespeichert.
