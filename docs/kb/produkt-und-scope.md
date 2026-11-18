# Produkt und Scope

Stand: Grilling 2026-11-18.

## Vision
Papier, Mails und Dateien des Haushalts landen an **einem** Ort: durchsuchbar, automatisch
zugeordnet, mit Erinnerung an Fristen – ohne dass ein Dokument das eigene Netz verlässt.

## Zielgruppe
- **MVP:** der eigene Haushalt (mehrere Konten, z. B. Partnerin/Partner).
- Gebaut so, dass eine öffentliche Version für andere Privatleute später möglich ist
  (Open Source, MIT, Setup per Docker Compose).
- **Nicht** im Scope: Firmen, Vereine, Rollen- und Rechtemodelle über „privat markiert“ hinaus.

## Grundsätze
- **Lokal:** Verarbeitung (OCR, LLM) und Speicherung nur auf dem eigenen Server (ADR-0002).
- **Unverlierbar:** Original bleibt ab Werk unverändert (Wahl je PDF, Papierkorb 30 Tage), zusätzlich PDF/A; eingebautes, verschlüsseltes
  Backup mit getesteter Wiederherstellung (ADR-0003, ADR-0008).
- **Ohne App lesbar:** Dateien liegen in einer lesbaren Ordnerstruktur.
- **Ordnung durch Felder und Tags**, nicht durch Ordner.
- **Barrierefrei von Anfang an:** Der Hauptnutzer ist blind bzw. stark sehbehindert. Dunkle,
  kontrastreiche App, Dokumente intelligent invertiert, Screenreader (ADR-0010, ADR-0011).

## MVP-Umfang (1.0)

| Bereich | Inhalt |
|---|---|
| Barrierefreiheit | ganze App dunkel/kontrastreich, Dokumente per WebGL-Shader intelligent invertiert (Farbton bleibt, Fotos original) überall in der App, stufenloser scharfer Zoom, Screenreader; Download immer Original |
| Konten | Haushalt mit mehreren Konten; alle sehen alles, Dokumente optional „privat“ |
| Eingänge | Upload im Browser, Kamera in der PWA (Kantenerkennung, Entzerrung), Scanner-Ordner (SMB), eigene Mail-Adresse per IMAP, iCloud Drive (eigene .NET-Umsetzung) |
| Verarbeitung | OCR → durchsuchbares PDF/A; lokales LLM erkennt Absender, Dokumentart, Datum, Betrag, Fristen, Tags |
| Ordnung & Suche | Felder (Absender, Art, Datum, Betrag) + freie Tags, Volltextsuche, Filter, Korrektur der Zuordnung |
| Scans bearbeiten | drehen, löschen, zusammenfügen, teilen, umsortieren, zuschneiden/entzerren; Stapel-Trennung als Vorschlag; per Tastatur mit Ansagen; Original + Version ab Werk, Wahl je PDF, Papierkorb 30 Tage (ADR-0013) |
| Verträge & Kosten | Vertrag als Objekt mit Dokumenten; LLM-Vorschlag; Kostenübersicht monatlich/jährlich nach Kategorie; Kündigungsfrist → Frist; Preiserhöhung erkennen (ADR-0014) |
| Duplikate | gleiches Dokument aus Mail und Scan wird nur einmal abgelegt |
| Fristen | Zahlungs-/Kündigungsfristen erkennen, Liste offener Fristen |
| Kalender | je Konto iCloud-Kalender per CalDAV; Übernahme je Fristart automatisch / nach Bestätigung (Werk) / nie; Termin nur Titel + Link; einseitig; Alarm-Vorlauf je Fristart (ADR-0012) |
| Home Assistant | eigene Integration (HACS): Sensoren (neue Dokumente, offene Fristen), Erinnerungen über HA-Benachrichtigungen |
| Steuer-Export | Belege eines Jahres nach Kategorie als ZIP + Liste |
| Backup | nächtlich, verschlüsselt, auf NAS und extern; Restore dokumentiert und getestet |
| Sprache | Oberfläche Deutsch und Englisch |

## Nach dem MVP (Ideen)
- Native iOS-App (Teilen-Menü), falls die PWA an Grenzen stößt
- Web Push aus der PWA
- weitere Cloud-Quellen (OneDrive, Nextcloud/WebDAV)
- LLM auf GPU für größere Modelle
- Garantien und Gewährleistung mit Erinnerung
- Fragen an Dokumente in natürlicher Sprache, Vorlesen per lokaler Sprachausgabe

## Bekannte Risiken
- **iCloud Drive:** keine offizielle Server-API, eigene .NET-Umsetzung des inoffiziellen
  Protokolls inkl. 2FA – Spike in M1 (ADR-0005).
- **LLM auf CPU:** Zuordnung dauert pro Dokument ggf. 10–60 s ⚠️ unverifiziert; läuft im Worker
  im Hintergrund.
- **Shader-Darstellung:** Foto-Erkennung und Schwellen im Browser auf schwächeren Handys ⚠️ unverifiziert – Spike in M1.
- **PWA auf iOS:** kein Web Share Target; Kamera per `getUserMedia` funktioniert.
