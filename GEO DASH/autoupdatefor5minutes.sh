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

# ---------------------------------------------------------------- один за раз
# Обновление может прийти сразу с двух сторон: заявкой с сайта и руками.
# Два git одновременно оставляют index.lock и всё ломают — поэтому замок.
LOCK=/run/geodash-update.lock
if [ -z "${GEODASH_LOCKED:-}" ]; then
  export GEODASH_LOCKED=1
  exec flock -w 300 "$LOCK" "$0" "$@"
fi

# если прошлый запуск умер на середине — снимаем брошенный замок git
if [ -f "$SRC/.git/index.lock" ] && ! pgrep -x git >/dev/null 2>&1; then
  rm -f "$SRC/.git/index.lock"
  log "убрал брошенный index.lock"
fi

# ---------------------------------------------------------- сборки с GitHub
# Сборки лежат прямо в репозитории: GEO DASH/download/GEO_DASH.exe и .apk
fetch_builds() {
  mkdir -p "$WEB/download"
  local changed_build=0

  if [ -d "$SRC/.git" ]; then
    for f in GEO_DASH.exe GEO_DASH.apk GEO_DASH_Setup.exe GEO_DASH_Setup.apk; do
      src="$SRC/$SUBDIR/download/$f"
      dst="$WEB/download/$f"
      [ -f "$src" ] || continue
      cmp -s "$src" "$dst" && continue

      # места должно хватить: файл весит до 70 МБ, иначе получится обрезок
      need=$(( $(stat -c%s "$src" 2>/dev/null || echo 0) / 1024 + 20480 ))
      free=$(df -Pk "$WEB/download" | awk 'NR==2{print $4}')
      if [ -n "$free" ] && [ "$free" -lt "$need" ]; then
        log "мало места на диске: нужно ${need}К, свободно ${free}К — $f не обновляю"
        continue
      fi

      cp -f "$src" "$dst.part" && mv -f "$dst.part" "$dst" || { rm -f "$dst.part"; continue; }
      touch "$dst"                     # отметка времени — когда файл лёг на сервер
      log "$f обновлён ($(du -h "$dst" | cut -f1))"
      changed_build=1
    done

    # autoupdate.exe — та же программа, что установщик. Делаем жёсткую ссылку,
    # чтобы не держать на диске вторую копию в 70 МБ.
    setup="$WEB/download/GEO_DASH_Setup.exe"
    auto="$WEB/download/autoupdate.exe"
    if [ -s "$setup" ] && ! cmp -s "$setup" "$auto"; then
      rm -f "$auto"
      ln -f "$setup" "$auto" 2>/dev/null || cp -f "$setup" "$auto" 2>/dev/null || true
      if [ -s "$auto" ]; then
        chmod 0644 "$auto" 2>/dev/null || true
        log "autoupdate.exe готов ($(du -h "$auto" | cut -f1))"
      else
        log "autoupdate.exe сделать не вышло"
      fi
    fi
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
  if [ ! -f "$WEB/download/GEO_DASH_Setup.apk" ]; then
    curl -fsSL --max-time 600 \
      "https://raw.githubusercontent.com/$OWNER/$REPO/main/GEO%20DASH/download/GEO_DASH_Setup.apk" \
      -o "$WEB/download/GEO_DASH_Setup.apk" 2>/dev/null && log "установщик apk скачан с raw" || true
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

  # нужна правка, если есть старый блок /download/ или строка «location = /download/»
  grep -qE 'location (= )?/download/' "$CONF" || return 0
  grep -q 'location = /download/' "$CONF" || grep -q 'location /download/ {' "$CONF" || return 0

  awk '
    # страница /download/ должна попадать в PHP, поэтому отдельный exact-блок убираем
    /location = \/download\/ *\{/ { next }

    # старый блок, который отдавал файлом вообще всё внутри /download/
    /location \/download\/ \{/ {
      print "    location ~ ^/download/.+\\.(exe|apk|zip)$ {";
      print "        add_header Content-Disposition \"attachment\";";
      print "        try_files $uri =404;";
      print "    }";
      skip = 1; next
    }
    skip && /\}/ { skip = 0; next }
    skip { next }
    { print }
  ' "$CONF" > "$CONF.new" 2>/dev/null || return 0

  [ -s "$CONF.new" ] || { rm -f "$CONF.new"; return 0; }
  cp -f "$CONF" "$CONF.bak"
  mv -f "$CONF.new" "$CONF"

  if nginx -t >/dev/null 2>&1; then
    systemctl reload nginx >/dev/null 2>&1 || true
    log "страница /download/ теперь открывается как PHP"
  else
    mv -f "$CONF.bak" "$CONF"
    log "правка nginx не подошла, вернул прежний конфиг"
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

# синхронизируем не только когда пришёл новый коммит: бывает, что коммит уже
# подтянут, а файлы на сервере остались от прошлой неудачной попытки
STALE=0
if [ ! -f "$APP/update.json" ] || ! cmp -s "$SRC/$SUBDIR/update.json" "$APP/update.json"; then STALE=1; fi
if [ ! -f "$WEB/index.php" ]; then STALE=1; fi
# сайт мог измениться без смены update.json — сверяем отметку коммита
if [ "$(cat "$WEB/.sitever" 2>/dev/null)" != "$(git -C "$SRC" rev-parse --short HEAD 2>/dev/null)" ]; then STALE=1; fi

if [ "$before" != "$after" ] || [ "$FORCED" = "1" ] || [ "$STALE" = "1" ]; then
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

  # ---- документация админа ----
  # кладём рядом с API, а не в сайт: внутри ключи выдачи, страница /admin/docs.php
  # читает файл сама и показывает его только админу
  if [ -f "$SRC/$SUBDIR/docs/ADMIN.md" ]; then
    cp -f "$SRC/$SUBDIR/docs/ADMIN.md" "$APP/ADMIN.md"
    chmod 0644 "$APP/ADMIN.md" 2>/dev/null || true
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

# заявка с сайта должна срабатывать мгновенно: ставим юниты, если их ещё нет
if [ ! -f /etc/systemd/system/geodash-request.path ]; then
  cat > /etc/systemd/system/geodash-request.service <<UNIT
[Unit]
Description=GEO DASH: обновление по заявке с сайта

[Service]
Type=oneshot
ExecStart=/bin/bash -c '[ -f $WEB/.update-request ] && /usr/local/bin/geodash-autoupdate || true'
UNIT
  cat > /etc/systemd/system/geodash-request.path <<UNIT
[Unit]
Description=GEO DASH: ждать заявку на обновление с сайта

[Path]
PathExists=$WEB/.update-request
Unit=geodash-request.service

[Install]
WantedBy=paths.target
UNIT
  systemctl daemon-reload >/dev/null 2>&1 || true
  log "поставил мгновенную реакцию на заявку с сайта"
fi
systemctl is-enabled geodash-request.path >/dev/null 2>&1 || \
  systemctl enable --now geodash-request.path >/dev/null 2>&1 || true

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
