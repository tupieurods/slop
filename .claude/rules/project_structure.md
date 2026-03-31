# Project Structure

## Solution

- **Solution file**: `src/SlopChat/SlopChat.slnx` (`.slnx` format — new Visual Studio solution format, not `.sln`)
- Build: `dotnet build src/SlopChat/SlopChat.slnx`

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

- GitHub Actions: `build.yml` (CI), `deploy.yml` (publish + SCP + systemctl restart)
- Target: DigitalOcean VPS, systemd service (`deploy/slopchat.service`)
- Config via `EnvironmentFile=/opt/slopchat/.env` with `SLOP_*` environment variables
