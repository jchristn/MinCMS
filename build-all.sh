#!/usr/bin/env bash
set -euo pipefail

if [ -z "${1:-}" ]; then
    echo "Usage: build-all.sh <tag> [namespace]"
    echo "Example: build-all.sh v1.0.0"
    echo "Example: build-all.sh v1.0.0 jchristn77"
    exit 1
fi

TAG="$1"
NAMESPACE="${2:-}"

cd "$(dirname "$0")"

./build-dashboard.sh "$TAG" ${NAMESPACE:+"$NAMESPACE"}
./build-server.sh "$TAG" ${NAMESPACE:+"$NAMESPACE"}

echo "Done."
