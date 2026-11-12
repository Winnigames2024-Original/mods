<?php
require_once __DIR__ . '/../_inc/config.php';
$PAGE_TITLE = 'Медиа — видео и фото · NareLauncher';
$PAGE_DESC  = 'Видео и фото сообщества NareLauncher: смотрите, лайкайте, комментируйте.';
require __DIR__ . '/../_inc/head.php';
$vids = api_get('/video/list')['videos'] ?? [];
$phs  = api_get('/photos')['photos'] ?? [];
?>
<div class="media-head">
  <h1>Медиа</h1>
  <?php if (me()): ?><a class="btn" href="/media/upload.php">Загрузить</a><?php endif; ?>
</div>

<h2>Видео</h2>
<?php if (!$vids): ?><p class="muted">Пока нет видео.</p><?php else: ?>
<div class="mgrid">
  <?php foreach ($vids as $v): ?>
  <a class="mcard" href="/media/watch.php?v=<?= urlencode($v['id']) ?>">
    <div class="mthumb vid"><?php if (!empty($v['thumb'])): ?><img src="<?= h($v['thumb']) ?>" alt=""><?php else: ?><span>▶</span><?php endif; ?></div>
    <div class="mmeta"><div class="mtitle"><?= h($v['title']) ?></div>
      <div class="muted"><?= h($v['user']) ?> · <?= (int)$v['views'] ?> просм.</div></div>
  </a>
  <?php endforeach; ?>
</div>
<?php endif; ?>

<h2 style="margin-top:22px">Фото</h2>
<?php if (!$phs): ?><p class="muted">Пока нет фото.</p><?php else: ?>
<div class="mgrid">
  <?php foreach ($phs as $p): ?>
  <a class="mcard" href="/media/photo.php?p=<?= urlencode($p['id']) ?>">
    <div class="mthumb"><img src="/photo/img/<?= urlencode($p['id']) ?>" alt="" loading="lazy"></div>
    <div class="mmeta"><div class="mtitle"><?= h($p['title']) ?></div>
      <div class="muted"><?= h($p['user']) ?> · <?= (int)$p['views'] ?> просм.</div></div>
  </a>
  <?php endforeach; ?>
</div>
<?php endif; ?>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
