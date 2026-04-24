# Project Structure

## Solution

- **Solution file**: `src/SlopChat.slnx` (`.slnx` format — new Visual Studio solution format, not `.sln`)
- Build: `dotnet build src/SlopChat.slnx`
- Test: `dotnet test src/SlopChat.Tests/SlopChat.Tests.csproj`

## Projects

| Project | Path | Purpose |
|---|---|---|
| `SlopChat` | `src/SlopChat/SlopChat.csproj` | Main Telegram bot (console app, entry point) |
| `SlopChat.Tests` | `src/SlopChat.Tests/SlopChat.Tests.csproj` | xUnit tests for SlopChat |
| `SlopMcp` | `src/SlopMcp/SlopMcp.csproj` | MCP server (tool provider for the bot) |
| `SlopTools` | `src/SlopTools/SlopTools.csproj` | CLI utility for getting Telegram chat IDs |

## SlopChat Folders

| Folder | Purpose |
|---|---|
| `Configuration/` | Config loaded from `SLOP_*` env vars (`BotOptions`) |
| `Services/` | Core services: message routing, conversation history, OpenRouter API client, markdown converter, media downloader |
| `Handlers/` | Command handler (`!reset`, `!model`, `!models`, `!version`, `!set_model`, `!draw`, `!draw_models`, `!set_draw_model`, `!video`, `!video_models`, `!set_video_model`) and Slop message handler (LLM completions) |
| `Models/` | DTOs for OpenRouter API (ChatMessage, ContentPart, ToolDefinition, ToolCall, ImageGenerationRequest/Response, etc.) |

## Key Services

| Service | File | Responsibility |
|---|---|---|
| `MessageRouter` | `Services/MessageRouter.cs` | Routes incoming Telegram messages to commands or Slop handler; dictionary-based command dispatch |
| `ConversationManager` | `Services/ConversationManager.cs` | Per-chat message history, per-chat model selection (LLM + draw), date injection, compaction/summarization |
| `OpenRouterClient` | `Services/OpenRouterClient.cs` | HTTP client for OpenRouter API (chat completions, models, image generation), tool call loop, wrench emoji prefix |
| `OpenRouterVideoClient` | `Services/OpenRouterVideoClient.cs` | HTTP client for OpenRouter video generation API (submit job, poll until completed, download bytes) |
| `McpToolService` | `Services/McpToolService.cs` | MCP tool provider (implements `IToolExecutor`) |
| `MarkdownConverter` | `Services/MarkdownConverter.cs` | Converts LLM markdown → plain text + Telegram `MessageEntity` list |
| `TelegramMessageHelper` | `Services/TelegramMessageHelper.cs` | Chunked message sending with entity-aware splitting |
| `TelegramMediaDownloader` | `Services/TelegramMediaDownloader.cs` | Downloads Telegram photos as base64 data URLs for multimodal API requests |

## Deployment

- GitHub Actions: `build.yml` (CI), `deploy.yml` (Docker build + push to Docker Hub + SSH deploy)
- Target: DigitalOcean VPS (Ubuntu 24.04), Docker Compose (`deploy/docker-compose.yml`)
- Config via GitHub Actions secrets, injected as env vars at deploy time
- See `deploy/SETUP.md` for first-time setup instructions
