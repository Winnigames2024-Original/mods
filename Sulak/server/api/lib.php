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
