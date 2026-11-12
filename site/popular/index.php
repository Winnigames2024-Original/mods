<?php
require_once __DIR__ . '/../_inc/config.php';
$PAGE_TITLE = 'Популярное — NareLauncher';
$PAGE_DESC  = 'Популярные видео, каналы и эфиры NareLauncher.';
require __DIR__ . '/../_inc/head.php';
$p = api_get('/popular');
$vids = $p['videos'] ?? []; $chans = $p['channels'] ?? []; $streams = $p['streams'] ?? [];
?>
<h1>Популярное</h1>

<?php if ($streams): ?><h2>Сейчас в эфире</h2><div class="mgrid">
<?php foreach ($streams as $s): $href=!empty($s['link'])?$s['link']:('/live/watch.php?s='.urlencode($s['id'])); ?>
  <a class="mcard" href="<?= h($href) ?>"><div class="mthumb vid"><span class="live-dot">● LIVE</span></div>
  <div class="mmeta"><div class="mtitle"><?= h($s['title']) ?></div><div class="muted"><?= h($s['user']) ?> · <?= (int)$s['viewers'] ?> смотрят</div></div></a>
<?php endforeach; ?></div><?php endif; ?>

<h2 style="margin-top:20px">Топ каналов</h2>
<?php if (!$chans): ?><p class="muted">Пока нет каналов с подписчиками.</p><?php else: ?>
<div class="srv-list">
<?php foreach ($chans as $c): ?>
  <a class="srv" href="/profile/view/?user=<?= urlencode($c['nick']) ?>" style="text-decoration:none;color:inherit">
    <div class="srv-main"><div class="srv-ico"><?= h(initial($c['user'])) ?></div><div class="srv-body">
      <div class="srv-name"><?= h($c['user']) ?> <?php if(!empty($c['live'])):?><span class="badge on">в эфире</span><?php endif;?></div>
      <div class="muted"><?= (int)$c['subscribers'] ?> подписчиков</div></div></div>
    <div class="srv-act"><span class="btn ghost">Канал</span></div></a>
<?php endforeach; ?></div><?php endif; ?>

<h2 style="margin-top:20px">Топ видео</h2>
<?php if (!$vids): ?><p class="muted">Пока нет видео.</p><?php else: ?>
<div class="mgrid">
<?php foreach ($vids as $v): ?>
  <a class="mcard" href="/media/watch.php?v=<?= urlencode($v['id']) ?>"><div class="mthumb vid"><?php if(!empty($v['thumb'])):?><img src="<?= h($v['thumb']) ?>" alt=""><?php else:?><span>▶</span><?php endif;?></div>
  <div class="mmeta"><div class="mtitle"><?= h($v['title']) ?></div><div class="muted"><?= h($v['user']) ?> · <?= (int)$v['views'] ?> просм. · 👍 <?= (int)$v['likes'] ?></div></div></a>
<?php endforeach; ?></div><?php endif; ?>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
