# ADR-0018: Ergebnis des iCloud-Spikes

- **Status:** Vorgeschlagen – wartet auf Test mit echter Apple-ID
- **Datum:** 2026-11-18

## Kontext
ADR-0005 legt fest: iCloud Drive wird selbst in .NET angebunden, das Risiko klärt ein
zeitbegrenzter Spike in M1 (Plan `m1-durchstich.md`, Aufgabe 7, Paket P2). Apple bietet dafür keine
offizielle Schnittstelle; es bleibt nur das undokumentierte Web-Protokoll von icloud.com.

Der Spike ist **bestanden**, wenn mit der Apple-ID des Nutzers alle vier Punkte gelingen:
1. Anmeldung inklusive Bestätigungscode (2FA)
2. iCloud-Drive-Ordner auflisten
3. eine Datei laden
4. die Sitzung übersteht einen Neustart (keine erneute Eingabe)

**Nicht bestanden:** einer der Punkte gelingt nicht und lässt sich in der Spike-Zeit (3 Tage)
nicht beheben.

## Entscheidung
**Noch offen.** Der Prototyp ist fertig, der Test mit einer echten Apple-ID steht aus. Der Agent
hat sich bewusst nicht selbst angemeldet. Nach dem Test wird dieses ADR ergänzt:
- bestanden → Umsetzung in M4 wie in ADR-0005, mit den Auflagen aus „Folgen“
- nicht bestanden → neues ADR für einen Fallback (siehe unten), ADR-0005 wird ersetzt

## Was gebaut wurde
Konsolenprogramm `spikes/icloud/` (.NET 10, nicht in der Solution, kein NuGet-Paket). Start und
Bedienung: `spikes/icloud/README.md`.

- Anmeldung mit SRP-6a (2048 Bit, SHA-256), Passwortschlüssel per PBKDF2 (Varianten `s2k`, `s2k_fo`)
- Bestätigungscode: Anforderung auf den vertrauten Geräten, Ausweichweg SMS, Prüfung, Vertrauen
- Sitzung: Cookies, Sitzungs- und Trust-Token in `%LOCALAPPDATA%\NoMorePaperSpike\session.dat`,
  verschlüsselt mit Windows DPAPI
- Wiederanmeldung in drei Stufen: Cookies prüfen → mit Sitzungs-Token erneuern → Passwort (dank
  Trust-Token ohne Code)
- iCloud Drive: Ordner auflisten, Unterordner öffnen, Datei laden (nur lesend)
- Fehler werden Klassen zugeordnet: falsches Passwort (401), Code nötig (409), Bestätigung im
  Browser nötig (412), Sitzung abgelaufen (401/421/450), gebremst (429/503), Konto gesperrt,
  Web-Zugriff gesperrt, Protokoll geändert, kein Netz

## Geprüft ohne Apple-ID
`dotnet run --project spikes/icloud -- --selftest` – 26 Prüfungen, alle grün:
- SRP-Rechnung gegen die Testvektoren aus RFC 5054 Anhang B; die Gruppen-Konstanten sind als
  sichere Primzahlen nachgewiesen
- Apple-Variante (SHA-256, 2048 Bit, leerer Benutzername, beide Passwortprotokolle) gegen eine
  unabhängig gerechnete Referenz; PBKDF2 gegen RFC 7914
- kompletter Ablauf gegen einen **nachgebauten** Apple-Server, der den SRP-Beweis wirklich prüft:
  Erstanmeldung mit Code, Liste, Download, Neustart ohne Eingabe, abgelaufene Cookies,
  abgelaufenes Token, SMS-Weg
- Fehlerfälle: falsches Passwort, gesperrtes Konto, 503 bei der Anmeldung, drei falsche Codes,
  Abbruch, Erweiterter Datenschutz, 421 und 503 im laufenden Betrieb, kein Netz
- Datenschutz: Passwort, Tokens, Cookie-Werte und signierte Adressen erscheinen weder auf der
  Konsole noch im HTTP-Protokoll; die Sitzungsdatei ist nicht lesbar
- Am 2026-11-18 einmal gegen Apple geprüft (ohne Konto, nur der öffentliche Anmelde-Rahmen): Adresse
  und Kennung der Web-App stimmen, Apple liefert die erwarteten Fortsetzungs-Header.

**Nicht geprüft** – das kann nur der Test mit echter Apple-ID zeigen: ob Apple den SRP-Beweis
annimmt, ob der Code auf den Geräten erscheint, ob Apples echte Cookies den Neustart tragen, die
echten Antwortformate von iCloud Drive, das Verhalten mit Erweitertem Datenschutz. Der
nachgebaute Server bildet nur ab, was öffentlich beschrieben ist.

## Befunde und Risiken
1. **Apple ändert das Protokoll ohne Ankündigung.** Im Frühjahr 2026 kam nach der Passwortprüfung
   kein Code mehr auf den Geräten an; alle bekannten Clients waren wochenlang gestört. Zwei
   Auswege sind öffentlich beschrieben:
   - eine zusätzliche Anforderung (`PUT …/verify/trusteddevice/securitycode`), die den Code
     auslöst – so macht es der Spike;
   - ein neues „Bridge“-Verfahren (WebSocket zu Apples Push-Dienst, Schlüsselaustausch ähnlich
     SPAKE2+, scrypt, AES-GCM), in der Python-Referenz rund 1 400 Zeilen. **Nicht umgesetzt.**
     Schaltet Apple den ersten Weg ab, steigt der Aufwand in .NET deutlich (scrypt und
     Punktarithmetik fehlen in der .NET-Basisbibliothek).
2. **Erweiterter Datenschutz (ADP)** sperrt den Web-Zugriff. Der Spike erkennt das und meldet es,
   kann es aber nicht umgehen. Der zusätzliche Freigabe-Ablauf über ein Gerät ist nicht umgesetzt.
   Die Feldnamen `isWebAccessAllowed` und `iCDPEnabled` sind ⚠️ unverifiziert.
3. **Sitzungsdauer.** Öffentlich berichtet: Trust-Token etwa 30 Tage (rclone-Dokumentation) bis
   etwa 2 Monate (Erfahrungsberichte) ⚠️ unverifiziert; Cookies kürzer. Danach ist ein neuer Code
   nötig. Der Spike speichert den Zeitpunkt der letzten Code-Bestätigung und zeigt das Alter an.
4. **SRP-Details.** Für seltene Zahlenwerte (führende Nullbytes) ist nicht öffentlich belegt, wie
   Apple rechnet. Folge: geschätzt bis etwa 1 % der Anmeldungen könnten trotz richtigem Passwort
   abgelehnt werden ⚠️ unverifiziert. Ein zweiter Versuch hilft in den meisten dieser Fälle.
5. **Kontosperre.** Mehrere abgelehnte Anmeldungen oder Codes in Folge können die Apple-ID
   sperren. Der Spike wiederholt deshalb nichts von selbst und erlaubt nur drei Code-Versuche.
6. **.NET-Eigenheit.** Der Standard-Cookie-Speicher von .NET (20 Cookies je Domain, 4 KB je Cookie)
   verliert Apples Cookies stillschweigend. Der Spike setzt die Grenzen hoch; der Selbsttest
   belegt, dass der Neustart sonst scheitert.
7. **Nicht abgedeckt:** Sicherheitsschlüssel (FIDO), Konten in Festland-China, alte zweistufige
   Bestätigung, geteilte Ordner, Hochladen.

## Test-Checkliste für den Nutzer
Befehle im Hauptordner des Repos. Zugangsdaten tippst du selbst; sie werden nicht angezeigt und
nicht gespeichert. Bitte je Schritt „ja“ oder „nein“ notieren, bei „nein“ die Zeilen mit `[http]`
und „Technisch: …“ dazu. **Keine Dateinamen** in die Notizen.

- [ ] **0. Selbsttest:** `dotnet run --project spikes/icloud -- --selftest` endet mit
      „Selbsttest bestanden: 26 Prüfungen“.
- [ ] **1. Anmeldung mit Code:** `dotnet run --project spikes/icloud`, Apple-ID und Passwort
      eingeben. Auf einem Apple-Gerät erscheint die Anmeldeanfrage und danach der Code. Nach der
      Eingabe steht dort „Angemeldet. Weg: Passwort und Bestätigungscode.“
      Notieren: Kam der Code von selbst, erst nach `n` oder nur per SMS (`s`)?
- [ ] **2. Liste:** Das Programm zeigt den obersten Ordner. Anzahl und Namen stimmen mit der
      Dateien-App überein.
- [ ] **3. Download:** Nummer einer PDF-Datei eingeben. Die Datei unter `%TEMP%\NoMorePaperSpike\`
      lässt sich öffnen, es erscheint keine Zeile „ACHTUNG: … Größe stimmt nicht überein“.
- [ ] **4. Neustart:** Programm erneut starten. Es fragt **nichts** und meldet „gespeicherte
      Sitzung wiederverwendet, keine Eingabe nötig“. Die Liste erscheint wieder.
- [ ] **5. Dauer (optional, sehr hilfreich):** Nach einem Tag, einer Woche und einem Monat erneut
      starten und den angezeigten „Weg“ notieren. Das ergibt die echte Sitzungsdauer.
- [ ] **6. Aufräumen:** `dotnet run --project spikes/icloud -- --logout`, geladene Datei löschen.

Zusätzlich notieren: Ist „Erweiterter Datenschutz“ eingeschaltet? Was zeigt die Zeile „Konto: …“?

**Bestanden:** Schritte 1 bis 4 alle „ja“. **Nicht bestanden:** einer davon „nein“ und in der
Spike-Zeit nicht behebbar. Scheitert nur Schritt 1 am Code auf den Geräten, die SMS geht aber,
gilt das als „bestanden mit Auflage“ (Befund 1).

## Fallback, falls nicht bestanden
- **iOS-Kurzbefehl:** Eine Automation auf iPhone oder iPad schickt neue Dateien eines
  iCloud-Ordners über Tailscale an die Upload-Schnittstelle von NoMorePaper. Keine Apple-ID auf
  dem Server, kein inoffizielles Protokoll. Nachteil: läuft nur, wenn das Gerät an und im Netz ist.
- **rclone (Backend `iclouddrive`):** fertiges Werkzeug (MIT), das einen iCloud-Ordner in den
  Eingangsordner spiegelt. Nutzt dasselbe inoffizielle Protokoll, also dieselben Risiken 1 bis 3,
  wird aber von anderen gepflegt. Erweiterter Datenschutz wird laut öffentlicher Dokumentation
  nicht unterstützt ⚠️ unverifiziert. In ADR-0005 verworfen, bei einem Scheitern neu zu bewerten.

## Folgen
Bei „bestanden“ gelten für M4 diese Auflagen:
- Der Bestätigungscode muss etwa alle ein bis zwei Monate neu eingegeben werden – über die
  Web-App und für den Hauptnutzer vollständig mit Screenreader bedienbar (ADR-0010).
- Sitzung nur verschlüsselt speichern (Data Protection, ADR-0015), nie in Logs.
- Jeder Fehler aus „Befunde“ braucht eine klare Meldung in der App und einen Telemetrie-Eintrag
  ohne Inhalte (AGENTS.md 2.3). Apple-Änderungen müssen sofort auffallen.
- Nie automatisch mit Passwort wiederholen; bei 429/503 lange Pausen.
- Bridge-Verfahren und ADP-Freigabe als bekanntes Restrisiko einplanen, nicht als Teil von M4.
- Der Eingang bleibt austauschbar (Interface in Application), damit ein Fallback später ohne
  Umbau möglich ist.
