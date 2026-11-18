# Konten und Haushalt

Wie Anmeldung, Rollen und Privatsphäre in M2 funktionieren. Entscheidung: [ADR-0021](../adr/0021-konten-mit-identity-und-cookies.md).
Betrieb (HTTPS, Proxy): [betrieb.md](betrieb.md), [ADR-0020](../adr/0020-https-ueber-tailscale-serve.md).

## Rollen
- **Ein Verwalter:** wer die Instanz einrichtet. Er lädt Mitglieder ein, setzt Passwörter per Rücksetzlink zurück und kann Mitglieder entfernen.
- **Mitglieder:** kommen nur per Einladungslink ins Haushaltskonto. Sonst sind alle gleichgestellt.
- Der Verwalter kann **sich selbst nicht entfernen**.

## Einrichtung und Einladung
- Die Einrichtung ist nur solange möglich, wie es noch kein Konto gibt. Danach antwortet sie mit 409.
- Einladungslink: 7 Tage gültig, einmalig. Der Empfänger wählt Benutzername, Anzeigename und Passwort.

## Anmeldung
- Benutzername + Passwort (ASP.NET Identity). Keine Passkeys, keine E-Mail.
- Passwort: mindestens 10 Zeichen, sonst keine Regeln.
- Benutzername: max. 64 Zeichen, nur Identity-Standardzeichen (keine Umlaute). Anzeigename: max. 100 Zeichen.
- **Sperre:** nach 5 Fehlversuchen für 5 Minuten. Die Antwort ist 423 mit `Retry-After`.
  - Die Sperre verrät, dass der Benutzername existiert. Das ist bekannt und akzeptiert.
  - Ein falsches aktuelles Passwort beim Ändern des Passworts zählt zur Sperre.

## Sitzung (Cookie)
- Name `__Host-nmp` (in `Development` `nmp`), `HttpOnly`, `SameSite=Strict`, `Secure`.
- 30 Tage gleitend: jede Nutzung verlängert die Laufzeit.
- „Abmelden“ wirkt sofort auf dieses Gerät.
- „Auf allen Geräten abmelden“ und das Entfernen eines Mitglieds wirken über den Security-Stamp. Andere Sitzungen sind danach innerhalb von **5 Minuten** ungültig.

## Tokens
- Einladungen und Rücksetzlinks bestehen aus 32 Byte Zufall. Gespeichert wird nur der SHA-256-Hash. Sie sind einmalig.
- Rücksetzlinks gelten 24 Stunden.
- Jede Passwortänderung macht offene Rücksetzlinks ungültig.

## Notfall-Code des Verwalters
- Wird bei der Einrichtung einmal angezeigt. Format: 20 Zeichen Base32.
- Gespeichert wird nur der Hash. Groß-/Kleinschreibung, Leerzeichen und Bindestriche sind egal.
- Nach jeder Benutzung wird ein neuer Code erzeugt und angezeigt.
- Mit dem Code setzt der Verwalter sein eigenes Passwort zurück, wenn er es vergessen hat.

## Passwort vergessen
- Mitglieder: Rücksetzlink durch den Verwalter (Seite „Mitglieder“).
- Verwalter: Notfall-Code.
- Es gibt keine E-Mail-Rücksetzung, weil keine E-Mail-Adresse gespeichert wird.

## Privatsphäre bei Dokumenten
- **Privat** heißt: nur der Besitzer (wer hochgeladen hat) sieht das Dokument. **Auch der Verwalter sieht es nicht.**
- Der Besitzer kann die Privatsphäre jederzeit umstellen.
- Die Filterung passiert im SQL (Liste, Detail, Datei, Suche), nicht erst in der Oberfläche.
- Fremde private Dokumente liefern **404** statt 403, damit ihre Existenz nicht verraten wird.
- Dokumente aus M1 (ohne Besitzer) gehören nach der Einrichtung dem Verwalter. Sie sind **nicht** privat.
- **Entfernte Mitglieder:** ihre geteilten Dokumente bleiben sichtbar. Ihre privaten Dokumente bleiben für alle verborgen.

## Bekannte Lücke
- Der Verwalter kann per Rücksetzlink ein Konto übernehmen und damit auf dessen Anmeldung zugreifen. Das ist das Vertrauensmodell des Haushalts (Entscheidung 2026-11-18).
- Schutz: Setzt ein Rücksetzlink des Verwalters das Passwort, sieht das Mitglied beim nächsten Anmelden einen Hinweis mit Datum, bis es „Verstanden“ drückt. Der Zeitstempel steht in `users.password_reset_by_admin_at`.
- Eigene Passwortänderungen und der Notfall-Code lösen den Hinweis nicht aus.
