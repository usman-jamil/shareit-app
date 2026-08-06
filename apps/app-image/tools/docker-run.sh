#!/usr/bin/env bash
#
# Runs the production image locally. Publishes 8080 only — the API's loopback port stays inside the
# container. Extra arguments are passed through to `docker run`, so environment the API needs can be
# supplied as `nx run app-image:docker-run --args="-e ConnectionStrings__Database=..."`.
#
set -euo pipefail

image_name="${APP_IMAGE_NAME:-app-image}"
image_tag="${APP_IMAGE_TAG:-local}"
image="${image_name}:${image_tag}"
container_name="${APP_IMAGE_CONTAINER_NAME:-app-image}"
published_port="${APP_IMAGE_PORT:-8080}"

printf 'docker-run: starting %s on http://localhost:%s\n' "$image" "$published_port"

exec docker run --rm \
  --name "$container_name" \
  --publish "${published_port}:8080" \
  "$@" \
  "$image"
