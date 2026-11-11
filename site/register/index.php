<?php
require_once __DIR__ . '/../_inc/config.php';
if (me()) { header('Location: /profile/my/'); exit; }
$err = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $r = api_post('/account/register', [
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
    $err = $r['error'] ?? 'Не удалось зарегистрироваться';
}
$PAGE_TITLE = 'Регистрация — NareLauncher';
$PAGE_DESC  = 'Создайте бесплатный аккаунт NareLauncher: профиль, скины, монеты и свои сервера Minecraft.';
require __DIR__ . '/../_inc/head.php';
?>
<form class="form" method="post">
  <h1>Регистрация</h1>
  <label>Ник (3–16: латиница, цифры, _)</label>
  <input name="nick" maxlength="16" pattern="[A-Za-z0-9_]{3,16}" autocomplete="username" required>
  <label>Пароль (от 4 знаков)</label>
  <input name="password" type="password" minlength="4" autocomplete="new-password" required>
  <button class="btn" type="submit">Создать аккаунт</button>
  <?php if ($err): ?><div class="msg"><?= h($err) ?></div><?php endif; ?>
  <p class="muted" style="margin-top:14px">Уже есть аккаунт? <a href="/login/">Вход</a></p>
</form>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
