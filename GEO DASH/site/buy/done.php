<?php
/**
 * Сюда покупатель возвращается после оплаты.
 *
 * Если он платил, не входя в аккаунт, здесь же и создаёт его — и ключ из
 * заказа сразу закрепляется за новым аккаунтом. Если аккаунт уже есть, можно
 * войти, и ключ уйдёт в него.
 */
require_once __DIR__ . '/../_inc/api.php';
require_once __DIR__ . '/../_inc/pay.php';

$order = (string)($_GET['order'] ?? $_POST['order'] ?? '');
$good  = (bool)preg_match('/^gd[0-9a-f]{16}$/', $order);
$err   = '';

// создание аккаунта или вход прямо здесь, с привязкой заказа
if ($_SERVER['REQUEST_METHOD'] === 'POST' && $good) {
    $act  = $_POST['act'] ?? '';
    $name = trim((string)($_POST['name'] ?? ''));
    $pass = (string)($_POST['pass'] ?? '');

    if ($act === 'register' && $pass !== (string)($_POST['pass2'] ?? '')) {
        $err = 'Пароли не совпали';
    } elseif ($act === 'register' || $act === 'login') {
        [$ok, $r] = gd_post($act === 'register' ? '/api/register' : '/api/login',
                            ['name' => $name, 'pass' => $pass]);
        if ($ok && !empty($r['ok'])) {
            $_SESSION['gd_token'] = $r['token'];
            $_SESSION['gd_user']  = $r['account']['name'] ?? $name;
            [$cok, $cr] = gd_pay_claim($order, (string)$_SESSION['gd_user']);
            if (!$cok) $err = (string)($cr['error'] ?? 'Ключ не удалось закрепить за аккаунтом');
            else { header('Location: /buy/done.php?order=' . urlencode($order)); exit; }
        } else {
            $err = (string)($r['error'] ?? 'Не получилось');
        }
    }
}

$keys = []; $found = false; $buyer = ''; $paid = false;
if ($good) {
    // уведомление могло не дойти — спросим у ЮMoney напрямую, если можем
    [$fok, $finfo] = gd_pay_order($order);
    $pending = $fok && !empty($finfo['order']) && empty($finfo['order']['keys']);
    if ($pending && gd_ym_ready()) gd_pay_poll($order);

    [$ok, $info] = gd_pay_order($order);
    $o = $ok ? ($info['order'] ?? null) : null;
    $found = (bool)$o;
    $keys  = $o['keys'] ?? [];
    $buyer = (string)($o['buyer'] ?? '');
    $paid  = !empty($keys);
}

$user = gd_user();
// вошёл, заказ оплачен и ещё ничей — закрепляем молча
if ($user !== null && $paid && $buyer === '') {
    [$cok] = gd_pay_claim($order, (string)$user);
    if ($cok) { header('Location: /buy/done.php?order=' . urlencode($order)); exit; }
}
$mine = $user !== null && ($buyer === '' || strcasecmp($buyer, (string)$user) === 0);

gd_head('Оплата', 'keys');
?>
<style>
  .gd-dialog { max-width:460px; margin:0 auto; border:3px solid #000; border-radius:18px;
               background:linear-gradient(180deg,#1b1940,#141230); padding:26px 24px;
               box-shadow:0 18px 40px #0008 }
  .gd-dialog h3 { margin:0 0 6px; font-size:22px }
  .gd-dialog label { display:block; margin:14px 0 6px; font-size:13px; opacity:.85 }
  .gd-dialog input { width:100%; box-sizing:border-box }
  .gd-key { font-family:Consolas,monospace; font-size:22px; letter-spacing:.5px }
  .gd-tabs { display:flex; gap:8px; margin-bottom:4px }
</style>
<section class="section"><div class="container" style="max-width:720px">

<?php if (!$good || !$found): ?>
  <h2>Заказ не найден</h2>
  <div class="card">
    <p>Если оплата прошла, ключ уже выписан — он найдётся по номеру заказа.
      Проверьте ссылку или напишите <?= h(GD_ADMIN) ?>, назвав номер заказа и время оплаты.</p>
    <p><a class="btn small ghost" href="/buy/">К покупке</a></p>
  </div>

<?php elseif (!$paid): ?>
  <h2>Оплата обрабатывается</h2>
  <div class="card">
    <p>Платёжная система ещё не подтвердила оплату. Обычно это занимает несколько секунд —
      страница обновится сама<?= gd_ym_ready() ? ' и заодно спросит у ЮMoney напрямую' : '' ?>.</p>
    <p class="muted">Номер заказа: <code><?= h($order) ?></code></p>
    <meta http-equiv="refresh" content="7">
  </div>

<?php elseif ($user === null): ?>
  <h2>Оплата прошла</h2>
  <p class="muted" style="margin-top:-8px">Остался один шаг: ключ закрепляется за аккаунтом —
    создайте его, и ключ сразу станет вашим.</p>

  <?php if ($err): ?><div class="alert err" style="max-width:460px;margin:0 auto 14px"><?= h($err) ?></div><?php endif; ?>

  <div class="gd-dialog">
    <div class="gd-tabs">
      <button class="btn small gold" type="button" data-tab="reg">НОВЫЙ АККАУНТ</button>
      <button class="btn small ghost" type="button" data-tab="log">У МЕНЯ ЕСТЬ АККАУНТ</button>
    </div>

    <form method="post" id="tab-reg">
      <h3>Создать аккаунт</h3>
      <p class="muted" style="margin:0">Этот же аккаунт работает в игре на Windows и на Android.</p>
      <input type="hidden" name="act" value="register">
      <input type="hidden" name="order" value="<?= h($order) ?>">
      <label>Имя игрока</label>
      <input type="text" name="name" required autofocus maxlength="20"
             value="<?= h($_POST['name'] ?? '') ?>" placeholder="как вас звать в игре">
      <label>Пароль</label>
      <input type="password" name="pass" required minlength="3">
      <label>Пароль ещё раз</label>
      <input type="password" name="pass2" required minlength="3">
      <p style="margin-top:20px"><button class="btn green" type="submit">СОЗДАТЬ И ПОЛУЧИТЬ КЛЮЧ</button></p>
    </form>

    <form method="post" id="tab-log" style="display:none">
      <h3>Войти</h3>
      <p class="muted" style="margin:0">Ключ уйдёт в этот аккаунт.</p>
      <input type="hidden" name="act" value="login">
      <input type="hidden" name="order" value="<?= h($order) ?>">
      <label>Имя игрока</label>
      <input type="text" name="name" required maxlength="20">
      <label>Пароль</label>
      <input type="password" name="pass" required>
      <p style="margin-top:20px"><button class="btn green" type="submit">ВОЙТИ И ПОЛУЧИТЬ КЛЮЧ</button></p>
    </form>
  </div>

  <p class="muted" style="text-align:center;margin-top:16px">
    Номер заказа: <code><?= h($order) ?></code> — запишите на случай, если страница закроется.
  </p>

  <script>
    document.querySelectorAll('[data-tab]').forEach(function (b) {
      b.addEventListener('click', function () {
        var reg = b.dataset.tab === 'reg';
        document.getElementById('tab-reg').style.display = reg ? '' : 'none';
        document.getElementById('tab-log').style.display = reg ? 'none' : '';
        document.querySelectorAll('[data-tab]').forEach(function (x) {
          var on = x === b;
          x.classList.toggle('gold', on);
          x.classList.toggle('ghost', !on);
        });
      });
    });
  </script>

<?php elseif (!$mine): ?>
  <h2>Заказ закреплён за другим аккаунтом</h2>
  <div class="card">
    <p>Этот заказ уже привязан к аккаунту <b><?= h($buyer) ?></b>. Войдите под ним,
      чтобы увидеть ключи, или напишите <?= h(GD_ADMIN) ?>.</p>
  </div>

<?php else: ?>
  <h2>Спасибо за покупку</h2>
  <div class="card">
    <p><?= count($keys) > 1 ? 'Ваши ключи:' : 'Ваш ключ:' ?></p>
    <?php foreach ($keys as $k): ?>
      <p><code class="gd-key"><?= h($k) ?></code></p>
    <?php endforeach; ?>
    <p class="muted">Первый ключ уже активирован на аккаунте <b><?= h($user) ?></b> — можно играть.
      Остальные лежат в <a href="/keys/">«Моих ключах»</a>, их можно отдать друзьям.</p>
    <p style="margin-top:16px">
      <a class="btn small" href="/keys/">МОИ КЛЮЧИ</a>
      <a class="btn small ghost" href="/download/">Скачать установщик</a>
    </p>
  </div>
<?php endif; ?>

</div></section>
<?php gd_foot();
