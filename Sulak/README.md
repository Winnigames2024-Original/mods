# Sulak

Репозиторий: `Winnigames2024-Original/mods`, папка `Sulak/`.

Мод для **Minecraft Forge 1.21.10** (Forge 60.1.18, Java 21).
Работает и на выделенном сервере, и в одиночной игре. Мод нужен на клиенте и на сервере.

## Что он делает

* **Сулак** — NPC-помощник. При заходе любого игрока на сервер он появляется
  рядом со случайным игроком и ходит за ним.
* Нажми на него → следующее сообщение, которое ты напишешь в чат, становится
  желанием. Сообщение в общий чат не уходит.
* Желание уходит в ИИ через `ai.winnigames2024.ru/api/api.php`. **У каждого
  игрока своя память** — ключ памяти это его UUID (`dbtoken`).
* ИИ возвращает JSON с действиями, мод их исполняет: выдать любые предметы и
  блоки, убить игрока, заменить алмазы в инвентаре на землю, повесить правило
  «из эндердракона падают алмазы», телепорт, эффекты, погода, молния и т.д.
* У каждого игрока **лимит желаний** (по умолчанию 10). Когда он кончается —
  с игроком начинают происходить странные вещи, и ему **меняется скин**.

## Сборка

```
gradlew build
```
Готовый jar: `build/libs/sulak-1.0.jar`.

## Настройка

Файл `config/sulak-common.toml` появится после первого запуска:

| Параметр | Смысл |
|---|---|
| `apiUrl` | адрес api.php |
| `apiKey` | общий ключ сервера (бесплатно на `/api/index.php`) |
| `requestLimit` | сколько желаний даётся игроку |
| `spawnOnJoin` / `spawnChance` | появляться ли при заходе игрока |
| `allowKill` | можно ли ИИ убивать игроков |
| `allowRawCommands` | можно ли ИИ выполнять любые ванильные команды (по умолчанию нет) |
| `curseEnabled` / `curseIntervalTicks` | проклятие после лимита |
| `curseChangesSkin` | менять ли скин проклятому |

## Команды

```
/sulak key <ключ>              — свой личный api_key
/sulak left                    — сколько желаний осталось
/sulak spawn [игрок]           — призвать Сулака         (оператор)
/sulak roll                    — перевыбрать владельца    (оператор)
/sulak despawn                 — убрать Сулака            (оператор)
/sulak limit <игрок> <число>   — поставить счётчик желаний(оператор)
/sulak reset <игрок>           — снять проклятие и лимит  (оператор)
/sulak curse <игрок>           — проклясть                (оператор)
/sulak drops clear             — убрать правила дропа     (оператор)
```

## Серверная часть

Всё в папке `server/`:

* `api/api.php` — шлюз: память по `dbtoken`, проверка `api_key`, вызов модели,
  разбор JSON с действиями;
* `api/index.php` — страница выдачи бесплатных ключей + документация;
* `api/config.php` — выбор движка: `qwen` (Qwen локально через Ollama — бесплатно,
  без ключей), `qwen_cloud` (Qwen через OpenRouter, бесплатная модель),
  `iskra` (своя модель);
* `install.sh` — установка и обновление одной командой с GitHub;
* `deploy.sh` — то же самое, но одним самодостаточным файлом, без интернета к GitHub;
* `nginx/` — отдельно сам конфиг.

### Установка на VPS 166.1.161.200

```
curl -fsSL https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/Sulak/server/install.sh | bash
```

С автообновлением раз в 10 минут:

```
curl -fsSL https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/Sulak/server/install.sh | bash -s -- --auto-update
```

Повторный запуск = обновление, `config.php` с ключами не трогается.

Потом A-запись `ai.winnigames2024.ru -> 166.1.161.200` и движок:

```
curl -fsSL https://ollama.com/install.sh | sh
ollama pull qwen2.5:3b
```

### Проверка через curl

```
curl "https://ai.winnigames2024.ru/api/api.php?dbtoken=test-uuid&system_prompt=Отвечай%20JSON&user_promps=привет&api_key=КЛЮЧ"
```

POST работает так же:

```
curl -X POST https://ai.winnigames2024.ru/api/api.php \
  -d dbtoken=test-uuid -d user_promps='дай алмазов' -d api_key=КЛЮЧ
```

## Формат ответа ИИ

```json
{
  "say": "Держи. Но помни, кто дал.",
  "actions": [
    {"type":"give","target":"@owner","item":"minecraft:diamond","count":64},
    {"type":"replace_items","target":"Steve","from":"minecraft:diamond","to":"minecraft:dirt"},
    {"type":"drop_rule","entity":"minecraft:ender_dragon","item":"minecraft:diamond","count":32},
    {"type":"kill","target":"Steve"}
  ]
}
```

Полный список типов действий — в `SulakPrompt.java`.
