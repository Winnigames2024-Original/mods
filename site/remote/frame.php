<?php
require_once __DIR__ . '/../_inc/config.php';
header('Content-Type: application/json; charset=utf-8');
if (!me() || empty($_SESSION['rtoken'])) { echo json_encode(['ok' => false, 'error' => 'нет сессии']); exit; }
echo json_encode(api_post('/remote/frame', ['rtoken' => $_SESSION['rtoken']]), JSON_UNESCAPED_UNICODE);
