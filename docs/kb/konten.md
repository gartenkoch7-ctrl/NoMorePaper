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
  - Ein falsches aktuelles Passwort beim Ändern des Passworts zählt zur Sperre. Ist das Konto gesperrt, antwortet
    auch `POST /api/account/password` mit 423 und `Retry-After` (wie die Anmeldung, auch beim fünften Fehlversuch).
- **Antwortzeit:** Eine abgelehnte Anmeldung (unbekannter Name oder falsches Passwort) wird frühestens nach
  300 ms beantwortet (`AccountRules.MinFailedSignInDuration`, Warten über `TimeProvider`). Ein falsches Passwort
  schreibt den Fehlversuch in die Datenbank, ein unbekannter Name nicht; ohne die Mindestdauer verriete die
  Antwortzeit, welche Namen existieren. Hält ein langsamer Server die 300 ms nicht ein, bleibt ein kleiner
  Unterschied; das ist akzeptiert (Haushalt im Tailnet).

## Sitzung (Cookie)
- Name `__Host-nmp` (in `Development` `nmp`), `HttpOnly`, `SameSite=Strict`, `Secure`.
- 30 Tage gleitend: jede Nutzung verlängert die Laufzeit.
- „Abmelden“ wirkt sofort auf dieses Gerät.
- „Auf allen Geräten abmelden“ und das Entfernen eines Mitglieds wirken über den Security-Stamp. Andere Sitzungen sind danach innerhalb von **5 Minuten** ungültig.
- **Entferntes Mitglied in diesen 5 Minuten:** Lesen geht noch, aber jede Änderung (Hochladen, Privatsphäre
  umstellen, Passwort ändern, Hinweis bestätigen) fragt vorher, ob das Konto noch existiert
  (`ICurrentAccount.ExistsAsync`), und antwortet sonst 401 und löscht das Cookie.

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
- `documents.owner_id` bleibt in der Datenbank nullable (Dokumente vor der Einrichtung, der Worker lädt sie).
  Nach der Einrichtung hat jedes Dokument einen Besitzer; geprüft durch `Server.Tests/Documents/OwnershipTests`.
- **Entfernte Mitglieder:** ihre geteilten Dokumente bleiben sichtbar. Ihre privaten Dokumente bleiben für alle verborgen.

## Bekannte Lücke
- Der Verwalter kann per Rücksetzlink ein Konto übernehmen und damit auf dessen Anmeldung zugreifen. Das ist das Vertrauensmodell des Haushalts (Entscheidung 2026-11-18).
- Schutz: Setzt ein Rücksetzlink des Verwalters das Passwort, sieht das Mitglied beim nächsten Anmelden einen Hinweis mit Datum, bis es „Verstanden“ drückt.
- Jede solche Rücksetzung ist eine Zeile in `admin_password_resets` (`reset_at`, `acknowledged_at`). Die Tabelle
  wird nur ergänzt: „Verstanden“ setzt `acknowledged_at`, löscht aber nichts.
- Solange eine Rücksetzung unbestätigt ist, zeigt der Hinweis **alle** Rücksetzungen des Kontos, auch schon
  bestätigte (`AccountDto.AdminResets`; `AdminResetAt` = älteste unbestätigte). Grund: Der Verwalter kann sich mit
  dem gesetzten Passwort anmelden und den Hinweis selbst bestätigen. Das Mitglied kommt dann nicht mehr hinein,
  bittet um einen neuen Link und sieht beim nächsten Hinweis auch die erste Rücksetzung.
- Grenze: Bestätigt der Verwalter auch die zweite Rücksetzung selbst und nennt dem Mitglied das Passwort, sieht
  das Mitglied nichts. Das gehört zum Vertrauensmodell oben.
- Eigene Passwortänderungen und der Notfall-Code lösen den Hinweis nicht aus.
