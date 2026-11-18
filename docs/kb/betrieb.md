# Betrieb

Einrichtung und Pflege des laufenden Stacks. Entscheidungen: [ADR-0002](../adr/0002-lokal-und-tailscale.md),
[ADR-0015](../adr/0015-data-protection-schluessel-verschluesselt.md),
[ADR-0020](../adr/0020-https-ueber-tailscale-serve.md).

## Erstinstallation
1. Repository klonen, `.env.example` nach `.env` kopieren (`.env` ist git-ignoriert).
2. `POSTGRES_PASSWORD` setzen: lang, zufällig, nur Buchstaben und Ziffern.
3. Schlüsselring-Zertifikat erzeugen (nächster Abschnitt) und die zwei ausgegebenen Zeilen in `.env` eintragen.
4. `docker compose up -d --build`; `docker compose ps` zeigt Server und Worker als `healthy`.

## Schlüsselring-Zertifikat
Zugangsdaten (IMAP, CalDAV, …) werden mit ASP.NET Data Protection verschlüsselt. Der Schlüsselring im
Volume `dpkeys` ist selbst mit einem Zertifikat verschlüsselt, damit ein Backup der Volumes nicht
Daten **und** Schlüssel enthält.

```powershell
powershell -NoProfile -File scripts/new-keyring-certificate.ps1 -OutputDirectory C:\Secrets\nomorepaper
```

- Das Skript schreibt `keyring.pfx` (RSA 3072, 10 Jahre, zufälliges Passwort) und gibt
  `NOMOREPAPER_KEYRING_CERT` und `NOMOREPAPER_KEYRING_CERT_PASSWORD` für `.env` aus. Das Passwort wird nur einmal angezeigt.
- Die Datei liegt **außerhalb** des Repositorys und der Docker-Volumes; Compose bindet sie lesend nach
  `/run/secrets/keyring.pfx` ein. Auf Linux-Hosts muss sie für die UID des Containers (`APP_UID`, 1654) lesbar sein.
- Datei **und** Passwort **getrennt** von den Daten-Backups sichern (z. B. Passwortmanager). Ohne sie sind
  wiederhergestellte Schlüssel unlesbar und gespeicherte Zugangsdaten verloren.
- Fehlt das Zertifikat, starten Server und Worker nicht (Ausnahme: `ASPNETCORE_ENVIRONMENT=Development`,
  nur für Entwicklung und Tests). Die Meldung nennt `NoMorePaper__KeyRingCertificatePath`.
- Ein Zertifikatwechsel wird noch nicht unterstützt: schon verschlüsselte Schlüssel bleiben nur mit dem
  alten Zertifikat lesbar (Restore-Anleitung in M7).
- **Umstieg von M1 (Schlüssel ohne Zertifikat):** Beim ersten Start mit Zertifikat widerrufen Server und Worker
  jeden noch gültigen Schlüssel ohne `encryptedSecret` (Grund „unencrypted key from before M2“, Ereignis 3410).
  Danach entsteht ein neuer, verschlüsselter Schlüssel. Damit sind **alle Sitzungen einmal abgemeldet**; alle
  melden sich neu an. Andere Daten sind mit diesen Schlüsseln nicht geschützt.
- Die widerrufenen Schlüsseldateien bleiben im Volume liegen (die Bibliothek löscht nichts). Sie schützen nichts
  mehr, das noch gilt; wer sie loswerden will, löscht nach dem Umstieg die `key-*.xml` ohne `encryptedSecret`.

## Zugriff von unterwegs (Tailscale)
Einmalig auf dem Host, nicht im Container:

```bash
tailscale serve --bg --https=443 http://127.0.0.1:8080
tailscale serve status
```

1. In der Tailscale-Verwaltung MagicDNS und HTTPS-Zertifikate aktivieren.
2. Den MagicDNS-Namen des Hosts (`host.tailnet-name.ts.net`) in `.env` eintragen:
   `NOMOREPAPER_ALLOWED_HOSTS=localhost;127.0.0.1;host.tailnet-name.ts.net`, dann `docker compose up -d`.
3. Kein `tailscale funnel`: der Dienst bleibt außerhalb des Tailnets unerreichbar.

## Vertrauenswürdige Proxys und Hostnamen
- `X-Forwarded-*`-Header werden nur von `127.0.0.1`/`::1` und von Netzen aus `NoMorePaper__TrustedProxies` übernommen. Andere Absender werden ignoriert.
- `compose` setzt `172.16.0.0/12` für den Server, weil Docker-NAT die Bridge-Adresse liefert. Der Port ist nur an `127.0.0.1` gebunden.
- Der weitergeleitete Host muss in `AllowedHosts` stehen (`NOMOREPAPER_ALLOWED_HOSTS`, z. B. `localhost;127.0.0.1;nomorepaper.<tailnet>.ts.net`).
- ⚠️ unverifiziert: Rancher Desktop unter Windows liefert Verbindungen von `192.168.127.1`. Proxmox/Linux mit echtem `tailscale serve` ist noch nicht getestet.
- Siehe ADR-0020 (Grundsatz) und [konten.md](konten.md) (Cookies hängen an HTTPS).

## Prüfliste nach Änderungen
- [ ] `docker compose ps`: `db`, `server`, `worker` healthy.
- [ ] Server-Log enthält „Key ring is encrypted with the certificate …“ (Ereignis 3400), keine Warnung 3401.
- [ ] Schlüsseldateien im Volume enthalten `encryptedSecret`, kein `<masterKey>` im Klartext:
      `docker run --rm -v nomorepaper_dpkeys:/k alpine sh -c "grep -L encryptedSecret /k/key-*.xml"` gibt nichts aus.
      Nach dem Umstieg von M1 stehen dort nur die widerrufenen alten Schlüssel (je eine `revocation-*.xml` mit
      ihrer Id; Ereignis 3410 im Log).
- [ ] `http://127.0.0.1:8080/health` antwortet; der MagicDNS-Name über HTTPS antwortet.
- [ ] Ein fremder Hostname wird mit 400 abgelehnt (`AllowedHosts`).
- [ ] Zertifikat und Passwort liegen getrennt von den Backups.
