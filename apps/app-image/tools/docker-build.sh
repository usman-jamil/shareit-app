#!/usr/bin/env bash
#
# Builds the production image from the staged context. Any extra arguments are passed straight
# through to `docker build`, so `nx run app-image:docker-build --args="--no-cache"` works.
#
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

context_dir="dist/apps/app-image/context"
image_name="${APP_IMAGE_NAME:-app-image}"
image_tag="${APP_IMAGE_TAG:-local}"
image="${image_name}:${image_tag}"

if [[ ! -f "${context_dir}/Dockerfile" ]]; then
  printf 'docker-build: no staged context at %s. Run: nx run app-image:build\n' "$context_dir" >&2
  exit 1
fi

printf 'docker-build: building %s from %s\n' "$image" "$context_dir"

docker build \
  --tag "$image" \
  --file "${context_dir}/Dockerfile" \
  "$@" \
  "$context_dir"

printf 'docker-build: built %s\n' "$image"
