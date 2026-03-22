# Project Overview

**SlopChat** is a Telegram AI chatbot written in C# / .NET 10.0.

## Architecture
Single-project console app using `Microsoft.Extensions.Hosting` generic host.

### Folder structure
- `Configuration/` — `BotOptions` (env var config via `SLOP_*` prefix)
- `Services/` — `TelegramBotService` (IHostedService, long-polling), `MessageRouter` (access control + routing), `ConversationManager` (per-chat history), `OpenRouterClient` (typed HttpClient)
- `Handlers/` — `SlopMessageHandler` (prefix detection → LLM → reply), `CommandHandler` (!models, !model, !reset)
- `Models/` — DTOs: `ChatMessage`, `OpenRouterRequest`, `OpenRouterResponse`, `OpenRouterModelsResponse`

### Key behaviors
- Responds to messages prefixed with "slop"/"слоп" (case-insensitive)
- Private chat: admin-only; Groups: allowed list only
- `!models` (admin-only), `!model`, `!reset` commands
- In-memory conversation history, capped at 50 pairs + system prompt

### Deployment
- GitHub Actions: `build.yml` (CI), `deploy.yml` (publish + SCP + systemctl restart)
- DigitalOcean VPS with systemd service (`deploy/slopchat.service`)
- Config via `EnvironmentFile=/opt/slopchat/.env`

### NuGet packages
- Telegram.Bot, Microsoft.Extensions.Hosting, Microsoft.Extensions.Http
