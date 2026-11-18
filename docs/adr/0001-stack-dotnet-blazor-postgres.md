# ADR-0001: Stack: .NET 10, Blazor WASM, PostgreSQL, Clean Architecture

- **Status:** Angenommen
- **Datum:** 2026-11-18

## Kontext
Selbst gehostete Web-App mit PWA fürs Handy, Hintergrundverarbeitung und Volltextsuche.
Vorhandene Erfahrung: C#/.NET.

## Entscheidung
- .NET 10 (LTS), ASP.NET Core API, EF Core, **PostgreSQL** (inkl. Volltextsuche).
- Frontend **Blazor WebAssembly + MudBlazor** als PWA – eine Sprache für alles.
- **Clean Architecture:** Domain ← Application ← Infrastructure ← Server/Worker; Web.Client
  kennt nur Contracts. Erzwungen durch Architekturtests.
- Neu aufgebaut, **ohne** Code aus anderen Projekten zu übernehmen.

## Verworfen
Python (stärker bei OCR/KI, aber zweiter Stack), React (zweite Sprache), Blazor Server
(braucht ständige Verbindung – schlecht fürs Handy), vertikale Slices (schwächere Grenzen).

## Folgen
Größerer erster Download der PWA. OCR/LLM laufen als externe Werkzeuge/Container, nicht als
.NET-Bibliotheken.
