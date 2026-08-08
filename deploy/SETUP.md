# SlopChat — First-Time Setup

Follow these steps **once** before the first Docker-based deploy.

---

## 1. Docker Hub

1. Sign in at [hub.docker.com](https://hub.docker.com).
2. Create a repository named **slopchat** (public or private — free tier supports one private repo).
3. Go to **Account Settings → Security → New Access Token**.
   - Description: `github-actions`
   - Permissions: **Read & Write**
4. Copy the token — you'll need it in the next step.

---

## 2. GitHub Repository Secrets

Go to **Settings → Secrets and variables → Actions** and add:

| Secret | Value |
|---|---|
| `DOCKERHUB_USERNAME` | Your Docker Hub username |
| `DOCKERHUB_TOKEN` | The access token from step 1 |
| `VPS_HOST` | Droplet IP address *(keep existing)* |
| `VPS_USER` | SSH username *(keep existing)* |
| `VPS_SSH_KEY` | SSH private key *(keep existing)* |
| `SLOP_TELEGRAM_TOKEN` | Telegram bot token *(keep existing)* |
| `SLOP_OPENROUTER_KEY` | OpenRouter API key *(keep existing)* |
| `SLOP_ADMIN_ID` | Telegram admin user ID *(keep existing)* |
| `SLOP_ALLOWED_CHATS` | Comma-separated allowed chat IDs *(keep existing)* |
| `SLOP_CRAWL4AI_TOKEN` | Random shared secret between SlopMcp and crawl4ai (`openssl rand -hex 32`) |

---

## 3. VPS — Install Docker

SSH into the droplet and run:

```bash
# Install Docker (official method for Ubuntu 24.04)
sudo apt-get update
sudo apt-get install -y ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
```

Verify:

```bash
docker --version
docker compose version
```

---

## 4. VPS — Allow Your User to Run Docker (Optional)

If deploying as a non-root user:

```bash
sudo usermod -aG docker $USER
# Log out and back in for group change to take effect
```

---

## 5. VPS — Create Deploy Directory

```bash
sudo mkdir -p /opt/slopchat
sudo chown $USER:$USER /opt/slopchat
```

---

## 6. Reading Logs

The bot writes file logs to `/app/logs/` inside the container, which is mounted to `/opt/slopchat/logs/` on the host.

```bash
# Follow the latest log file
tail -f /opt/slopchat/logs/*.log

# List all log files
ls -lt /opt/slopchat/logs/

# Search logs for a keyword
grep -r "error" /opt/slopchat/logs/

# View Docker stdout/stderr (NLog console output)
docker logs slopchat --tail 100 -f

# Reading crawl4ai logs
docker logs crawl4ai --tail 200 -f
```

With `GUNICORN_CMD_ARGS=--access-logfile - ...` set in `docker-compose.yml`, every HTTP
request to the crawl4ai container produces a gunicorn access log line like:

```
[2026-08-08 09:57:24 +0000] [25] [INFO] 172.20.0.3:57292 - "POST /crawl/job HTTP/1.1" 400 87 "-" "-"
```

When a non-2xx is returned, SlopMcp also logs the first 500 characters of the response
body at ERROR level (including the correlation id), so you can cross-reference
`docker logs crawl4ai` with SlopMcp's own log file. The same 400 body preview is
included in the tool result text returned to the LLM / visible in the bot log.

**Why `CRAWL4AI_ALLOW_INTERNAL_URLS=true` is set on the crawl4ai service.**
Crawl4ai 0.9's egress guard rejects any URL whose resolved IP is not globally
routable — including our webhook callback `http://slopmcp:8080/internal/crawl4ai-callback`,
which resolves to the private Docker-compose subnet (172.x.x.x). Without this flag,
every `POST /crawl/job` with a `webhook_config` on the compose network returns
`HTTP 400 {"detail":"URL blocked"}`. Enabling the flag is safe here: crawl4ai
is not published on any host port, still requires the bearer token, and the
SlopMcp-side `FetchUrlTool.IsInternalHost` SSRF filter blocks LLM-supplied
internal targets before they ever reach crawl4ai.

## Anti-bot detection

Some sites detect headless browsers and return CAPTCHAs, blocks, or timeouts
(especially likely from a datacentre IP such as DigitalOcean). Our `Crawl4AiClient`
submits every crawl job with `browser_config.params.enable_stealth = true`, which
enables crawl4ai's playwright-stealth patches (removes `navigator.webdriver`,
patches plugin/CDP fingerprints, adjusts navigator properties). Combined with the
server-side `simulate_user: true` default from crawl4ai's `config.yml`, this is
step 1 of crawl4ai's documented progressive-enhancement anti-detection ladder.

The stronger options — `magic`, `override_navigator`, `simulate_user` (per-request),
and the `UndetectedAdapter` browser adapter — are **not** exposed via the crawl4ai
HTTP API (`UNTRUSTED_FORBIDDEN_FIELDS` in `async_configs.py`). Using them would
require running crawl4ai in-process via the Python SDK. If stealth alone proves
insufficient for a given site, the practical options are (a) route the crawl4ai
container's egress through a residential proxy, or (b) accept that the site is
unreachable from our datacentre IP and pick a different source.

---

## Done

Push to `master` and the GitHub Actions workflow will:

1. Build the Docker image
2. Push it to Docker Hub
3. Copy `docker-compose.yml` to `/opt/slopchat/`
4. SSH in, inject secrets as env vars, and run `docker compose up -d`
