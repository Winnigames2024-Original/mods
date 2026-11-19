<?php
require_once __DIR__ . '/../_inc/api.php';
$user = trim($_GET['user'] ?? '');
if ($user === '') { header('Location: /top/'); exit; }

[$okU, $acc]    = gd_get('/api/users/' . rawurlencode($user));
[$okL, $levels] = gd_get('/api/levels?author=' . urlencode($user));
$msg = '';

if ($_SERVER['REQUEST_METHOD'] === 'POST' && gd_token()) {
    [$ok, $r] = gd_post('/api/friends/request', ['name' => $user], gd_token());
    $msg = $ok ? 'Запрос в друзья отправлен' : ($r['error'] ?? 'Не вышло');
}

gd_head('Профиль ' . $user, 'profile');
?>
<section class="section"><div class="container">
  <?php if (!$okU || !$acc): ?>
    <h2>Игрок не найден</h2>
  <?php else: ?>
    <h2><?= h($acc['name']) ?>
      <?php if (!empty($acc['admin'])): ?><span class="badge">админ</span><?php endif; ?>
      <?php if (!empty($acc['banned'])): ?><span class="badge" style="background:#5c1d28">заблокирован</span><?php endif; ?>
    </h2>
    <?php if ($msg): ?><div class="alert info"><?= h($msg) ?></div><?php endif; ?>

    <div class="stats" style="margin:16px 0 24px">
      <div class="stat"><b><?= (int)$acc['stars'] ?></b><span>звёзды</span></div>
      <div class="stat"><b><?= (int)$acc['coins'] ?></b><span>монеты</span></div>
      <div class="stat"><b><?= (int)$acc['demons'] ?></b><span>демоны</span></div>
      <div class="stat"><b><?= (int)$acc['levels'] ?></b><span>уровней</span></div>
      <div class="stat"><b><?= (int)($acc['friends'] ?? 0) ?></b><span>друзей</span></div>
    </div>

    <?php if (gd_token() && strcasecmp(gd_user(), $acc['name']) !== 0): ?>
      <form method="post" style="margin-bottom:20px"><button class="btn pink" type="submit">Добавить в друзья</button></form>
    <?php endif; ?>

    <h2>Уровни игрока</h2>
    <table class="lv">
      <tr><th>ID</th><th>Название</th><th>Сложность</th><th>★</th><th>♥</th><th>↓</th></tr>
      <?php foreach ($levels ?: [] as $l): ?>
        <tr>
          <td class="muted">#<?= (int)$l['id'] ?></td>
          <td class="lvname"><a href="/level/?id=<?= (int)$l['id'] ?>"><?= h($l['name']) ?></a></td>
          <td><span class="diff <?= gd_diff_class((int)$l['difficulty']) ?>"><?= h(gd_diff_name((int)$l['difficulty'])) ?></span></td>
          <td><?= (int)$l['stars'] ?></td><td><?= (int)$l['likes'] ?></td><td><?= (int)$l['downloads'] ?></td>
        </tr>
      <?php endforeach; ?>
      <?php if (!$levels): ?><tr><td colspan="6" class="muted">Уровней нет.</td></tr><?php endif; ?>
    </table>
  <?php endif; ?>
</div></section>
<?php gd_foot();
