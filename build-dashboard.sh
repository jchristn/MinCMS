#!/usr/bin/env bash
set -euo pipefail

if [ -z "${1:-}" ]; then
    echo "Usage: build-dashboard.sh <tag> [namespace]"
    echo "Example: build-dashboard.sh v1.0.0"
    echo "Example: build-dashboard.sh v1.0.0 jchristn77"
    exit 1
fi

TAG="$1"
NAMESPACE="${2:-jchristn77}"
IMAGE=mincms-dashboard

cd "$(dirname "$0")"

echo "Building for linux/amd64 and linux/arm64/v8..."
if ! docker buildx build \
    -f dashboard/Dockerfile \
    --builder cloud-jchristn77-jchristn77 \
    --platform linux/amd64,linux/arm64/v8 \
    --tag "$NAMESPACE/$IMAGE:$TAG" \
    --tag "$NAMESPACE/$IMAGE:latest" \
    --push \
    dashboard/; then
    echo "Build failed for $IMAGE."
    exit 1
fi

echo "Done."
