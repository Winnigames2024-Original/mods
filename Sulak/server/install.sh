#!/usr/bin/env bash
# =============================================================================
#  Установка / обновление ai.winnigames2024.ru прямо с GitHub.
#
#  Установить:
#    curl -fsSL https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/Sulak/server/install.sh | bash
#
#  То же самое + автообновление раз в 10 минут:
#    curl -fsSL https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/Sulak/server/install.sh | bash -s -- --auto-update
#
#  Повторный запуск = обновление. config.php с ключами не перезаписывается.
# =============================================================================
set -euo pipefail

RAW="https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/Sulak/server"
DOMAIN="ai.winnigames2024.ru"
ROOT="/var/www/ai"
DBDIR="/var/lib/sulak-ai"
AUTO_UPDATE=0

for arg in "$@"; do
    [ "$arg" = "--auto-update" ] && AUTO_UPDATE=1
done

if [ "$(id -u)" != "0" ]; then
    echo "Запускай от root: curl -fsSL ... | sudo bash" >&2
    exit 1
fi

FIRST_RUN=0
[ -f "${ROOT}/api/api.php" ] || FIRST_RUN=1

if [ "$FIRST_RUN" = "1" ]; then
    echo ">>> пакеты"
    export DEBIAN_FRONTEND=noninteractive
    apt-get update -qq
    apt-get install -y -qq nginx php-fpm php-cli php-sqlite3 php-curl php-mbstring curl ca-certificates
fi

PHPV="$(php -r 'echo PHP_MAJOR_VERSION.".".PHP_MINOR_VERSION;')"
SOCK="/run/php/php${PHPV}-fpm.sock"

mkdir -p "${ROOT}/api" "${DBDIR}"

echo ">>> качаю файлы сайта"
for f in lib.php api.php index.php; do
    curl -fsSL "${RAW}/api/${f}" -o "${ROOT}/api/${f}.new"
    mv "${ROOT}/api/${f}.new" "${ROOT}/api/${f}"
    echo "    api/${f}"
done

# config.php бережём: в нём ключи и выбор движка
if [ ! -f "${ROOT}/api/config.php" ]; then
    curl -fsSL "${RAW}/api/config.php" -o "${ROOT}/api/config.php"
    echo "    api/config.php (новый)"
else
    curl -fsSL "${RAW}/api/config.php" -o "${ROOT}/api/config.php.example"
    echo "    api/config.php оставлен как есть, свежий образец рядом в config.php.example"
fi

cat > "${ROOT}/index.html" <<'__HOME__'
<!doctype html><html lang="ru"><head><meta charset="utf-8">
<title>ai.winnigames2024.ru</title>
<style>body{background:#0d0b12;color:#e8e4f0;font:16px/1.6 system-ui,sans-serif;
display:grid;place-items:center;height:100vh;margin:0}a{color:#b48cff}</style></head>
<body><div><h1>ai.winnigames2024.ru</h1>
<p>API для мода Sulak: <a href="/api/index.php">получить бесплатный ключ</a></p></div></body></html>
__HOME__

chown -R www-data:www-data "${ROOT}" "${DBDIR}"
chmod 750 "${DBDIR}"
chmod 640 "${ROOT}/api/config.php"

if [ "$FIRST_RUN" = "1" ]; then
    echo ">>> nginx"
    cat > "/etc/nginx/sites-available/${DOMAIN}" <<__NGINX__
server {
    listen 80;
    listen [::]:80;
    server_name ${DOMAIN};

    root ${ROOT};
    index index.php index.html;

    access_log /var/log/nginx/ai.access.log;
    error_log  /var/log/nginx/ai.error.log;

    client_max_body_size 2m;

    location / {
        try_files \$uri \$uri/ =404;
    }

    location ~ \.php\$ {
        include snippets/fastcgi-php.conf;
        fastcgi_pass unix:${SOCK};
        fastcgi_read_timeout 180;
    }

    location ~ /\. { deny all; }
    location ~* /(config|lib)\.php\$ { deny all; }
}
__NGINX__
    ln -sf "/etc/nginx/sites-available/${DOMAIN}" "/etc/nginx/sites-enabled/${DOMAIN}"
    rm -f /etc/nginx/sites-enabled/default
    systemctl enable --now "php${PHPV}-fpm" nginx >/dev/null 2>&1 || true
fi

nginx -t
systemctl reload nginx

sudo -u www-data php -r 'require "/var/www/ai/api/lib.php"; db();' && echo ">>> база готова"

if [ "$AUTO_UPDATE" = "1" ]; then
    echo ">>> автообновление"
    cat > /etc/systemd/system/sulak-ai-update.service <<'__SVC__'
[Unit]
Description=Обновление Sulak AI с GitHub
After=network-online.target

[Service]
Type=oneshot
ExecStart=/bin/bash -c 'curl -fsSL https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/Sulak/server/install.sh | bash'
__SVC__
    cat > /etc/systemd/system/sulak-ai-update.timer <<'__TMR__'
[Unit]
Description=Обновление Sulak AI раз в 10 минут

[Timer]
OnBootSec=3min
OnUnitActiveSec=10min

[Install]
WantedBy=timers.target
__TMR__
    systemctl daemon-reload
    systemctl enable --now sulak-ai-update.timer
fi

if [ "$FIRST_RUN" = "1" ]; then
cat <<'__DONE__'

=============================================================
 Установлено.

 1) A-запись:  ai.winnigames2024.ru  ->  166.1.161.200

 2) Движок — Qwen. По умолчанию локально, бесплатно, без ключей:
       curl -fsSL https://ollama.com/install.sh | sh
       ollama pull qwen2.5:3b
    (если на сервере 8+ ГБ RAM, лучше qwen2.5:7b — поправь model
     в /var/www/ai/api/config.php)

    Либо Qwen в облаке: в config.php поставь
       'provider' => 'qwen_cloud',
    и вставь бесплатный ключ с openrouter.ai/keys

 3) HTTPS:
       apt-get install -y certbot python3-certbot-nginx
       certbot --nginx -d ai.winnigames2024.ru

 4) Ключ себе:  http://ai.winnigames2024.ru/api/index.php
=============================================================
__DONE__
else
    echo ">>> обновлено"
fi
