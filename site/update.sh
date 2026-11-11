#!/usr/bin/env bash
# Автообновление NareLauncher на VPS: тянет репозиторий и применяет изменения.
# Запускается systemd-таймером (см. install.sh). Если ничего не поменялось — выходит.

set -euo pipefail

SRC="/opt/narelauncher-mods"
WEBROOT="/var/www/narelauncher"

[ -d "$SRC/.git" ] || exit 0
cd "$SRC"

before="$(git rev-parse HEAD 2>/dev/null || echo none)"
git pull --ff-only --quiet || exit 0
after="$(git rev-parse HEAD 2>/dev/null || echo none)"
[ "$before" = "$after" ] && exit 0      # нет изменений — ничего не делаем

changed=0

# --- сайт ---
if [ -d "$SRC/site" ]; then
  cp -r "$SRC/site/." "$WEBROOT/"
  rm -f "$WEBROOT/install.sh" "$WEBROOT/update.sh" "$WEBROOT/DEPLOY.md"
  chown -R www-data:www-data "$WEBROOT"
  changed=1
fi

# --- nare-net.js (Node) ---
if [ -f "$SRC/narenet/nare-net.js" ] && [ -d /root/narenet ]; then
  if ! cmp -s "$SRC/narenet/nare-net.js" /root/narenet/nare-net.js; then
    cp "$SRC/narenet/nare-net.js" /root/narenet/nare-net.js
    systemctl restart narenet || true
    changed=1
  fi
fi

if [ "$changed" = "1" ]; then
  nginx -t >/dev/null 2>&1 && systemctl reload nginx || true
  logger "narelauncher: автообновление применено ($after)"
fi
