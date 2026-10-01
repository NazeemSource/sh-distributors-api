#!/usr/bin/env bash
set -euo pipefail
umask 077
base=/home/cwebsite
mode="${1:-}"
case "$mode" in preview|apply) ;; *) echo 'Use preview or apply.'; exit 1;; esac
if [ "$mode" = preview ]; then
  /usr/local/apps/php84/bin/php dedupe_products.php preview
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
trap 'status=$?; trap - EXIT; restart_api || status=1; exit "$status"' EXIT
result="$(/usr/local/apps/php84/bin/php dedupe_products.php apply REMOVE-EXACT-PRICE-DUPLICATES 2>&1)"
printf '%s\n' "$result"
case "$result" in *'DEDUPE COMPLETE.'*) ;; *) echo 'Dedupe did not complete.'; exit 1;; esac
verification="$(/usr/local/apps/php84/bin/php dedupe_products.php verify 2>&1)"
printf '%s\n' "$verification"
case "$verification" in *VERIFIED*) ;; *) echo 'Post-dedupe verification failed.'; exit 1;; esac
