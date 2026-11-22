# 04 – Eingänge (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `8309d6614c42454c9b8c0d0dec206ac0`
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.webp](screen.webp)
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/Inputs.razor` (`/eingaenge`),
  `Pages/InputsEntry.razor`, `Pages/MailSettings.razor` (`/eingaenge/mail`)

## Übernehmen (passt zur App)

- **Pfad + Überschrift + Einleitung**, rechts Knopf „Aktualisieren“ (heute schon vorhanden).
- **Reiter je Eingang** mit Anzahl: Scanner-Ordner, Mail, iCloud – heute stehen die Abschnitte
  untereinander.
- **Karte je Quelle:** Name, Status als Abzeichen mit Text („Verbunden & bereit“, „Standby“),
  darunter „Letzter Lauf: vor 3 Minuten (14 Seiten)“ und Knöpfe („Verbindung testen“,
  „Bearbeiten“).
- **Postfach-Karte:** Adresse groß, Art (IMAP, SSL/STARTTLS), Abruf-Takt, „Dokumente (24 h): 12“,
  „Verbindung testen“ – passt zu `MailSettings` (Speichern, Testen).
- **Regelliste** mit Nummer, Titel, Erklärung und „Aktiv“ – passend für die tatsächlichen
  Mail-Regeln (Spam-Prüfung durch das LLM, Spam-Ordner, Absender je Konto).
- **Rechte Spalte als Detail** zur gewählten Quelle (Einstellungen + letztes Ergebnis).

## Abgleich mit der App

| Im Entwurf | In NoMorePaper |
|---|---|
| Hardware-Scanner (TWAIN/SANE/WIA), IP, Treiber, Bridge-Daemon, mTLS | gibt es nicht; der Scanner legt Dateien in den **Scanner-Ordner (SMB)** – Karte zeigt Ordner + letzten Lauf |
| Scan-Profile, DPI, Farbtiefe, Ultraschall-Einzug, Testscan | gibt es nicht (stellt man am Scanner ein) |
| Auto-Crop / Deskew | nur in der **Kamera** und ab M4b „Seiten bearbeiten“ |
| Trennblatt Patch-Code / QR | gibt es nicht; M4b bringt **Stapel-Trennung als Vorschlag** |
| Office 365 OAuth2, Webhook, Microsoft Graph | gibt es nicht – nur **IMAP** (ein Postfach des Haushalts) |
| Mehrere Postfächer, „Postfach hinzufügen“ | heute **ein** Postfach (nur Admin) |
| Regeln SKR03/04-Vorkontierung, ZUGFeRD, „Archiv/GoBD“-Ordner | gibt es nicht; echte Regeln: Spam-Prüfung (LLM), Spam-Ordner, Absenderadressen je Konto |
| „Tesseract / LLM Hybrid – Lokal (GPU)“ | lokal, aber **CPU**; als Info höchstens „Erkennung lokal“ |
| GoBD-Audit, SHA-256 am Einzug, BSI RESISCAN, S3-WORM, Zertifikat | gibt es nicht – weglassen |
| Reiter „KI- & OCR-Pipeline-Filter“, „Protokolle & Logs“ | gibt es nicht |
| „+ Neue Quelle anbinden“, „Schnittstellen-Diagnose“ | gibt es nicht |
| fehlt | Abschnitt **iCloud Drive** je Konto mit „Einrichten“ / „Code erneuern“ |
| fehlt | Hinweis „Nur ein Administrator kann das Postfach einrichten“ für Mitglieder |
| fehlt | „Zuletzt erfolgreich“ getrennt von „Zuletzt gelaufen“; „Stand: 14:32“ |

## Beim Übernehmen nicht mitnehmen (§2.5)

- Status als pulsierender Punkt (`animate-ping`, `animate-pulse`) – Bewegung stört; Text reicht.
- Reiter als einfache Knöpfe ohne `role="tab"`/`aria-selected`.
- Klickbare `span`s („Token prüfen“, „Verbindung testen“, Zahnrad) statt Knöpfen mit Namen.
- Farbtiefen-Umschalter ohne `aria-pressed`; Labels ohne `for`.
- `truncate` schneidet Adressen ab; `focus:outline-none`.
- Tailwind-CDN und Google Fonts – Werte ins MudBlazor-Theme und `app.css` übertragen.
