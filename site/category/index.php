<?php
require_once __DIR__ . '/../_inc/config.php';
$cat = strtolower(trim($_GET['cat'] ?? ''));
$sub = trim($_GET['sub'] ?? '');

$CATS = ['games' => 'Игры', 'just-chatting' => 'Разговоры', 'music' => 'Музыка', 'creative' => 'Творчество', 'stream' => 'Трансляции'];
$catRu = $CATS[$cat] ?? ($cat !== '' ? $cat : 'Все');

// стримы этой категории/подкатегории
$streams = api_get('/stream/list')['streams'] ?? [];
$streams = array_values(array_filter($streams, function ($s) use ($cat, $sub) {
    if ($cat !== '' && strtolower($s['category'] ?? '') !== $cat) return false;
    if ($sub !== '' && mb_strtolower($s['subcategory'] ?? '') !== mb_strtolower($sub)) return false;
    return true;
}));
// видео этой категории/подкатегории
$q = '/video/list';
$params = [];
if ($cat !== '') $params[] = 'category=' . urlencode($cat);
if ($sub !== '') $params[] = 'subcategory=' . urlencode($sub);
if ($params) $q .= '?' . implode('&', $params);
$vids = api_get($q)['videos'] ?? [];

$PAGE_TITLE = trim($catRu . ($sub !== '' ? ' · ' . $sub : '')) . ' — NareLauncher';
$PAGE_DESC  = 'Видео и трансляции: ' . $catRu . ($sub !== '' ? ' / ' . $sub : '') . ' на NareLauncher.';
require __DIR__ . '/../_inc/head.php';
?>
<h1><?= h($catRu) ?><?php if ($sub !== ''): ?> <span class="muted">/ <?= h($sub) ?></span><?php endif; ?></h1>

<h2>Трансляции</h2>
<?php if (!$streams): ?><p class="muted">Сейчас нет эфиров в этой категории.</p><?php else: ?>
<div class="mgrid">
  <?php foreach ($streams as $s): $href = !empty($s['link']) ? $s['link'] : ('/live/watch.php?s=' . urlencode($s['id'])); ?>
  <a class="mcard" href="<?= h($href) ?>">
    <div class="mthumb vid"><span class="live-dot">● LIVE</span></div>
    <div class="mmeta"><div class="mtitle"><?= h($s['title']) ?></div>
      <div class="muted"><?= h($s['user']) ?> · <?= (int)$s['viewers'] ?> смотрят<?php if (!empty($s['subcategory'])): ?> · <?= h($s['subcategory']) ?><?php endif; ?></div></div>
  </a>
  <?php endforeach; ?>
</div>
<?php endif; ?>

<h2 style="margin-top:22px">Видео</h2>
<?php if (!$vids): ?><p class="muted">Нет видео в этой категории.</p><?php else: ?>
<div class="mgrid">
  <?php foreach ($vids as $v): ?>
  <a class="mcard" href="/media/watch.php?v=<?= urlencode($v['id']) ?>">
    <div class="mthumb vid"><?php if (!empty($v['thumb'])): ?><img src="<?= h($v['thumb']) ?>" alt=""><?php else: ?><span>▶</span><?php endif; ?></div>
    <div class="mmeta"><div class="mtitle"><?= h($v['title']) ?></div>
      <div class="muted"><?= h($v['user']) ?> · <?= (int)$v['views'] ?> просм.<?php if (!empty($v['subcategory'])): ?> · <?= h($v['subcategory']) ?><?php endif; ?></div></div>
  </a>
  <?php endforeach; ?>
</div>
<?php endif; ?>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
