<?php
require_once __DIR__ . '/../_inc/config.php';
header('Content-Type: application/json; charset=utf-8');

// Завершаем вход только если ожидается второй шаг и его токен реально подтверждён на сервере.
if (empty($_SESSION['pending']) || empty($_SESSION['login2fa'])) {
    echo json_encode(['ok' => false, 'error' => 'нет ожидания входа']); exit;
}
$st = api_post('/verify/status', ['token' => $_SESSION['login2fa']]);
if (empty($st['verified'])) { echo json_encode(['ok' => false, 'error' => 'ещё не подтверждено']); exit; }

$p = $_SESSION['pending'];
$_SESSION['nick']  = $p['nick'];
$_SESSION['token'] = $p['token'];
$_SESSION['admin'] = !empty($p['admin']);
nare_set_auth($p['token']);   // запомнить вход в cookie
unset($_SESSION['pending'], $_SESSION['login2fa']);
echo json_encode(['ok' => true]);
