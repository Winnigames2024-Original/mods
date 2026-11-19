<?php
require_once __DIR__ . '/../_inc/api.php';
$id = (int)($_GET['id'] ?? 0);
[$ok, $lv] = gd_get('/api/levels/' . $id);
[$okS, $scores] = gd_get('/api/levels/' . $id . '/scores');
$msg = '';

if ($_SERVER['REQUEST_METHOD'] === 'POST' && gd_token()) {
    $act = $_POST['act'] ?? '';
    if ($act === 'like') { [$o, $r] = gd_post("/api/levels/$id/like", ['like' => true], gd_token()); $msg = $o ? 'Лайк засчитан' : ($r['error'] ?? ''); }
    if ($act === 'delete' && gd_is_admin()) { gd_post("/api/levels/$id/delete", [], gd_token()); header('Location: /levels/'); exit; }
}

// защита от копирования — читаем из данных уровня
$copyMode = 0; $objects = 0;
if ($ok && !empty($lv['data'])) {
    $j = json_decode($lv['data'], true);
    $copyMode = (int)($j['Settings']['CopyMode'] ?? 0);
    $objects  = is_array($j['Objects'] ?? null) ? count($j['Objects']) : 0;
}
$copyText = [0 => 'закрыт от копирования', 1 => 'копируется по паролю', 2 => 'свободное копирование'][$copyMode] ?? '';

gd_head($ok ? $lv['name'] : 'Уровень', 'levels');
?>
<section class="section"><div class="container">
<?php if (!$ok || !$lv): ?>
  <h2>Уровень не найден</h2>
<?php else: ?>
  <h2><?= h($lv['name']) ?></h2>
  <?php if ($msg): ?><div class="alert info"><?= h($msg) ?></div><?php endif; ?>

  <div class="grid c2">
    <div class="card">
      <div class="flex" style="margin-bottom:12px">
        <span class="diff <?= gd_diff_class((int)$lv['difficulty']) ?>"><?= h(gd_diff_name((int)$lv['difficulty'])) ?></span>
        <span class="badge">★ <?= (int)$lv['stars'] ?></span>
        <span class="badge">♥ <?= (int)$lv['likes'] ?></span>
        <span class="badge">↓ <?= (int)$lv['downloads'] ?></span>
        <span class="badge">▶ <?= (int)$lv['plays'] ?></span>
      </div>
      <p>Автор: <a href="/profile/?user=<?= urlencode($lv['author']) ?>"><b><?= h($lv['author']) ?></b></a></p>
      <?php if (!empty($lv['desc'])): ?><p class="muted"><?= nl2br(h($lv['desc'])) ?></p><?php endif; ?>
      <p class="muted">
        ID уровня: <b>#<?= (int)$lv['id'] ?></b><br>
        Музыка: <?= (int)$lv['songId'] < 0 ? 'встроенный трек' : ('#' . (int)$lv['songId'] . ' ' . h($lv['songName'])) ?><br>
        Объектов: <?= $objects ?> · длина: <?= (int)(($lv['length'] ?? 0) / 30) ?> клеток<br>
        Копирование: <?= h($copyText) ?><br>
        Обновлён: <?= h(substr((string)($lv['updated'] ?? ''), 0, 16)) ?>
      </p>
      <p style="margin-top:14px" class="flex">
        <?php if (gd_token()): ?>
          <form method="post"><input type="hidden" name="act" value="like"><button class="btn small pink" type="submit">ЛАЙК</button></form>
        <?php endif; ?>
        <a class="btn small ghost" href="/download/">Играть в игре</a>
        <?php if (gd_is_admin()): ?>
          <form method="post" onsubmit="return confirm('Удалить уровень?')">
            <input type="hidden" name="act" value="delete"><button class="btn small red" type="submit">Удалить</button></form>
        <?php endif; ?>
      </p>
      <p class="muted">В игре: Онлайн → найди <b>#<?= (int)$lv['id'] ?></b> → «Играть» или «Сохранить (SAVED)» для офлайна.</p>
    </div>

    <div class="card">
      <h3>Лучшие результаты</h3>
      <table class="lv">
        <tr><th>#</th><th>Игрок</th><th>%</th><th>Монеты</th><th>Попыток</th></tr>
        <?php foreach (array_slice($scores ?: [], 0, 20) as $i => $s): ?>
          <tr>
            <td class="muted"><?= $i + 1 ?></td>
            <td><a href="/profile/?user=<?= urlencode($s['user']) ?>"><?= h($s['user']) ?></a></td>
            <td><b><?= (int)$s['percent'] ?>%</b></td>
            <td><?php for ($c = 1; $c <= 3; $c++): ?><span class="coin<?= $c <= (int)$s['coins'] ? '' : ' off' ?>"></span> <?php endfor; ?></td>
            <td class="muted"><?= (int)$s['attempts'] ?></td>
          </tr>
        <?php endforeach; ?>
        <?php if (!$scores): ?><tr><td colspan="5" class="muted">Ещё никто не играл.</td></tr><?php endif; ?>
      </table>
    </div>
  </div>
<?php endif; ?>
</div></section>
<?php gd_foot();
