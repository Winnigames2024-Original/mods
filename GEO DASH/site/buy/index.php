<?php
/** Покупка лицензионного ключа через ЮMoney. */
require_once __DIR__ . '/../_inc/api.php';
require_once __DIR__ . '/../_inc/pay.php';
gd_login_required();
$me = gd_me();
if (!$me) { session_destroy(); header('Location: /login/'); exit; }

$count = max(1, min(20, (int)($_GET['count'] ?? 1)));
$order = null; $fields = [];
if (gd_pay_ready()) {
    $order = gd_pay_start(gd_user(), $count);
    if ($order) $fields = gd_pay_form($order, $count, 'https://' . ($_SERVER['HTTP_HOST'] ?? 'geodash.winnigames2024.ru'));
}

gd_head('Купить ключ', 'account');
?>
<section class="section"><div class="container" style="max-width:720px">
  <h2>Лицензионный ключ GEO DASH</h2>

  <div class="card">
    <p>Один ключ открывает игру на аккаунте <b><?= h(gd_user()) ?></b> — навсегда и на всех ваших
      устройствах: и на компьютере, и на телефоне. Ключ активируется один раз.</p>
    <p class="muted">Ключей можно взять несколько: лишние останутся в вашем аккаунте, и их можно
      отдать друзьям — каждый активирует свой на своём аккаунте.</p>

    <?php if (!gd_pay_ready()): ?>
      <div class="alert err" style="margin-top:16px">
        Оплата на сайте пока не настроена. Ключ можно получить у <?= h(GD_ADMIN) ?>.
        <?php if (gd_is_admin()): ?>
          <br><a href="/admin/yoomoney.php"><b>Настроить ЮMoney →</b></a>
        <?php endif; ?>
      </div>
    <?php elseif (!$order): ?>
      <div class="alert err" style="margin-top:16px">Сервер игры не отвечает — попробуйте чуть позже.</div>
    <?php else: ?>
      <div class="flex" style="gap:10px;margin:18px 0">
        <?php foreach ([1, 2, 3, 5] as $n): ?>
          <a class="btn small <?= $n === $count ? 'gold' : 'ghost' ?>" href="/buy/?count=<?= $n ?>"><?= $n ?> шт.</a>
        <?php endforeach; ?>
      </div>

      <p style="font-size:26px;margin:0 0 14px"><b><?= (int)(gd_pay_price() * $count) ?> ₽</b>
        <span class="muted" style="font-size:15px">за <?= $count ?> <?= $count === 1 ? 'ключ' : 'ключа' ?></span></p>

      <form method="POST" action="https://yoomoney.ru/quickpay/confirm">
        <?php foreach ($fields as $k => $v): ?>
          <input type="hidden" name="<?= h($k) ?>" value="<?= h($v) ?>">
        <?php endforeach; ?>
        <button class="btn gold" type="submit">ОПЛАТИТЬ ЧЕРЕЗ ЮMONEY</button>
      </form>

      <p class="muted" style="margin-top:14px">Номер заказа: <code><?= h($order) ?></code>.
        Ключ появится в разделе <a href="/account/">«Мой аккаунт»</a> сразу после оплаты —
        обычно в ту же секунду.</p>
    <?php endif; ?>
  </div>
</div></section>
<?php gd_foot();
