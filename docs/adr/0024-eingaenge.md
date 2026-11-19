# ADR-0024: Eingänge – Besitzer, Bibliotheken, Samba und Spam-Prüfung

- **Status:** Angenommen
- **Datum:** 2026-11-19
- **Ergänzt:** [ADR-0005](0005-icloud-eigene-dotnet-umsetzung.md), [ADR-0009](0009-eingaenge-und-mail-adresse.md)
  (bleiben gültig)

## Kontext
M4 bringt die Eingänge aus ADR-0009: Kamera in der PWA, Scanner-Ordner (SMB), eigene Mail-Adresse
(IMAP) und iCloud Drive. Hintergrund-Eingänge haben keinen angemeldeten Menschen; Mail bringt Spam;
die Kamera muss für einen blinden Hauptnutzer bedienbar sein (ADR-0010). Grilling 2026-11-19.

## Entscheidung
- **Besitzer:** Jedes Konto hat einen eigenen Eingang – Unterordner im Scanner-Ordner, hinterlegte
  Absender-Adressen für Mail (eine Adresse gehört genau einem Konto), eigene Apple-ID für iCloud.
  Nicht Zuordenbares gehört dem **Verwalter**, ist **privat**, bis er es geprüft hat, und steht in
  seinem „Zu prüfen“ (`NeedsOwnerCheck`); er kann es behalten oder an ein Konto weitergeben (dann gilt
  dessen Privat-Einstellung). Ein Mail-Absender zählt nur mit bestandenem SPF/DMARC laut
  `Authentication-Results` des eigenen Mailservers.
- **Privat/geteilt:** Einstellung je Konto für alle Eingänge, ab Werk geteilt.
- **Gemeinsame Annahme:** `IntakeDocument` nimmt mit ausdrücklichem Besitzer an; der Browser-Upload
  nutzt sie nach der Kontoprüfung. Eine Datei, die der Besitzer schon hat, wird bei
  Hintergrund-Eingängen still verworfen.
- **Kamera:** OpenCV.js (Apache-2.0) lokal im Browser für Kanten und Entzerrung, nur auf der
  Kamera-Seite geladen. Führung über seltene Ansagen, Töne wie eine Einparkhilfe, Vibration (wo der
  Browser sie erlaubt) und Auto-Auslöser. Gespeichert wird nur die entzerrte Seite; der Server fügt die
  Seiten mit **PDFsharp** (MIT) zu einem PDF.
- **Scanner-Ordner:** eigener **Samba-Container** in Compose, nur SMB2/3, Port 445 nur an einer
  konfigurierten LAN- oder Tailscale-Adresse. Samba steht unter GPL; es läuft als **eigenständiges
  Programm in eigenem Container**, wird nicht gelinkt und nicht mit unserem Code verteilt. Die Regel
  „keine Pakete mit Copyleft-Lizenz“ (AGENTS.md §3) gilt für Abhängigkeiten unseres Codes, nicht für
  getrennt laufende Dienste. Nach dem Einlesen wird die Datei gelöscht.
- **Mail:** **MailKit** (MIT). Der Verwalter richtet das Postfach in der App ein; das Passwort liegt
  nur mit Data Protection verschlüsselt in Postgres (ADR-0015). Abruf alle 5 Minuten.
  Das **lokale LLM** (ADR-0022, gleiches Ollama) prüft Mails unbekannter Absender auf Spam und
  entscheidet bei Mails ohne Anhang, ob sie ein Dokument sind; für alle Mails liefert es einen kurzen
  Hinweis „Aus der Mail“. Spam wird nicht abgelegt und in den Ordner `NoMorePaper-Spam` verschoben;
  eine von dort zurückgeschobene Mail wird ohne Prüfung angenommen (Hash der Message-ID gemerkt).
  Mails ohne Anhang werden als Text-PDF (PDFsharp) abgelegt.
- **iCloud:** wie ADR-0005, je Konto, nur lesen, alle 15 Minuten; Umsetzung erst nach bestandenem
  Test (ADR-0018).

## Verworfen
- Alle Hintergrund-Dokumente an den Verwalter (verrät privaten Haushaltsmitgliedern nichts, aber jedes
  Dokument müsste umgehängt werden).
- Kanten nur auf dem Server (keine Führung während des Fotografierens), eigene Kantenerkennung
  (unzuverlässig bei Schatten).
- Mailtext immer als PDF ablegen (viel Spam und Newsletter im Archiv); nur Anhänge (Rechnungen im
  Mailtext gingen verloren).
- HTML-Mails mit eingebautem Chromium drucken (~300 MB größerer Worker, Bilder aus dem Netz).
- Zugangsdaten in `.env` (Klartext auf dem Host, Änderung nur per Neustart).

## Folgen
- Jede Mail eines unbekannten Absenders kostet eine LLM-Anfrage und konkurriert mit der Erkennung;
  höchstens 20 Mails je Lauf.
- Ein falsch als Spam erkannter Brief liegt nur im Postfach-Ordner, nicht im Archiv; die Seite
  „Eingänge“ und `betrieb.md` weisen auf den Ordner hin.
- Der Samba-Port ist eine neue Angriffsfläche im LAN; Passwort aus `.env`, kein Gastzugang, kein SMB1.
- OpenCV.js macht die Kamera-Seite beim ersten Öffnen ~8–10 MB schwer.

## Nachtrag 2026-11-19: Samba-Image (Spur B)
- **Gewählt:** `crazymax/samba:4.23.8` mit Digest
  `sha256:b37f7af97c773eddb593537f64da6389e5ee6695bcecf44f3ba1a8a6bcf34125` (Image vom 2026-07-17,
  Quelle github.com/crazy-max/docker-samba). Gepflegt (regelmäßige Samba-Versionen), Alpine-basiert,
  Einrichtung über eine YAML-Datei mit `password_file` – das Passwort kommt als Compose-Secret aus `.env`
  und steht nie in der Konfiguration.
- **Lizenzen:** Die Image-Skripte stehen unter **MIT** (Label `org.opencontainers.image.licenses`); Samba
  selbst unter **GPL-3.0**. Beides läuft nur im eigenen Container, wird nicht gelinkt und nicht mit unserem
  Code verteilt (siehe Entscheidung oben).
- **Verworfen:** `dperson/samba` (seit Jahren ohne Pflege); `ghcr.io/servercontainers/samba` (gepflegt, aber
  ohne erkennbare Lizenz im Repository, Passwort nur als Umgebungsvariable).
- Aktualisieren: neue Version **und** neuen Digest in `docker-compose.yml` eintragen, danach die Prüfliste
  in [betrieb.md](../kb/betrieb.md) („Scanner-Ordner“).
