<?php
require_once __DIR__ . '/../_inc/config.php';
$PAGE_TITLE = 'Трансляции — NareLauncher';
$PAGE_DESC  = 'Прямые трансляции сообщества NareLauncher.';
require __DIR__ . '/../_inc/head.php';
$streams = api_get('/stream/list')['streams'] ?? [];
?>
<div class="media-head">
  <h1>Трансляции</h1>
  <?php if (me()): ?><a class="btn" href="/live/go.php">Начать трансляцию</a><?php endif; ?>
</div>
<?php if (!$streams): ?>
  <p class="muted">Сейчас нет прямых эфиров.</p>
<?php else: ?>
<div class="mgrid">
  <?php foreach ($streams as $s): ?>
  <a class="mcard" href="/live/watch.php?s=<?= urlencode($s['id']) ?>">
    <div class="mthumb vid"><span class="live-dot">● LIVE</span></div>
    <div class="mmeta"><div class="mtitle"><?= h($s['title']) ?></div>
      <div class="muted"><?= h($s['user']) ?> · <?= (int)$s['viewers'] ?> смотрят</div></div>
  </a>
  <?php endforeach; ?>
</div>
<?php endif; ?>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
