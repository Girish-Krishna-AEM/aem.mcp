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
# keys. `--disablerepo` on individual installs isn't reliably enough on its
# own (dnf can still touch a repo during cache/metadata refresh), so disable
# any known-broken repo permanently up front instead.
BROKEN_REPOS="google-chrome"
for repo in $BROKEN_REPOS; do
    repofile="/etc/yum.repos.d/${repo}.repo"
    if [[ -f "$repofile" ]] && grep -q "^enabled=1" "$repofile" 2>/dev/null; then
        log "Disabling repo '$repo' (pre-existing on this instance, GPG key mismatch unrelated to this deploy)..."
        dnf config-manager --set-disabled "$repo" 2>/dev/null || sed -i 's/^enabled=1/enabled=0/' "$repofile"
    fi
done

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
    log "Docker Compose plugin not found — trying distro package first..."
    if ! (dnf install -y $DNF_SAFE_OPTS docker-compose-plugin || yum install -y docker-compose-plugin); then
        log "Distro package unavailable (common on Amazon Linux 2023's default repos) — installing the official binary directly..."
        COMPOSE_PLUGIN_DIR="/usr/local/lib/docker/cli-plugins"
        mkdir -p "$COMPOSE_PLUGIN_DIR"
        ARCH="$(uname -m)"
        curl -fsSL "https://github.com/docker/compose/releases/latest/download/docker-compose-linux-${ARCH}" \
            -o "$COMPOSE_PLUGIN_DIR/docker-compose"
        chmod +x "$COMPOSE_PLUGIN_DIR/docker-compose"
    fi
else
    log "Docker Compose plugin already installed ($(docker compose version))."
fi

if ! docker compose version &>/dev/null; then
    echo "Docker Compose plugin install failed — 'docker compose version' still doesn't work. Aborting." >&2
    exit 1
fi

# `docker compose build` requires buildx >= 0.17.0 (it shells out to buildx bake).
# Amazon Linux 2023's distro docker package ships with no buildx plugin at all (or
# a very old one via docker-buildx-plugin), which fails with "compose build
# requires buildx 0.17.0 or later" — confirmed hitting this on a fresh instance.
# Same idempotent fallback pattern as the Compose plugin above: distro package
# first, then the official GitHub-release binary if that's missing/too old.
log "Checking Docker Buildx plugin version..."
REQUIRED_BUILDX="0.17.0"

get_buildx_version() {
    docker buildx version 2>/dev/null | grep -oE 'v[0-9]+\.[0-9]+\.[0-9]+' | head -1 | sed 's/^v//' || true
}

buildx_meets_requirement() {
    local current="$1"
    [[ -z "$current" ]] && return 1
    local highest
    highest="$(printf '%s\n%s\n' "$REQUIRED_BUILDX" "$current" | sort -V | tail -1)"
    [[ "$highest" == "$current" ]]
}

CURRENT_BUILDX="$(get_buildx_version)"
if buildx_meets_requirement "$CURRENT_BUILDX"; then
    log "Docker Buildx already up to date (v$CURRENT_BUILDX)."
else
    log "Docker Buildx missing or older than $REQUIRED_BUILDX (found: ${CURRENT_BUILDX:-none}) — trying distro package first..."
    dnf install -y $DNF_SAFE_OPTS docker-buildx-plugin || yum install -y docker-buildx-plugin || true

    # Re-check the actual version after the distro install, not just whether the
    # command runs — AL2023's docker-buildx-plugin package can install successfully
    # while still being below 0.17.0, which would otherwise be silently accepted.
    CURRENT_BUILDX="$(get_buildx_version)"
    if buildx_meets_requirement "$CURRENT_BUILDX"; then
        log "Distro package provided Buildx v$CURRENT_BUILDX (meets requirement)."
    else
        log "Distro package unavailable or still below $REQUIRED_BUILDX (found: ${CURRENT_BUILDX:-none}) — installing the official binary directly..."
        BUILDX_PLUGIN_DIR="/usr/local/lib/docker/cli-plugins"
        mkdir -p "$BUILDX_PLUGIN_DIR"
        case "$(uname -m)" in
            x86_64) BUILDX_ARCH="amd64" ;;
            aarch64) BUILDX_ARCH="arm64" ;;
            *) BUILDX_ARCH="$(uname -m)" ;;
        esac
        BUILDX_TAG="$(curl -fsSL https://api.github.com/repos/docker/buildx/releases/latest \
            | sed -n 's/.*"tag_name": *"\([^"]*\)".*/\1/p' | head -1 || true)"
        if [[ -z "$BUILDX_TAG" ]]; then
            echo "Could not determine the latest Buildx release tag from the GitHub API. Aborting." >&2
            exit 1
        fi
        curl -fsSL "https://github.com/docker/buildx/releases/download/${BUILDX_TAG}/buildx-${BUILDX_TAG}.linux-${BUILDX_ARCH}" \
            -o "$BUILDX_PLUGIN_DIR/docker-buildx"
        chmod +x "$BUILDX_PLUGIN_DIR/docker-buildx"
    fi
fi

CURRENT_BUILDX="$(get_buildx_version)"
if ! buildx_meets_requirement "$CURRENT_BUILDX"; then
    echo "Docker Buildx plugin install failed or is still below $REQUIRED_BUILDX (found: ${CURRENT_BUILDX:-none}). Aborting." >&2
    exit 1
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
