# ADR-0026: Optik nach dem Stitch-Design-System, Fokus durch die Bildschirmlupe

- **Status:** Angenommen
- **Datum:** 2026-11-22
- **Ergänzt:** [ADR-0010](0010-barrierefreiheit-grundregel.md) (bleibt gültig bis auf die Optik-Werte)

## Kontext
ADR-0010 verlangt „große Schrift und deutlichen Fokusrahmen“. Umgesetzt war das bis M4 als 18 px
Grundschrift, Gelb `#FFD60A` als Akzent und ein 3 px gelber Fokusrahmen an jedem Element. Für M4u
liegen Entwürfe aus Google Stitch vor (Design-System „Obsidian Slate“: dunkel, monochrom, Geist und
JetBrains Mono, Grundschrift 14 px). Der Hauptnutzer hat die Entwürfe angesehen und als für ihn
gut lesbar und barrierefrei genug freigegeben.

## Entscheidung
- Die **Optik** (Farben, Schriftgrößen, Abstände, Radien, Schriften) kommt **unverändert** aus dem
  Stitch-Design-System; Abschrift in [KB oberflaeche.md](../kb/oberflaeche.md). Keine Anhebung auf
  größere Schrift, kein Gelb.
- **Kein eigener Fokusrahmen.** Fokus sieht aus wie im Entwurf (Eingabefeld: Rand wird weiß), sonst
  bleibt die Darstellung des Browsers (MudBlazor schaltet sie an Links und Knöpfen ab; `base.css`
  stellt sie mit `outline: revert` wieder her, damit WCAG 2.4.7 auch ohne Lupe erfüllt ist). Die Bildschirmlupe des Nutzers (z. B. ZoomText) zeichnet den
  Fokus selbst.
- Die Schriften Geist und JetBrains Mono werden **lokal** ausgeliefert (SIL Open Font License 1.1,
  Lizenzdatei neben der Schrift; ADR-0002: keine Google Fonts).
- Unverändert gilt aus ADR-0010: dunkle Oberfläche, vollständig per Tastatur und Screenreader
  bedienbar, Zoom des Browsers nie gesperrt, nie Information nur über Farbe, jedes Bedienelement mit
  zugänglichem Namen, Dokumente intelligent invertiert.

## Begründung
Der Nutzer entscheidet über seine eigene Lesbarkeit; er arbeitet mit Bildschirmlupe, die Schrift
vergrößert und Fokus hervorhebt. Ein zusätzlicher gelber Rahmen der App wäre doppelt und bricht das
freigegebene schwarz-weiße Design.

Verworfen: Stitch-Werte auf 20 px und Gelb anheben (Nutzer lehnt ab); Systemschrift statt Geist
(weicht vom Entwurf ab).

## Folgen
- Die UI-Tests prüfen nicht mehr die Breite des Fokusrahmens, weiter aber Tab-Reihenfolge und Fokusziel.
- axe prüft weiter WCAG A/AA (Kontrast 4,5 : 1); die AAA-Regel (7 : 1) entfällt, weil einige Grautöne des
  Designs darunter liegen (siehe KB oberflaeche.md, Abschnitt „Kontrast“). Auf Wunsch des Nutzers passen
  sich die Tests dem Design an, nicht umgekehrt.
- Neue Seiten folgen dem [UI-Prozess](../kb/ui-prozess.md).
