#!/bin/bash
# =====================================================================
#  GEO DASH — автообновление (запускается systemd-таймером каждые 5 минут)
#
#  Тянет репозиторий mods, применяет сайт и geodash-api.js,
#  перезапускает сервис только если файл реально изменился,
#  и подтягивает свежие .exe / .apk из последнего релиза GitHub
#  в раздел «Скачать» на сайте.
#
#  Ручной запуск:  sudo /usr/local/bin/geodash-autoupdate
#  Только сборки:  sudo /usr/local/bin/geodash-autoupdate --only-builds
# =====================================================================
set -euo pipefail

SRC="/opt/geodash-mods"
APP="/opt/geodash"
WEB="/var/www/geodash"
SUBDIR="GEO DASH"
OWNER="Winnigames2024-Original"
REPO="mods"
ONLY_BUILDS="${1:-}"

log() { logger -t geodash-update "$*" 2>/dev/null || true; echo "$*"; }

# ---------------------------------------------------------- сборки с GitHub
# Сборки лежат прямо в репозитории: GEO DASH/download/GEO_DASH.exe и .apk
fetch_builds() {
  mkdir -p "$WEB/download"
  local changed_build=0

  if [ -d "$SRC/.git" ]; then
    # autoupdate.exe — та же программа, что и установщик, просто под другим именем
    if [ -f "$SRC/$SUBDIR/download/GEO_DASH_Setup.exe" ]; then
      cp -f "$SRC/$SUBDIR/download/GEO_DASH_Setup.exe" "$WEB/download/autoupdate.exe"
    fi
    for f in GEO_DASH.exe GEO_DASH.apk GEO_DASH_Setup.exe; do
      if [ -f "$SRC/$SUBDIR/download/$f" ]; then
        if ! cmp -s "$SRC/$SUBDIR/download/$f" "$WEB/download/$f"; then
          cp -f "$SRC/$SUBDIR/download/$f" "$WEB/download/$f"
          log "$f обновлён ($(du -h "$WEB/download/$f" | cut -f1))"
          changed_build=1
        fi
      fi
    done
  fi

  # запасной путь: если репозиторий ещё не скачан — тянем напрямую с raw.githubusercontent
  if [ ! -f "$WEB/download/GEO_DASH.exe" ]; then
    curl -fsSL --max-time 600 \
      "https://raw.githubusercontent.com/$OWNER/$REPO/main/GEO%20DASH/download/GEO_DASH.exe" \
      -o "$WEB/download/GEO_DASH.exe" 2>/dev/null && log "exe скачан с raw" || true
  fi
  if [ ! -f "$WEB/download/GEO_DASH.apk" ]; then
    curl -fsSL --max-time 600 \
      "https://raw.githubusercontent.com/$OWNER/$REPO/main/GEO%20DASH/download/GEO_DASH.apk" \
      -o "$WEB/download/GEO_DASH.apk" 2>/dev/null && log "apk скачан с raw" || true
  fi

  if [ "$changed_build" = "1" ] || [ ! -f "$WEB/download/.updated" ]; then
    [ -f "$SRC/$SUBDIR/update.json" ] && \
      grep -m1 '"version"' "$SRC/$SUBDIR/update.json" | sed 's/.*: *"\(.*\)".*/\1/' > "$WEB/download/.release" 2>/dev/null || true
    date -Iseconds > "$WEB/download/.updated"
  fi
  chown -R www-data:www-data "$WEB/download" 2>/dev/null || true
}


# ------------------------------------------- страница «Скачать» в nginx
# В старом конфиге весь /download/ отдавался как файл, из-за чего сама
# страница /download/ возвращала 404. Чиним на месте, без переустановки.
fix_download_location() {
  local CONF=/etc/nginx/sites-available/geodash
  [ -f "$CONF" ] || return 0
  grep -q 'location = /download/' "$CONF" && return 0
  grep -q 'location /download/' "$CONF" || return 0

  perl -0pi -e 's|\s*location /download/ \{\n\s*add_header Content-Disposition "attachment";\n\s*try_files \$uri =404;\n\s*\}|\n    location = /download/ { try_files /download/index.php =404; }\n    location ~ ^/download/.+\\.(exe|apk|zip)\$ {\n        add_header Content-Disposition "attachment";\n        try_files \$uri =404;\n    }|s' "$CONF"

  if nginx -t >/dev/null 2>&1; then
    systemctl reload nginx >/dev/null 2>&1 || true
    log "страница /download/ починена в nginx"
  fi
}

if [ "$ONLY_BUILDS" = "--only-builds" ]; then
  fetch_builds
  fix_download_location
  exit 0
fi

fix_download_location

# заявка с сайта: /api.php?action=updateserver&value=КЛЮЧ
REQ="$WEB/.update-request"
FORCED=0
if [ -f "$REQ" ]; then
  log "пришла заявка на обновление с сайта"
  rm -f "$REQ"
  FORCED=1
fi

[ -d "$SRC/.git" ] || exit 0
cd "$SRC"

before="$(git rev-parse HEAD 2>/dev/null || echo none)"
git fetch --quiet origin 2>/dev/null || exit 0
git reset --hard --quiet origin/main 2>/dev/null || exit 0
after="$(git rev-parse HEAD 2>/dev/null || echo none)"

changed=0

if [ "$before" != "$after" ] || [ "$FORCED" = "1" ]; then
  # ---- сайт ----
  if [ -d "$SRC/$SUBDIR/site" ]; then
    cp -r "$SRC/$SUBDIR/site/." "$WEB/"
    cp -f "$SRC/$SUBDIR/install.sh" "$WEB/install.sh" 2>/dev/null || true
    cp -f "$SRC/$SUBDIR/autoupdatefor5minutes.sh" "$WEB/autoupdatefor5minutes.sh" 2>/dev/null || true
    chmod +x "$WEB/install.sh" "$WEB/autoupdatefor5minutes.sh" 2>/dev/null || true
    git rev-parse --short HEAD > "$WEB/.sitever" 2>/dev/null || true
    date '+%Y-%m-%d %H:%M:%S' > "$WEB/.update-log" 2>/dev/null || true
    chown -R www-data:www-data "$WEB"
    changed=1
  fi

  # ---- версия клиента ----
  if [ -f "$SRC/$SUBDIR/update.json" ]; then
    cp -f "$SRC/$SUBDIR/update.json" "$APP/update.json"
    cp -f "$SRC/$SUBDIR/update.json" "$WEB/update.json"
  fi

  # ---- API (рестарт только при изменении) ----
  if [ -f "$SRC/$SUBDIR/server/geodash-api.js" ]; then
    if ! cmp -s "$SRC/$SUBDIR/server/geodash-api.js" "$APP/geodash-api.js"; then
      cp -f "$SRC/$SUBDIR/server/geodash-api.js" "$APP/geodash-api.js"
      chown geodash:geodash "$APP/geodash-api.js" 2>/dev/null || true
      systemctl restart geodash || true
      log "geodash-api.js обновлён, сервис перезапущен"
      changed=1
    fi
  fi

  # ---- systemd unit ----
  if [ -f "$SRC/$SUBDIR/server/geodash.service" ] && \
     ! cmp -s "$SRC/$SUBDIR/server/geodash.service" /etc/systemd/system/geodash.service; then
    cp -f "$SRC/$SUBDIR/server/geodash.service" /etc/systemd/system/geodash.service
    systemctl daemon-reload
    systemctl restart geodash || true
    log "unit обновлён"
  fi

  # ---- ручной апдейтер ----
  if [ -f "$SRC/$SUBDIR/update.sh" ]; then
    install -m 0755 "$SRC/$SUBDIR/update.sh" /usr/local/bin/geodash-update 2>/dev/null || true
    cp -f "$SRC/$SUBDIR/update.sh" "$WEB/update.sh" 2>/dev/null || true
  fi

  # ---- скрипт автообновления обновляет сам себя ----
  if [ -f "$SRC/$SUBDIR/autoupdatefor5minutes.sh" ] && \
     ! cmp -s "$SRC/$SUBDIR/autoupdatefor5minutes.sh" /usr/local/bin/geodash-autoupdate; then
    install -m 0755 "$SRC/$SUBDIR/autoupdatefor5minutes.sh" /usr/local/bin/geodash-autoupdate
    log "скрипт автообновления обновлён"
  fi
fi

# ---- сторож: API должен быть включён в автозапуск и работать ----
systemctl is-enabled geodash >/dev/null 2>&1 || systemctl enable geodash >/dev/null 2>&1 || true
if ! systemctl is-active --quiet geodash; then
  systemctl restart geodash || true
  log "сервис лежал — поднял"
fi
if ! curl -fsS --max-time 5 "http://127.0.0.1:${GEODASH_PORT:-37534}/api/ping" >/dev/null 2>&1; then
  systemctl restart geodash || true
  log "API не отвечал — перезапустил"
fi
systemctl is-enabled geodash-update.timer >/dev/null 2>&1 || \
  systemctl enable --now geodash-update.timer >/dev/null 2>&1 || true

# ---- свежие сборки игры ----
fetch_builds

if [ "$changed" = "1" ]; then
  nginx -t >/dev/null 2>&1 && systemctl reload nginx || true
  log "обновление применено ($after)"
fi

# ---- сторож: если API лежит — поднимем ----
if ! curl -fsS --max-time 5 "http://127.0.0.1:${GEODASH_PORT:-37534}/api/ping" >/dev/null 2>&1; then
  systemctl restart geodash || true
  log "API не отвечал — перезапустил сервис"
fi
