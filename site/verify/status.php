<?php
require_once __DIR__ . '/../_inc/config.php';
header('Content-Type: application/json; charset=utf-8');
$t = $_GET['t'] ?? '';
if ($t === '') { echo json_encode(['ok' => false]); exit; }
echo json_encode(api_post('/verify/status', ['token' => $t]), JSON_UNESCAPED_UNICODE);
