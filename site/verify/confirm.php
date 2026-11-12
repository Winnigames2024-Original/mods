<?php
require_once __DIR__ . '/../_inc/config.php';
header('Content-Type: application/json; charset=utf-8');
$raw = file_get_contents('php://input');
$in = json_decode($raw, true);
$t = is_array($in) ? ($in['token'] ?? '') : ($_POST['token'] ?? '');
if ($t === '') { echo json_encode(['ok' => false, 'error' => 'нет кода']); exit; }
echo json_encode(api_post('/verify/confirm', ['token' => $t]), JSON_UNESCAPED_UNICODE);
