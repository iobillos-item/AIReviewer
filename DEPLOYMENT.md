# AI Reviewer — Docker Deployment Guide

This guide covers deploying the AI PR Reviewer on a fresh machine using Docker.
Both Linux (Ubuntu) and Windows instructions are provided.

---

## Prerequisites

The only software you need to install is:
- **Git** (to clone the repo)
- **Docker** and **Docker Compose**

Everything else (the .NET runtime, PostgreSQL with pgvector) runs inside containers.

---

## Part A — Linux (Ubuntu 22.04 / 24.04)

### 1. Install Docker

```bash
# Update packages
sudo apt-get update
sudo apt-get install -y ca-certificates curl

# Add Docker's official GPG key and repo
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin

# Allow running docker without sudo
sudo usermod -aG docker $USER
newgrp docker

# Verify
docker --version
docker compose version
```

### 2. Install Git

```bash
sudo apt-get install -y git
```

### 3. Clone the Repository

```bash
git clone https://github.com/your-org/ai-reviewer.git
cd ai-reviewer
```

### 4. Configure Environment Variables

```bash
# Copy the example env file
cp .env.example .env

# Edit with your actual values
nano .env
```

Fill in your real values:

```
DB_PASSWORD=YourSecureDbPassword
GITHUB_ACCESS_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxx
GITHUB_REPOSITORY=your-org/your-repo
OPENAI_API_KEY=sk-xxxxxxxxxxxxxxxxxxxx
OPENAI_MODEL=gpt-4o
```

Save and exit (Ctrl+X, Y, Enter in nano).

### 5. Build and Start

```bash
docker compose up -d --build
```

This will:
- Pull the pgvector image
- Build the AI Reviewer app image (runs tests during build)
- Start PostgreSQL and wait for it to be healthy
- Automatically run the migration SQL (via docker-entrypoint-initdb.d)
- Start the AI Reviewer API on port 8080

### 6. Verify Everything Is Running

```bash
# Check containers
docker compose ps

# Check app logs
docker compose logs aireviewer --tail 50

# Check database
docker exec aireviewer-db psql -U postgres -d ai_reviewer -c "\dt"
# Should show: sop_embeddings table

# Test the API
curl -X POST http://localhost:8080/api/github/test
```

### 7. Ingest SOP Rules

```bash
curl -X POST http://localhost:8080/api/admin/ingest-sop
```

### 8. Expose to the Internet (for GitHub Webhooks)

Option A — If the machine has a public IP (e.g., Azure VM, AWS EC2):

Your webhook URL is: `http://YOUR_PUBLIC_IP:8080/api/github/webhook`

For production, put Nginx in front:

```bash
sudo apt-get install -y nginx

sudo tee /etc/nginx/sites-available/aireviewer > /dev/null <<EOF
server {
    listen 80;
    server_name YOUR_DOMAIN_OR_IP;

    location / {
        proxy_pass http://localhost:8080;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_read_timeout 300s;
    }
}
EOF

sudo ln -sf /etc/nginx/sites-available/aireviewer /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t
sudo systemctl restart nginx
```

Add HTTPS with Let's Encrypt:

```bash
sudo apt-get install -y certbot python3-certbot-nginx
sudo certbot --nginx -d yourdomain.com
```

Webhook URL becomes: `https://yourdomain.com/api/github/webhook`

Option B — If behind NAT (home/office network):

```bash
# Install ngrok
curl -sSL https://ngrok-agent.s3.amazonaws.com/ngrok-v3-stable-linux-amd64.tgz | sudo tar xz -C /usr/local/bin
ngrok config add-authtoken YOUR_NGROK_TOKEN

# Start tunnel
ngrok http 8080
```

Use the ngrok URL as your webhook URL.

### 9. Configure GitHub Webhook

1. GitHub repo → Settings → Webhooks → Add webhook
2. Payload URL: your public URL + `/api/github/webhook`
3. Content type: `application/x-www-form-urlencoded`
4. Events: Pull requests, Pushes
5. Save

### 10. Managing the Deployment

```bash
# View logs
docker compose logs -f aireviewer

# Restart
docker compose restart

# Stop everything
docker compose down

# Stop and remove data (fresh start)
docker compose down -v

# Update after code changes
git pull
docker compose up -d --build
```

---

## Part B — Windows 10/11

### 1. Install Docker Desktop

1. Download from: https://www.docker.com/products/docker-desktop/
2. Run the installer
3. Choose WSL 2 backend when prompted (recommended)
4. Restart your computer when prompted
5. Open Docker Desktop and wait for it to start
6. Open PowerShell and verify:

```powershell
docker --version
docker compose version
```

### 2. Install Git

1. Download from: https://git-scm.com/download/win
2. Run the installer (default options are fine)
3. Verify:

```powershell
git --version
```

### 3. Clone the Repository

```powershell
git clone https://github.com/your-org/ai-reviewer.git
cd ai-reviewer
```

### 4. Configure Environment Variables

```powershell
# Copy the example env file
Copy-Item .env.example .env

# Edit with notepad
notepad .env
```

Fill in your real values:

```
DB_PASSWORD=YourSecureDbPassword
GITHUB_ACCESS_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxx
GITHUB_REPOSITORY=your-org/your-repo
OPENAI_API_KEY=sk-xxxxxxxxxxxxxxxxxxxx
OPENAI_MODEL=gpt-4o
```

Save and close notepad.

### 5. Build and Start

```powershell
docker compose up -d --build
```

This will:
- Pull the pgvector image
- Build the AI Reviewer app image (runs tests during build)
- Start PostgreSQL and wait for it to be healthy
- Automatically run the migration SQL
- Start the AI Reviewer API on port 8080

Note: The first build takes a few minutes to download the .NET SDK and runtime images.

### 6. Verify Everything Is Running

```powershell
# Check containers
docker compose ps

# Check app logs
docker compose logs aireviewer --tail 50

# Check database
docker exec aireviewer-db psql -U postgres -d ai_reviewer -c "\dt"

# Test the API
Invoke-RestMethod -Uri http://localhost:8080/api/github/test -Method Post
```

### 7. Ingest SOP Rules

```powershell
Invoke-RestMethod -Uri http://localhost:8080/api/admin/ingest-sop -Method Post
```

### 8. Expose to the Internet (for GitHub Webhooks)

Option A — ngrok (recommended for local machines):

1. Download ngrok from: https://ngrok.com/download
2. Unzip to a folder like `C:\tools\ngrok`
3. Sign up at https://ngrok.com and get your auth token
4. Configure and run:

```powershell
C:\tools\ngrok\ngrok.exe config add-authtoken YOUR_NGROK_TOKEN

# If you have a static domain:
C:\tools\ngrok\ngrok.exe http --domain=your-subdomain.ngrok-free.app 8080

# If you don't have a static domain:
C:\tools\ngrok\ngrok.exe http 8080
```

Option B — If the machine has a public IP (Azure VM, etc.):

Your webhook URL is: `http://YOUR_PUBLIC_IP:8080/api/github/webhook`

For production, set up IIS as a reverse proxy (see Windows Server deployment guide).

### 9. Configure GitHub Webhook

1. GitHub repo → Settings → Webhooks → Add webhook
2. Payload URL: your public URL + `/api/github/webhook`
3. Content type: `application/x-www-form-urlencoded`
4. Events: Pull requests, Pushes
5. Save

### 10. Managing the Deployment

```powershell
# View logs
docker compose logs -f aireviewer

# Restart
docker compose restart

# Stop everything
docker compose down

# Stop and remove data (fresh start)
docker compose down -v

# Update after code changes
git pull
docker compose up -d --build
```

---

## Troubleshooting

### App can't connect to database

```bash
# Check if pgvector is healthy
docker compose ps

# Check pgvector logs
docker compose logs pgvector

# Manually test connection from app container
docker exec aireviewer-api bash -c "apt-get update && apt-get install -y postgresql-client && psql -h pgvector -U postgres -d ai_reviewer -c '\dt'"
```

### Migration didn't run

The migration SQL runs automatically on first start via `docker-entrypoint-initdb.d`.
If the database already existed (from a previous run), it won't re-run.
To force it:

```bash
# Remove the volume and restart
docker compose down -v
docker compose up -d --build
```

Or run it manually:

```bash
docker exec -i aireviewer-db psql -U postgres -d ai_reviewer < AIReviewer.Infrastructure/Migrations/001_CreateSopEmbeddings.sql
```

### GitHub webhook returns 422 or errors

Check the webhook delivery tab in GitHub (Settings → Webhooks → Recent Deliveries)
and check app logs:

```bash
docker compose logs aireviewer --tail 100
```

### OpenAI rate limiting (429 errors)

The app has built-in retry logic with exponential backoff (up to 3 retries).
If you're hitting limits frequently, consider upgrading your OpenAI plan or
reducing the number of agents in `Program.cs`.

### Rebuilding after code changes

```bash
docker compose up -d --build
```

This rebuilds only the app container. The database volume persists.
