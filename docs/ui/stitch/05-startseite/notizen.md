# 05 – Startseite (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `f4ee99bb221f4809ba95c39afb54a2e6`
  (HTML-Datei `projects/17713397687229704853/files/f1ab9ebe7dd04730a80282845cc87d38`)
- **Verworfene Varianten im Projekt:** `0a1eec48bacd4b869fe872a56342f363`, `71c0d66578634444ba5d77a4d5d2c93b`,
  `5047c86ac5d34a08a971d74601c08007`, `ecb8af4eac49464bb7058893ab991763` – Zusätze wie Abzeichen
  „Server-Status“, Fußzeile mit „Server-Logs“/„Systeminfo“, Aktionsspalten in der Liste; `5047…` nutzt
  eine Serifenschrift (nicht Obsidian Slate). Gewählt: `f4ee99bb…`.
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/Home.razor` (`/`)
- **Prompt:** [prompts-spur-e.md](../prompts-spur-e.md) „05 – Startseite“, Beschreibung prompts.md §3.1

## Entschieden

- **2026-11-23, Nutzer:** Die Startseite bekommt die Übersicht (Zu prüfen, Zuletzt hinzugefügt, Eingänge,
  zwei Knöpfe) – steht als Aufgabe in M4u Spur E.
- **2026-11-23, Nutzer:** Keine weitere Stitch-Runde; die Punkte unter „Abgleich“ und „Nachbessern in
  Stitch?“ werden beim Umsetzen nach App-Texten und Theme behoben.

## Übernehmen (passt zur App)

- **App-Rahmen** wie 02–04: Seitenleiste 18rem, Kopf mit Suche, „Hochladen“, Sprache, „Angemeldet als“,
  „Abmelden“ – kommt aus dem bestehenden Layout, nicht neu bauen.
- **H1 „Guten Tag, Anna“** + Einleitungssatz; darunter Karten untereinander (`surface-container-low`,
  `rounded-xl`, `p-space-lg`), Abstand `gap-space-xl`, Breite `max-w-5xl`.
- **Karte „Zu prüfen“:** Symbol, H2 mit Zähler, Satz „3 Dokumente warten auf deine Prüfung“, weißer
  Link-Knopf „Jetzt prüfen“ (`/zu-pruefen`); Zähler aus `ReviewCounter`.
- **„Zuletzt hinzugefügt“:** echte `table` mit `th scope="col"`, 5 Zeilen, Titel als Link, Status als
  Symbol **und** Wort („Fertig“, „Wird verarbeitet“), Link „Alle anzeigen“ → `/dokumente`.
- **„Eingänge“:** eine Zeile je Eingang, Fehler als roter Abzeichen mit Symbol + Text, Hinweiszeile darunter.
- **Zwei große Knöpfe** „Datei hochladen“ (weiß) / „Mit Kamera aufnehmen“ (dunkel) – als Links auf
  `/hochladen` und `/kamera`.

## Abgleich mit der App

| Im Entwurf | In NoMorePaper |
|---|---|
| Ganze Übersicht (Zu prüfen, Zuletzt, Eingänge, Schnellaktionen) | `Home.razor` hat heute nur H1, `AdminResetNotice` und Einleitung; Übersicht ist nur „Vorschlag Neuentwurf“ (prompts.md §3.1) und **nicht** als Punkt im M4u-Plan (Spur E: „angleichen“) – braucht Freigabe + Planeintrag, Daten aus vorhandenen APIs (Review-Zähler, Dokumentliste, Eingänge-Status) |
| fehlt | Hinweis „Ein Administrator hat dein Passwort zurückgesetzt“ (`AdminResetNotice`) oben |
| Zeile „Heimserver / Haushalt“ über der H1 | gibt es nicht (kein Pfad auf der Startseite) – weglassen |
| Einleitung „Zentraler papierloser Dokumentenspeicher …“ | vorhandener Text `HomeIntro` („Dein Dokumentenarchiv für Papier, Mails und Dateien …“) |
| Titel als Dateinamen „….pdf“, Symbol `picture_as_pdf` | Titel ist der erkannte Dokumenttitel, kein Dateiname; Bilder/HEIC sind auch Dokumente |
| Spalte „Eingangsdatum“ | heißt in der App „Eingegangen“ (`ColReceived`) |
| Status nur „Fertig“/„Wird verarbeitet“ | App kennt auch „Wartet“, „Fehlgeschlagen“ |
| „Scanner-Ordner (Local SMB / Network Share)“, „/volume1/scans“ | Deutsch: „Scanner-Ordner“; keine vollen Pfade (§2.3), höchstens Ordnername |
| „Mail (E-Mail-Postfach)“ mit Adresse | ein Postfach je Haushalt; Adresse nur für Admin sinnvoll |
| iCloud: „App-spezifisches Passwort abgelaufen“ | falsch: iCloud nutzt eine Apple-ID-**Sitzung** mit Code („Code erneuern“ auf `/eingaenge`), kein App-Passwort; Hinweis mit Link auf `/eingaenge` |
| iCloud als **ein** Eingang | iCloud Drive ist je Konto eingerichtet |
| Status „Läuft“ | App unterscheidet „Zuletzt gelaufen“ / „Zuletzt erfolgreich“ (Eingänge-Seite) |
| Fehler zuerst | Entwurf zeigt Fehler zuletzt; laut §3.1 Fehler oben sortieren |
| Kopf: Benutzer-Kreis mit `person` | nur Text „Angemeldet als …“ wie im bestehenden Layout |
| Marke „nomorepaper“ klein | wie 02–04 (Layout bleibt) |

## Beim Übernehmen nicht mitnehmen (§2.5)

- Material-Symbols in Seitenleiste und Kopf ohne `aria-hidden` (werden als „home“, „search“ vorgelesen);
  im Inhalt korrekt.
- Suchfeld ohne `label`, nur `placeholder`; `focus:outline-none` (Ring als Ersatz vorhanden, eigener
  Rahmen kommt trotzdem nicht dazu – ADR-0026).
- Sprachumschalter als zwei Knöpfe ohne `aria-pressed`/`lang`; „|“ wird vorgelesen.
- Zähler „3“ in Navigation und H2 ohne Zusammenhang („Zu prüfen 3“) – Text wie `NavReviewCount`.
- Tabelle ohne `caption`; Statusabzeichen nur als `span`.
- Eingänge als `div`-Zeilen ohne Liste; Name/Status nicht verbunden.
- „Datei hochladen“/„Kamera“ als `button` ohne Ziel – sind Navigation, also `a`.
- Kein „Zum Inhalt springen“; zwei `header`-Elemente (Kopf + Inhalt).
- Tailwind-CDN, Google Fonts (`fonts.googleapis`, `fonts.gstatic`) – Werte ins Theme/`app.css`.
- `::-webkit-scrollbar { display: none; }` – Bildlaufleiste unsichtbar, nicht übernehmen.
- Kein Zoom-Verbot im Viewport (gut).

## Nachbessern in Stitch?

- iCloud-Zeile: „App-spezifisches Passwort“ → „Anmeldung abgelaufen – Code erneuern“ (falsche Funktion).
- Englisch „Local SMB / Network Share“, Pfad `/volume1/scans` und Pfadzeile „Heimserver / Haushalt“ entfernen.
- Eingang mit Problem nach oben sortieren; Titel ohne „.pdf“.
- Grundsatz vorher klären: Soll die Startseite überhaupt zur Übersicht werden (neue Funktion, nicht im Plan)?

Status: Vorschlag – Freigabe durch den Nutzer offen
