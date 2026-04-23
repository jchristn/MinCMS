#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
COMPOSE_MAIN="$DOCKER_DIR/compose.yaml"

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
echo "  - Docker containers from docker/compose.yaml will be stopped and removed"
echo "  - docker/server/mincms.json will be restored to the bundled Less3-backed defaults"
echo "  - docker/dashboard/entrypoint.sh will be restored to the factory copy"
echo "  - docker/less3/system.json and docker/less3/less3.db will be restored"
echo "  - docker/server/logs, docker/dashboard/logs, docker/less3/logs,"
echo "    docker/less3/temp, and docker/less3/disk will be cleared"
echo
echo "After reset, MinCMS will target the bundled Less3 service using"
echo "access key default, secret key default, and bucket default."
echo
read -r -p "Type RESET to continue: " CONFIRM
if [[ "$CONFIRM" != "RESET" ]]; then
  echo
  echo "Aborted. No changes were made."
  exit 0
fi

echo
echo "[1/4] Stopping Docker deployment..."
compose_down "$COMPOSE_MAIN"
docker rm -f less3 >/dev/null 2>&1 || true
docker rm -f less3-ui >/dev/null 2>&1 || true
docker rm -f mincms-server >/dev/null 2>&1 || true
docker rm -f mincms-dashboard >/dev/null 2>&1 || true

echo
echo "[2/4] Restoring factory config files..."
restore_file "$SCRIPT_DIR/mincms_server_config/mincms.json" "$DOCKER_DIR/server/mincms.json"
restore_file "$SCRIPT_DIR/mincms_dashboard_config/entrypoint.sh" "$DOCKER_DIR/dashboard/entrypoint.sh"
restore_file "$SCRIPT_DIR/less3_config/system.json" "$DOCKER_DIR/less3/system.json"
restore_file "$SCRIPT_DIR/less3_database/less3.db" "$DOCKER_DIR/less3/less3.db"
rm -f "$DOCKER_DIR/less3/less3.db-shm" "$DOCKER_DIR/less3/less3.db-wal"
echo "        Restored MinCMS and Less3 deployment files"

echo
echo "[3/4] Resetting runtime directories..."
mirror_directory "$SCRIPT_DIR/mincms_server_logs" "$DOCKER_DIR/server/logs"
mirror_directory "$SCRIPT_DIR/mincms_dashboard_logs" "$DOCKER_DIR/dashboard/logs"
mirror_directory "$SCRIPT_DIR/less3_logs" "$DOCKER_DIR/less3/logs"
mirror_directory "$SCRIPT_DIR/less3_temp" "$DOCKER_DIR/less3/temp"
mirror_directory "$SCRIPT_DIR/less3_disk" "$DOCKER_DIR/less3/disk"
echo "        Cleared local MinCMS and Less3 runtime directories"

echo
echo "[4/4] Factory reset complete."
echo
echo "To restart the deployment:"
echo "  cd \"$DOCKER_DIR\""
echo "  docker compose up -d"
echo
echo "Less3 will be available on http://localhost:8000 and MinCMS on http://localhost:8200."
