# iCloud-Spike (M1, Paket P2)

Wegwerf-Prototyp zu ADR-0005. Er beantwortet eine Frage: **Kann NoMorePaper iCloud Drive aus .NET
lesen?** Das Ergebnis steht in [ADR-0018](../../docs/adr/0018-icloud-spike-ergebnis.md).

Das Programm gehört **nicht** zur Solution `NoMorePaper.slnx` und wird nicht ausgeliefert.

## Was das Programm tut

1. Anmeldung mit Apple-ID und Passwort (das Passwort wird nicht übertragen, sondern per
   SRP-Verfahren nachgewiesen).
2. Bestätigungscode abfragen (Zwei-Faktor-Authentifizierung), wahlweise per SMS.
3. Sitzung verschlüsselt speichern.
4. Obersten Ordner von iCloud Drive auflisten, Unterordner öffnen.
5. Eine gewählte Datei in einen temporären Ordner laden.

Das Programm **liest nur**. Es ändert, löscht und lädt nichts in iCloud hoch.

## Voraussetzungen

- Windows und das .NET 10 SDK (wie für das restliche Projekt)
- eine Apple-ID mit Zwei-Faktor-Authentifizierung
- ein Apple-Gerät in Reichweite, das den Bestätigungscode anzeigt (oder die hinterlegte
  Telefonnummer für SMS)
- Ist „Erweiterter Datenschutz“ (Advanced Data Protection) eingeschaltet, muss in den
  iCloud-Einstellungen „Über das Internet auf iCloud-Daten zugreifen“ aktiv sein. Sonst meldet
  das Programm „Web-Zugriff gesperrt“.

## Datenschutz

- Verbindungen gehen **nur** zu Apple: `idmsa.apple.com`, `setup.icloud.com`, `*.icloud.com`,
  `*.icloud-content.com`. Es gibt keine anderen Ziele.
- Apple-ID, Passwort und Code tippst **du selbst** ein. Das Passwort wird beim Tippen nicht
  angezeigt, nie gespeichert und nie ausgegeben.
- Gespeichert wird nur die Sitzung (Cookies und Tokens), verschlüsselt mit Windows-Mitteln (DPAPI,
  an dein Windows-Konto gebunden): `%LOCALAPPDATA%\NoMorePaperSpike\session.dat`. Die Datei liegt
  außerhalb des Repos.
- Die geladene Datei liegt in `%TEMP%\NoMorePaperSpike\`.
- Die Zeilen mit `[http]` zeigen nur Methode, Server, Pfad und Statuscode – keine Tokens, keine
  Cookies, keine Dateinamen. Sie dürfen in einen Fehlerbericht.
- Die **Dateiliste** und der Pfad der geladenen Datei sind deine Inhalte. Sie gehören **nicht** in
  einen Bericht, ein Issue oder einen Chat.

## Ablauf

Alle Befehle im Hauptordner des Repos ausführen.

### 1. Selbsttest (ohne Netz, ohne Apple-ID)

```powershell
dotnet run --project spikes/icloud -- --selftest
```

Erwartet: letzte Zeile `Selbsttest bestanden: 26 Prüfungen, alle ohne Netz und ohne Apple-ID.`

### 2. Erster Lauf: Anmeldung, Liste, Download

```powershell
dotnet run --project spikes/icloud
```

- Apple-ID eingeben (E-Mail-Adresse), dann das Passwort (keine Anzeige beim Tippen), Enter.
- Auf einem Apple-Gerät erscheint eine Anmeldeanfrage. „Erlauben“ wählen, den sechsstelligen Code
  ablesen und eingeben.
  - `s` fordert stattdessen einen Code per SMS an.
  - `n` fordert einen neuen Code an.
  - Leere Eingabe bricht ab.
- Danach zeigt das Programm den obersten Ordner. Nummer einer Datei eingeben → sie wird geladen.
  Nummer eines Ordners → er wird geöffnet. Leere Eingabe beendet.
- Am Ende steht ein Block „Ergebnis für die Checkliste in ADR-0018“.

### 3. Zweiter Lauf: Neustart

Denselben Befehl noch einmal starten. Erwartet: **keine** Frage nach Apple-ID, Passwort oder Code,
und die Zeile `Angemeldet. Weg: gespeicherte Sitzung wiederverwendet, keine Eingabe nötig.`

### 4. Aufräumen

```powershell
dotnet run --project spikes/icloud -- --logout
```

Löscht die Sitzungsdatei. Die geladene Datei in `%TEMP%\NoMorePaperSpike\` bitte von Hand löschen.

### Optionen

- `--selftest` – Prüfungen ohne Netz
- `--logout` – Sitzungsdatei löschen
- `--no-preflight` – den Anmelde-Rahmen vor der Passwortprüfung nicht laden (nur zur Fehlersuche)
- `--quiet` – `[http]`-Zeilen ausblenden
- `--help` – Kurzhilfe

## Wenn etwas nicht klappt

- **Kein Code kommt an:** `n` für einen neuen Code, sonst `s` für SMS. Apple hat den Ablauf im
  Frühjahr 2026 geändert; kommt auf keinem Weg ein Code, ist das ein wichtiger Befund für das ADR.
  Öffentlich berichteter Notbehelf ⚠️ unverifiziert: iPhone in den Flugmodus, WLAN aus, dann
  Einstellungen → dein Name → „Anmeldung & Sicherheit“ → „Bestätigungscode erhalten“.
- **„Apple hat Apple-ID oder Passwort abgelehnt“:** höchstens **einmal** wiederholen. Mehrere
  Fehlversuche können die Apple-ID sperren.
- **„Apple bremst gerade“ (HTTP 429/503):** mindestens 30 Minuten warten.
- **„Web-Zugriff gesperrt“:** siehe Voraussetzungen (Erweiterter Datenschutz).
- **„Bestätigung im Browser nötig“ (HTTP 412):** einmal unter <https://www.icloud.com> anmelden und
  dort alles bestätigen.
- **Neustart fragt wieder nach dem Passwort:** Zeilen mit `cookie rejected` und die
  `[http]`-Zeilen notieren – sie zeigen, an welcher Stelle die Sitzung verloren ging.

Bei jedem Fehler bitte die `[http]`-Zeilen und die Zeile „Technisch: …“ in ADR-0018 eintragen.

## Aufbau

- `Program.cs`, `SpikeApp.cs` – Start und Ablauf eines Laufs
- `Auth/` – SRP-Rechnung, Passwortschlüssel, Anmeldung, Bestätigungscode, Kontostatus
- `Http/` – HTTP-Zugriff mit eigenem Cookie-Speicher, Fehlerzuordnung
- `Session/` – Sitzungsdaten und verschlüsselte Sitzungsdatei
- `Drive/` – Ordner auflisten, Datei laden
- `Terminal/` – Ein- und Ausgabe auf der Konsole
- `SelfTest/` – nachgebauter Apple-Server und alle Prüfungen ohne Netz

## Build-Regeln

Es gelten die Regeln aus `Directory.Build.props` des Repos unverändert (Warnungen sind Fehler). Eine
eigene `Directory.Build.props` war nicht nötig. Einzige Besonderheit in `ICloudSpike.csproj`:
`AllowUnsafeBlocks`, weil die Windows-Verschlüsselung (DPAPI) direkt aufgerufen wird. So kommt der
Spike ohne zusätzliches NuGet-Paket aus.

## Quellen

Das Protokoll ist von Apple nicht dokumentiert. Das Wissen stammt aus öffentlichem Quelltext und
den Issues zweier Open-Source-Projekte, die nur gelesen wurden:

- pyicloud (Fork `timlaing/pyicloud`), MIT-Lizenz
- iCloud Photos Downloader (`icloud-photos-downloader/icloud_photos_downloader`), MIT-Lizenz

Der C#-Code ist eine eigene Umsetzung; es wurde kein Code übernommen. Die SRP-Rechnung folgt
RFC 2945 und RFC 5054.
