<?php
/**
 * Выдача лицензии по одноразовому коду:
 *
 *   /lklr/?action=givelicense&value=<код>
 *
 * Сюда платёжная система возвращает покупателя после оплаты. Код привязан к
 * заказу, живёт до первого получения и работает ровно один раз: страница
 * просит войти или создать аккаунт, а потом закрепляет ключи за ним.
 *
 * Сам код в метку платежа не попадает — в ней только номер заказа, — поэтому
 * увидеть его в истории операций или в уведомлении нельзя.
 */
require_once __DIR__ . '/../_inc/api.php';
require_once __DIR__ . '/../_inc/pay.php';

$action = strtolower((string)($_GET['action'] ?? 'givelicense'));
$token  = preg_replace('/[^0-9a-f]/', '', (string)($_GET['value'] ?? $_POST['value'] ?? ''));
$good   = strlen($token) === 32 && $action === 'givelicense';
$err    = '';

// вход или создание аккаунта прямо здесь, с получением ключа
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
            [$cok, $cr] = gd_pay_claim_token($token, (string)$_SESSION['gd_user']);
            if (!$cok) $err = (string)($cr['error'] ?? 'Ключ не удалось выдать');
            else { header('Location: /lklr/?action=givelicense&value=' . urlencode($token)); exit; }
        } else {
            $err = (string)($r['error'] ?? 'Не получилось');
        }
    }
}

$order = null;
if ($good) {
    [$ok, $info] = gd_pay_by_token($token);
    $order = $ok ? ($info['order'] ?? null) : null;
}

$user = gd_user();
$paid = $order && !empty($order['keys']);
$used = $order && !empty($order['tokenUsed']);
$owner = (string)($order['buyer'] ?? '');

// оплата прошла, а уведомление могло не дойти — спросим у ЮMoney сами
if ($order && !$paid && gd_ym_ready()) {
    gd_pay_poll((string)$order['order']);
    [$ok2, $info2] = gd_pay_by_token($token);
    $order = $ok2 ? ($info2['order'] ?? $order) : $order;
    $paid = $order && !empty($order['keys']);
}

// вошёл, код ещё ничей — выдаём молча
if ($user !== null && $paid && !$used && ($owner === '' || strcasecmp($owner, (string)$user) === 0)) {
    [$cok] = gd_pay_claim_token($token, (string)$user);
    if ($cok) { header('Location: /lklr/?action=givelicense&value=' . urlencode($token)); exit; }
}

$mine = $user !== null && ($owner === '' || strcasecmp($owner, (string)$user) === 0);

gd_head('Получение лицензии', 'keys');
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

<?php if (!$good || !$order): ?>
  <h2>Код не подошёл</h2>
  <div class="card">
    <p>Ссылка неполная или код уже не числится за заказом. Если оплата прошла, ключ не потерян:
      он найдётся по номеру заказа — напишите <?= h(GD_ADMIN) ?>, назвав время оплаты.</p>
    <p><a class="btn small ghost" href="/buy/">К покупке</a>
       <?php if ($user !== null): ?><a class="btn small" href="/keys/">Мои ключи</a><?php endif; ?></p>
  </div>

<?php elseif (!$paid): ?>
  <h2>Оплата обрабатывается</h2>
  <div class="card">
    <p>Платёжная система ещё не подтвердила оплату. Обычно это несколько секунд —
      страница обновится сама<?= gd_ym_ready() ? ' и заодно спросит у ЮMoney напрямую' : '' ?>.</p>
    <p class="muted">Ссылку можно не закрывать: код в ней остаётся рабочим, пока ключ не получен.</p>
    <meta http-equiv="refresh" content="7">
  </div>

<?php elseif ($used && !$mine): ?>
  <h2>Этот код уже использован</h2>
  <div class="card">
    <p>Лицензия по нему выдана на аккаунт <b><?= h($owner) ?></b>. Код работает один раз —
      второй аккаунт по нему ключ не получит.</p>
    <p>Если это ваш аккаунт — <a href="/login/">войдите</a>, ключи всегда видны в разделе «Ключи».</p>
  </div>

<?php elseif ($user === null): ?>
  <h2>Оплата прошла — получите лицензию</h2>
  <p class="muted" style="margin-top:-8px">Ключ закрепляется за аккаунтом. Создайте его или войдите —
    и ключ сразу станет вашим.</p>

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
      <input type="hidden" name="value" value="<?= h($token) ?>">
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
      <input type="hidden" name="value" value="<?= h($token) ?>">
      <label>Имя игрока</label>
      <input type="text" name="name" required maxlength="20">
      <label>Пароль</label>
      <input type="password" name="pass" required>
      <p style="margin-top:20px"><button class="btn green" type="submit">ВОЙТИ И ПОЛУЧИТЬ КЛЮЧ</button></p>
    </form>
  </div>

  <p class="muted" style="text-align:center;margin-top:16px">
    Ссылка одноразовая: как только ключ получен, по ней его больше не выдать.
    Сохраните её, пока не завели аккаунт.
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

<?php else: ?>
  <h2>Лицензия выдана</h2>
  <div class="card">
    <p><?= count($order['keys']) > 1 ? 'Ваши ключи:' : 'Ваш ключ:' ?></p>
    <?php foreach ((array)$order['keys'] as $k): ?>
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
