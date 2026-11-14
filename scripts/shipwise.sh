#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
action=${1:-config}
shift || true
case "$action" in config|up|down|logs|build) ;; *) echo 'Use config|up|down|logs|build [profiles...]' >&2; exit 2 ;; esac
profiles=("$@")
if [ ${#profiles[@]} -eq 0 ]; then profiles=(gateway); fi
args=(compose -f deploy/compose.yaml)
for profile in "${profiles[@]}"; do
  case "$profile" in gateway|telemetry|infrastructure) ;; *) echo 'Unknown profile' >&2; exit 2 ;; esac
  args+=(--profile "$profile")
done
args+=("$action")
if [ "$action" = up ]; then args+=(-d --build); fi
docker "${args[@]}"
