#!/usr/bin/env bash
# Установка сайта NareLauncher на VPS одной командой:
#   curl -fsSL https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/site/install.sh | sudo bash
#
# Делает всё сам: ставит nginx + PHP, тянет сайт из репозитория, настраивает
# nginx так, что страницы сайта отдаёт PHP, а всё остальное (API и панели
# NareNet) проксируется в Node на 127.0.0.1:2445. nare-net.js не трогается.

set -euo pipefail

DOMAIN="narelauncher.winnigames2024.ru"
WEBROOT="/var/www/narelauncher"
REPO="https://github.com/Winnigames2024-Original/mods.git"
SRC="/opt/narelauncher-mods"
NODE_PORT="2445"

echo "==> NareLauncher site installer"

if [ "$(id -u)" != "0" ]; then
  echo "Запусти под root (sudo)."; exit 1
fi

echo "==> Ставлю пакеты (nginx, php-fpm, php-curl, git)…"
export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get install -y nginx php-fpm php-curl php-mbstring git curl

echo "==> Тяну сайт из репозитория…"
if [ -d "$SRC/.git" ]; then
  git -C "$SRC" pull --ff-only
else
  rm -rf "$SRC"
  git clone --depth 1 "$REPO" "$SRC"
fi

echo "==> Копирую файлы сайта в $WEBROOT…"
mkdir -p "$WEBROOT"
cp -r "$SRC/site/." "$WEBROOT/"
rm -f "$WEBROOT/install.sh" "$WEBROOT/DEPLOY.md"   # служебное в вебрут не кладём
chown -R www-data:www-data "$WEBROOT"

echo "==> Ищу сокет PHP-FPM…"
FPM_SOCK="$(ls /run/php/php*-fpm.sock 2>/dev/null | head -1 || true)"
if [ -z "$FPM_SOCK" ]; then
  echo "Не нашёл php-fpm сокет. Проверь: systemctl status php*-fpm"; exit 1
fi
echo "    сокет: $FPM_SOCK"

echo "==> Пишу конфиг nginx…"
cat > /etc/nginx/sites-available/narelauncher <<NGINX
server {
    listen 80;
    listen [::]:80;
    server_name $DOMAIN;
    root $WEBROOT;
    index index.php index.html;

    # Есть файл/папка сайта — отдаём её (PHP). Иначе — в NareNet (Node).
    location / {
        try_files \$uri \$uri/ @node;
    }

    location ~ \.php\$ {
        include snippets/fastcgi-php.conf;
        fastcgi_pass unix:$FPM_SOCK;
    }

    # Всё, чего нет на диске сайта — проксируем в Node: API и старые панели.
    location @node {
        proxy_pass http://127.0.0.1:$NODE_PORT;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection "upgrade";
    }

    location = /robots.txt  { }
    location = /sitemap.xml { }
}
NGINX

ln -sf /etc/nginx/sites-available/narelauncher /etc/nginx/sites-enabled/narelauncher
# убираем дефолтный сайт, если он перехватывает 80
rm -f /etc/nginx/sites-enabled/default

echo "==> Проверяю и перезагружаю nginx…"
nginx -t
systemctl reload nginx
systemctl enable nginx >/dev/null 2>&1 || true

echo "==> Настраиваю автообновление (systemd-таймер, каждые 5 минут)…"
chmod +x "$SRC/site/update.sh"
cat > /etc/systemd/system/narelauncher-update.service <<UNIT
[Unit]
Description=NareLauncher: автообновление сайта и nare-net из GitHub
After=network-online.target

[Service]
Type=oneshot
ExecStart=$SRC/site/update.sh
UNIT

cat > /etc/systemd/system/narelauncher-update.timer <<UNIT
[Unit]
Description=NareLauncher: проверять обновления каждые 5 минут

[Timer]
OnBootSec=2min
OnUnitActiveSec=5min
Persistent=true

[Install]
WantedBy=timers.target
UNIT

systemctl daemon-reload
systemctl enable --now narelauncher-update.timer >/dev/null 2>&1 || true

echo
echo "================================================================"
echo " Готово. Сайт: http://$DOMAIN/"
echo " Страницы: /home/ /login/ /register/ /profile/my/ /download/ /launch/ /versions/"
echo
echo " HTTPS (рекомендуется):"
echo "   apt-get install -y certbot python3-certbot-nginx"
echo "   certbot --nginx -d $DOMAIN"
echo
echo " Проверь, что NareNet (Node) слушает 127.0.0.1:$NODE_PORT —"
echo " его панели и API теперь доступны через этот же домен."
echo
echo " Автообновление включено: каждые 5 минут VPS тянет GitHub и сам"
echo " применяет изменения сайта и nare-net.js (рестарт narenet только"
echo " если файл поменялся). Статус: systemctl status narelauncher-update.timer"
echo "================================================================"
