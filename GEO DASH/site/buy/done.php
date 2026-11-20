<?php
/** Сюда ЮMoney возвращает игрока после оплаты. Ключ показываем, как только он выдан. */
require_once __DIR__ . '/../_inc/api.php';
require_once __DIR__ . '/../_inc/pay.php';
gd_login_required();

$order = (string)($_GET['order'] ?? '');
$keys = []; $found = false; $buyer = '';
if (preg_match('/^gd[0-9a-f]{16}$/', $order)) {
    [$ok, $info] = gd_pay_order($order);
    $o = $ok ? ($info['order'] ?? null) : null;
    $found = (bool)$o;
    $keys = $o['keys'] ?? [];
    $buyer = (string)($o['buyer'] ?? '');
}
// ключи заказа показываем только покупателю
$mine = $buyer === '' || strcasecmp($buyer, (string)gd_user()) === 0;

gd_head('Оплата', 'account');
?>
<section class="section"><div class="container" style="max-width:720px">
  <h2>Спасибо за покупку</h2>
  <div class="card">
    <?php if (!$found): ?>
      <p>Заказ не найден. Если оплата прошла — ключ придёт в <a href="/account/">аккаунт</a> в течение минуты.</p>
    <?php elseif (!$mine): ?>
      <p>Этот заказ оформлен на другой аккаунт.</p>
    <?php elseif ($keys): ?>
      <p>Ваши ключи:</p>
      <?php foreach ($keys as $k): ?>
        <p><code style="font-size:22px"><?= h($k) ?></code></p>
      <?php endforeach; ?>
      <p class="muted">Введите ключ в игре при первом запуске. Он останется за аккаунтом навсегда,
        и те же ключи всегда видны в <a href="/account/">аккаунте</a>.</p>
      <p><a class="btn small" href="/download/">Скачать установщик</a></p>
    <?php else: ?>
      <p>Оплата ещё обрабатывается. Обновите страницу через несколько секунд —
        или посмотрите в <a href="/account/">аккаунте</a>, ключ появится там сам.</p>
      <meta http-equiv="refresh" content="7">
    <?php endif; ?>
  </div>
</div></section>
<?php gd_foot();
