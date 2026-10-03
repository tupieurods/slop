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
| `SlopMcp` | `src/SlopMcp/SlopMcp.csproj` | MCP server (tool provider for the bot): web/image search, URL fetch, date, and `render_chart` / `render_diagram` / `render_world_map` visualization tools (return PNG `ImageContentBlock`s) |
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
| `OpenRouterClient` | `Services/OpenRouterClient.cs` | HTTP client for OpenRouter API (chat completions, models, image generation — chat/completions for image+text models, `/images` endpoint for image-only models), tool call loop, wrench emoji prefix |
| `OpenRouterVideoClient` | `Services/OpenRouterVideoClient.cs` | HTTP client for OpenRouter video generation API (submit job, poll until completed, download bytes) |
| `McpToolService` | `Services/McpToolService.cs` | MCP tool provider (implements `IToolExecutor`); returns tool text plus decoded image blocks (`ToolExecutionResult`). Images collected during the tool loop (`OpenRouterClient.GetCompletionWithMediaAsync`) are sent as photos by `SlopMessageHandler` |
| `MarkdownConverter` | `Services/MarkdownConverter.cs` | Converts LLM markdown → plain text + Telegram `MessageEntity` list (fallback path when a rich message is rejected) |
| `TelegramMessageHelper` | `Services/TelegramMessageHelper.cs` | Rich message sending (`SendRichAsync`, falls back to the legacy path on API errors) and legacy chunked sending with entity-aware splitting (`SendChunkedAsync`) |
| `RichMarkdownSplitter` | `Services/RichMarkdownSplitter.cs` | Splits markdown into rich-message-sized chunks at blank-line boundaries outside code fences |
| `TelegramMediaDownloader` | `Services/TelegramMediaDownloader.cs` | Downloads Telegram photos as base64 data URLs for multimodal API requests |

## SlopMcp Visualization Services

| Service | File | Responsibility |
|---|---|---|
| `ChartRenderer` | `src/SlopMcp/Services/Visualization/ChartRenderer.cs` | ScottPlot line/bar/scatter/pie charts → PNG |
| `DiagramRenderer` | `src/SlopMcp/Services/Visualization/DiagramRenderer.cs` | MSAGL (Sugiyama) layout + SkiaSharp drawing of node/edge diagrams → PNG |
| `WorldMapRenderer` | `src/SlopMcp/Services/Visualization/WorldMapRenderer.cs` | Choropleth/categorical world maps from embedded Natural Earth GeoJSON (`Resources/ne_110m_admin_0_countries.geojson`), country lookup by ISO2/ISO3/name → PNG |

`Dockerfile.mcp` installs `libfontconfig1` + `fonts-dejavu-core` for SkiaSharp text rendering on Linux.

## Deployment

- GitHub Actions: `build.yml` (CI), `deploy.yml` (Docker build + push to Docker Hub + SSH deploy)
- Target: DigitalOcean VPS (Ubuntu 24.04), Docker Compose (`deploy/docker-compose.yml`)
- Config via GitHub Actions secrets, injected as env vars at deploy time
- See `deploy/SETUP.md` for first-time setup instructions
