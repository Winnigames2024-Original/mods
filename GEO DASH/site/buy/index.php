<?php
/** Покупка лицензионного ключа: через ЮKassa или через кошелёк ЮMoney. */
require_once __DIR__ . '/../_inc/api.php';
require_once __DIR__ . '/../_inc/pay.php';
gd_login_required();
$me = gd_me();
if (!$me) { session_destroy(); header('Location: /login/'); exit; }

$count = max(1, min(20, (int)($_GET['count'] ?? 1)));
$how   = gd_pay_provider();
$err   = (string)($_GET['err'] ?? '');

$order = null; $fields = [];
if ($how !== '') {
    $order = gd_pay_start(gd_user(), $count);
    if ($order && $how === 'yoomoney')
        $fields = gd_pay_form($order, $count, 'https://' . ($_SERVER['HTTP_HOST'] ?? 'geodash.winnigames2024.ru'));
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

    <?php if ($err): ?>
      <div class="alert err" style="margin-top:16px"><?= h($err) ?></div>
    <?php endif; ?>

    <?php if ($how === ''): ?>
      <div class="alert err" style="margin-top:16px">
        Оплата на сайте пока не настроена. Ключ можно получить у <?= h(GD_ADMIN) ?>.
        <?php if (gd_is_admin()): ?>
          <br><a href="/admin/pay.php"><b>Настроить оплату →</b></a>
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

      <?php if ($how === 'yookassa'): ?>
        <form class="form" method="post" action="/buy/pay.php">
          <input type="hidden" name="order" value="<?= h($order) ?>">
          <input type="hidden" name="count" value="<?= (int)$count ?>">
          <?php if (gd_pay_needs_email()): ?>
            <label>E-mail для чека</label>
            <input type="email" name="email" required placeholder="вы@почта.ру">
            <p class="muted" style="margin:6px 0 14px">Чек на оплату придёт на эту почту — так требует закон.</p>
          <?php endif; ?>
          <button class="btn gold" type="submit">ОПЛАТИТЬ КАРТОЙ</button>
        </form>
        <p class="muted" style="margin-top:14px">Платёж принимает ЮKassa: карта, СБП, кошелёк ЮMoney —
          способ выбирается на её странице.</p>
      <?php else: ?>
        <form method="POST" action="<?= h(gd_pay_action()) ?>">
          <?php foreach ($fields as $k => $v): ?>
            <input type="hidden" name="<?= h($k) ?>" value="<?= h($v) ?>">
          <?php endforeach; ?>

          <p style="margin:0 0 10px"><b>Чем платите</b></p>
          <div class="flex" style="gap:18px;margin-bottom:18px;flex-wrap:wrap">
            <label class="flex" style="gap:8px;align-items:center;cursor:pointer">
              <input type="radio" name="paymentType" value="AC" checked>
              <span>Банковская карта</span>
            </label>
            <label class="flex" style="gap:8px;align-items:center;cursor:pointer">
              <input type="radio" name="paymentType" value="PC">
              <span>Кошелёк ЮMoney</span>
            </label>
          </div>

          <button class="btn gold" type="submit">ОПЛАТИТЬ <?= (int)(gd_pay_price() * $count) ?> ₽</button>
        </form>
      <?php endif; ?>

      <p class="muted" style="margin-top:14px">Номер заказа: <code><?= h($order) ?></code>.
        Ключ появится в разделе <a href="/account/">«Мой аккаунт»</a> сразу после оплаты —
        обычно в ту же секунду.</p>
    <?php endif; ?>
  </div>
</div></section>
<?php gd_foot();
