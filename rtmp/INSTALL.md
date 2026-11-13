# Эфиры как в VK / OBS (RTMP → HLS)

OBS отдаёт поток по **RTMP**, зрители смотрят по **HLS** — ровно как в VK Video.
Нужно один раз поставить на VPS медиа-сервер (node-media-server) и ffmpeg.

## 1. Поставить (один раз), на VPS под root

```bash
apt-get update && apt-get install -y ffmpeg
mkdir -p /root/narenet-rtmp /var/lib/narenet/hls
cd /root/narenet-rtmp
npm init -y >/dev/null 2>&1
npm i node-media-server@2
# скопировать nms.js из репозитория (update.sh сделает это сам при автодеплое,
# либо вручную):
cp /opt/narelauncher-src/mods/rtmp/nms.js /root/narenet-rtmp/nms.js   # путь под свой
cp /opt/narelauncher-src/mods/rtmp/nare-rtmp.service /etc/systemd/system/nare-rtmp.service
systemctl daemon-reload
systemctl enable --now nare-rtmp
systemctl status nare-rtmp --no-pager
```

## 2. Открыть порт RTMP (1935)

```bash
ufw allow 1935/tcp   # если используется ufw
```

(Порт 8000 наружу открывать не нужно — HLS раздаёт nginx через /live-hls/.)

## 3. Секрет

Секрет хука должен совпадать в двух местах (по умолчанию `nare-rtmp-9f3b`):
- в `nare-rtmp.service` — `NARE_RTMP_SECRET`
- в NareNet — переменная `NARENET_RTMP_SECRET` (если меняешь, задай обе).

## 4. Как стримить из OBS

В профиле на сайте (раздел «Трансляция через OBS») показан:
- **Сервер:** `rtmp://151.245.217.104/live`
- **Ключ потока:** (личный, не показывай никому)

В OBS: Настройки → Трансляция → Сервис «Пользовательский», вставить сервер и ключ → «Запустить трансляцию».
Эфир появится на сайте в разделе «Эфиры», зрители смотрят через HLS (hls.js).

nginx-правило для `/live-hls/` добавляется автоматически при автодеплое (update.sh).
