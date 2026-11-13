<?php
require_once __DIR__ . '/../_inc/config.php';
if (me()) { header('Location: /profile/my/'); exit; }
$err = '';

// шаг пароля
if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['password'])) {
    $r = api_post('/account/login', [
        'nick' => $_POST['nick'] ?? '',
        'password' => $_POST['password'] ?? '',
        'device' => 'web-' . substr(md5($_SERVER['REMOTE_ADDR'] ?? ''), 0, 8),
    ]);
    if (!empty($r['ok'])) {
        if (!empty($r['twofa'])) {
            // нужен второй шаг — пароль пройден, но сессию ещё не открываем
            $_SESSION['pending'] = ['nick' => $r['nick'], 'token' => $r['token'], 'admin' => !empty($r['admin'])];
            $vt = api_post('/verify/new', ['purpose' => 'login2fa', 'nick' => $r['nick']]);
            $_SESSION['login2fa'] = $vt['token'] ?? '';
            header('Location: /login/?stage=2fa'); exit;
        }
        $_SESSION['nick'] = $r['nick'];
        $_SESSION['token'] = $r['token'];
        $_SESSION['admin'] = !empty($r['admin']);
        nare_set_auth($r['token']);   // запомнить вход в cookie
        header('Location: /profile/my/'); exit;
    }
    $err = $r['error'] ?? 'Не удалось войти';
}

$stage2fa = ($_GET['stage'] ?? '') === '2fa' && !empty($_SESSION['pending']) && !empty($_SESSION['login2fa']);

$PAGE_TITLE = 'Вход — NareLauncher';
$PAGE_DESC  = 'Войдите в аккаунт NareLauncher: профиль, скины, монеты и сервера.';
require __DIR__ . '/../_inc/head.php';
?>
<?php if ($stage2fa): $vt = $_SESSION['login2fa']; ?>
  <div class="form" style="text-align:center">
    <h1>Подтвердите вход</h1>
    <p class="muted">У вас включена защита входа. Отсканируйте QR телефоном или откройте код на <b>/qrverify</b> и нажмите «Подтвердить».</p>
    <div class="qr-box"><canvas id="qr"></canvas></div>
    <p><a class="btn ghost" id="dl" download="qr.png">Скачать QR (PNG)</a></p>
    <p>Код: <code><?= h($vt) ?></code></p>
    <p class="muted" id="st">Ожидаю подтверждения…</p>
  </div>
  <script src="/assets/qr.js"></script>
  <script>
    var link = <?= json_encode(SITE . '/qrverify/?t=' . $vt) ?>;
    NareQR.draw(document.getElementById('qr'), link, {scale:6});
    try { document.getElementById('dl').href = document.getElementById('qr').toDataURL('image/png'); } catch(e){}
    var token = <?= json_encode($vt) ?>, done = false;
    function poll(){
      if (done) return;
      fetch('/verify/status.php?t=' + encodeURIComponent(token)).then(function(r){return r.json();}).then(function(j){
        if (j && j.verified){
          done = true;
          document.getElementById('st').textContent = 'Подтверждено ✓ Входим…';
          fetch('/login/finish.php', {method:'POST'}).then(function(r){return r.json();}).then(function(x){
            if (x && x.ok) location.href = '/profile/my/'; else location.href = '/login/';
          });
        }
      }).catch(function(){}).finally(function(){ if(!done) setTimeout(poll, 1500); });
    }
    poll();
  </script>
<?php else: ?>
  <form class="form" method="post">
    <h1>Вход</h1>
    <label>Ник</label>
    <input name="nick" maxlength="16" autocomplete="username" required>
    <label>Пароль</label>
    <input name="password" type="password" autocomplete="current-password" required>
    <button class="btn" type="submit">Войти</button>
    <?php if ($err): ?><div class="msg"><?= h($err) ?></div><?php endif; ?>
    <p class="muted" style="margin-top:14px">Нет аккаунта? <a href="/register/">Регистрация</a></p>
  </form>
<?php endif; ?>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
