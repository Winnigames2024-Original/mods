#!/usr/bin/env bash
# =====================================================================
#  GEO DASH — обновить сервер прямо сейчас и проверить, что всё живо.
#
#  Одной командой:
#    curl -fsSL "https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/GEO%20DASH/update.sh" | sudo bash
#  Или на сервере:
#    sudo geodash-update
#
#  Что делает:
#   - тянет свежий репозиторий и применяет сайт, API, update.json, сборки;
#   - следит, чтобы сервис geodash был включён в автозапуск и работал;
#   - следит, чтобы таймер обновления каждые 5 минут был включён;
#   - если API не отвечает — перезапускает его.
# =====================================================================
set -euo pipefail

API_PORT="${GEODASH_PORT:-37534}"
SRC="/opt/geodash-mods"
APP="/opt/geodash"
WEB="/var/www/geodash"
REPO="https://github.com/Winnigames2024-Original/mods.git"
SUBDIR="GEO DASH"

say() { echo -e "\033[1;36m==>\033[0m $*"; }

if [ "$(id -u)" != "0" ]; then
  echo "Запусти под root:  sudo geodash-update"
  exit 1
fi

# ---------------------------------------------------------------- установка, если её нет
if [ ! -f "$APP/geodash-api.js" ]; then
  say "GEO DASH ещё не установлен — ставлю целиком"
  curl -fsSL "https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/GEO%20DASH/install.sh" | bash
  exit $?
fi

# ---------------------------------------------------------------- репозиторий
if [ ! -d "$SRC/.git" ]; then
  say "Клонирую репозиторий заново"
  rm -rf "$SRC"
  git clone --depth 1 "$REPO" "$SRC" >/dev/null 2>&1 || { echo "git clone не удался"; exit 1; }
fi

# ---------------------------------------------------------------- применяем обновление
if [ -x /usr/local/bin/geodash-autoupdate ]; then
  say "Применяю обновление"
  /usr/local/bin/geodash-autoupdate || true
else
  say "Скрипта автообновления нет — ставлю его из репозитория"
  cd "$SRC" && git fetch --quiet origin && git reset --hard --quiet origin/main
  install -m 0755 "$SRC/$SUBDIR/autoupdatefor5minutes.sh" /usr/local/bin/geodash-autoupdate
  /usr/local/bin/geodash-autoupdate || true
fi

# ---------------------------------------------------------------- автозапуск API
say "Проверяю автозапуск"
systemctl enable geodash >/dev/null 2>&1 || true
if ! systemctl is-active --quiet geodash; then
  say "Сервис стоял — запускаю"
  systemctl restart geodash || true
fi

# таймер обновления каждые 5 минут
if ! systemctl is-enabled --quiet geodash-update.timer 2>/dev/null; then
  say "Включаю таймер обновления (каждые 5 минут)"
  systemctl enable --now geodash-update.timer >/dev/null 2>&1 || true
fi

# ---------------------------------------------------------------- проверка живости
sleep 1
PING="$(curl -fsS --max-time 5 "http://127.0.0.1:$API_PORT/api/ping" 2>/dev/null || echo FAIL)"
if [ "$PING" = "FAIL" ]; then
  say "API не ответил — перезапускаю и жду"
  systemctl restart geodash || true
  sleep 3
  PING="$(curl -fsS --max-time 5 "http://127.0.0.1:$API_PORT/api/ping" 2>/dev/null || echo FAIL)"
fi

VER="$(grep -m1 '"version"' "$APP/update.json" 2>/dev/null | sed 's/.*: *"\(.*\)".*/\1/' || echo '?')"

echo
echo "================================================================"
echo "  GEO DASH обновлён"
echo "----------------------------------------------------------------"
echo "  Версия игры:  $VER"
echo "  API:          $PING"
echo "  Сервис:       $(systemctl is-active geodash 2>/dev/null) / автозапуск: $(systemctl is-enabled geodash 2>/dev/null)"
echo "  Таймер:       $(systemctl is-active geodash-update.timer 2>/dev/null) / $(systemctl is-enabled geodash-update.timer 2>/dev/null)"
echo "  Сайт:         $WEB"
echo "  Логи:         journalctl -u geodash -f"
echo "================================================================"
