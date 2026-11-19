# Testumgebung

## Automatisch geprüft

- **Entwicklungsrechner:** Windows 11 mit Docker (Rancher Desktop, WSL).
- **Browser:** Playwright Chromium, headless, für die automatischen Tests.
- **Barrierefreiheit (axe-core):** WCAG 2.0, 2.1 und 2.2, Stufen A und AA, dazu Best Practices und
  `color-contrast-enhanced`. Geprüft werden alle Seiten in DE und EN.
- **Tastatur:** Skip-Link, Tab-Reihenfolge, Fokusrahmen mindestens 3 px.
- **Datenbank:** Testcontainers mit `postgres:17-alpine`. Jeder Test hat eine eigene Datenbank;
  `Infrastructure.Tests` verbindet sich deshalb **ohne Pooling**. Ein Pool je Datenbank hält seine
  Verbindungen bis zum Ende des Laufs offen und sprengt ab etwa 80 Datenbanken die 100 Verbindungen
  des Servers (`53300: too many clients`, seit M3).

Befehle und Ablauf: [build-und-tooling.md](build-und-tooling.md).

## Nicht automatisch geprüft

Diese Punkte brauchen eine Sichtprüfung mit einem Screenreader: NVDA unter Windows, VoiceOver auf
dem iPhone.

- **Skip-Link:** Ansage des Skip-Links, und die Landung in `main`.
- **Sprachknöpfe:** Ansage „Deutsch/English, Umschalter, gedrückt“, in der Gruppe „Sprache“.
- **Sprachwechsel:** Ansage nach dem Neuladen.
- **Lade- und Fehlertext:** zweisprachig in `index.html`.
- **Fokus nach Navigation:** Der Fokus landet auf der H1.
- **Fokusrahmen:** Der gelbe Rahmen um den gelb gefüllten Knopf ist sichtbar.
- **PWA-Installation:** auf iOS und Android.

Begründung: axe-core prüft nicht, ob Ansagen sinnvoll sind (siehe [AGENTS.md §2.5](../../AGENTS.md)).
