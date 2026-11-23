# 09 – Konto (Stitch-Entwurf)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `4c56ae56917342f184414ee675bc3c8f` (v2,
  M4b-Nachtrag; ersetzt v1 `e1ff8585acd64b48adb507fb1178530d`),
  HTML `projects/17713397687229704853/files/b1cd74b9a8ce4e2f87b28338b98a53f4`
  (v1: `projects/17713397687229704853/files/cc5371b8d05f4df1b9c19824eb20a290`)
- **Dateien:** [code.html](code.html) (v2, unverändert aus Stitch), [screen.png](screen.png) (v2)
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/Account.razor` (`/konto`) mit
  `PreferencesSection.razor` (Eingänge) und `FormField.razor` (Passwortfelder); ab M4b dazu
  `Accounts/AfterEditingCard.razor` (P2d)
- Die Abschnitte „Übernehmen“ bis „Nachbessern in Stitch?“ beschreiben den **bestehenden Teil** (Prüfung v1,
  in M4u umgesetzt). Der neue Teil steht unter „M4b-Nachtrag“.

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

Status (bestehender Teil): umgesetzt (M4u)

## M4b-Nachtrag (Karte Bearbeitete Dokumente)

Gebraucht von P2d (`AfterEditingCard`). S0-Vorgabe: Karte „Bearbeitete Dokumente“ mit Wahl „Nach dem
Bearbeiten behalten“ (drei Radios, je eine Erklärzeile) unter „Eingänge und neue Dokumente“.

### S0-Vorgabe – erfüllt

- Karte über die volle Breite **unter** „Eingänge und neue Dokumente“ (Lesereihenfolge F1 bleibt). ✔
- `section aria-labelledby` mit H2 „Bearbeitete Dokumente“, Symbol `auto_fix_high` mit `aria-hidden`. ✔
- `fieldset`/`legend` „Nach dem Bearbeiten behalten“, drei Radios mit je einer Erklärzeile ✔:
  - „Beides“ – „Original und bearbeitete Fassung bleiben.“ (vorgewählt = Werkswert)
  - „Nur bearbeitete Fassung“ – „Das Original kommt 30 Tage in den Papierkorb.“
  - „Nur Original“ – „Die bearbeitete Fassung kommt 30 Tage in den Papierkorb.“
  Begriffe wie in Entwurf 16 (Fassungen).
- Hinweis „Gilt für Dokumente, die du ab jetzt bearbeitest. Je Dokument kannst du es im Bereich
  „Fassungen“ ändern.“ – passt zu „Werkswert je Konto“ (Detailplan, Entschieden) und zur Wahl je Dokument (16). ✔
- „Speichern“ als weißer Hauptknopf (einziger der Karte). ✔
- Design-System: Karte `surface-container`, Optionen `surface-container-low`, Radius `xl`/`lg`, keine Rahmen,
  kein Rot. Schrift: `fontFamily` korrekt (Geist / JetBrains Mono), `screen.png` ohne Serifen. ✔

### Übernehmen

- Optionen als ganze klickbare Flächen (`label` umschließt Radio + Text), untereinander, `gap-space-sm`.
- Radios 18 px (Plan: 1,125 rem), `accent-primary`.
- Hinweiszeile unter der Gruppe, „Speichern“ linksbündig darunter.

### Abgleich mit der App

| Im Entwurf | In NoMorePaper (P2d) |
|---|---|
| `form` mit `onsubmit` | Blazor-Formular; Laden/Fehler wie `PreferencesSection` (lädt, Ladefehler + „Erneut versuchen“) |
| fehlt | Erfolgs-/Fehleransage nach „Speichern“ über `LiveRegion` bzw. `NoticeBox`; kein stehendes Abzeichen |
| Werte `both` / `edited_only` / `original_only` | Werte aus dem Contract (`GET/PUT /api/account/after-editing`) |
| keine Rückfrage | richtig: der Werkswert gilt nur für künftige Bearbeitungen, nichts wandert sofort in den Papierkorb |

### Beim Übernehmen nicht mitnehmen (§2.5)

- Erklärzeile nur als `span` im `label` – in der App je Radio `aria-describedby` auf die Erklärzeile (wie
  `VersionsSection`), damit der Name kurz bleibt.
- Hover-Fläche als einziger Hinweis auf die Option – die Auswahl zeigt das Radio selbst; kein eigener Fokusrahmen
  (ADR-0026).

### Bestehender Teil in v2 – nicht übernehmen (bestehende Umsetzung gilt)

v2 hat den bestehenden Teil gegenüber v1 aufgeräumt (Karte „Sprache“ und Nummern „01“–„04“ weg, „Verwalter“,
H2 „Eingänge und neue Dokumente“ – passt zur Umsetzung). Weiterhin anders als die Umsetzung:
- „Neue Dokumente sind privat“ + „Änderungen werden sofort gespeichert …“ (App: „Neue Dokumente privat ablegen“ +
  eigener Hinweis).
- Stehendes Abzeichen „Gespeichert“ (App: Ansage per `LiveRegion`).
- H3 „Meine Mail-Adressen“, „Adresse hinzufügen“ (App: „Absender-Adressen für Mail“ + Erklärsatz,
  „Absender-Adresse hinzufügen“).
- „Überall abmelden“, „… Sitzungen auf dem Heimserver“ (App: „Auf allen Geräten abmelden“).
- Rahmen (Seitenleiste, Kopfzeile) – App-Rahmen aus `MainLayout` gilt.
- Alle Punkte aus „Beim Übernehmen nicht mitnehmen“ oben gelten weiter.

### Nachbessern in Stitch?

Nein. S0-Vorgabe vollständig gezeigt; Restpunkte sind unsichtbar (ARIA) oder betreffen den bestehenden Teil.

Status (M4b-Nachtrag): freigegeben durch den Nutzer (2026-11-23); Restpunkte werden beim Umsetzen nach Plan behoben
