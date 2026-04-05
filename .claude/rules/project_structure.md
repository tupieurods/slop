# Project Structure

## Solution

- **Solution file**: `src/SlopChat.slnx` (`.slnx` format — new Visual Studio solution format, not `.sln`)
- Build: `dotnet build src/SlopChat.slnx`

## Projects

| Project | Path | Purpose |
|---|---|---|
| `SlopChat` | `src/SlopChat/SlopChat.csproj` | Main Telegram bot (console app, entry point) |
| `SlopTools` | `src/SlopTools/SlopTools.csproj` | CLI utility for getting Telegram chat IDs |

## SlopChat Folders

| Folder | Purpose |
|---|---|
| `Configuration/` | Config loaded from `SLOP_*` env vars |
| `Services/` | Hosted service, message routing, conversation history, OpenRouter API client |
| `Handlers/` | Prefix-triggered LLM handler and bot commands (`!reset`, `!model`, `!models`) |
| `Models/` | DTOs |

## SlopTools

Single-file CLI utility for Telegram chat ID lookup (`Program.cs`).

## Deployment

- GitHub Actions: `build.yml` (CI), `deploy.yml` (Docker build + push to Docker Hub + SSH deploy)
- Target: DigitalOcean VPS (Ubuntu 24.04), Docker Compose (`deploy/docker-compose.yml`)
- Config via GitHub Actions secrets, injected as env vars at deploy time
- See `deploy/SETUP.md` for first-time setup instructions
