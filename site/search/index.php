<?php
require_once __DIR__ . '/../_inc/config.php';
$q = trim($_GET['q'] ?? '');
$PAGE_TITLE = ($q !== '' ? ('Поиск: ' . $q) : 'Поиск') . ' — NareLauncher';
require __DIR__ . '/../_inc/head.php';
$vids = $players = $streams = [];
if ($q !== '') {
    $vids = api_get('/video/list?q=' . urlencode($q))['videos'] ?? [];
    $players = api_get('/search?q=' . urlencode($q))['players'] ?? [];
    $all = api_get('/stream/list')['streams'] ?? [];
    $ql = mb_strtolower($q);
    $streams = array_values(array_filter($all, function ($s) use ($ql) {
        return mb_strpos(mb_strtolower(($s['title'] ?? '') . ' ' . ($s['user'] ?? '') . ' ' . ($s['subcategory'] ?? '')), $ql) !== false;
    }));
}
?>
<h1>Поиск</h1>
<form method="get" class="row-form" style="margin:12px 0">
  <input name="q" value="<?= h($q) ?>" placeholder="Видео, каналы, эфиры, игроки…" style="flex:1" autofocus>
  <button class="btn" type="submit">Искать</button>
</form>
<?php if ($q === ''): ?><p class="muted">Введите запрос.</p><?php else: ?>

<?php if ($streams): ?><h2>Эфиры</h2><div class="mgrid">
<?php foreach ($streams as $s): $href=!empty($s['link'])?$s['link']:('/live/watch.php?s='.urlencode($s['id'])); ?>
  <a class="mcard" href="<?= h($href) ?>"><div class="mthumb vid"><span class="live-dot">● LIVE</span></div>
  <div class="mmeta"><div class="mtitle"><?= h($s['title']) ?></div><div class="muted"><?= h($s['user']) ?> · <?= (int)$s['viewers'] ?> смотрят</div></div></a>
<?php endforeach; ?></div><?php endif; ?>

<?php if ($vids): ?><h2 style="margin-top:20px">Видео</h2><div class="mgrid">
<?php foreach ($vids as $v): ?>
  <a class="mcard" href="/media/watch.php?v=<?= urlencode($v['id']) ?>"><div class="mthumb vid"><?php if(!empty($v['thumb'])):?><img src="<?= h($v['thumb']) ?>" alt=""><?php else:?><span>▶</span><?php endif;?></div>
  <div class="mmeta"><div class="mtitle"><?= h($v['title']) ?></div><div class="muted"><?= h($v['user']) ?> · <?= (int)$v['views'] ?> просм.</div></div></a>
<?php endforeach; ?></div><?php endif; ?>

<?php if ($players): ?><h2 style="margin-top:20px">Игроки и каналы</h2><div class="srv-list">
<?php foreach ($players as $p): ?>
  <a class="srv" href="/profile/view/?user=<?= urlencode($p['nick']) ?>" style="text-decoration:none;color:inherit">
    <div class="srv-main"><div class="srv-ico"><?= h(initial($p['username'])) ?></div><div class="srv-body">
      <div class="srv-name"><?= h($p['username']) ?> <?php if(!empty($p['online'])):?><span class="badge on">онлайн</span><?php endif;?></div>
      <div class="muted">@<?= h($p['nick']) ?></div></div></div>
    <div class="srv-act"><span class="btn ghost">Профиль</span></div></a>
<?php endforeach; ?></div><?php endif; ?>

<?php if (!$streams && !$vids && !$players): ?><p class="muted">Ничего не найдено по «<?= h($q) ?>».</p><?php endif; ?>
<?php endif; ?>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
