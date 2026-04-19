# 🤖 SlopChat

A Telegram group chat bot powered by [OpenRouter](https://openrouter.ai/) — access 100+ LLM models (Gemini, GPT, Claude, and more) directly from your Telegram chats.

[![Build](https://github.com/tupieurods/slop/actions/workflows/build.yml/badge.svg)](https://github.com/tupieurods/slop/actions/workflows/build.yml)
[![Deploy](https://github.com/tupieurods/slop/actions/workflows/deploy.yml/badge.svg)](https://github.com/tupieurods/slop/actions/workflows/deploy.yml)

## Features

- **Multi-Model** — switch between LLM models on the fly with `!set_model`
- **Conversation Memory** — per-chat history with automatic summarization to keep token costs down
- **Access Control** — admin-only commands, allowlisted chats, private-chat restriction
- **Docker Deployment** — one-push CI/CD via GitHub Actions → Docker Hub → VPS
- **CLI Tooling** — helper utility to look up Telegram chat IDs for configuration

## Quick Start

### Prerequisites

- A [Telegram Bot](https://core.telegram.org/bots#how-do-i-create-a-bot) token
- An [OpenRouter](https://openrouter.ai/) API key
- Docker & Docker Compose on your server

### Configuration

All configuration is done via environment variables:

| Variable | Required | Description |
|---|:---:|---|
| `SLOP_TELEGRAM_TOKEN` | ✅ | Telegram Bot API token |
| `SLOP_OPENROUTER_KEY` | ✅ | OpenRouter API key |
| `SLOP_ADMIN_ID` | | Telegram user ID of the bot admin |
| `SLOP_ALLOWED_CHATS` | | Comma-separated chat IDs where the bot is allowed (e.g. `-100123,-100456`) |

### Deployment

See [`deploy/SETUP.md`](deploy/SETUP.md) for full first-time setup instructions (Docker Hub, GitHub secrets, VPS preparation).

Once set up, every push to `master` automatically:

1. Builds a Docker image
2. Pushes it to Docker Hub (tagged `latest` + commit SHA)
3. Deploys to your VPS via SSH

## Usage

### Talking to the Bot

Prefix your message with **`slop`** (or **`слоп`**):

```
slop what is the mass of the sun?
```

The bot maintains conversation context per chat — follow-up questions work naturally.

### Commands

| Command | Who | Description |
|---|---|---|
| `!reset` | Everyone | Clear conversation history for this chat |
| `!model` | Everyone | Show the currently active LLM model |
| `!models` | Admin | List all available models from OpenRouter |
| `!set_model <name>` | Admin | Switch to a different model (resets history) |
| `!version` | Admin | Show the build timestamp |

## Tech Stack

- **C# / .NET 10**
- **[Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot)** — Telegram API client
- **[OpenRouter API](https://openrouter.ai/docs/api-reference/overview)** — direct HTTP integration (HttpClient + System.Text.Json)
- **NLog** — file-based logging with daily rotation
- **Docker** + **GitHub Actions** — CI/CD pipeline

## License

[MIT](LICENSE)
