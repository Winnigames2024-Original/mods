<?php
/** Короткий адрес для уведомлений ЮMoney: без параметров, как требует форма. */
require_once __DIR__ . '/../_inc/pay.php';
$res = gd_pay_notify($_POST);
if (!$res['ok']) { http_response_code($res['code'] ?? 400); echo $res['error']; exit; }
header('Content-Type: text/plain; charset=utf-8');
echo 'OK';
