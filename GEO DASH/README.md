# GEO DASH — Winnigames2024

Ритм-платформер (клон механики Geometry Dash, свой движок и своя графика) + сервер аккаунтов,
сайт и сборки для Windows и Android.

## Установка сервера одной командой (Debian 12 / Ubuntu)

```bash
curl -fsSL "https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/GEO%20DASH/install.sh" | sudo bash
```

После установки то же самое доступно короче:

```bash
curl -fsSL http://geodash.winnigames2024.ru/install.sh | sudo bash
```

Что делает install.sh:

* ставит nginx + php-fpm + Node.js 20;
* поднимает API `geodash` на порту **37534** (systemd-сервис, данные в `/var/lib/geodash`);
* раскладывает сайт в `/var/www/geodash` и настраивает домен **geodash.winnigames2024.ru**
  (`/` открывает `/geodash/index.php`, `/geodash/...` — тот же сайт);
* включает таймер `geodash-update` — каждые 5 минут тянет GitHub, применяет сайт и API
  (рестарт только при изменении) и забирает свежие `.exe` / `.apk` из последнего релиза
  в раздел «Скачать».

DNS: A-запись `geodash` → IP сервера. HTTPS: `certbot --nginx -d geodash.winnigames2024.ru`.

## Структура

```
GEO DASH/
  install.sh                     установка сервера одной командой
  autoupdatefor5minutes.sh       автообновление (systemd-таймер, каждые 5 минут)
  update.json                    версия клиента для автообновления игры
  server/geodash-api.js          API: аккаунты, уровни, друзья, daily/weekly, музыка, админка
  server/geodash.service         systemd-юнит
  site/                          сайт (PHP): главная, уровни, топ, профиль, друзья, скачать, админка
  client/                        исходники игры для Windows (C# WinForms, .NET 8)
  android/                       исходники приложения для Android (Java)
```

## Игра

* Режимы: куб, корабль, шар, НЛО, волна, робот, паук и **червяк** — по клику переворачивает
  гравитацию и проходит сквозь блоки (шипы по-прежнему убивают).
* Скорости: 0.5x, 1x, 2x, 3x, 4x, **5x**, **6x**.
* Триггеры: Move, Rotate, Scale, Alpha, Toggle, Spawn, Stop, Pulse, Color, BG, Ground, Shake,
  Follow, Follow Player Y, Animate, Touch, Count, Instant Count, Pickup, Collision, Random,
  Timer, Camera, Camera Zoom, Song, Gravity, Teleport, On Death, Trail, Hide/Show Player,
  Options, Checkpoint, End, Reverse, Keyframe, Speed.
* Основные уровни: **Neon Start** (Easy), **Static Rush** (Hard), **Hyperdrive** (Insane) —
  со встроенным саундтреком, который генерируется кодом при первом запуске.
* Музыка по ID: любой трек Newgrounds — сервер скачивает его и раздаёт клиенту,
  файл ложится в `assets/music` и играет офлайн.
* Офлайн: свои уровни, SAVED, монеты, прогресс и очередь загрузок работают без сети
  и синхронизируются при её появлении.
* Защита уровней: закрыт / по паролю / свободное копирование.

## Данные игры

Всё лежит в папке `assets` рядом с игрой (а если она недоступна для записи —
в `%LOCALAPPDATA%\GEO DASH\assets`):

```
assets/levels/    мои уровни (.gdl)
assets/saved/     скачанные и офлайн-уровни
assets/official/  основные уровни
assets/music/     музыка (mp3 по ID + встроенный саундтрек .wav)
assets/cache/     кэш списков для офлайна
```

## Сборка

* Windows: `client/build.bat` или `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
* Android: `cd android && gradle assembleDebug`
* Оба артефакта собираются автоматически через GitHub Actions и попадают в релиз,
  откуда сервер забирает их на сайт в раздел «Скачать».
