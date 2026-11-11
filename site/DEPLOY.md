# Деплой сайта NareLauncher (PHP) на VPS

Сайт — обычный PHP (без фреймворков). Разговаривает с NareNet (Node, порт 2445)
по локальному адресу 127.0.0.1:2445.

## 1. Поставить PHP и nginx
```bash
apt update && apt install -y nginx php-fpm php-curl
```

## 2. Положить файлы
Скопировать папку `site/` на сервер, например в `/var/www/narelauncher`:
```bash
mkdir -p /var/www/narelauncher
# скопировать сюда содержимое site/ (из репозитория Winnigames2024-Original/mods)
```

## 3. nginx: PHP-сайт + проксирование API в Node
Важно: страницы сайта отдаёт PHP, а запросы к NareNet (вход, профиль, панели,
сервера и т.д.) проксируются в Node на 2445. Пример `/etc/nginx/sites-available/narelauncher`:

```nginx
server {
    listen 80;
    server_name narelauncher.winnigames2024.ru;
    root /var/www/narelauncher;
    index index.php;

    # API и панели NareNet — в Node (дополните список своими путями при необходимости)
    location ~ ^/(account|profile|tags|clicker|roulette|promo|reward|admin|events|host|server|coins|chat|video|restartflag|panel|app)\b {
        proxy_pass http://127.0.0.1:2445;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }

    # Страницы сайта
    location / { try_files $uri $uri/ /index.php?$query_string; }

    location ~ \.php$ {
        include snippets/fastcgi-php.conf;
        fastcgi_pass unix:/run/php/php-fpm.sock;   # путь уточните: php -v / ls /run/php
    }
}
```
```bash
ln -s /etc/nginx/sites-available/narelauncher /etc/nginx/sites-enabled/
nginx -t && systemctl reload nginx
```

> Если домен сейчас целиком отдаёт Node — именно этот шаг переносит корень на PHP,
> а Node остаётся за прокси по перечисленным путям. Проверьте, что список `location ~ ^/(...)`
> покрывает все пути, которые использует лаунчер и панели. Ничего в самом nare-net.js
> менять не нужно — он продолжает слушать 2445.

## 4. HTTPS
```bash
apt install -y certbot python3-certbot-nginx
certbot --nginx -d narelauncher.winnigames2024.ru
```

## 5. SEO (честно)
- На страницах уже есть title, description, keywords, OpenGraph, Twitter-card,
  JSON-LD (SoftwareApplication), `robots.txt` и `sitemap.xml`.
- Добавьте сайт в **Google Search Console** и отправьте `sitemap.xml` —
  так Google быстрее его проиндексирует.
- Выход в топ по «minecraft launcher» разметкой **не гарантируется**: это очень
  конкурентный запрос, он зависит от ссылок, возраста домена, трафика и контента.
  Разметка и карта сайта — это то, что реально в наших руках; остальное набирается временем.
