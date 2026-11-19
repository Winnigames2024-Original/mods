#!/bin/bash
# =====================================================================
#  GEO DASH — установка сервера одной командой (Debian 12 / Ubuntu)
#
#    curl -fsSL "https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/GEO%20DASH/install.sh" | sudo bash
#
#  Ставит: nginx + php-fpm + node, API на порт 37534, сайт
#  geodash.winnigames2024.ru (/ -> /geodash/index.php), автообновление
#  из GitHub каждые 5 минут и свежие .exe/.apk в раздел «Скачать».
# =====================================================================
set -euo pipefail

DOMAIN="${GEODASH_DOMAIN:-geodash.winnigames2024.ru}"
API_PORT="${GEODASH_PORT:-37534}"
REPO="https://github.com/Winnigames2024-Original/mods.git"
REPO_RAW="https://raw.githubusercontent.com/Winnigames2024-Original/mods/main"
SRC="/opt/geodash-mods"
APP="/opt/geodash"
WEB="/var/www/geodash"
DATA="/var/lib/geodash"
SUBDIR="GEO DASH"
# HTTPS: ставится сам сертификат Let's Encrypt. Выключить: GEODASH_SSL=off
SSL="${GEODASH_SSL:-on}"
SSL_EMAIL="${GEODASH_EMAIL:-winnigames2024@gmail.com}"

say() { echo -e "\033[1;36m==>\033[0m $*"; }

export DEBIAN_FRONTEND=noninteractive
# apt сам подождёт, если система в этот момент ставит обновления
APT=(apt-get -y -o DPkg::Lock::Timeout=600)

# ждём, пока освободится блокировка dpkg (автообновления Debian и т.п.)
wait_apt() {
  local waited=0
  while fuser /var/lib/dpkg/lock-frontend >/dev/null 2>&1 \
     || fuser /var/lib/apt/lists/lock >/dev/null 2>&1 \
     || pgrep -x unattended-upgr >/dev/null 2>&1; do
    if [ "$waited" = "0" ]; then
      say "Система сейчас ставит свои обновления — жду, пока освободится apt (до 10 минут)"
    fi
    sleep 5
    waited=$((waited + 5))
    if [ "$waited" -ge 600 ]; then
      say "apt занят слишком долго. Посмотри, кто держит:  ps aux | grep -E 'apt|dpkg'"
      say "и повтори установку этой же командой."
      exit 1
    fi
  done
}

if [ "$(id -u)" != "0" ]; then
  echo "Запусти под root:  curl -fsSL .../install.sh | sudo bash"
  exit 1
fi

say "GEO DASH — установка на $(. /etc/os-release && echo "$PRETTY_NAME")"

export DEBIAN_FRONTEND=noninteractive
say "Ставлю пакеты (nginx, php-fpm, git, curl, unzip)…"
wait_apt
"${APT[@]}" update
wait_apt
"${APT[@]}" install nginx php-fpm php-curl php-mbstring git curl ca-certificates unzip jq psmisc >/dev/null

# ---------------------------------------------------------------- node
if ! command -v node >/dev/null 2>&1 || [ "$(node -v | sed 's/v\([0-9]*\).*/\1/')" -lt 18 ]; then
  say "Ставлю Node.js 20…"
  curl -fsSL https://deb.nodesource.com/setup_20.x | bash - >/dev/null
  wait_apt
  "${APT[@]}" install nodejs >/dev/null
fi
say "node $(node -v)"

# ---------------------------------------------------------------- исходники
say "Тяну репозиторий…"
if [ -d "$SRC/.git" ]; then
  git -C "$SRC" fetch --quiet origin && git -C "$SRC" reset --hard --quiet origin/main
else
  rm -rf "$SRC"
  git clone --depth 1 --quiet "$REPO" "$SRC"
fi

# ---------------------------------------------------------------- сервер API
say "Ставлю API в $APP…"
id -u geodash >/dev/null 2>&1 || useradd --system --home "$DATA" --shell /usr/sbin/nologin geodash
mkdir -p "$APP" "$DATA/leveldata" "$DATA/music"
cp -f "$SRC/$SUBDIR/server/geodash-api.js" "$APP/geodash-api.js"
cp -f "$SRC/$SUBDIR/update.json" "$APP/update.json" 2>/dev/null || true
chown -R geodash:geodash "$DATA" "$APP"
chmod 755 "$DATA" "$DATA/music"        # чтобы nginx отдавал /songs/<id>.mp3

cp -f "$SRC/$SUBDIR/server/geodash.service" /etc/systemd/system/geodash.service
sed -i "s/GEODASH_PORT=.*/GEODASH_PORT=$API_PORT/" /etc/systemd/system/geodash.service
systemctl daemon-reload
systemctl enable --now geodash >/dev/null 2>&1 || true
systemctl restart geodash

# ---------------------------------------------------------------- сайт
say "Ставлю сайт в $WEB…"
mkdir -p "$WEB" "$WEB/download"
cp -r "$SRC/$SUBDIR/site/." "$WEB/"
cp -f "$SRC/$SUBDIR/install.sh" "$WEB/install.sh"
cp -f "$SRC/$SUBDIR/autoupdatefor5minutes.sh" "$WEB/autoupdatefor5minutes.sh"
chmod +x "$WEB/install.sh" "$WEB/autoupdatefor5minutes.sh"
chown -R www-data:www-data "$WEB"

# свежие сборки игры (если релиз уже собран GitHub Actions)
say "Качаю последние .exe и .apk (если есть релиз)…"
"$SRC/$SUBDIR/autoupdatefor5minutes.sh" --only-builds || true

# ---------------------------------------------------------------- nginx
FPM_SOCK="$(ls /run/php/php*-fpm.sock 2>/dev/null | head -1 || true)"
if [ -z "$FPM_SOCK" ]; then echo "Не нашёл php-fpm сокет"; exit 1; fi
say "php-fpm: $FPM_SOCK"

cat > /etc/nginx/sites-available/geodash <<NGINX
server {
    listen 80;
    listen [::]:80;
    server_name $DOMAIN;

    root $WEB;
    index index.php index.html;
    client_max_body_size 64m;

    # /geodash/... — тот же сайт (как просили: / открывает /geodash/index.php)
    location /geodash/ { rewrite ^/geodash/(.*)\$ /\$1 last; }
    location = /geodash  { return 301 /; }

    # установка сервера одной командой: curl -fsSL http://$DOMAIN/install.sh | sudo bash
    location = /install.sh { default_type text/x-shellscript; }
    location = /autoupdatefor5minutes.sh { default_type text/x-shellscript; }
    location = /update.sh { default_type text/x-shellscript; }

    # API игры
    location /api/ {
        proxy_pass http://127.0.0.1:$API_PORT;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_read_timeout 120s;
    }

    location /download/ {
        add_header Content-Disposition "attachment";
        try_files \$uri =404;
    }

    # песни: скачанные по ID лежат рядом с данными сервера и раздаются как /songs/<id>.mp3
    location /songs/ {
        alias $DATA/music/;
        add_header Access-Control-Allow-Origin *;
        expires 7d;
    }

    location / { try_files \$uri \$uri/ /index.php?\$query_string; }

    location ~ \.php\$ {
        include snippets/fastcgi-php.conf;
        fastcgi_pass unix:$FPM_SOCK;
        fastcgi_read_timeout 120s;
    }

    location ~ /\.(git|env) { deny all; }
}
NGINX

ln -sf /etc/nginx/sites-available/geodash /etc/nginx/sites-enabled/geodash

# если домен занят другим сайтом — скажем об этом прямо
CONFLICT="$(grep -rl "server_name[^;]*$DOMAIN" /etc/nginx/sites-enabled/ 2>/dev/null | grep -v "/geodash$" || true)"
if [ -n "$CONFLICT" ]; then
  say "ВНИМАНИЕ: домен $DOMAIN уже прописан в: $CONFLICT"
  say "Отключаю эти конфиги, чтобы домен открывал GEO DASH (файлы остаются в sites-available)"
  for c in $CONFLICT; do rm -f "$c"; done
fi

nginx -t
systemctl reload nginx
systemctl enable nginx >/dev/null 2>&1 || true

# ---------------------------------------------------------------- https
SCHEME="http"
if [ "$SSL" = "on" ]; then
  say "Включаю HTTPS (Let's Encrypt) для $DOMAIN…"
  wait_apt
  "${APT[@]}" install certbot python3-certbot-nginx >/dev/null 2>&1 || true
  if certbot --nginx -d "$DOMAIN" --non-interactive --agree-tos --redirect \
       -m "$SSL_EMAIL" >/tmp/geodash-certbot.log 2>&1; then
    SCHEME="https"
    say "Сертификат получен, http отправляет на https"
    systemctl enable --now certbot.timer >/dev/null 2>&1 || true
  else
    say "Сертификат не выдали (подробности: /tmp/geodash-certbot.log). Сайт работает по http."
    say "Частые причины: A-запись домена ещё не на этот сервер или закрыт порт 80."
  fi
  # install.sh и песни должны открываться и без шифрования
  nginx -t >/dev/null 2>&1 && systemctl reload nginx || true
fi

# ---------------------------------------------------------------- автообновление
say "Включаю автообновление каждые 5 минут…"
install -m 0755 "$SRC/$SUBDIR/autoupdatefor5minutes.sh" /usr/local/bin/geodash-autoupdate
install -m 0755 "$SRC/$SUBDIR/update.sh" /usr/local/bin/geodash-update 2>/dev/null || true
cp -f "$SRC/$SUBDIR/update.sh" "$WEB/update.sh" 2>/dev/null || true

cat > /etc/systemd/system/geodash-update.service <<UNIT
[Unit]
Description=GEO DASH: автообновление из GitHub (сайт, API, сборки игры)
After=network-online.target

[Service]
Type=oneshot
ExecStart=/usr/local/bin/geodash-autoupdate
UNIT

cat > /etc/systemd/system/geodash-update.timer <<UNIT
[Unit]
Description=GEO DASH: проверять обновления каждые 5 минут

[Timer]
OnBootSec=2min
OnUnitActiveSec=5min
Persistent=true

[Install]
WantedBy=timers.target
UNIT

systemctl daemon-reload
systemctl enable --now geodash-update.timer >/dev/null 2>&1 || true

# ---------------------------------------------------------------- итог
sleep 1
API_OK="$(curl -fsS --max-time 5 "http://127.0.0.1:$API_PORT/api/ping" || echo FAIL)"

echo
echo "================================================================"
echo "  GEO DASH установлен"
echo "----------------------------------------------------------------"
echo "  Сайт:        $SCHEME://$DOMAIN/            (это и есть /geodash/index.php)"
echo "  Скачать:     $SCHEME://$DOMAIN/download/"
echo "  API:         http://$(curl -fsS --max-time 4 ifconfig.me || echo SERVER_IP):$API_PORT/api/ping"
echo "  Проверка:    $API_OK"
echo "  Админ:       Winnigames2024 (зарегистрируй это имя — права выдадутся сами)"
echo "  Данные:      $DATA   (уровни, аккаунты, музыка)"
echo
echo "  Сервис:      systemctl status geodash"
echo "  Логи:        journalctl -u geodash -f"
echo "  Обновление:  каждые 5 минут (systemctl status geodash-update.timer)"
echo "  Вручную:     sudo geodash-update   (или curl -fsSL $SCHEME://$DOMAIN/update.sh | sudo bash)"
echo
if [ "$SCHEME" = "https" ]; then
echo "  HTTPS:       включён, сертификат продлевается сам (certbot.timer)"
else
echo "  HTTPS:       повторить:  certbot --nginx -d $DOMAIN --redirect -m $SSL_EMAIL"
fi
echo "  DNS:         A-запись  geodash -> IP этого сервера"
echo "  Порты:       80, 443 и $API_PORT должны быть открыты (фаервол тут не трогается)"
echo "================================================================"
