<?php
require_once __DIR__ . '/../_inc/config.php';
$PAGE_TITLE = 'Трансляции — NareLauncher';
$PAGE_DESC  = 'Прямые трансляции сообщества NareLauncher.';
require __DIR__ . '/../_inc/head.php';
$streams = api_get('/stream/list')['streams'] ?? [];
?>
<div class="media-head">
  <h1>Трансляции</h1>
  <?php if (me()): ?><a class="btn" href="/live/go.php">🔴 Начать эфир с браузера</a><?php else: ?><a class="btn ghost" href="/login/">Войдите, чтобы вести эфир</a><?php endif; ?>
</div>
<?php if (me()): ?><p class="muted" style="margin-top:-6px">Можно вести эфир прямо из браузера — <b>приложение не нужно</b>. Браузер спросит разрешение на демонстрацию экрана/окна. Либо стримьте из лаунчера (студия как в OBS).</p><?php endif; ?>
<?php if (!$streams): ?>
  <p class="muted">Сейчас нет прямых эфиров.</p>
<?php else: ?>
<div class="mgrid">
  <?php foreach ($streams as $s): $href = !empty($s['link']) ? $s['link'] : ('/live/watch.php?s=' . urlencode($s['id'])); ?>
  <a class="mcard" href="<?= h($href) ?>">
    <div class="mthumb vid"<?= !empty($s['cover']) ? ' style="background-image:url(\'data:image/jpeg;base64,'.h($s['cover']).'\');background-size:cover;background-position:center"' : '' ?>><span class="live-dot">● LIVE</span></div>
    <div class="mmeta"><div class="mtitle"><?= h($s['title']) ?></div>
      <div class="muted"><?= h($s['user']) ?> · <?= (int)$s['viewers'] ?> смотрят<?php if (!empty($s['subcategory'])): ?> · <?= h($s['subcategory']) ?><?php elseif (!empty($s['category'])): ?> · <?= h($s['category']) ?><?php endif; ?></div></div>
  </a>
  <?php endforeach; ?>
</div>
<?php endif; ?>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
