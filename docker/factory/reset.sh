#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
COMPOSE_MAIN="$DOCKER_DIR/compose.yaml"
COMPOSE_SERVER="$DOCKER_DIR/compose-server.yaml"
COMPOSE_DASHBOARD="$DOCKER_DIR/compose-dashboard.yaml"

compose_down() {
  local compose_file="$1"

  if [[ -f "$compose_file" ]]; then
    docker compose -f "$compose_file" down --remove-orphans >/dev/null 2>&1 || true
  fi
}

restore_file() {
  local source_file="$1"
  local target_file="$2"

  if [[ ! -f "$source_file" ]]; then
    echo "Missing factory file: $source_file" >&2
    exit 1
  fi

  mkdir -p "$(dirname "$target_file")"
  cp "$source_file" "$target_file"
}

mirror_directory() {
  local source_dir="$1"
  local target_dir="$2"

  if [[ ! -d "$source_dir" ]]; then
    echo "Missing factory directory: $source_dir" >&2
    exit 1
  fi

  mkdir -p "$target_dir"
  rm -rf "$target_dir"/* "$target_dir"/.[!.]* "$target_dir"/..?* 2>/dev/null || true
  tar -C "$source_dir" -cf - . | tar -C "$target_dir" -xf -
}

echo
echo "=========================================================="
echo "  MinCMS Docker Factory Reset"
echo "=========================================================="
echo
echo "WARNING: This is a destructive reset of the local Docker"
echo "deployment state. The following will be changed:"
echo
echo "  - Docker containers from the MinCMS compose files will be stopped and removed"
echo "  - docker/server/mincms.json will be restored to the factory template"
echo "  - docker/dashboard/entrypoint.sh will be restored to the factory copy"
echo "  - docker/server/logs and docker/dashboard/logs will be cleared"
echo
echo "This reset does NOT delete collections or files already stored"
echo "in the configured S3-compatible bucket."
echo
echo "After reset, update docker/server/mincms.json with valid S3"
echo "settings before starting the deployment again."
echo
read -r -p "Type RESET to continue: " CONFIRM
if [[ "$CONFIRM" != "RESET" ]]; then
  echo
  echo "Aborted. No changes were made."
  exit 0
fi

echo
echo "[1/4] Stopping Docker deployments..."
compose_down "$COMPOSE_MAIN"
compose_down "$COMPOSE_SERVER"
compose_down "$COMPOSE_DASHBOARD"
docker rm -f mincms-server >/dev/null 2>&1 || true
docker rm -f mincms-dashboard >/dev/null 2>&1 || true

echo
echo "[2/4] Restoring factory config files..."
restore_file "$SCRIPT_DIR/mincms_server_config/mincms.json" "$DOCKER_DIR/server/mincms.json"
restore_file "$SCRIPT_DIR/mincms_dashboard_config/entrypoint.sh" "$DOCKER_DIR/dashboard/entrypoint.sh"
echo "        Restored server and dashboard deployment files"

echo
echo "[3/4] Resetting runtime directories..."
mirror_directory "$SCRIPT_DIR/mincms_server_logs" "$DOCKER_DIR/server/logs"
mirror_directory "$SCRIPT_DIR/mincms_dashboard_logs" "$DOCKER_DIR/dashboard/logs"
echo "        Cleared local MinCMS log directories"

echo
echo "[4/4] Factory reset complete."
echo
echo "To restart the deployment:"
echo "  cd \"$DOCKER_DIR\""
echo "  docker compose up -d"
echo
echo "Remember to reconfigure docker/server/mincms.json first if you need a working S3 connection."
