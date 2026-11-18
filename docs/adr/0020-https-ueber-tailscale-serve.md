# ADR-0020: HTTPS über `tailscale serve`, Weiterleitungs-Header nur vom Host

- **Status:** Angenommen
- **Datum:** 2026-11-18
- **Ergänzt:** ADR-0002 (bleibt gültig), ADR-0015

## Kontext
Mit Konten (M2) gibt es Sitzungs-Cookies; die dürfen nur über HTTPS reisen. ADR-0002 legt fest, dass
der Zugriff von unterwegs nur über Tailscale geht, sagt aber nicht, wo TLS endet. Der Server selbst
lauscht nur auf `127.0.0.1:8080` (HTTP).

## Entscheidung
- TLS endet auf dem Host bei **`tailscale serve`** (`--https=443`, Ziel `http://127.0.0.1:8080`);
  das Zertifikat stellt Tailscale für den MagicDNS-Namen aus. Kein Funnel, kein öffentlicher Zugang.
- Der Server vertraut `X-Forwarded-For/-Proto/-Host` nur, wenn sie von `127.0.0.1` oder `::1`
  kommen (`KnownProxies`, `ForwardLimit = 1`); `UseForwardedHeaders` ist die erste Middleware.
  Von anderen Absendern werden die Header ignoriert.
- Der MagicDNS-Name wird in `NOMOREPAPER_ALLOWED_HOSTS` (`AllowedHosts`) eingetragen; Werkseinstellung
  bleibt `localhost;127.0.0.1`.
- Kein TLS im Container, kein Caddy (siehe ADR-0002, verworfen).

## Folgen
- Einrichtung einmalig von Hand ([betrieb.md](../kb/betrieb.md)).
- Läuft der Docker-Port über ein NAT, kommt die Anfrage im Container evtl. nicht von `127.0.0.1`;
  dann werden die Header ignoriert und `Request.IsHttps` bleibt falsch. ⚠️ unverifiziert für Rancher
  Desktop und Linux-Docker; bei Bedarf die vertrauenswürdige Adresse konfigurierbar machen.
