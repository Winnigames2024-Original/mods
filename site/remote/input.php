<?php
require_once __DIR__ . '/../_inc/config.php';
header('Content-Type: application/json; charset=utf-8');
if (!me() || empty($_SESSION['rtoken'])) { echo json_encode(['ok' => false, 'error' => 'нет сессии']); exit; }
$raw = file_get_contents('php://input');
$in = json_decode($raw, true);
$events = (is_array($in) && isset($in['events']) && is_array($in['events'])) ? $in['events'] : [];
echo json_encode(api_post('/remote/input', ['rtoken' => $_SESSION['rtoken'], 'events' => $events]), JSON_UNESCAPED_UNICODE);
