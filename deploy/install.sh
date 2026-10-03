#!/usr/bin/env bash
set -euo pipefail
umask 077
base=/home/cwebsite
build="$(pwd -P)"
case "$build" in "$base/apps/shdistrapi-builds/"*) ;; *) echo 'Unexpected API release directory'; exit 1;; esac
release="$build/release/api"
shared="$base/apps/shdistrapi-shared"
public="$base/public_html/shdistrapi/public"
mkdir -p "$shared" "$public"
# Prevent the watchdog from restarting the previous release between stop and start.
exec 9>"$shared/api-keepalive.lock"
flock -x 9
cp deploy/.htaccess "$public/.htaccess"
/usr/local/apps/php84/bin/php deploy/configure.php
chmod +x "$release/API"
ln -sfn "$shared/appsettings.Production.json" "$release/appsettings.Production.json"
pidfile="$shared/api.pid"
if [ -f "$pidfile" ]; then
  pid="$(cat "$pidfile")"; case "$pid" in ''|*[!0-9]*) exit 1;; esac
  actual="$(readlink -f "/proc/$pid/cwd" 2>/dev/null || true)"
  case "$actual" in "$base/apps/shdistrapi-builds/"*/release/api) kill "$pid"; for i in $(seq 1 30); do kill -0 "$pid" 2>/dev/null || break; sleep 1; done; if kill -0 "$pid" 2>/dev/null; then echo 'Previous API process did not stop'; exit 1; fi;; '') ;; *) echo 'Refusing to stop unrelated API process'; exit 1;; esac
fi
cd "$release"
ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://127.0.0.1:5041 nohup ./API > "$shared/api.log" 2>&1 < /dev/null 9>&- &
newpid=$!
echo "$newpid" > "$pidfile"
healthy=0
for i in $(seq 1 60); do
  if ! kill -0 "$newpid" 2>/dev/null; then tail -n 100 "$shared/api.log"; exit 1; fi
  actual="$(readlink -f "/proc/$newpid/cwd" 2>/dev/null || true)"
  if [ "$actual" != "$release" ]; then echo 'New API process is not running from this release'; exit 1; fi
  if curl -fsS http://127.0.0.1:5041/health >/dev/null 2>&1; then healthy=1; break; fi
  sleep 2
done
if [ "$healthy" != 1 ]; then tail -n 100 "$shared/api.log"; exit 1; fi
ln -sfn "$build" "$base/apps/shdistrapi-current"
watchdog="* * * * * /bin/bash $base/apps/shdistrapi-current/deploy/keepalive.sh >/dev/null 2>&1"
existing_cron="$(crontab -l 2>/dev/null || true)"
{ printf '%s\n' "$existing_cron" | sed '\|shdistrapi-current/deploy/keepalive.sh|d'; printf '%s\n' "$watchdog"; } | crontab -
echo 'Deployment completed and health check passed.'
