<?php
/** Короткий адрес для уведомлений ЮKassa. */
require_once __DIR__ . '/../_inc/pay.php';
$res = gd_kassa_notify((string)file_get_contents('php://input'), (string)($_SERVER['REMOTE_ADDR'] ?? ''));
if (!$res['ok']) { http_response_code($res['code'] ?? 400); echo $res['error']; exit; }
header('Content-Type: text/plain; charset=utf-8');
echo 'OK';
