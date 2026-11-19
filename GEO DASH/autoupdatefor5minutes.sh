#!/usr/bin/env bash
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
fetch_builds() {
  mkdir -p "$WEB/download"
  local api="https://api.github.com/repos/$OWNER/$REPO/releases/latest"
  local json
  json="$(curl -fsSL --max-time 25 "$api" 2>/dev/null || true)"
  [ -z "$json" ] && return 0

  local tag
  tag="$(echo "$json" | grep -m1 '"tag_name"' | sed 's/.*: *"\(.*\)".*/\1/')" || true
  [ -z "$tag" ] && return 0

  local cur=""
  [ -f "$WEB/download/.release" ] && cur="$(cat "$WEB/download/.release")"
  [ "$cur" = "$tag" ] && return 0

  log "новый релиз: $tag — качаю сборки"
  echo "$json" | grep '"browser_download_url"' | sed 's/.*: *"\(.*\)".*/\1/' | while read -r url; do
    case "$url" in
      *.exe) curl -fsSL --max-time 300 "$url" -o "$WEB/download/GEO_DASH.exe" && log "exe обновлён" ;;
      *.apk) curl -fsSL --max-time 300 "$url" -o "$WEB/download/GEO_DASH.apk" && log "apk обновлён" ;;
    esac
  done

  echo "$tag" > "$WEB/download/.release"
  date -Iseconds > "$WEB/download/.updated"
  chown -R www-data:www-data "$WEB/download" 2>/dev/null || true
}

if [ "$ONLY_BUILDS" = "--only-builds" ]; then
  fetch_builds
  exit 0
fi

[ -d "$SRC/.git" ] || exit 0
cd "$SRC"

before="$(git rev-parse HEAD 2>/dev/null || echo none)"
git fetch --quiet origin 2>/dev/null || exit 0
git reset --hard --quiet origin/main 2>/dev/null || exit 0
after="$(git rev-parse HEAD 2>/dev/null || echo none)"

changed=0

if [ "$before" != "$after" ]; then
  # ---- сайт ----
  if [ -d "$SRC/$SUBDIR/site" ]; then
    cp -r "$SRC/$SUBDIR/site/." "$WEB/"
    cp -f "$SRC/$SUBDIR/install.sh" "$WEB/install.sh" 2>/dev/null || true
    cp -f "$SRC/$SUBDIR/autoupdatefor5minutes.sh" "$WEB/autoupdatefor5minutes.sh" 2>/dev/null || true
    chmod +x "$WEB/install.sh" "$WEB/autoupdatefor5minutes.sh" 2>/dev/null || true
    git rev-parse --short HEAD > "$WEB/.sitever" 2>/dev/null || true
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

  # ---- скрипт автообновления обновляет сам себя ----
  if [ -f "$SRC/$SUBDIR/autoupdatefor5minutes.sh" ] && \
     ! cmp -s "$SRC/$SUBDIR/autoupdatefor5minutes.sh" /usr/local/bin/geodash-autoupdate; then
    install -m 0755 "$SRC/$SUBDIR/autoupdatefor5minutes.sh" /usr/local/bin/geodash-autoupdate
    log "скрипт автообновления обновлён"
  fi
fi

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
