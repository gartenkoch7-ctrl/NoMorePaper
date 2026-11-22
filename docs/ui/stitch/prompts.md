# Stitch-Vorlage – Seiten und Features von NoMorePaper

Stand 2026-11-19 (Code-Stand M4, geplante Seiten aus M4b–M7). Zweck: Beschreibungen, die man in
Google Stitch einfügen kann, um die Oberfläche neu zu entwerfen. Pro Seite: **Zweck**, **Inhalt**,
**Aktionen**, **Zustände** und ein **Stitch-Prompt** (englisch, weil Stitch damit am zuverlässigsten
arbeitet – zum Kopieren).

Vorgehen in Stitch: Zuerst den **Design-Rahmen** (Abschnitt 1) als Projektbeschreibung einfügen,
dann jede Seite einzeln als eigenen Screen erzeugen. So bleiben Farben, Schrift und Navigation gleich.

---

## 1. Design-Rahmen (für jede Seite gültig)

Pflicht aus AGENTS.md §2.5 / ADR-0010, ADR-0011. Der Hauptnutzer ist blind bzw. stark sehbehindert.

> **Stand 2026-11-22:** Für die Optik (Farben, Schriftgrößen, Akzent) gilt das Stitch-Design-System
> „Obsidian Slate“, vom Nutzer so freigegeben. Die Werte 20 px, 7:1, 48 px und Gelb `#FFD600`
> unten sind überholt. Für neue Screens den Prompt-Zusatz aus [KB ui-prozess.md](../../kb/ui-prozess.md) nehmen.

- Dunkles, kontrastreiches Theme (Text mind. 7:1), große Grundschrift (mind. 20 px), deutlicher,
  dicker Fokusrahmen (mind. 3 px, gut sichtbare Farbe).
- Nie Information nur über Farbe: Status immer **Symbol + Text**.
- Jede Seite: genau eine H1, klare H2-Abschnitte, „Zum Inhalt springen“-Link ganz oben.
- Keine Modaldialoge: Rückfragen (Löschen, Zusammenführen) erscheinen **in der Seite** als Box,
  sichere Wahl („Abbrechen“) zuerst.
- Tabellen statt Kartenraster, wo Daten verglichen werden (Screenreader-Tabellennavigation).
- Fehler: rote Box mit Fehlersymbol **und** Text, dazu „Erneut versuchen“.
- Dokumente erscheinen überall **intelligent invertiert** (schwarzer Hintergrund, weiße Schrift,
  Fotos original).
- Mobil (PWA auf dem iPhone) und Desktop; große Touch-Ziele (mind. 48 px).
- Sprachen Deutsch/Englisch (Umschalter im Kopf).

```text
Stitch design brief (paste first):
App "NoMorePaper" – a self-hosted household document archive (scan, upload, mail in; OCR; a local
AI fills in sender, type, date, amount, tags). Responsive web app / PWA, desktop and iPhone.
Primary user is blind or severely visually impaired. Design rules for every screen:
- Dark, high-contrast theme (near-black background, white text, contrast ≥ 7:1), one bright accent
  colour (e.g. yellow #FFD600) used for focus and primary actions.
- Large type (base 20px, H1 40px), generous spacing, touch targets ≥ 48px.
- Very visible focus ring (3–4px solid accent outline with offset).
- Status always shown as icon + word, never colour alone.
- No modal dialogs: confirmations appear inline as a bordered box inside the page, "Cancel" first.
- Prefer simple tables and vertical lists over card grids; one H1 per page, clear H2 sections.
- Document previews are colour-inverted (black page, white text).
- Header: app name, main navigation, global search field (shortcut "/"), signed-in name,
  sign-out button, language switch DE/EN. A "Skip to content" link appears on focus.
German UI labels.
```

---

## 2. Vorschlag: Navigation sauber aufteilen

Heute hat der Kopf **10 gleichrangige Links** (Start, Hochladen, Kamera, Dokumente, Zu prüfen,
Suche, Listen, Eingänge, Konto, Mitglieder). Für Screenreader und Lupe ist das lang. Vorschlag für
den Neuentwurf – 5 Hauptpunkte, der Rest als Unterseiten:

| Hauptpunkt | Enthält (heutige Seiten) | Begründung |
|---|---|---|
| **Start** | Übersicht (neu gefüllt) | Einstieg: Was ist neu, was wartet? |
| **Hinzufügen** | Hochladen, Kamera | Beides sind Wege, ein Dokument hineinzubringen |
| **Dokumente** | Dokumentliste mit Filter, Suchergebnisse, Viewer | Suche führt ohnehin zu Dokumenten |
| **Zu prüfen (n)** | Prüfliste | Tägliche Arbeit, Zähler im Link |
| **Einstellungen** | Konto, Eingänge (+ Mail, iCloud), Listen, Mitglieder (nur Admin), später Papierkorb, Backup | Selten genutzt, gehört zusammen |

Die globale Suche bleibt im Kopf. Später (M5/M6) kommen **Fristen** und **Verträge & Kosten** als
eigene Hauptpunkte dazu.

```text
Stitch prompt – navigation:
Design the app header and navigation for NoMorePaper. Five main items: "Start", "Hinzufügen",
"Dokumente", "Zu prüfen (3)" (count badge as text), "Einstellungen". Global search field with
button "Suchen" always visible. Right side: "Angemeldet als Anna", button "Abmelden", language
switch "DE | EN". On mobile the navigation collapses into a large "Menü" button that opens a
full-width vertical list (not a hamburger icon alone – the button has visible text). Current page
is marked with an underline AND bold text.
```

---

## 3. Seiten (heute vorhanden)

### 3.1 Start – `/`
- **Zweck:** Einstieg nach der Anmeldung.
- **Heute:** nur Überschrift, Einleitungstext und ggf. Hinweis „Ein Admin hat dein Passwort
  zurückgesetzt“.
- **Vorschlag Neuentwurf:** Übersicht mit „Zu prüfen: n Dokumente“, „Zuletzt hinzugefügt“ (5),
  Status der Eingänge (Fehler zuerst), Schnellaktionen Hochladen/Kamera. Später: nächste Fristen.

```text
Stitch prompt – Start / dashboard:
Dashboard page "Start". H1 "Guten Tag, Anna". Sections stacked vertically:
1) "Zu prüfen" – sentence "3 Dokumente warten auf deine Prüfung" + button "Jetzt prüfen".
2) "Zuletzt hinzugefügt" – simple list of 5 documents: title (link), sender, received date, status
   with icon+word ("Fertig", "Wird verarbeitet").
3) "Eingänge" – one line per input (Scanner-Ordner, Mail, iCloud) with icon+word status
   ("Läuft", "Fehler: Anmeldung fehlgeschlagen").
4) Two large action buttons: "Datei hochladen", "Mit Kamera scannen".
Optional info box at top: "Ein Administrator hat dein Passwort zurückgesetzt."
```

### 3.2 Hochladen – `/hochladen`
- **Zweck:** Dateien hochladen (PDF, JPG, PNG, HEIC), mehrere auf einmal.
- **Inhalt:** Schalter „Privat“ mit Erklärung (steht **vor** der Dateiauswahl, weil die Auswahl den
  Upload sofort startet); Ablagefeld mit Dateiauswahl-Knopf und Hinweis (Drag & Drop nur Zusatz).
- **Ergebnisse:** Liste je Datei: ✓ „hochgeladen“ + Link „In der Liste zeigen“; ℹ „schon vorhanden“
  + Link zum vorhandenen Dokument; ✗ Fehler mit Grund. Warnbox „übersprungene Dateien“ mit Namen.

```text
Stitch prompt – Upload:
Page "Hochladen". H1 "Dokumente hochladen". First a checkbox "Privat – nur ich sehe diese
Dokumente" with a hint line below. Then a large dashed drop zone with a big button "Dateien
auswählen" and the hint "PDF, JPG, PNG oder HEIC, mehrere möglich. Du kannst Dateien auch
hierher ziehen." Below, H2 "Ergebnis" with a list: each row = icon + sentence + link, e.g.
"✓ Rechnung.pdf hochgeladen – In der Liste zeigen", "ℹ Brief.pdf ist schon vorhanden – Vorhandenes
öffnen", "✗ Foto.heic: Datei zu groß". A warning box above the list: "2 Dateien übersprungen"
followed by the file names.
```

### 3.3 Kamera – `/kamera`
- **Zweck:** Papier mit dem Handy scannen; Kantenerkennung, gesprochene Hinweise, Auto-Auslöser.
- **Inhalt:** Einleitung; Schalter „Töne“ (+ Hinweis); Schalter „Privat“; Knopf „Kamera starten“;
  Kamerabild (nur für Sehende, für Screenreader verborgen); aktueller Hinweis als Text
  („Näher ran“, „Blatt erkannt“); Knöpfe „Aufnehmen“, „Verwerfen“, „Fertig“; Liste „Aufgenommene
  Seiten“ (Seite 1, Seite 2 …); Erfolgsbox mit Link; Fehlerbox mit „Stattdessen hochladen“.

```text
Stitch prompt – Camera scan (mobile first):
Mobile page "Kamera". H1 "Mit Kamera scannen", short intro. Two checkboxes: "Töne bei
Hinweisen", "Privat". Big button "Kamera starten". When running: camera preview with a thick
white border filling most of the width, a detected-document outline in accent colour, below it a
large text line "Hinweis: Etwas näher heran". Bottom action bar with three large buttons:
"Aufnehmen" (primary), "Verwerfen", "Fertig". Section "Aufgenommene Seiten": numbered list
"Seite 1", "Seite 2". Success box after finishing: "✓ Dokument mit 2 Seiten gespeichert – In der
Liste zeigen". Error variant: "✗ Kein Kamerazugriff – Stattdessen Datei hochladen".
```

### 3.4 Dokumente – `/dokumente`
- **Zweck:** Alle Dokumente des Haushalts, die man sehen darf.
- **Filter** (aufklappbar, „Filter (2 aktiv)“): Absender, Art, Datum von/bis, Betrag von/bis,
  Sichtbarkeit (alle/nur privat/nur geteilt), Tags (Mehrfachauswahl) + „alle Tags müssen passen“;
  Knöpfe „Filtern“, „Zurücksetzen“. Ergebnis wird angesagt („12 Dokumente gefunden“).
- **Tabelle:** Titel (mit Miniatur, Link, Abzeichen „Privat“ und „Automatisch erkannt“), Absender,
  Art, Datum, Betrag, Eingegangen, Status (Symbol + Wort: Wartet, Wird verarbeitet, Fertig,
  Fehlgeschlagen). Aktualisiert sich selbst.
- **Zustände:** lädt, Fehler + „Erneut versuchen“, leer, leer durch Filter.

```text
Stitch prompt – Document list:
Page "Dokumente". H1 "Dokumente". A disclosure button "Filter (2 aktiv)" that expands a filter
panel: dropdowns "Absender", "Art", date fields "Datum von", "Datum bis", text fields "Betrag
von", "Betrag bis", dropdown "Sichtbarkeit" (Alle / Nur privat / Nur geteilt), a tag picker
"Tags" with removable chips (each chip has a visible "entfernen" label), checkbox "Alle Tags
müssen passen", buttons "Filtern" (primary) and "Zurücksetzen". Below a status line "12 Dokumente
gefunden". Then a data table with caption, columns: Titel (small inverted thumbnail + link +
text badges "Privat", "Automatisch erkannt"), Absender, Art, Datum, Betrag, Eingegangen, Status
(icon + word: "Fertig", "Wird verarbeitet", "Fehlgeschlagen"). Empty values show "–". On mobile
each row becomes a stacked block with label: value pairs.
```

### 3.5 Dokument ansehen – `/dokumente/{id}`
- **Zweck:** Ein Dokument lesen, Angaben prüfen/ändern, herunterladen.
- **Kopf:** Titel, Status, Schalter „Privat“ (nur Besitzer) mit Hinweis, Downloads „Original“ und
  „Archiv-PDF (PDF/A)“.
- **Angaben** (Abschnitt): Zusammenfassung zuerst; „Aus der Mail“ (Notiz, Absender-Adresse,
  Betreff) falls per Mail; Herkunft „Automatisch erkannt“ / „Bestätigt“; Warnung „Mögliches
  Duplikat – anderes anzeigen“; Liste Absender, Art, Datum, Betrag, Tags; Knöpfe „Bearbeiten“,
  „Bestätigen“.
- **Bearbeiten** (in der Seite): Titel, Absender und Art (Eingabe mit Vorschlägen, neue Einträge
  möglich), Datum, Betrag + Währung, Tags, Zusammenfassung; „Speichern“, „Abbrechen“.
- **Besitz-Prüfung** (bei unklarer Herkunft): „Passt so“ oder „Weitergeben an …“ (Auswahl Mitglied).
- **Bearbeiter** (nur Besitzer): Mitglieder auswählen, die Angaben ändern dürfen; „Speichern“.
- **Viewer:** Werkzeugleiste Zoom −/+, Zurücksetzen, Breite anpassen, „Originalfarben“ (an/aus);
  Seitenleiste „Vorherige Seite – Seite 2 von 5 – Nächste Seite“; Tastenhinweis; Hinweis „Kein
  Text erkannt“; großer, invertierter Dokumentbereich.

```text
Stitch prompt – Document viewer (desktop: two columns, mobile: stacked):
Page for one document. H1 = document title "Stromrechnung März 2026". Status line "Status:
Fertig". Checkbox "Privat" with hint. Links "Original herunterladen", "Archiv-PDF herunterladen".
Left/main column: document viewer with a toolbar group "Ansicht": buttons "Verkleinern",
"Vergrößern", "Zurücksetzen", "Breite anpassen", toggle "Originalfarben" (pressed state shown with
text "an/aus"), zoom text "150 %". Second toolbar "Seiten": "Vorherige Seite", "Seite 2 von 5",
"Nächste Seite". Hint line: "Pfeiltasten scrollen, Plus/Minus zoomt, Bild auf/ab blättert". Large
canvas showing an inverted page (black page, white text).
Right column "Angaben": paragraph "Zusammenfassung: …" first; optional box "Aus der Mail" (Notiz,
Absender, Betreff); line "✨ Automatisch erkannt"; warning "⚠ Mögliches Duplikat – Anderes
anzeigen"; definition list Absender, Art, Datum, Betrag, Tags; buttons "Bearbeiten",
"Bestätigen". Optional box "Wem gehört das?" with button "Passt so" and dropdown "Weitergeben an"
+ button "Weitergeben". Section "Bearbeiter" with member checkboxes and "Speichern".
```

```text
Stitch prompt – Edit fields (inline form, same page):
Inline edit form "Angaben bearbeiten" replacing the read-only list: text field "Titel";
combobox "Absender" with suggestion list and option "Neu anlegen: …"; combobox "Art"; date field
"Datum" with hint "TT.MM.JJJJ"; field "Betrag" + small field "Währung" (EUR); tag picker "Tags";
textarea "Zusammenfassung". Buttons "Speichern" (primary), "Abbrechen". Validation errors
appear as text under the field with an error icon.
```

### 3.6 Zu prüfen – `/zu-pruefen`
- **Zweck:** Automatisch erkannte Dokumente bestätigen oder korrigieren; Duplikate klären;
  Besitz unklarer Dokumente klären.
- **Inhalt:** „3 Dokumente zu prüfen“; Liste, je Eintrag: Titel (Link), Zusammenfassung,
  Erkennungsstatus, Angaben-Liste, ggf. Besitz-Prüfung, ggf. Duplikat-Abschnitt mit
  Vergleichstabelle (Feld | dieses | anderes) und Knöpfen „Behalten“ / „Löschen“ (mit Rückfrage
  in der Seite), Knöpfe „Bestätigen“, „Bearbeiten“ (Editor wie 3.5 erscheint im Eintrag).
- **Später (M4b):** Trennvorschlag für Stapel („3 Dokumente: Seiten 1–2, 3, 4–6“) zum Bestätigen.

```text
Stitch prompt – Review queue:
Page "Zu prüfen". H1, then "3 Dokumente warten auf Prüfung". Vertical list of review cards
(bordered sections, not a grid). Each card: H2 title link; "Zusammenfassung: …"; line
"✨ Automatisch erkannt"; definition list Absender, Art, Datum, Betrag, Tags; buttons
"Bestätigen" (primary) and "Bearbeiten". Variant card with a duplicate section: H3 "Mögliches
Duplikat", explanation sentence, small comparison table (rows Titel, Eingegangen, Datum,
Absender; columns "Dieses Dokument", "Anderes Dokument"), buttons "Beide behalten", "Dieses
löschen". Inline confirm box after delete: "Wirklich löschen?" with "Abbrechen" first, then
"Löschen". Variant card for batch split (later): "Trennvorschlag: 3 Dokumente – Seiten 1–2, 3,
4–6. Seite 5 vermutlich leer." buttons "Trennen", "Nicht trennen".
```

### 3.7 Suche – `/suche?q=…`
- **Zweck:** Volltextsuche (OCR-Text). Eingabe im Kopf (Taste „/“), Seite zeigt Treffer.
- **Inhalt:** „7 Treffer für ‚Versicherung‘“; je Treffer: Titel (H2-Link), Abzeichen „Privat“,
  Miniatur, Textausschnitt, Eingangsdatum. Leer: Aufforderung zu suchen.

```text
Stitch prompt – Search results:
Page "Suche". H1 "Suche", summary line "7 Treffer für „Versicherung“". List of results, each:
small inverted thumbnail on the left, H2 title link, optional badge "Privat", a 2-line text
snippet with the search term in bold (not colour only), line "Eingegangen am 03.03.2026".
Empty state: "Gib oben einen Suchbegriff ein (Taste /)."
```

### 3.8 Listen – `/listen`
- **Zweck:** Gepflegte Listen: **Absender**, **Dokumentarten**, **Tags**.
- **Je Liste:** Tabelle Name | Auswahl (Checkbox) | Aktion „Umbenennen“; „2 ausgewählt“;
  Knopf „Zusammenführen“. Umbenennen und Zusammenführen (Ziel-Eintrag wählen) als Box in der Seite.

```text
Stitch prompt – Lists (correspondents, types, tags):
Page "Listen". H1, intro sentence. Three sections with H2: "Absender", "Dokumentarten", "Tags".
Each section: table with columns Name, "Zum Zusammenführen auswählen" (checkbox), Aktionen
(button "Umbenennen"). Below the table "2 ausgewählt" and button "Zusammenführen". Inline box
for rename: H3 "„Stadtwerke“ umbenennen", text field "Neuer Name", buttons "Abbrechen",
"Umbenennen". Inline box for merge: "2 Einträge zusammenführen?", explanation, dropdown "Behalten
wird", buttons "Abbrechen", "Zusammenführen".
```

### 3.9 Eingänge – `/eingaenge`
- **Zweck:** Zustand aller automatischen Eingänge.
- **Abschnitte:** **Scanner-Ordner** (SMB), **Mail** (inkl. Hinweis Spam-Ordner; Admin: Link
  „Einrichten“/„Einstellungen“), **iCloud Drive** (je Konto; Link „Einrichten“, „Code erneuern“).
  Je Eingang/Konto: Status (Symbol + Satz), „Zuletzt gelaufen“, „Zuletzt erfolgreich“.
  Knopf „Aktualisieren“, „Stand: 14:32“.

```text
Stitch prompt – Inputs status:
Page "Eingänge". H1, intro, button "Aktualisieren", line "Stand: 14:32:05". Three sections (H2):
"Scanner-Ordner", "Mail", "iCloud Drive". Each has a description sentence and one status block
per source/account: optional H3 account name, status line with icon + sentence ("✓ Läuft
normal", "✗ Anmeldung fehlgeschlagen – Passwort prüfen"), definition list "Zuletzt gelaufen: vor
2 Minuten", "Zuletzt erfolgreich: vor 2 Minuten". Mail section ends with link "Mail-Postfach
einstellen" (admin) or text "Nur ein Administrator kann das Postfach einrichten". iCloud section
ends with link "iCloud einrichten" or "Bestätigungscode erneuern".
```

### 3.10 Mail-Postfach – `/eingaenge/mail` (nur Admin)
- **Formular:** Server, Port, Sicherheit (SSL/STARTTLS), Benutzername, Passwort (wird nie
  angezeigt; Hinweis „gespeichert“/„neu eingeben“, Knopf „Anzeigen“), AuthServ-ID (optional).
  Knöpfe „Speichern“, „Verbindung testen“. Abschnitt „Ordner“ mit Erklärung.

```text
Stitch prompt – Mail settings:
Settings form "Mail-Postfach". Explanation paragraph. Fields with label above and hint below:
"Server" (hint "z. B. imap.example.de"), "Port" (number), "Sicherheit" dropdown (SSL/TLS,
STARTTLS), "Benutzername", "Passwort" with a visible-text toggle button "Anzeigen" and hint
"Ein Passwort ist gespeichert. Leer lassen, um es zu behalten.", "AuthServ-ID (optional)".
Buttons "Speichern" (primary), "Verbindung testen". Status message area below buttons. Section
H2 "Ordner" with explanatory text.
```

### 3.11 Konto – `/konto`
- **Inhalt:** „Angemeldet als Anna (anna), Administrator“; **Passwort ändern** (aktuell, neu,
  wiederholen); **Eingänge-Einstellungen**: „Neue Dokumente privat“ (sofort gespeichert),
  **eigene Mail-Absenderadressen** (Liste mit „Entfernen“, Feld + „Hinzufügen“); **Abmelden** /
  „Überall abmelden“. Später: „Nach dem Bearbeiten behalten“ (Original/bearbeitet/beides).

```text
Stitch prompt – Account:
Page "Konto". Line "Angemeldet als Anna (anna) – Administrator". Section "Passwort ändern":
three password fields (Aktuelles, Neues with hint "mindestens 12 Zeichen", Wiederholen), button
"Passwort ändern". Section "Eingänge": checkbox "Neue Dokumente sind privat" with hint (saves
immediately, status text "Gespeichert"); H3 "Meine Mail-Adressen" with list of addresses each
with button "Entfernen", then field "Adresse hinzufügen" + button "Hinzufügen". Section
"Abmelden": hint text, buttons "Abmelden", "Überall abmelden".
```

### 3.12 Mitglieder – `/mitglieder` (nur Admin)
- **Inhalt:** **Einladen** (Knopf → Link mit Kopierfeld, „gültig bis …“); **Tabelle** Name |
  Benutzername | Rolle | Aktionen („Passwort-Link“, „Entfernen“); Rücksetz-Link-Box; Rückfrage
  „Entfernen?“ in der Seite.

```text
Stitch prompt – Members (admin):
Page "Mitglieder". Section "Einladen": explanation, button "Einladungslink erstellen"; result box
with read-only field "Einladungslink" + button "Kopieren", line "Gültig bis 26.11.2026, 14:00".
Section "Mitglieder des Haushalts": table Name, Benutzername, Rolle (Administrator/Mitglied),
Aktionen (buttons "Passwort-Link erstellen", "Entfernen" – each with the person's name in its
accessible label). Inline result box "Passwort-Link für Ben" with copy field. Inline confirm box
"Ben wirklich entfernen?" with "Abbrechen" first.
```

### 3.13 Anmelden – `/anmelden`
- Benutzername, Passwort, „Anmelden“; Hinweis „Passwort vergessen? Admin fragen“; Link
  „Notfall-Code verwenden“.

### 3.14 Ersteinrichtung – `/einrichten`
- Erster Admin: Anzeigename, Benutzername, Passwort, Wiederholung → danach **Notfall-Code**
  anzeigen (kopieren, „Ich habe ihn aufgeschrieben“ bestätigen).

### 3.15 Einladung annehmen – `/einladung/{token}`
- „Einladung gültig bis …“; Anzeigename, Benutzername, Passwort, Wiederholung; Fehlerbox bei
  abgelaufenem Link.

### 3.16 Passwort zurücksetzen – `/passwort/{token}`
- Neues Passwort + Wiederholung; Erfolgsbox mit Link „Zur Anmeldung“.

### 3.17 Notfall-Zugang – `/notfall`
- Benutzername, Notfall-Code, neues Passwort, Wiederholung → neuer Notfall-Code wird angezeigt.

```text
Stitch prompt – Auth screens (one consistent set, centred single column, max 32rem wide):
Create five screens sharing one layout: app name on top, H1, short intro, form fields with label
above and hint below, one full-width primary button, links below.
1) "Anmelden": Benutzername, Passwort, button "Anmelden", text "Passwort vergessen? Frag deinen
   Administrator.", link "Mit Notfall-Code anmelden".
2) "Einrichten": Anzeigename, Benutzername, Passwort, Passwort wiederholen, button "Einrichten";
   follow-up screen showing an emergency code in large monospaced letters with "Kopieren" and
   checkbox+button "Ich habe den Code sicher aufbewahrt – Weiter".
3) "Einladung annehmen": intro "Die Einladung gilt bis …", same four fields, button "Konto anlegen";
   error variant with red box + icon "Dieser Link ist abgelaufen".
4) "Neues Passwort": two fields, button; success box "✓ Passwort geändert" + link "Zur Anmeldung".
5) "Notfall-Zugang": Benutzername, Notfall-Code, neues Passwort, wiederholen, button.
```

### 3.18 Nicht gefunden – `/not-found`
- Kurzer Text „Seite nicht gefunden“ + Link zur Startseite.

---

## 4. Geplante Seiten (für ein einheitliches Design gleich mitentwerfen)

### 4.1 Seiten bearbeiten – `/dokumente/{id}/seiten` (M4b)
- Seitenliste; je Seite Ansage (Seitenzahl, Textanfang, „vermutlich leer“) und große invertierte
  Vorschau; je Seite Knöpfe Drehen links/rechts, Nach oben/unten, Löschen, „Ränder und Schräglage
  automatisch“, Ecken per Pfeiltasten verschieben; „Dokument anhängen“, „Teilen nach Seite …“;
  erst „Speichern“ erzeugt neue Version, „Verwerfen“ macht alles rückgängig.

```text
Stitch prompt – Edit pages:
Page "Seiten bearbeiten – Stromrechnung". Top bar: buttons "Speichern" (primary), "Alles
verwerfen", "Dokument anhängen". Vertical list of pages (not a grid). Each page item: large
inverted preview on the left, on the right H2 "Seite 2 von 5", text "Beginnt mit: Sehr geehrte…",
optional badge "⚠ Vermutlich leer", button group "Nach links drehen", "Nach rechts drehen",
"Nach oben", "Nach unten", "Seite löschen", "Ränder und Schräglage automatisch", "Hier teilen".
After auto-crop a status text: "Um 3 Grad gedreht, Rand oben entfernt". Crop mode: preview with
four large corner handles and hint "Ecke mit Pfeiltasten verschieben, Tab wechselt die Ecke".
```

### 4.2 Papierkorb – `/papierkorb` (M4b, nur eigene Dokumente)
- Tabelle Titel | Art (Dokument/Original/alte Version) | gelöscht am | endgültig weg am |
  Aktionen „Wiederherstellen“, „Endgültig löschen“; Knopf „Papierkorb leeren“ (Rückfrage).

```text
Stitch prompt – Trash:
Page "Papierkorb". Intro "Gelöschtes bleibt 30 Tage hier." Table: Titel, Was (Dokument /
Original / Ältere Version), Gelöscht am, Endgültig gelöscht am, Aktionen ("Wiederherstellen",
"Endgültig löschen"). Button "Papierkorb leeren" with inline confirm box ("Abbrechen" first).
Empty state "Der Papierkorb ist leer."
```

### 4.3 Versionen (im Viewer, M4b)
- Abschnitt „Versionen“: Original / bearbeitete Fassung, Auswahl „Behalten: beides / nur
  bearbeitet / nur Original“, „Seiten bearbeiten“-Link.

### 4.4 Fristen – `/fristen` (M5)
- Liste nach Fälligkeit: Datum, Was, Dokument-Link, Art der Frist, „erledigt“-Schalter,
  „im Kalender“-Hinweis. Einstellungen: Fristarten mit Übernahme und Vorlauf (Tage).

```text
Stitch prompt – Deadlines:
Page "Fristen". Groups with H2: "Überfällig", "Diese Woche", "Später". Each row: date in large
text, sentence "Zahlung 84,20 € an Stadtwerke", link to document, deadline type, checkbox
"Erledigt", text "Im Kalender eingetragen". Settings sub-page "Fristarten": table Art,
"Automatisch übernehmen" (checkbox), "Erinnern … Tage vorher" (number).
```

### 4.5 Verträge & Kosten – `/vertraege`, `/kosten` (M6)
- Verträge: Anbieter, Art, Betrag/Monat, Laufzeit, Kündigungsfrist, Hinweis „Preiserhöhung
  erkannt“. Kosten: Tabelle nach Kategorie und Jahr (Tabelle zuerst, Diagramm nur Zusatz).

### 4.6 Backup & Export – in Einstellungen (M7)
- Backup: letzter Lauf, Status, „Jetzt sichern“, „Wiederherstellung testen“. Steuer-Export:
  Jahr wählen, Kategorien, „Export erstellen“.

---

## 5. Gemeinsame Bausteine (in Stitch einmal definieren, überall gleich)

| Baustein | Aussehen/Verhalten |
|---|---|
| Statuszeile | Symbol + Wort (✓ Fertig, ⏳ Wird verarbeitet, ✗ Fehlgeschlagen, ✨ Automatisch erkannt) |
| Abzeichen | „Privat“ (Schloss + Text), „Automatisch erkannt“ |
| Fehlerbox | Rahmen, Fehlersymbol, Satz, Knopf „Erneut versuchen“ |
| Erfolgsbox | Rahmen, Häkchen, Satz, Link zum Ergebnis |
| Rückfrage-Box | in der Seite, H3-Frage, Erklärung, „Abbrechen“ zuerst, dann Aktion |
| Formularfeld | Label oben, Hinweis darunter, Fehlertext mit Symbol unter dem Feld |
| Kopierfeld | schreibgeschütztes Feld + Knopf „Kopieren“ |
| Tag-Auswahl | Eingabe mit Vorschlägen, gewählte Tags als Chips mit sichtbarem „entfernen“ |
| Miniatur | kleine, invertierte Seitenvorschau, dekorativ (Titel steht daneben) |

Hinweis für die Umsetzung: Stitch erzeugt HTML/Tailwind. Übernommen werden nur **Layout, Abstände,
Farben und Typografie** in das MudBlazor-Theme und `app.css`; die Bedienlogik (Live-Regionen,
Fokusführung, ARIA) bleibt wie im bestehenden Code.
