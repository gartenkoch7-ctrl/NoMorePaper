# 01 – Anmelden (Stitch-Entwurf)

- **Stitch-Screen:** Projekt `17713397687229704853`, Screen `1c74d93908724b15850c0bae1a350c`
- **Dateien:** [code.html](code.html) (unverändert aus Stitch), [screen.png](screen.png)
- **Ziel im Code:** `src/NoMorePaper.Web.Client/Pages/SignIn.razor` (`/anmelden`)

## Abgleich mit der App

Was beim Übernehmen **wegfällt oder anders wird**, weil die App es nicht hat oder es nicht stimmt:

| Im Entwurf | In NoMorePaper |
|---|---|
| Feld „E-Mail-Adresse“ | **Benutzername** (kein E-Mail-Login) |
| „Angemeldet bleiben“ | gibt es nicht |
| „Passwort vergessen?“ als Link | Text „Passwort vergessen? Frag deinen Administrator.“ |
| „Mit Passkey / Biometrie anmelden“ | gibt es nicht |
| „Microsoft / Azure AD“, „Google Workspace“ | gibt es nicht – würde ADR-0002 (nichts verlässt das Netz) widersprechen |
| „Jetzt registrieren“ | gibt es nicht; Konten nur per Einladung |
| „Ende-zu-Ende verschlüsselt nach DSGVO & GoBD“ | falsche Aussage – weglassen |
| Footer Datenschutz / Impressum / Hilfe | gibt es nicht |
| „nomorepaper workspace“, Logo-Bilder von Google-Servern | Logo lokal ablegen; keine externen Bilder/Fonts (ADR-0002) |
| fehlt | Link **„Mit Notfall-Code anmelden“** (`/notfall`) |

## Beim Übernehmen nicht mitnehmen (§2.5)

- `user-scalable=no, maximum-scale=1.0` – verhindert Zoomen.
- `outline-none` an Feldern ohne Ersatz – kein sichtbarer Fokusrahmen.
- Schriftgrößen 10–14 px (`label-sm`, `body-sm`, `body-md`) – zu klein.
- Fehler- und Ladezustände nur im Knopftext – Ansage über `LiveRegion` wie heute.
- Tailwind-CDN und Google Fonts – Farben, Abstände und Radien werden ins MudBlazor-Theme und
  `app.css` übertragen, nicht das HTML selbst.

Übernommen werden: Aufbau (Logo, Überschrift, Unterzeile, Karte mit Formular), Farbpalette,
Abstände, Radien, Knopf-Hierarchie (weißer Hauptknopf, dunkle Nebenknöpfe), Icons in Feldern.
