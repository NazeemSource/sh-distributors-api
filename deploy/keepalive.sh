#!/usr/bin/env bash
set -euo pipefail
base=/home/cwebsite
shared="$base/apps/shdistrapi-shared"
exec 9>"$shared/api-keepalive.lock"
flock -n 9 || exit 0
if curl -fsS --max-time 5 http://127.0.0.1:5041/health >/dev/null 2>&1; then exit 0; fi
pidfile="$shared/api.pid"
if [ -f "$pidfile" ]; then pid="$(cat "$pidfile")"; case "$pid" in ''|*[!0-9]*) exit 1;; esac; if kill -0 "$pid" 2>/dev/null; then exit 1; fi; fi
build="$(readlink -f "$base/apps/shdistrapi-current")"
case "$build" in "$base/apps/shdistrapi-builds/"*) ;; *) exit 1;; esac
release="$build/release/api"
cd "$release"
ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://127.0.0.1:5041 nohup ./API >> "$shared/api.log" 2>&1 < /dev/null &
echo $! > "$pidfile"
for i in $(seq 1 45); do if curl -fsS --max-time 5 http://127.0.0.1:5041/health >/dev/null 2>&1; then exit 0; fi; sleep 2; done
exit 1
