<?php
require_once __DIR__ . '/../../_inc/config.php';
if (isset($_GET['logout'])) { session_destroy(); header('Location: /home/'); exit; }
require_login();
$nick = me();
$p = api_get('/profile/get?nick=' . urlencode($nick));
$PAGE_TITLE = 'Мой профиль — NareLauncher';
$PAGE_DESC  = 'Ваш профиль NareLauncher: статус, монеты, тег.';
require __DIR__ . '/../../_inc/head.php';
$letter = mb_strtoupper(mb_substr($p['username'] ?? $nick, 0, 1));
?>
<div class="panel">
  <div class="prof">
    <div class="ava"><?= h($letter) ?></div>
    <div>
      <h1 style="margin:0"><?= h($p['username'] ?? $nick) ?>
        <?php if (!empty($p['admin'])): ?><span class="badge">админ</span>
        <?php elseif (!empty($p['moderator'])): ?><span class="badge">модератор</span><?php endif; ?>
        <?php if (!empty($p['online'])): ?><span class="badge on">онлайн</span><?php endif; ?>
      </h1>
      <div class="muted">@<?= h($nick) ?> <?php if (!empty($p['tag'])): ?>· <span class="tag"><?= h($p['tag']) ?></span><?php endif; ?></div>
      <?php if (!empty($p['status'])): ?><div style="margin-top:6px"><?= h($p['status']) ?></div><?php endif; ?>
    </div>
  </div>
  <div class="grid" style="margin-top:18px">
    <div class="card"><h3>Монеты</h3><p class="coins" style="font-size:22px"><?= (int)($p['coins'] ?? 0) ?></p></div>
    <div class="card"><h3>Ник на сервере</h3><p><?= h($p['chatName'] ?: $nick) ?></p></div>
    <div class="card"><h3>Регистрация</h3><p><?= $p['since'] ? h(date('d.m.Y', (int)($p['since']/1000))) : '—' ?></p></div>
  </div>
  <p style="margin-top:18px">
    <a class="btn ghost" href="/profile/view/?user=<?= urlencode($nick) ?>">Открыть публичный профиль</a>
    <a class="btn ghost" href="/profile/my/?logout=1">Выйти</a>
  </p>
</div>
<?php require __DIR__ . '/../../_inc/foot.php'; ?>
