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

# --- правило nginx для красивых ссылок категорий (однократно, с проверкой и бэкапом) ---
NGINX_CONF=/etc/nginx/sites-available/narelauncher
if [ -f "$NGINX_CONF" ] && ! grep -q "narecat" "$NGINX_CONF"; then
  cp "$NGINX_CONF" "$NGINX_CONF.bak"
  awk '1; /index index\.php/ && !d {
    print "    # narecat: красивые ссылки на категории";
    print "    rewrite ^/category/([^/]+)/([^/]+)/?$ /category/index.php?cat=$1&sub=$2 last;";
    print "    rewrite ^/category/([^/]+)/?$ /category/index.php?cat=$1 last;";
    d=1 }' "$NGINX_CONF" > "$NGINX_CONF.tmp" && mv "$NGINX_CONF.tmp" "$NGINX_CONF"
  if nginx -t >/dev/null 2>&1; then systemctl reload nginx; rm -f "$NGINX_CONF.bak"; logger "narelauncher: nginx правило категорий добавлено";
  else mv "$NGINX_CONF.bak" "$NGINX_CONF"; logger "narelauncher: nginx правило категорий откатано (ошибка конфига)"; fi
fi

# --- правило nginx для красивых ссылок видео: /watch/<id>/ -> /media/watch.php?v=<id> ---
if [ -f "$NGINX_CONF" ] && ! grep -q "narewatch" "$NGINX_CONF"; then
  cp "$NGINX_CONF" "$NGINX_CONF.bak"
  awk '1; /index index\.php/ && !d {
    print "    # narewatch: красивые ссылки на видео";
    print "    rewrite ^/watch/([^/]+)/?$ /media/watch.php?v=$1 last;";
    d=1 }' "$NGINX_CONF" > "$NGINX_CONF.tmp" && mv "$NGINX_CONF.tmp" "$NGINX_CONF"
  if nginx -t >/dev/null 2>&1; then systemctl reload nginx; rm -f "$NGINX_CONF.bak"; logger "narelauncher: nginx правило видео добавлено";
  else mv "$NGINX_CONF.bak" "$NGINX_CONF"; logger "narelauncher: nginx правило видео откатано (ошибка конфига)"; fi
fi

# --- правило nginx для HLS-эфиров (OBS/RTMP -> HLS через node-media-server на :8000) ---
if [ -f "$NGINX_CONF" ] && ! grep -q "narehls" "$NGINX_CONF"; then
  cp "$NGINX_CONF" "$NGINX_CONF.bak"
  awk '1; /index index\.php/ && !d {
    print "    # narehls: HLS-эфиры с RTMP-сервера";
    print "    location /live-hls/ { proxy_pass http://127.0.0.1:8000/live/; add_header Access-Control-Allow-Origin *; add_header Cache-Control no-cache; }";
    d=1 }' "$NGINX_CONF" > "$NGINX_CONF.tmp" && mv "$NGINX_CONF.tmp" "$NGINX_CONF"
  if nginx -t >/dev/null 2>&1; then systemctl reload nginx; rm -f "$NGINX_CONF.bak"; logger "narelauncher: nginx правило HLS добавлено";
  else mv "$NGINX_CONF.bak" "$NGINX_CONF"; logger "narelauncher: nginx правило HLS откатано (ошибка конфига)"; fi
fi

# --- обновляем nms.js у RTMP-сервера, если он уже установлен ---
if [ -d /root/narenet-rtmp ] && [ -f "$SRC/rtmp/nms.js" ]; then
  if ! cmp -s "$SRC/rtmp/nms.js" /root/narenet-rtmp/nms.js; then
    cp "$SRC/rtmp/nms.js" /root/narenet-rtmp/nms.js
    systemctl restart nare-rtmp 2>/dev/null || true
    logger "narelauncher: nms.js обновлён"
  fi
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
