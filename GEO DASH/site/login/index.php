<?php
require_once __DIR__ . '/../_inc/api.php';
$err = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    [$ok, $r] = gd_post('/api/login', ['name' => $_POST['name'] ?? '', 'pass' => $_POST['pass'] ?? '']);
    if ($ok && !empty($r['ok'])) {
        $_SESSION['gd_token'] = $r['token'];
        $_SESSION['gd_user']  = $r['account']['name'] ?? ($_POST['name'] ?? '');
        header('Location: /account/'); exit;
    }
    $err = $r['error'] ?? 'Не удалось войти';
}
gd_head('Вход', 'login');
?>
<section class="section"><div class="container">
  <h2>Вход</h2>
  <?php if ($err): ?><div class="alert err"><?= h($err) ?></div><?php endif; ?>
  <form class="form card" method="post">
    <label>Имя игрока</label>
    <input type="text" name="name" value="<?= h($_POST['name'] ?? '') ?>" required autofocus>
    <label>Пароль</label>
    <input type="password" name="pass" required>
    <p style="margin-top:18px"><button class="btn green" type="submit">ВОЙТИ</button>
      <a class="btn ghost" href="/register/">Регистрация</a></p>
    <p class="muted">Тот же аккаунт работает и в игре на Windows, и в приложении на Android.</p>
  </form>
</div></section>
<?php gd_foot();
