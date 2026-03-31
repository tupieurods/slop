# Project Structure

## Solution

- **Solution file**: `src/SlopChat/SlopChat.slnx` (`.slnx` format — new Visual Studio solution format, not `.sln`)
- Build: `dotnet build src/SlopChat/SlopChat.slnx`

## Projects

| Project | Path | Purpose |
|---|---|---|
| `SlopChat` | `src/SlopChat/SlopChat.csproj` | Main Telegram bot (console app, entry point) |
| `SlopTools` | `src/SlopTools/SlopTools.csproj` | CLI utility for getting Telegram chat IDs |

## SlopChat Layout

```
src/SlopChat/
├── SlopChat.slnx
├── SlopChat.csproj
├── Program.cs                        # Host setup, DI registration
├── nlog.config
├── Configuration/
│   └── BotOptions.cs                 # Config from SLOP_* env vars
├── Services/
│   ├── TelegramBotService.cs         # IHostedService, long-polling
│   ├── MessageRouter.cs              # Access control + message routing
│   ├── ConversationManager.cs        # Per-chat in-memory history (capped at 50 pairs)
│   └── OpenRouterClient.cs           # OpenRouter API client (via OpenAI SDK)
├── Handlers/
│   ├── SlopMessageHandler.cs         # "slop"/"слоп" prefix → LLM → reply
│   └── CommandHandler.cs             # !reset, !model, !models commands
└── Models/                           # DTOs
```

## SlopTools Layout

```
src/SlopTools/
├── SlopTools.csproj
└── Program.cs                        # Interactive CLI for Telegram chat ID lookup
```

## Deployment

- GitHub Actions: `build.yml` (CI), `deploy.yml` (publish + SCP + systemctl restart)
- Target: DigitalOcean VPS, systemd service (`deploy/slopchat.service`)
- Config via `EnvironmentFile=/opt/slopchat/.env` with `SLOP_*` environment variables
