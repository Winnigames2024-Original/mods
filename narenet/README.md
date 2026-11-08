# NareNet

Сборный пункт для игры вместе: кто открыл одиночный мир, с какой версией
и какими модами, и как к нему попасть.

Виртуальной сети, как в Radmin VPN, здесь нет — для неё нужен драйвер в
системе, из лаунчера такого не сделать. Вместо неё две дороги:

1. **Напрямую** — если у хозяина открыт порт. Сервер сам проверяет это,
   пробуя к нему подключиться, и пишет в списке `reachable`.
2. **Через туннель** — хозяин держит исходящее соединение к серверу,
   сервер выдаёт ему публичный порт и проводит через себя трафик гостей.
   Так работает подключение из-под любого NAT, и настоящий адрес хозяина
   гостям не раздаётся.

Моды гостям достаются по sha1: хозяин выкладывает свои jar на сервер один
раз, дальше они раздаются всем. Зависимостей у сервера нет — только то,
что есть в Node.js.

Сборных пунктов может быть несколько: лаунчер и мод держат список
адресов и переключаются между ними на ходу. Сейчас в списке по умолчанию
два — `166.1.161.200:2445` и `151.245.217.104:2445`. Ставить сервер нужно
на каждый, список правится в настройках лаунчера.

## Установка на Debian 12

Всё под root, одним куском. Служба будет работать не под root, а под
своим пользователем.

```bash
apt-get update && apt-get install -y nodejs curl

id narenet >/dev/null 2>&1 || useradd -r -s /usr/sbin/nologin narenet
mkdir -p /opt/narenet /var/lib/narenet

curl -fsSL https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/narenet/nare-net.js \
     -o /opt/narenet/nare-net.js
curl -fsSL https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/narenet/narenet.service \
     -o /etc/systemd/system/narenet.service

chown -R narenet:narenet /var/lib/narenet
systemctl daemon-reload
systemctl enable --now narenet
systemctl status narenet --no-pager
```

Проверка с самого сервера:

```bash
curl -s http://127.0.0.1:2445/ping
```

Должно ответить `{"ok":true,"name":"NareNet",...}`.

Если на сервере включён firewall, нужно открыть порт **2445** и диапазон
**30000–30060** (через него идут туннели):

```bash
# nftables, как в Debian 12 по умолчанию
nft add rule inet filter input tcp dport 2445 accept
nft add rule inet filter input tcp dport 30000-30060 accept

# либо ufw, если он у вас
ufw allow 2445/tcp && ufw allow 30000:30060/tcp
```

Обновить сервер до новой версии:

```bash
curl -fsSL https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/narenet/nare-net.js \
     -o /opt/narenet/nare-net.js && systemctl restart narenet
```

Журнал: `journalctl -u narenet -f`

## Что сервер понимает

| Запрос | Зачем |
|---|---|
| `GET /ping` | жив ли, сколько комнат |
| `GET /self` | какой адрес сервер видит у вас |
| `GET /rooms` | список открытых миров |
| `POST /announce` | объявить или обновить свой мир, в ответ приходит токен |
| `POST /bye` | снять объявление |
| `HEAD /file/<sha1>` | есть ли уже такой файл |
| `PUT /file/<sha1>` | выложить jar мода (до 32 МБ) |
| `GET /file/<sha1>` | скачать его |

Объявление выглядит так:

```json
{
  "nick": "Winnigames2024",
  "port": 25565,
  "version": "1.21.4",
  "loader": "forge",
  "world": "Мой мир",
  "players": 1,
  "max": 8,
  "mode": "tunnel",
  "mods": [
    { "id": "forge_narevisuals", "name": "NareVisuals", "version": "2.9.0",
      "file": "forge_narevisuals-2.9.0.jar", "sha1": "fa62…", "size": 810459 }
  ],
  "pack": { "name": "narevisuals", "sha1": "…", "size": 12345 }
}
```

Комната живёт минуту без обновления, поэтому объявляться надо примерно
раз в 20 секунд. Токен привязан к адресу: с другого адреса им
воспользоваться нельзя.

## Туннель

На том же порту 2445. Соединение, которое начинается со слова `NARE1`,
уходит в туннель, остальное читается как HTTP.

```
хозяин  -> NARE1 HOST <токен>\n      сервер -> {"port":30000,"ok":true}
                                     сервер -> {"open":"<id>"}   на каждого гостя
хозяин  -> NARE1 DATA <id>\n         встречный канал, дальше просто трафик
```

Хозяин раз в 20 секунд посылает `{"ping":1}`, сервер отвечает
`{"pong":1}`. Оборвалась связь с хозяином — туннель закрывается, комната
возвращается к прямому подключению.

## Проверка

`test.js` поднимает всё целиком: объявление, список, файлы, туннель
насквозь с двумя гостями и отказ по чужому токену.

```bash
NARENET_PORT=24450 NARENET_DIR=/tmp/nndata node nare-net.js &
node test.js
```

## Место на диске

Файлы модов лежат в `/var/lib/narenet/files` по sha1. Больше 3 ГБ не
занимают: когда места нет, уходят те, к которым дольше всего не
обращались. Один файл — не больше 32 МБ.
