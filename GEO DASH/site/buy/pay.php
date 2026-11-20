<?php
/**
 * Создаёт платёж в ЮKassa и отправляет покупателя на её страницу оплаты.
 * Отдельным шагом — потому что адрес оплаты выдаёт сама ЮKassa, по запросу.
 */
require_once __DIR__ . '/../_inc/api.php';
require_once __DIR__ . '/../_inc/pay.php';
// вход не обязателен: аккаунт можно создать после оплаты

$back = '/buy/';
if ($_SERVER['REQUEST_METHOD'] !== 'POST') { header('Location: ' . $back); exit; }
if (gd_pay_provider() !== 'yookassa') {
    header('Location: ' . $back . '?err=' . urlencode('Оплата через ЮKassa не настроена')); exit;
}

$order = (string)($_POST['order'] ?? '');
$count = max(1, min(20, (int)($_POST['count'] ?? 1)));
$email = trim((string)($_POST['email'] ?? ''));
$token = preg_replace('/[^0-9a-f]/', '', (string)($_POST['token'] ?? ''));

if (!preg_match('/^gd[0-9a-f]{16}$/', $order)) {
    header('Location: ' . $back . '?err=' . urlencode('Заказ потерялся — начните заново')); exit;
}
if (gd_pay_needs_email() && !filter_var($email, FILTER_VALIDATE_EMAIL)) {
    header('Location: ' . $back . '?count=' . $count . '&err=' . urlencode('Нужен e-mail для чека')); exit;
}

// заказ мог не записаться, если API не отвечал, — записываем ещё раз
gd_pay_api('POST', '/api/licence/order', [
    'order' => $order, 'buyer' => (string)(gd_user() ?? ''),
    'count' => $count, 'sum' => gd_pay_price() * $count,
]);

[$ok, $res] = gd_kassa_start($order, $count,
    'https://' . ($_SERVER['HTTP_HOST'] ?? 'geodash.winnigames2024.ru'), $email, $token);

if (!$ok) {
    header('Location: ' . $back . '?count=' . $count . '&err=' . urlencode($res));
    exit;
}

header('Location: ' . $res, true, 303);
exit;
