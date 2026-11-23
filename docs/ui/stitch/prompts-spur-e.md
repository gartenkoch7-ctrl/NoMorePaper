# Stitch-Prompts für M4u Spur E (zum Kopieren)

Stand 2026-11-23. Je Seite **ein** Block: in Stitch im Projekt „NoMorePaper“ einen neuen Screen anlegen,
Gerät **Desktop**, Design-System „Obsidian Slate“ auswählen, den Block komplett einfügen. Ein Screen nach dem
anderen (mehrere gleichzeitig hängen sich auf). Grundlage: [prompts.md](prompts.md) §3, Zusatz aus
[KB ui-prozess.md](../../kb/ui-prozess.md). Danach sagst du Bescheid; die Entwürfe werden abgeholt, geprüft
(`NN-seitenname/notizen.md`) und dir zur Freigabe vorgelegt.

## 05 – Startseite

```text
Screen "nomorepaper – Start (Desktop)". Same app frame as the existing desktop screens: left side navigation 18rem with brand "NoMorePaper" and items "Startseite" (current), "Hinzufügen", "Dokumente", "Zu prüfen (3)", bottom "Einstellungen"; top header with search field, white button "Hochladen", "Angemeldet als Anna", button "Abmelden", language switch "Deutsch | English".
Dashboard page "Start". H1 "Guten Tag, Anna". Sections stacked vertically:
1) "Zu prüfen" – sentence "3 Dokumente warten auf deine Prüfung" + button "Jetzt prüfen".
2) "Zuletzt hinzugefügt" – simple list of 5 documents: title (link), sender, received date, status with icon+word ("Fertig", "Wird verarbeitet").
3) "Eingänge" – one line per input (Scanner-Ordner, Mail, iCloud) with icon+word status ("Läuft", "Problem: Anmeldung fehlgeschlagen").
4) Two large action buttons: "Datei hochladen", "Mit Kamera aufnehmen".
Keep the project's design system exactly (colors, fonts, sizes, spacing, radius). Household app, runs only on a home server. No business, compliance, cloud or AI-score terms (no GoBD, WORM, DATEV, ZUGFeRD, S3, Office 365, SSO, sign-up, workflows, confidence %, notifications bell, storage quota). Never disable zoom. Every status = icon + word, no animation. Native elements only: a, button, input with label, select, table. Icons aria-hidden. German UI labels.
```

## 06 – Zu prüfen

```text
Screen "nomorepaper – Zu prüfen (Desktop)". Same app frame as the existing desktop screens: left side navigation 18rem with brand "NoMorePaper" and items "Startseite", "Hinzufügen", "Dokumente", "Zu prüfen (3)" (current), bottom "Einstellungen"; top header with search field, white button "Hochladen", "Angemeldet als Anna", button "Abmelden", language switch.
Page "Zu prüfen". H1, then "3 Dokumente warten auf Prüfung". Vertical list of review cards (bordered sections, not a grid). Each card: H2 title link; "Zusammenfassung: …"; badge "Automatisch erkannt" with icon; definition list Absender, Art, Datum, Betrag, Tags; buttons "Bestätigen" (primary white) and "Bearbeiten". Second card with a duplicate section: H3 "Mögliches Duplikat", explanation sentence, small comparison table (rows Titel, Eingegangen, Datum, Absender; columns "Dieses Dokument", "Anderes Dokument"), buttons "Beide behalten", "Dieses löschen". Inline confirm box inside that card: H3 "Wirklich löschen?", sentence, buttons "Abbrechen" first, then "Löschen".
Keep the project's design system exactly (colors, fonts, sizes, spacing, radius). Household app, runs only on a home server. No business, compliance, cloud or AI-score terms (no GoBD, WORM, DATEV, ZUGFeRD, S3, Office 365, SSO, sign-up, workflows, confidence %, notifications bell). Never disable zoom. Every status = icon + word, no animation. Native elements only: a, button, input with label, select, table. Icons aria-hidden. German UI labels.
```

## 07 – Suche

```text
Screen "nomorepaper – Suche (Desktop)". Same app frame as the existing desktop screens: left side navigation 18rem with brand "NoMorePaper" and items "Startseite", "Hinzufügen", "Dokumente" (current), "Zu prüfen (3)", bottom "Einstellungen"; top header with search field containing "Versicherung", white button "Hochladen", "Angemeldet als Anna", button "Abmelden", language switch.
Page "Suche". H1 "Suche", summary line "7 Treffer für „Versicherung“". List of results, each: small thumbnail of the first page on the left (dark, inverted document), H2 title link, optional badge "Privat" with lock icon, a 2-line text snippet with the search term in bold (not colour only), line "Eingegangen am 03.03.2026 über Mail". Show 4 results.
Keep the project's design system exactly (colors, fonts, sizes, spacing, radius). Household app, runs only on a home server. No business, compliance, cloud or AI-score terms (no GoBD, WORM, DATEV, ZUGFeRD, S3, Office 365, SSO, relevance score %, notifications bell). Never disable zoom. Every status = icon + word, no animation. Native elements only: a, button, input with label, select, table. Icons aria-hidden. German UI labels.
```

## 08 – Listen

```text
Screen "nomorepaper – Listen (Desktop)". Same app frame as the existing desktop screens: left side navigation 18rem with brand "NoMorePaper" and items "Startseite", "Hinzufügen", "Dokumente", "Zu prüfen (3)", bottom "Einstellungen" (current); top header with search field, white button "Hochladen", "Angemeldet als Anna", button "Abmelden", language switch. Breadcrumb "Einstellungen / Listen".
Page "Listen". H1, intro sentence "Absender, Dokumentarten und Tags des Haushalts". Three sections with H2: "Absender", "Dokumentarten", "Tags". Each section: table with columns Name, "Zum Zusammenführen auswählen" (checkbox), Aktionen (button "Umbenennen"). Below the table "2 ausgewählt" and button "Zusammenführen". In the first section an inline box for rename: H3 "„Stadtwerke“ umbenennen", text field "Neuer Name", buttons "Abbrechen", "Umbenennen". In the second section an inline box for merge: H3 "2 Einträge zusammenführen?", explanation, select "Behalten wird", buttons "Abbrechen", "Zusammenführen".
Keep the project's design system exactly (colors, fonts, sizes, spacing, radius). Household app, runs only on a home server. No business, compliance, cloud or AI-score terms (no GoBD, WORM, DATEV, SKR, ZUGFeRD, S3, Office 365, SSO, workflows, notifications bell). Never disable zoom. Every status = icon + word, no animation. Native elements only: a, button, input with label, select, table. Icons aria-hidden. German UI labels.
```

## 09 – Konto

```text
Screen "nomorepaper – Konto (Desktop)". Same app frame as the existing desktop screens: left side navigation 18rem with brand "NoMorePaper" and items "Startseite", "Hinzufügen", "Dokumente", "Zu prüfen (3)", bottom "Einstellungen" (current); top header with search field, white button "Hochladen", "Angemeldet als Anna", button "Abmelden", language switch. Breadcrumb "Einstellungen / Konto".
Page "Konto". Line "Angemeldet als Anna (anna) – Administrator". Section "Passwort ändern": three password fields (Aktuelles Passwort, Neues Passwort with hint "mindestens 12 Zeichen", Neues Passwort wiederholen), each with an eye button inside the field, button "Passwort ändern". Section "Eingänge": checkbox "Neue Dokumente sind privat" with hint (saves immediately, status text "Gespeichert" with check icon); H3 "Meine Mail-Adressen" with list of addresses each with button "Entfernen", then field "Adresse hinzufügen" + button "Hinzufügen". Section "Sprache": "Deutsch | English". Section "Abmelden": hint text, buttons "Abmelden", "Überall abmelden".
Keep the project's design system exactly (colors, fonts, sizes, spacing, radius). Household app, runs only on a home server. No business, compliance, cloud or AI-score terms (no GoBD, Office 365, SSO, two-factor apps, avatar upload, billing, plans, notifications bell). Never disable zoom. Every status = icon + word, no animation. Native elements only: a, button, input with label, select, table. Icons aria-hidden. German UI labels.
```

## 10 – Mitglieder

```text
Screen "nomorepaper – Mitglieder (Desktop)". Same app frame as the existing desktop screens: left side navigation 18rem with brand "NoMorePaper" and items "Startseite", "Hinzufügen", "Dokumente", "Zu prüfen (3)", bottom "Einstellungen" (current); top header with search field, white button "Hochladen", "Angemeldet als Anna", button "Abmelden", language switch. Breadcrumb "Einstellungen / Mitglieder".
Page "Mitglieder" (only for the household administrator). Section "Einladen": explanation sentence, button "Einladungslink erstellen"; result box with read-only field "Einladungslink" + button "Kopieren", line "Gültig bis 26.11.2026, 14:00". Section "Mitglieder des Haushalts": table Name, Benutzername, Rolle (badge "Administrator" / "Mitglied"), Aktionen (buttons "Passwort-Link erstellen", "Entfernen"). Inline result box "Passwort-Link für Ben" with copy field. Inline confirm box H3 "Ben wirklich entfernen?" with explanation, "Abbrechen" first, then "Entfernen".
Keep the project's design system exactly (colors, fonts, sizes, spacing, radius). Household app, runs only on a home server, at most a few members. No business, compliance, cloud or AI-score terms (no GoBD, Office 365, SSO, teams, departments, seats, billing, permissions matrix, notifications bell). Never disable zoom. Every status = icon + word, no animation. Native elements only: a, button, input with label, select, table. Icons aria-hidden. German UI labels.
```
