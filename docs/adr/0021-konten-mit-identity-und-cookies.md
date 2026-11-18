# ADR-0021: Konten mit ASP.NET Identity und Cookie-Anmeldung

- **Status:** Angenommen
- **Datum:** 2026-11-18
- **Ergänzt:** ADR-0002 (Tailscale-Zugriff), ADR-0020 (HTTPS und Weiterleitungs-Header)

## Kontext
M2 braucht Konten, damit nur Haushaltsmitglieder Dokumente sehen und Dokumente privat sein können.
Es gibt keine E-Mail-Adressen und keinen Mailversand. Der Zugriff bleibt auf das Tailnet beschränkt.

## Entscheidung
- **Anmeldung:** Benutzername + Passwort über ASP.NET Identity mit EF Core. Passwort mindestens 10 Zeichen,
  sonst keine Regeln. Sperre nach 5 Fehlversuchen für 5 Minuten (Antwort 423 mit `Retry-After`).
- **Sitzung:** Cookie `__Host-nmp` (`HttpOnly`, `SameSite=Strict`, `Secure`; in `Development` `nmp`),
  30 Tage gleitend. „Abmelden auf allen Geräten“ und das Entfernen eines Mitglieds ändern den Security-Stamp.
- **Rollen:** ein Verwalter (Einrichtung, Einladungen, Rücksetzlinks, Mitglieder entfernen). Mitglieder kommen
  per Einladungslink (7 Tage, einmalig) hinein.
- **Tokens:** Einladungen und Rücksetzlinks (24 h) sind 32 Byte zufällig, gespeichert wird nur der SHA-256-Hash.
  Jede Passwortänderung macht offene Rücksetzlinks ungültig.
- **Notfall-Code des Verwalters:** 20 Zeichen Base32, bei der Einrichtung einmal angezeigt, nur Hash gespeichert,
  nach Benutzung neu erzeugt.
- **Passwort vergessen:** Rücksetzlink durch den Verwalter; der Verwalter selbst nutzt den Notfall-Code.
- **Bekannte Lücke:** Der Verwalter kann per Rücksetzlink ein Konto übernehmen. Das Mitglied wird beim nächsten
  Anmelden darüber informiert (`users.password_reset_by_admin_at`).

## Verworfene Alternativen
- **Passkeys:** verworfen auf Wunsch des Nutzers (Passwort-Anmeldung).
- **Tailscale-Identität** (Anmeldung über die Tailnet-Identität): verworfen.
- **Passwort-Rücksetzung per E-Mail:** verworfen, weil keine E-Mail-Adressen gespeichert werden.

## Folgen
- Keine E-Mail-Adressen, kein Mailversand. Rücksetzung ist nur über den Verwalter oder den Notfall-Code möglich.
- Der Verwalter ist vertrauenswürdig. Er kann Konten von Mitgliedern übernehmen; das Mitglied wird informiert.
- Sitzungen, die gesperrt oder entfernt werden, sind bis zu **5 Minuten** lang noch gültig (Prüfintervall des
  Security-Stamps).
- Private Dokumente sind für den Verwalter ebenfalls verborgen. Filterung im SQL, nicht in der Oberfläche.
- Details: [docs/kb/konten.md](../kb/konten.md).
