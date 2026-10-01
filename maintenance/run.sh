#!/usr/bin/env bash
set -euo pipefail
umask 077
base=/home/cwebsite
mode="${1:-}"
case "$mode" in preview|apply) ;; *) echo 'Use preview or apply.'; exit 1;; esac
test "$(pwd -P)" != / || exit 1
if [ "$mode" = preview ]; then
  /usr/local/apps/php84/bin/php reset_live.php preview
  exit
fi

current="$(readlink -f "$base/apps/shdistrapi-current/release/api")"
case "$current" in "$base/apps/shdistrapi-builds/"*/release/api) ;; *) echo 'Unexpected API release path'; exit 1;; esac
pidfile="$base/apps/shdistrapi-shared/api.pid"
if [ -f "$pidfile" ]; then
  pid="$(cat "$pidfile")"
  case "$pid" in ''|*[!0-9]*) echo 'Invalid API pid'; exit 1;; esac
  actual="$(readlink -f "/proc/$pid/cwd" 2>/dev/null || true)"
  if [ -n "$actual" ] && [ "$actual" != "$current" ]; then echo 'Refusing to stop unrelated process'; exit 1; fi
  if [ "$actual" = "$current" ]; then
    kill "$pid"
    for i in $(seq 1 30); do kill -0 "$pid" 2>/dev/null || break; sleep 1; done
    if kill -0 "$pid" 2>/dev/null; then echo 'API did not stop'; exit 1; fi
  fi
fi
restart_api() {
  cd "$current"
  ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://127.0.0.1:5041 nohup ./API > "$base/apps/shdistrapi-shared/api.log" 2>&1 < /dev/null &
  echo $! > "$pidfile"
  for i in $(seq 1 60); do
    if curl -fsS http://127.0.0.1:5041/health >/dev/null 2>&1; then echo 'API healthy'; return 0; fi
    sleep 2
  done
  echo 'API failed to restart'
  return 1
}
trap 'restart_api' EXIT
/usr/local/apps/php84/bin/php reset_live.php apply RESET-LIVE-178
cd "$(dirname "$0")"
/usr/local/apps/php84/bin/php reset_live.php verify
