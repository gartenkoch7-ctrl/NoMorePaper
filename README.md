# NoMorePaper

[![CI](https://github.com/gartenkoch7-ctrl/NoMorePaper/actions/workflows/ci.yml/badge.svg)](https://github.com/gartenkoch7-ctrl/NoMorePaper/actions/workflows/ci.yml)

Selbst gehostetes, quelloffenes Dokumentenarchiv (MIT) für den Haushalt: Papier, Mails und
Dateien rein, per OCR durchsuchbar, von einem lokalen LLM automatisch zugeordnet. Kein Dokument
verlässt das eigene Netz. Die Oberfläche ist dunkel, kontrastreich und barrierefrei.

## Start

```powershell
docker compose up -d --build   # Platzhalter, entsteht in M0
```

## Entwicklung

```powershell
dotnet build NoMorePaper.slnx
dotnet test --solution NoMorePaper.slnx
```

Regeln, Architektur und Konventionen für Menschen und Agents: [AGENTS.md](AGENTS.md).

## Lizenz

[MIT](LICENSE)
