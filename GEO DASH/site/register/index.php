<?php
require_once __DIR__ . '/../_inc/api.php';
$err = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $n = trim($_POST['name'] ?? ''); $p = $_POST['pass'] ?? ''; $p2 = $_POST['pass2'] ?? '';
    if ($p !== $p2) $err = 'Пароли не совпадают';
    else {
        [$ok, $r] = gd_post('/api/register', ['name' => $n, 'pass' => $p]);
        if ($ok && !empty($r['ok'])) {
            $_SESSION['gd_token'] = $r['token'];
            $_SESSION['gd_user']  = $r['account']['name'] ?? $n;
            header('Location: /account/'); exit;
        }
        $err = $r['error'] ?? 'Не удалось зарегистрироваться';
    }
}
gd_head('Регистрация', 'register');
?>
<section class="section"><div class="container">
  <h2>Регистрация</h2>
  <?php if ($err): ?><div class="alert err"><?= h($err) ?></div><?php endif; ?>
  <form class="form card" method="post">
    <label>Имя игрока (3–20 символов)</label>
    <input type="text" name="name" value="<?= h($_POST['name'] ?? '') ?>" required autofocus>
    <label>Пароль</label>
    <input type="password" name="pass" required>
    <label>Пароль ещё раз</label>
    <input type="password" name="pass2" required>
    <p style="margin-top:18px"><button class="btn green" type="submit">СОЗДАТЬ АККАУНТ</button></p>
    <p class="muted">Аккаунт хранится на сервере: уровни, звёзды, монеты, демоны и друзья синхронизируются между игрой и сайтом.</p>
  </form>
</div></section>
<?php gd_foot();
