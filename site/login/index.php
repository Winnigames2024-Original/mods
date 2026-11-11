<?php
require_once __DIR__ . '/../_inc/config.php';
if (me()) { header('Location: /profile/my/'); exit; }
$err = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $r = api_post('/account/login', [
        'nick' => $_POST['nick'] ?? '',
        'password' => $_POST['password'] ?? '',
        'device' => 'web-' . substr(md5($_SERVER['REMOTE_ADDR'] ?? ''), 0, 8),
    ]);
    if (!empty($r['ok'])) {
        $_SESSION['nick'] = $r['nick'];
        $_SESSION['token'] = $r['token'];
        $_SESSION['admin'] = !empty($r['admin']);
        header('Location: /profile/my/'); exit;
    }
    $err = $r['error'] ?? 'Не удалось войти';
}
$PAGE_TITLE = 'Вход — NareLauncher';
$PAGE_DESC  = 'Войдите в аккаунт NareLauncher: профиль, скины, монеты и сервера.';
require __DIR__ . '/../_inc/head.php';
?>
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
<?php require __DIR__ . '/../_inc/foot.php'; ?>
