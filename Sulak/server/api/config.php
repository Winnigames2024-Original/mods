<?php
// ---------------------------------------------------------------------------
//  Настройки шлюза ai.winnigames2024.ru/api
// ---------------------------------------------------------------------------

return [
    // Движок: qwen | qwen_cloud | iskra
    //   qwen       — Qwen локально на этом VPS через Ollama. Бесплатно, без ключей.
    //   qwen_cloud — Qwen через OpenRouter, бесплатная модель (нужен бесплатный ключ).
    //   iskra      — своя модель «Искра».
    'provider' => 'qwen',

    // --- Qwen локально (Ollama на этом же сервере)
    //     установка: curl -fsSL https://ollama.com/install.sh | sh
    //                ollama pull qwen2.5:3b
    //     моделей побольше: qwen2.5:7b (нужно ~8 ГБ RAM), qwen2.5:14b
    'qwen' => [
        'url'   => 'http://127.0.0.1:11434/api/chat',
        'model' => 'qwen2.5:3b',
    ],

    // --- Qwen в облаке (OpenRouter, бесплатный тариф: openrouter.ai/keys)
    'qwen_cloud' => [
        'key'   => getenv('OPENROUTER_KEY') ?: '',
        'model' => 'qwen/qwen-2.5-72b-instruct:free',
    ],

    // --- Своя «Искра»
    'iskra' => [
        'url' => 'http://185.182.108.16:2446/generate',
    ],

    // База: sqlite-файл с ключами и памятью игроков
    'db' => '/var/lib/sulak-ai/ai.db',

    // Сколько пар реплик помнить на каждого игрока (dbtoken)
    'memory_turns' => 12,

    // Лимиты
    'rate_per_minute'   => 20,   // запросов в минуту на один api_key
    'keys_per_ip_day'   => 5,    // сколько ключей можно выпустить с одного IP в сутки
    'max_prompt_chars'  => 6000,

    // Выдавать ключи автоматически на index.php
    'free_keys' => true,
];
