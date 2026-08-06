#!/bin/bash
#
# PID 1 for the app image. Runs the two .NET processes that make up the container and ties their
# lifetimes together: if either one stops, the container stops.
#
# Why bash rather than /bin/sh: the coordination this needs is "block until *any* child exits, and
# tell me which one". In POSIX sh that means either polling, which cannot distinguish a running
# child from an unreaped zombie (`kill -0` succeeds for both), or a FIFO wrapper that puts a
# subshell between us and the child and breaks signal forwarding. `wait -n -p` answers it exactly,
# with no race and no extra process. bash is already in the Debian-based aspnet image, so this
# costs nothing to ship — the Dockerfile asserts it is present at build time. Moving to a chiseled
# or distroless base would mean replacing this script (and the health check).
#
set -uo pipefail

APP_DIR="${APP_DIR:-/app}"
API_DIR="${APP_DIR}/api"
HOST_DIR="${APP_DIR}/host"

# Assembly names are resolved from the published output when the image is staged, rather than
# hardcoded here. See apps/app-image/tools/stage-artifacts.sh.
# shellcheck source=/dev/null
source "${APP_DIR}/app.env"

# Addresses. The API is loopback-only: it is not published by Docker and must not be reachable from
# outside the container. The host is the single public entry point.
API_URLS="${APP_IMAGE_API_URLS:-http://127.0.0.1:5000}"
HOST_URLS="${APP_IMAGE_HOST_URLS:-http://0.0.0.0:8080}"

API_ENVIRONMENT="${APP_IMAGE_API_ENVIRONMENT:-${ASPNETCORE_ENVIRONMENT:-Production}}"
HOST_ENVIRONMENT="${APP_IMAGE_HOST_ENVIRONMENT:-${ASPNETCORE_ENVIRONMENT:-Production}}"

# How long children get to drain in-flight requests before being killed.
SHUTDOWN_TIMEOUT="${APP_IMAGE_SHUTDOWN_TIMEOUT_SECONDS:-15}"

declare -A pending=()   # pid -> name, for children that are still running
started_pid=0
shutting_down=0
exit_code=0

log() { printf '[entrypoint] %s\n' "$*" >&2; }

fail() {
  log "$*"
  exit 1
}

is_pending() { [[ -n "${pending[$1]:-}" ]]; }

# Starts one child with its own environment, and leaves its pid in $started_pid. Arguments after the
# fifth are extra NAME=VALUE pairs applied to that child only.
#
# Every variable that decides where a process listens is set inline here, and the ones ASP.NET Core
# would otherwise pick up from the container environment are removed, so a stray ASPNETCORE_URLS
# cannot make the API bind a public port or make the two processes fight over 8080. Each child also
# runs with its own directory as the working directory, which is what ASP.NET Core uses as the
# content root — that is how each one finds its own appsettings.json and not the other's.
#
# The subshell `exec`s, so $! is the dotnet process itself: signals reach it directly and there is
# no intermediate shell to leave behind. This must not be called in a command substitution — that
# would run it in a subshell, and the child would not be a job of this shell for `wait` to see.
start_child() {
  local name="$1" dir="$2" dll="$3" urls="$4" environment="$5"
  shift 5
  local extra_env=("$@")

  [[ -f "${dir}/${dll}" ]] || fail "${dir}/${dll} is missing; the image was not staged correctly."

  (
    cd "$dir" || exit 127
    exec env -u ASPNETCORE_HTTP_PORTS -u ASPNETCORE_HTTPS_PORTS -u DOTNET_URLS -u APP_IMAGE_HEALTHCHECK_URL \
      ASPNETCORE_URLS="$urls" \
      ASPNETCORE_ENVIRONMENT="$environment" \
      ${extra_env[@]+"${extra_env[@]}"} \
      dotnet "./${dll}"
  ) &

  started_pid=$!
  pending["$started_pid"]="$name"
  log "started ${name} (pid ${started_pid}) on ${urls}"
}

stop_children() {
  local signal="${1:-TERM}" pid
  for pid in "${!pending[@]}"; do
    kill -"$signal" "$pid" 2>/dev/null || true
  done
}

on_signal() {
  local signal="$1"

  if (( shutting_down != 0 )); then
    # A second signal during the drain means "stop waiting".
    log "received SIG${signal} again; sending SIGKILL"
    stop_children KILL
    return 0
  fi

  shutting_down=1
  log "received SIG${signal}; forwarding to children"
  stop_children TERM
}

trap 'on_signal TERM' TERM
trap 'on_signal INT' INT
trap 'on_signal HUP' HUP

# Records a child's exit. The first one to go decides the container's fate: a required process that
# stops on its own is a failure even when it exited cleanly, because the container would otherwise
# keep serving a half-broken stack.
handle_exit() {
  local pid="$1" status="$2" name="${pending[$1]}"
  unset "pending[$pid]"

  if (( shutting_down != 0 )); then
    log "${name} exited with status ${status}"
    return 0
  fi

  log "${name} exited unexpectedly with status ${status}; stopping the container"
  exit_code=$(( status == 0 ? 1 : status ))
  shutting_down=1
  stop_children TERM
}

# The API logs through Serilog, and its Serilog configuration lives only in
# appsettings.Development.json. In Production `ReadFrom.Configuration` finds no Serilog section and
# builds a logger with no sinks, and `AddSerilog` has already replaced the default providers — so
# the API is completely silent, including its stack traces. A container whose main process writes
# nothing to stdout cannot be operated, so it gets a console sink by default here.
#
# Any Serilog__* variable on the container switches this off completely, so a real logging
# configuration (Seq, a file, a different level) always wins rather than being merged into.
api_logging=()
if ! compgen -e | grep -q '^Serilog__'; then
  api_logging=(
    "Serilog__Using__0=Serilog.Sinks.Console"
    "Serilog__MinimumLevel__Default=Information"
    "Serilog__MinimumLevel__Override__Microsoft.AspNetCore=Warning"
    "Serilog__WriteTo__0__Name=Console"
  )
  log "no Serilog__* configuration supplied; defaulting the api to a console sink"
fi

start_child api "$API_DIR" "$APP_IMAGE_API_DLL" "$API_URLS" "$API_ENVIRONMENT" ${api_logging[@]+"${api_logging[@]}"}
start_child host "$HOST_DIR" "$APP_IMAGE_HOST_DLL" "$HOST_URLS" "$HOST_ENVIRONMENT"

# Phase 1 — run until a signal arrives or a child exits.
while (( shutting_down == 0 )) && (( ${#pending[@]} > 0 )); do
  # `wait -n -p` *unsets* the variable when it returns without a pid, so every read of it below
  # has to tolerate that — an unset read would abort the script under `set -u` mid-shutdown.
  finished_pid=""
  wait -n -p finished_pid
  status=$?

  # No pid means `wait` was interrupted by a trapped signal rather than by a child exiting.
  if [[ -n "${finished_pid:-}" ]] && is_pending "$finished_pid"; then
    handle_exit "$finished_pid" "$status"
  fi
done

# Phase 2 — drain. A watchdog job lets `wait -n` return on a timeout without polling for it.
if (( ${#pending[@]} > 0 )); then
  sleep "$SHUTDOWN_TIMEOUT" &
  watchdog_pid=$!

  while (( ${#pending[@]} > 0 )); do
    finished_pid=""
    wait -n -p finished_pid
    status=$?

    [[ -n "${finished_pid:-}" ]] || continue

    if [[ "$finished_pid" == "$watchdog_pid" ]]; then
      log "children did not exit within ${SHUTDOWN_TIMEOUT}s; sending SIGKILL"
      stop_children KILL
      watchdog_pid=0
      continue
    fi

    if is_pending "$finished_pid"; then
      handle_exit "$finished_pid" "$status"
    fi
  done

  if (( watchdog_pid != 0 )); then
    kill -TERM "$watchdog_pid" 2>/dev/null || true
  fi
fi

# Reap anything still outstanding so nothing is left behind as a zombie.
wait 2>/dev/null || true

log "exiting with status ${exit_code}"
exit "$exit_code"
