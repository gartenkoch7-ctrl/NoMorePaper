# 10 – Mitglieder (Stitch-Entwurf, Vorschlag)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen „Mitglieder v3“
  `ffaa133b205a423089da35ec6528cb22`, HTML `projects/17713397687229704853/files/696418abb6d34fa78da87753c4029922`
- **Ersetzt:** Screen `c4f3f978250d4a239c0f75501c704d19` („Mitglieder v2“) und
  `b6e62ede0981480189e01a4a431160ea` (erste Fassung). v3 wurde mit den Texten der App erzeugt
  (siehe [UI-Prozess](../../../kb/ui-prozess.md), Regeln).
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/Members.razor` + `Members.razor.cs`
  (`/mitglieder`, nur Admin), `CopyField.razor` für die Linkfelder

## Entschieden

- **2026-11-23, Nutzer:** Der Knopf „Ja, entfernen“ bleibt **neutral** wie in der App
  (`nmp-button`), kein Rot. Rot (`--nmp-error`) bleibt Fehlern vorbehalten (ungültige Felder,
  `error-box`). v3 setzt das um: dunkler Standardknopf, kein Warnsymbol (im Bild geprüft).

## Übernehmen (passt zur App)

- **Rahmen** wie die übrigen Desktop-Screens: Marke „NoMorePaper“, Navigation, Kopfzeile mit
  „Angemeldet als …“ und „Abmelden“ als Symbol **und** Text.
- **Pfad** „Einstellungen / Mitglieder“ mit Abstand zur Kopfzeile, H1 ohne Einleitungssatz.
- **Einladen-Karte:** H2 „Mitglied einladen“, Erklärung wie in der App links, weißer Knopf rechts;
  darunter Ergebnisfläche mit Label „Einladungslink“, schreibgeschütztem Linkfeld (Mono) +
  „Kopieren“ und Gültigkeitszeile „Der Link gilt bis … und kann nur einmal benutzt werden.“ –
  passt zu `CopyField`.
- **Tabelle** unter H2 „Alle Mitglieder“, `caption` „Mitglieder des Haushalts“, Name | Benutzername |
  Rolle | Aktionen; Rolle als Abzeichen mit Symbol **und** Text („Verwalter“, „Mitglied“); beim
  Verwalter leere Aktionen-Zelle, bei Mitgliedern „Rücksetzlink erstellen“ und „Entfernen“ rechts.
- **Rücksetzlink-Fläche** H2 „Rücksetzlink für Ben“ unter der Tabelle, Feld mit Label
  „Rücksetzlink“, Kopieren und Gültigkeit; kein „Schließen“.
- **Rückfrage in der Seite** (kein Dialog): H2 „Ben wirklich entfernen?“, Erklärung wie in der App,
  „Abbrechen“ zuerst, dann „Ja, entfernen“ – beide neutral.

## Abgleich mit der App

| Im Entwurf | In NoMorePaper |
|---|---|
| Knopf „Einladungslink erstellen“ | „Neues Mitglied einladen“ (`MembersInviteButton`) – v2 hatte den App-Text, v3 nicht |
| `caption` „Mitglieder des Haushalts“ nur für Screenreader (`sr-only`) | `caption` sichtbar (`.doc-table caption`) |
| fehlt | Zustände „lädt“ und Ladefehler (`error-box` mit Symbol + Text); Ansagen per `LiveRegion`, Fokus wandert ins Linkfeld bzw. nach Entfernen zum Einladen-Knopf |
| fehlt | Nicht-Verwalter sehen nur „Diese Seite ist nur für den Verwalter.“ |

## Beim Übernehmen nicht mitnehmen (§2.5)

- `focus:outline-none` an Suchfeld und beiden Linkfeldern; Ersatz nur am Suchfeld
  (`focus:border-[#4f4f4f]`), Linkfelder ohne Ersatz. In der App gilt der Fokus des Design-Systems.
- Sprachumschaltung: jetzt echte Knöpfe in `role="group"`, aber ohne `aria-pressed` – der aktive
  Zustand ist nur optisch erkennbar.
- Knöpfe „Rücksetzlink erstellen“/„Entfernen“ ohne Personennamen im zugänglichen Namen
  (App: „Rücksetzlink erstellen für Ben“, „Entfernen: Ben“).
- Namen in der Tabelle als `td` statt `th scope="row"` (Kopfzellen haben jetzt `scope="col"`).
- Rückfrage ohne `role="group"`, kein Escape, keine Fokusführung (App: Gruppe nimmt Fokus,
  Escape bricht ab).
- `select-all` an den Linkfeldern.
- Tailwind-CDN, Google Fonts (`fonts.googleapis`) – Werte ins MudBlazor-Theme und `app.css`, Schriften lokal.
- Gegenüber v2 behoben: Symbole alle `aria-hidden`, Hochladen-Knopf mit `type`, Pfad „Einstellungen“
  ist ein Link, kein `onclick`/„Kopiert!“, kein Rot beim Überfahren von „Entfernen“, `caption` vorhanden.

## Schrift (geprüft)

- Tailwind-`font-mono` → `"JetBrains Mono", monospace`, Grundschrift Geist; beide per Google Fonts.
- Im v3-Bild sind Pfad, Etiketten, Tabellenkopf, Benutzernamen, Abzeichen, Linkfelder, Gültigkeit und
  Sprachumschaltung korrekt in JetBrains Mono gerendert (der Serifen-Ersatz aus v2 ist weg).

## Nachbessern in Stitch?

- Nur noch der Text des Einladen-Knopfs („Einladungslink erstellen“ → „Neues Mitglied einladen“) und
  die sichtbare `caption`. Beides kann beim Umsetzen aus der App übernommen werden; ein v4 ist nur
  nötig, wenn der Nutzer das Mockup exakt an die App angleichen will.

Status: Vorschlag – Freigabe durch den Nutzer offen
