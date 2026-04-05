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
```

---

## Done

Push to `master` and the GitHub Actions workflow will:

1. Build the Docker image
2. Push it to Docker Hub
3. Copy `docker-compose.yml` to `/opt/slopchat/`
4. SSH in, inject secrets as env vars, and run `docker compose up -d`
