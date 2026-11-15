#!/usr/bin/env bash
# =============================================================================
#  Установка ai.winnigames2024.ru (nginx + php-fpm + sqlite) на чистый VPS
#  Запускать от root:   bash deploy.sh
# =============================================================================
set -euo pipefail

DOMAIN="ai.winnigames2024.ru"
ROOT="/var/www/ai"
DBDIR="/var/lib/sulak-ai"

echo ">>> пакеты"
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq nginx php-fpm php-cli php-sqlite3 php-curl php-mbstring curl ca-certificates

PHPV="$(php -r 'echo PHP_MAJOR_VERSION.".".PHP_MINOR_VERSION;')"
SOCK="/run/php/php${PHPV}-fpm.sock"
echo ">>> php ${PHPV}, сокет ${SOCK}"

echo ">>> каталоги"
mkdir -p "${ROOT}/api" "${DBDIR}"

echo ">>> /api/config.php"
cat > "${ROOT}/api/config.php" <<'__EOF_config__'
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
__EOF_config__

echo ">>> /api/lib.php"
cat > "${ROOT}/api/lib.php" <<'__EOF_lib__'
<?php
// ---------------------------------------------------------------------------
//  Общие функции: база, ключи, память, вызов модели
// ---------------------------------------------------------------------------

function cfg(): array {
    static $c = null;
    if ($c === null) $c = require __DIR__ . '/config.php';
    return $c;
}

function db(): PDO {
    static $pdo = null;
    if ($pdo !== null) return $pdo;

    $path = cfg()['db'];
    $dir  = dirname($path);
    if (!is_dir($dir)) @mkdir($dir, 0775, true);

    $pdo = new PDO('sqlite:' . $path);
    $pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
    $pdo->exec('PRAGMA journal_mode=WAL');
    $pdo->exec('CREATE TABLE IF NOT EXISTS keys (
        api_key  TEXT PRIMARY KEY,
        owner    TEXT,
        ip       TEXT,
        created  INTEGER,
        requests INTEGER DEFAULT 0,
        last_use INTEGER DEFAULT 0,
        banned   INTEGER DEFAULT 0
    )');
    $pdo->exec('CREATE TABLE IF NOT EXISTS memory (
        id       INTEGER PRIMARY KEY AUTOINCREMENT,
        dbtoken  TEXT NOT NULL,
        role     TEXT NOT NULL,
        content  TEXT NOT NULL,
        ts       INTEGER NOT NULL
    )');
    $pdo->exec('CREATE INDEX IF NOT EXISTS memory_token ON memory(dbtoken, id)');
    $pdo->exec('CREATE TABLE IF NOT EXISTS hits (
        api_key TEXT, ts INTEGER
    )');
    $pdo->exec('CREATE INDEX IF NOT EXISTS hits_key ON hits(api_key, ts)');
    return $pdo;
}

function json_out(array $data, int $code = 200): void {
    http_response_code($code);
    header('Content-Type: application/json; charset=utf-8');
    header('Cache-Control: no-store');
    echo json_encode($data, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

function fail(string $why, int $code = 400): void {
    json_out(['ok' => false, 'error' => $why], $code);
}

function client_ip(): string {
    return $_SERVER['HTTP_X_REAL_IP'] ?? $_SERVER['REMOTE_ADDR'] ?? '0.0.0.0';
}

function new_key(string $owner, string $ip): string {
    $key = 'sk-wg-' . bin2hex(random_bytes(16));
    $st = db()->prepare('INSERT INTO keys (api_key, owner, ip, created) VALUES (?,?,?,?)');
    $st->execute([$key, mb_substr($owner, 0, 40), $ip, time()]);
    return $key;
}

function check_key(string $key): array {
    if ($key === '') fail('нужен api_key — получить бесплатно на /api/index.php', 401);
    $st = db()->prepare('SELECT * FROM keys WHERE api_key = ?');
    $st->execute([$key]);
    $row = $st->fetch(PDO::FETCH_ASSOC);
    if (!$row) fail('неизвестный api_key', 401);
    if ((int)$row['banned'] === 1) fail('ключ заблокирован', 403);

    $limit = (int)cfg()['rate_per_minute'];
    $since = time() - 60;
    db()->prepare('DELETE FROM hits WHERE ts < ?')->execute([time() - 3600]);
    $st = db()->prepare('SELECT COUNT(*) FROM hits WHERE api_key = ? AND ts > ?');
    $st->execute([$key, $since]);
    if ((int)$st->fetchColumn() >= $limit) fail('слишком часто, подожди минуту', 429);

    db()->prepare('INSERT INTO hits (api_key, ts) VALUES (?,?)')->execute([$key, time()]);
    db()->prepare('UPDATE keys SET requests = requests + 1, last_use = ? WHERE api_key = ?')
        ->execute([time(), $key]);
    return $row;
}

// ---- память игрока --------------------------------------------------------

function memory_load(string $dbtoken): array {
    $turns = (int)cfg()['memory_turns'] * 2;
    $st = db()->prepare('SELECT role, content FROM memory WHERE dbtoken = ? ORDER BY id DESC LIMIT ?');
    $st->bindValue(1, $dbtoken, PDO::PARAM_STR);
    $st->bindValue(2, $turns, PDO::PARAM_INT);
    $st->execute();
    return array_reverse($st->fetchAll(PDO::FETCH_ASSOC));
}

function memory_add(string $dbtoken, string $role, string $content): void {
    db()->prepare('INSERT INTO memory (dbtoken, role, content, ts) VALUES (?,?,?,?)')
        ->execute([$dbtoken, $role, mb_substr($content, 0, 4000), time()]);
    // подрезаем историю
    db()->prepare('DELETE FROM memory WHERE dbtoken = ? AND id NOT IN
                   (SELECT id FROM memory WHERE dbtoken = ? ORDER BY id DESC LIMIT 200)')
        ->execute([$dbtoken, $dbtoken]);
}

function memory_count(string $dbtoken): int {
    $st = db()->prepare('SELECT COUNT(*) FROM memory WHERE dbtoken = ?');
    $st->execute([$dbtoken]);
    return (int)$st->fetchColumn();
}

// ---- HTTP -----------------------------------------------------------------

function http_json(string $url, array $payload, array $headers = [], int $timeout = 60): array {
    $ch = curl_init($url);
    curl_setopt_array($ch, [
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_POST           => true,
        CURLOPT_POSTFIELDS     => json_encode($payload, JSON_UNESCAPED_UNICODE),
        CURLOPT_HTTPHEADER     => array_merge(['Content-Type: application/json'], $headers),
        CURLOPT_TIMEOUT        => $timeout,
        CURLOPT_CONNECTTIMEOUT => 15,
    ]);
    $body = curl_exec($ch);
    $code = curl_getinfo($ch, CURLINFO_HTTP_CODE);
    $err  = curl_error($ch);
    curl_close($ch);
    if ($body === false) return ['code' => 0, 'json' => null, 'error' => $err];
    return ['code' => $code, 'json' => json_decode($body, true), 'raw' => $body, 'error' => null];
}

// ---- вызов модели ---------------------------------------------------------

function ask_model(string $system, array $history, string $user): string {
    $c = cfg();
    switch ($c['provider']) {

        case 'qwen': {
            // Qwen локально через Ollama — бесплатно, без внешних ключей
            $messages = [['role' => 'system', 'content' => $system]];
            foreach ($history as $m) $messages[] = ['role' => $m['role'], 'content' => $m['content']];
            $messages[] = ['role' => 'user', 'content' => $user];

            $res = http_json($c['qwen']['url'], [
                'model'    => $c['qwen']['model'],
                'messages' => $messages,
                'stream'   => false,
                'format'   => 'json',
                'options'  => ['temperature' => 0.9, 'num_predict' => 700],
            ], [], 180);

            if ($res['code'] !== 200) {
                fail('qwen (ollama): HTTP ' . $res['code'] . ' ' . ($res['error'] ?? ''), 502);
            }
            return (string)($res['json']['message']['content'] ?? '');
        }

        case 'qwen_cloud': {
            // Qwen через OpenRouter (бесплатная модель)
            $messages = [['role' => 'system', 'content' => $system]];
            foreach ($history as $m) $messages[] = ['role' => $m['role'], 'content' => $m['content']];
            $messages[] = ['role' => 'user', 'content' => $user];

            $res = http_json('https://openrouter.ai/api/v1/chat/completions', [
                'model'           => $c['qwen_cloud']['model'],
                'messages'        => $messages,
                'temperature'     => 0.9,
                'response_format' => ['type' => 'json_object'],
            ], [
                'Authorization: Bearer ' . $c['qwen_cloud']['key'],
                'X-Title: Sulak',
            ]);

            if ($res['code'] !== 200) {
                $msg = $res['json']['error']['message'] ?? ('HTTP ' . $res['code']);
                fail('qwen (openrouter): ' . $msg, 502);
            }
            return (string)($res['json']['choices'][0]['message']['content'] ?? '');
        }

        case 'iskra': {
            $prompt = $system . "\n\n";
            foreach ($history as $m) $prompt .= strtoupper($m['role']) . ': ' . $m['content'] . "\n";
            $prompt .= "USER: " . $user . "\nASSISTANT:";
            $res = http_json($c['iskra']['url'], ['prompt' => $prompt, 'max_tokens' => 512], [], 120);
            if ($res['code'] !== 200) fail('iskra: HTTP ' . $res['code'], 502);
            return (string)($res['json']['text'] ?? $res['json']['result'] ?? $res['raw'] ?? '');
        }
    }
    fail('неизвестный provider в config.php', 500);
    return '';
}

/** Достаёт {"say":...,"actions":[...]} из ответа модели. */
function extract_payload(string $text): array {
    $t = trim($text);
    if (str_starts_with($t, '```')) {
        $t = preg_replace('/^```[a-zA-Z]*\s*/', '', $t);
        $t = preg_replace('/```\s*$/', '', $t);
        $t = trim($t);
    }
    $a = strpos($t, '{');
    $b = strrpos($t, '}');
    if ($a !== false && $b !== false && $b > $a) {
        $obj = json_decode(substr($t, $a, $b - $a + 1), true);
        if (is_array($obj)) {
            return [
                'say'     => (string)($obj['say'] ?? $obj['text'] ?? ''),
                'actions' => is_array($obj['actions'] ?? null) ? $obj['actions'] : [],
            ];
        }
    }
    return ['say' => $t, 'actions' => []];
}
__EOF_lib__

echo ">>> /api/api.php"
cat > "${ROOT}/api/api.php" <<'__EOF_api__'
<?php
// ---------------------------------------------------------------------------
//  ai.winnigames2024.ru/api/api.php
//
//  Параметры (GET или POST, читаются из $_REQUEST):
//    dbtoken       — UUID игрока, он же ключ памяти ИИ
//    system_prompt — системный промпт (его шлёт мод)
//    user_promps   — что игрок написал в чат
//    api_key       — бесплатный ключ с /api/index.php
//
//  Ответ:
//    {"ok":true,"say":"...","actions":[...],"memory":12}
//    {"ok":false,"error":"..."}
// ---------------------------------------------------------------------------

declare(strict_types=1);
require __DIR__ . '/lib.php';

header('Access-Control-Allow-Origin: *');
if (($_SERVER['REQUEST_METHOD'] ?? 'GET') === 'OPTIONS') { http_response_code(204); exit; }

$dbtoken = trim((string)($_REQUEST['dbtoken'] ?? ''));
$system  = trim((string)($_REQUEST['system_prompt'] ?? ''));
$user    = trim((string)($_REQUEST['user_promps'] ?? $_REQUEST['user_prompt'] ?? ''));
$apiKey  = trim((string)($_REQUEST['api_key'] ?? ''));

if ($dbtoken === '') fail('нет dbtoken');
if ($user === '')    fail('нет user_promps');
if (!preg_match('/^[a-zA-Z0-9\-_]{4,64}$/', $dbtoken)) fail('неверный dbtoken');

$max = (int)cfg()['max_prompt_chars'];
if (mb_strlen($system) > $max) $system = mb_substr($system, 0, $max);
if (mb_strlen($user) > 1000)   $user   = mb_substr($user, 0, 1000);

check_key($apiKey);

if ($system === '') {
    $system = 'Ты — Сулак, дух-помощник в Minecraft. Отвечай строго JSON-ом вида '
            . '{"say":"реплика","actions":[]}.';
}

$history = memory_load($dbtoken);

try {
    $raw = ask_model($system, $history, $user);
} catch (Throwable $e) {
    fail('движок недоступен: ' . $e->getMessage(), 502);
}

$payload = extract_payload($raw);

memory_add($dbtoken, 'user', $user);
memory_add($dbtoken, 'assistant', $payload['say'] !== '' ? $payload['say'] : $raw);

json_out([
    'ok'      => true,
    'say'     => $payload['say'],
    'actions' => $payload['actions'],
    'memory'  => memory_count($dbtoken),
]);
__EOF_api__

echo ">>> /api/index.php"
cat > "${ROOT}/api/index.php" <<'__EOF_index__'
<?php
declare(strict_types=1);
require __DIR__ . '/lib.php';

$issued = null;
$error  = null;

if (($_SERVER['REQUEST_METHOD'] ?? 'GET') === 'POST' && cfg()['free_keys']) {
    $owner = trim((string)($_POST['owner'] ?? ''));
    if ($owner === '') {
        $error = 'Напиши свой ник.';
    } else {
        $ip = client_ip();
        $st = db()->prepare('SELECT COUNT(*) FROM keys WHERE ip = ? AND created > ?');
        $st->execute([$ip, time() - 86400]);
        if ((int)$st->fetchColumn() >= (int)cfg()['keys_per_ip_day']) {
            $error = 'С этого адреса уже выдано слишком много ключей. Попробуй завтра.';
        } else {
            $issued = new_key($owner, $ip);
        }
    }
}
?><!doctype html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Sulak AI API — winnigames2024</title>
<style>
  :root { color-scheme: dark; }
  * { box-sizing: border-box; }
  body { margin:0; background:#0d0b12; color:#e8e4f0;
         font:16px/1.6 ui-sans-serif,system-ui,"Segoe UI",Roboto,sans-serif; }
  .wrap { max-width:760px; margin:0 auto; padding:48px 20px 80px; }
  h1 { font-size:30px; margin:0 0 6px; letter-spacing:-.02em; }
  h2 { font-size:19px; margin:40px 0 10px; color:#c9b6ff; }
  .sub { color:#8d85a3; margin:0 0 32px; }
  .card { background:#161222; border:1px solid #2a2140; border-radius:14px; padding:22px; }
  input[type=text] { width:100%; padding:12px 14px; border-radius:10px;
    border:1px solid #2f2749; background:#0f0d18; color:#e8e4f0; font-size:16px; }
  button { margin-top:12px; padding:12px 20px; border:0; border-radius:10px;
    background:#7c4dff; color:#fff; font-size:16px; font-weight:600; cursor:pointer; }
  button:hover { background:#8d63ff; }
  code, pre { font-family:ui-monospace,"JetBrains Mono",Consolas,monospace; font-size:14px; }
  pre { background:#0f0d18; border:1px solid #241d38; border-radius:10px;
        padding:14px; overflow:auto; }
  .key { font-size:17px; word-break:break-all; background:#101a12;
         border:1px solid #1f4d2c; color:#9bf0b4; padding:14px; border-radius:10px; }
  .err { background:#1d1013; border:1px solid #5a2230; color:#ffb3b3;
         padding:12px; border-radius:10px; }
  table { width:100%; border-collapse:collapse; margin-top:8px; }
  td, th { text-align:left; padding:7px 10px; border-bottom:1px solid #241d38; vertical-align:top; }
  th { color:#9b8fc0; font-weight:600; }
  .foot { margin-top:48px; color:#6b6481; font-size:14px; }
</style>
</head>
<body>
<div class="wrap">
  <h1>Sulak AI API</h1>
  <p class="sub">Бесплатный шлюз для мода Sulak и других проектов winnigames2024.</p>

  <div class="card">
    <?php if ($issued): ?>
      <p>Твой ключ (сохрани, он показывается один раз):</p>
      <div class="key"><?= htmlspecialchars($issued, ENT_QUOTES) ?></div>
      <p style="margin-top:14px">В игре: <code>/sulak key <?= htmlspecialchars($issued, ENT_QUOTES) ?></code><br>
         Или в <code>config/sulak-common.toml</code> в поле <code>apiKey</code>.</p>
    <?php else: ?>
      <?php if ($error): ?><p class="err"><?= htmlspecialchars($error, ENT_QUOTES) ?></p><?php endif; ?>
      <form method="post">
        <label for="owner">Ник в Minecraft</label>
        <input type="text" id="owner" name="owner" placeholder="Steve" maxlength="40" required>
        <button type="submit">Получить бесплатный ключ</button>
      </form>
    <?php endif; ?>
  </div>

  <h2>Как дёргать</h2>
  <pre>GET/POST https://ai.winnigames2024.ru/api/api.php
    ?dbtoken=&lt;uuid игрока — это и есть память ИИ&gt;
    &amp;system_prompt=&lt;системный промпт&gt;
    &amp;user_promps=&lt;что написал игрок&gt;
    &amp;api_key=&lt;ключ отсюда&gt;</pre>

  <h2>Параметры</h2>
  <table>
    <tr><th>dbtoken</th><td>UUID игрока. Для каждого dbtoken сервер хранит свою историю диалога, так что у каждого игрока — своя память.</td></tr>
    <tr><th>system_prompt</th><td>Роль и список разрешённых действий. Мод шлёт его сам.</td></tr>
    <tr><th>user_promps</th><td>Текст желания игрока.</td></tr>
    <tr><th>api_key</th><td>Ключ с этой страницы.</td></tr>
  </table>

  <h2>Ответ</h2>
  <pre>{
  "ok": true,
  "say": "Держи. Но помни, кто дал.",
  "actions": [
    {"type":"give","target":"@owner","item":"minecraft:diamond","count":64}
  ],
  "memory": 8
}</pre>

  <h2>Ошибки</h2>
  <pre>{"ok": false, "error": "неизвестный api_key"}</pre>

  <p class="foot">winnigames2024 · лимит <?= (int)cfg()['rate_per_minute'] ?> запросов в минуту на ключ</p>
</div>
</body>
</html>
__EOF_index__

echo ">>> главная страница"
cat > "${ROOT}/index.html" <<'__EOF_HOME__'
<!doctype html><html lang="ru"><head><meta charset="utf-8">
<title>ai.winnigames2024.ru</title>
<style>body{background:#0d0b12;color:#e8e4f0;font:16px/1.6 system-ui,sans-serif;
display:grid;place-items:center;height:100vh;margin:0}
a{color:#b48cff}</style></head>
<body><div><h1>ai.winnigames2024.ru</h1>
<p>API для мода Sulak: <a href="/api/index.php">получить бесплатный ключ</a></p></div></body></html>
__EOF_HOME__

echo ">>> права"
chown -R www-data:www-data "${ROOT}" "${DBDIR}"
chmod 750 "${DBDIR}"
chmod 640 "${ROOT}/api/config.php"

echo ">>> nginx"
cat > "/etc/nginx/sites-available/${DOMAIN}" <<__EOF_NGINX__
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
__EOF_NGINX__

ln -sf "/etc/nginx/sites-available/${DOMAIN}" "/etc/nginx/sites-enabled/${DOMAIN}"
rm -f /etc/nginx/sites-enabled/default
nginx -t
systemctl enable --now "php${PHPV}-fpm" nginx >/dev/null 2>&1 || true
systemctl reload nginx

echo ">>> инициализация базы"
sudo -u www-data php -r 'require "/var/www/ai/api/lib.php"; db(); echo "ok\n";'

cat <<'DONE'

=============================================================
 Готово.

 1) Пропиши A-запись:   ai.winnigames2024.ru  ->  166.1.161.200
 2) Вставь ключ движка: nano /var/www/ai/api/config.php
       'provider' => 'qwen'   — Qwen локально, бесплатно:
          curl -fsSL https://ollama.com/install.sh | sh
          ollama pull qwen2.5:3b

       'provider' => 'qwen_cloud' — Qwen через OpenRouter,
          бесплатный ключ: openrouter.ai/keys

 3) HTTPS:
       apt-get install -y certbot python3-certbot-nginx
       certbot --nginx -d ai.winnigames2024.ru

 4) Проверка:
       curl "http://ai.winnigames2024.ru/api/api.php?dbtoken=test-uuid&user_promps=привет&api_key=КЛЮЧ"

 Ключи игрокам: http://ai.winnigames2024.ru/api/index.php
=============================================================
DONE
