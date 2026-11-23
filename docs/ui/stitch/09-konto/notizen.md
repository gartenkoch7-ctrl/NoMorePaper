# 09 – Konto (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `e1ff8585acd64b48adb507fb1178530d`,
  HTML `projects/17713397687229704853/files/cc5371b8d05f4df1b9c19824eb20a290`
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/Account.razor` (`/konto`) mit
  `PreferencesSection.razor` (Eingänge) und `FormField.razor` (Passwortfelder)

## Übernehmen (passt zur App)

- **Rahmen** wie die anderen Desktop-Screens (Seitenleiste 18rem, Kopfzeile), Pfad
  „Einstellungen / Konto“, H1 „Konto“, rechts daneben „Angemeldet als Anna (anna) – Administrator“.
- **Karten-Raster:** links „Passwort ändern“ (7/12), rechts „Abmelden“ (5/12), darunter „Eingänge“ über
  die volle Breite. Karte = `surface-container`, Radius `xl`, Innenabstand `space-lg`.
- **Passwortfelder** mit Label darüber, Hinweis „mindestens 12 Zeichen“ rechts am Label, Auge-Knopf im
  Feld – passt zu `FormField` (`Password="true"`). Weißer Hauptknopf rechtsbündig.
- **Eingänge:** Kontrollkästchen mit Hinweiszeile in eigener Fläche; Adressliste als Zeilen
  (Symbol + Adresse + „Entfernen“ rechts), darunter Feld + Knopf „Hinzufügen“ in einer Zeile.
- **Abmelden:** zwei Knöpfe nebeneinander, Hinweistext darüber.

## Abgleich mit der App

| Im Entwurf | In NoMorePaper |
|---|---|
| Karte „Sprache“ (Deutsch \| English) | gibt es auf `/konto` nicht; die Umschaltung sitzt in der Kopfzeile (`LanguageSwitch`). Doppelt – weglassen oder bewusst entscheiden |
| Nummern „01“–„04“ an den Karten, Reihenfolge 01, 03, 04, 02 | Zierde ohne Bedeutung, Zählung passt nicht zur Lesereihenfolge – weglassen |
| Reihenfolge Passwort → Sprache → Abmelden → Eingänge | App: Passwort → Eingänge → Abmelden. Bei zwei Spalten zuerst festlegen, wie Screenreader liest |
| Abzeichen „Gespeichert“ dauerhaft neben dem Kästchen | App sagt Speichern per `LiveRegion` an („privat an/aus“); kein stehendes Abzeichen |
| H2 „Eingänge“ | „Eingänge und neue Dokumente“ |
| „Neue Dokumente sind privat“ + Hinweis „Änderungen werden sofort gespeichert …“ | „Neue Dokumente privat ablegen“ + Hinweis, welche Eingänge gelten und dass nur du sie siehst |
| H3 „Meine Mail-Adressen“, „Adresse hinzufügen“ | „Absender-Adressen für Mail“ + Erklärsatz (ein Konto je Adresse, höchstens 20), „Absender-Adresse hinzufügen“ |
| „Überall abmelden“, Hinweis „… Sitzungen auf dem Heimserver“ | „Auf allen Geräten abmelden“, Hinweis „… auch auf deinen anderen Geräten“ |
| „Abmelden“ als Link in der Kopfzeile und Knopf in der Karte | beides vorhanden – passt |
| fehlt | Zustände: „lädt“, Ladefehler mit „Erneut versuchen“, „keine Adressen“, Fehlertext am Formular |
| „Administrator“ | Deutsch **„Verwalter“** (`RoleAdmin`); Nicht-Admins „Mitglied“ |

## Beim Übernehmen nicht mitnehmen (§2.5)

- `focus:outline-none` an allen Eingabefeldern ohne sichtbaren Ersatz (nur das Suchfeld hat `focus:ring-1`).
- Material-Symbole im App-Rahmen (Seitenleiste, Kopfzeile) ohne `aria-hidden`; im Seiteninhalt korrekt.
- Auge-Knopf wechselt `aria-label` statt `aria-pressed` mit festem Namen „Passwort anzeigen für …“
  (so macht es `FormField`).
- Ein Name für alle drei Auge-Knöpfe („Passwort anzeigen“) – nicht unterscheidbar.
- Neuer-Passwort-Hinweis als `font-label-sm` 10 px – sehr klein.
- `truncate` an den Adressen schneidet lange Adressen ab.
- „Entfernen“-Knöpfe ohne Adresse im Namen (App: `aria-label` „… entfernen“ mit Adresse).
- Kein „Zum Inhalt springen“, keine Live-Region; Statusabzeichen `#private-status` ohne `role`.
- Tailwind-CDN, Google Fonts (dreimal Material Symbols) – Werte ins MudBlazor-Theme und `app.css`.

## Nachbessern in Stitch?

- Keine groben Verstöße; Design-System und Rahmen eingehalten, nichts Erfundenes.
- Klein (Nutzer entscheidet): Karte „Sprache“ (vom Spur-E-Prompt verlangt, aber doppelt zur Kopfzeile)
  und die Nummern „01“–„04“ entfernen; Texte an die App angleichen (siehe Abgleich).

Status: freigegeben durch den Nutzer (2026-11-23); Restpunkte werden beim Umsetzen behoben
