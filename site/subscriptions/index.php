<?php
require_once __DIR__ . '/../_inc/config.php';
require_login();
$r = api_post('/subscribed', ['token' => token()]);
$channels = $r['channels'] ?? [];
$videos = $r['videos'] ?? [];
$PAGE_TITLE = 'Подписки — NareLauncher';
require __DIR__ . '/../_inc/head.php';
?>
<h1>Подписки</h1>
<?php if (!$channels): ?>
  <p class="muted">Вы пока ни на кого не подписаны. Откройте профиль игрока и нажмите «Подписаться».</p>
<?php else: ?>
<div class="srv-list">
  <?php foreach ($channels as $c): ?>
  <div class="srv">
    <div class="srv-main"><div class="srv-ico"><?= h(initial($c['user'])) ?></div><div class="srv-body">
      <div class="srv-name"><?= h($c['user']) ?>
        <?php if (!empty($c['live'])): ?><span class="badge on">в эфире</span><?php elseif (!empty($c['online'])): ?><span class="badge on">онлайн</span><?php endif; ?></div>
      <div class="muted"><?= (int)$c['subscribers'] ?> подписчиков</div>
    </div></div>
    <div class="srv-act">
      <?php if (!empty($c['live'])): ?><a class="btn" href="/live/">Смотреть эфир</a><?php endif; ?>
      <a class="btn ghost" href="/profile/view/?user=<?= urlencode($c['nick']) ?>">Канал</a>
    </div>
  </div>
  <?php endforeach; ?>
</div>

<h2 style="margin-top:22px">Свежие видео подписок</h2>
<?php if (!$videos): ?><p class="muted">Пока нет новых видео.</p><?php else: ?>
<div class="mgrid">
  <?php foreach ($videos as $v): ?>
  <a class="mcard" href="/media/watch.php?v=<?= urlencode($v['id']) ?>">
    <div class="mthumb vid"><?php if (!empty($v['thumb'])): ?><img src="<?= h($v['thumb']) ?>" alt=""><?php else: ?><span>▶</span><?php endif; ?></div>
    <div class="mmeta"><div class="mtitle"><?= h($v['title']) ?></div>
      <div class="muted"><?= h($v['user']) ?> · <?= (int)$v['views'] ?> просм.</div></div>
  </a>
  <?php endforeach; ?>
</div>
<?php endif; ?>
<?php endif; ?>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
