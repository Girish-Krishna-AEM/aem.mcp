#!/usr/bin/env bash
#
# Deploys (or updates) the Lightning Detection MCP stack on an Amazon Linux 2023 EC2
# instance: installs Docker + Compose if missing, clones/updates the repo, and runs
# docker compose build && up -d. Safe to re-run — every step is idempotent.
#
# Usage (on the EC2 instance):
#   chmod +x deploy-ec2.sh
#   sudo ./deploy-ec2.sh
#
# Override the repo URL or target directory if needed:
#   REPO_URL=https://github.com/you/fork.git TARGET_DIR=/opt/my-dir sudo ./deploy-ec2.sh

set -euo pipefail

REPO_URL="${REPO_URL:-https://github.com/Girish-Krishna-AEM/aem.mcp.git}"
TARGET_DIR="${TARGET_DIR:-/opt/lightning-mcp}"
DEPLOY_USER="${SUDO_USER:-ec2-user}"

log() { echo -e "\n>>> $1"; }

if [[ $EUID -ne 0 ]]; then
    echo "This script installs system packages and must be run with sudo." >&2
    exit 1
fi

# No blanket `dnf/yum update` here — this instance is repurposed and may have
# unrelated third-party repos (e.g. google-chrome) with broken/mismatched GPG
# keys that would abort the whole script. Only the specific packages we need
# are installed below, with that repo disabled defensively in case it's ever
# hit during metadata refresh.
DNF_SAFE_OPTS="--disablerepo=google-chrome"

log "Checking Docker..."
if ! command -v docker &>/dev/null; then
    log "Docker not found — installing..."
    dnf install -y $DNF_SAFE_OPTS docker || yum install -y docker
    systemctl enable --now docker
else
    log "Docker already installed ($(docker --version))."
    systemctl enable --now docker
fi

usermod -aG docker "$DEPLOY_USER" || true

log "Checking Docker Compose plugin..."
if ! docker compose version &>/dev/null; then
    log "Docker Compose plugin not found — installing..."
    dnf install -y $DNF_SAFE_OPTS docker-compose-plugin || yum install -y docker-compose-plugin
else
    log "Docker Compose plugin already installed ($(docker compose version))."
fi

log "Checking git..."
if ! command -v git &>/dev/null; then
    log "git not found — installing..."
    dnf install -y $DNF_SAFE_OPTS git || yum install -y git
fi

log "Preparing $TARGET_DIR..."
mkdir -p "$TARGET_DIR"
chown "$DEPLOY_USER":"$DEPLOY_USER" "$TARGET_DIR"

if [[ -d "$TARGET_DIR/.git" ]]; then
    log "Repo already present — pulling latest..."
    sudo -u "$DEPLOY_USER" git -C "$TARGET_DIR" pull origin main
else
    log "Cloning repo into $TARGET_DIR..."
    sudo -u "$DEPLOY_USER" git clone "$REPO_URL" "$TARGET_DIR"
fi

cd "$TARGET_DIR"

if [[ ! -f .env ]]; then
    log "No .env found — creating from .env.example."
    cp .env.example .env
    chown "$DEPLOY_USER":"$DEPLOY_USER" .env
    echo
    echo "!!! ACTION REQUIRED !!!"
    echo "Edit $TARGET_DIR/.env and set the real values for:"
    echo "  - LIGHTNING_PULSE_API_KEY"
    echo "  - WEATHER_FORECAST_API_BASE_URL"
    echo "Then re-run this script (it will skip re-cloning and go straight to build+up)."
    exit 0
else
    log ".env already present — leaving it as-is."
fi

if ! grep -q "^WEATHER_FORECAST_API_BASE_URL=.\+" .env; then
    echo
    echo "!!! WARNING !!!"
    echo "WEATHER_FORECAST_API_BASE_URL is empty in .env — the real daily/hourly weather"
    echo "forecast tools will fail at call time until this is set. Continuing anyway"
    echo "(the lightning-strike and mock-weather tools don't need it)."
fi

log "Building images..."
docker compose build

log "Starting services..."
docker compose up -d

log "Waiting for health checks..."
sleep 10
docker compose ps

log "Health check results:"
curl -sf http://localhost:8020/health && echo || echo "lightning-mcp-server not healthy yet — check: docker compose logs lightning-mcp-server"
curl -sf http://localhost:8001/health && echo || echo "coordinator-agent not healthy yet — check: docker compose logs coordinator-agent"

log "Done. Tail logs with: docker compose logs -f"
