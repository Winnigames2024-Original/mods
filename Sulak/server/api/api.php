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
