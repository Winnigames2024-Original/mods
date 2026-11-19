<?php
require_once __DIR__ . '/../_inc/api.php';
[$ok, $top] = gd_get('/api/top');
gd_head('Топ игроков', 'top');
?>
<section class="section"><div class="container">
  <h2>Топ игроков</h2>
  <table class="lv">
    <tr><th>#</th><th>Игрок</th><th>★ звёзды</th><th>Монеты</th><th>Демоны</th><th>Уровни</th></tr>
    <?php foreach ($top ?: [] as $i => $u): ?>
      <tr>
        <td class="muted"><?= $i + 1 ?></td>
        <td class="lvname"><a href="/profile/?user=<?= urlencode($u['name']) ?>"><?= h($u['name']) ?></a>
          <?php if (!empty($u['admin'])): ?> <span class="badge">админ</span><?php endif; ?></td>
        <td><b><?= (int)$u['stars'] ?></b></td>
        <td><?= (int)$u['coins'] ?></td>
        <td><?= (int)$u['demons'] ?></td>
        <td><?= (int)$u['levels'] ?></td>
      </tr>
    <?php endforeach; ?>
    <?php if (!$top): ?><tr><td colspan="6" class="muted">Пока нет игроков.</td></tr><?php endif; ?>
  </table>
</div></section>
<?php gd_foot();
