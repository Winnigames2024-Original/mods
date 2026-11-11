<?php
require_once __DIR__ . '/../../_inc/config.php';
$user = $_GET['user'] ?? '';
$p = $user !== '' ? api_get('/profile/get?nick=' . urlencode($user)) : [];
$found = !empty($p['ok']) || !empty($p['nick']);
$name = $found ? ($p['username'] ?? $user) : $user;
$PAGE_TITLE = $found ? ('Профиль ' . $name . ' — NareLauncher') : 'Профиль не найден — NareLauncher';
$PAGE_DESC  = $found ? ('Публичный профиль игрока ' . $name . ' в NareLauncher.') : 'Такого игрока нет в NareLauncher.';
$CANON = SITE . '/profile/view/?user=' . urlencode($user);
require __DIR__ . '/../../_inc/head.php';
?>
<?php if (!$found): ?>
  <div class="panel">
    <h1>Профиль не найден</h1>
    <p class="muted">Игрока «<?= h($user) ?>» нет. Проверьте ник.</p>
    <p style="margin-top:12px"><a class="btn ghost" href="/home/">На главную</a></p>
  </div>
<?php else:
  $letter = mb_strtoupper(mb_substr($name, 0, 1)); ?>
  <div class="panel">
    <div class="prof">
      <div class="ava"><?= h($letter) ?></div>
      <div>
        <h1 style="margin:0"><?= h($name) ?>
          <?php if (!empty($p['admin'])): ?><span class="badge">админ</span>
          <?php elseif (!empty($p['moderator'])): ?><span class="badge">модератор</span><?php endif; ?>
          <?php if (!empty($p['online'])): ?><span class="badge on">онлайн</span><?php endif; ?>
        </h1>
        <div class="muted">@<?= h($p['nick'] ?? $user) ?> <?php if (!empty($p['tag'])): ?>· <span class="tag"><?= h($p['tag']) ?></span><?php endif; ?></div>
        <?php if (!empty($p['status'])): ?><div style="margin-top:6px"><?= h($p['status']) ?></div><?php endif; ?>
      </div>
    </div>
    <div class="grid" style="margin-top:18px">
      <div class="card"><h3>Монеты</h3><p class="coins" style="font-size:22px"><?= (int)($p['coins'] ?? 0) ?></p></div>
      <div class="card"><h3>Регистрация</h3><p><?= !empty($p['since']) ? h(date('d.m.Y', (int)($p['since']/1000))) : '—' ?></p></div>
      <div class="card"><h3>Статус</h3><p><?= !empty($p['online']) ? 'в сети' : 'не в сети' ?></p></div>
    </div>
  </div>
<?php endif; ?>
<?php require __DIR__ . '/../../_inc/foot.php'; ?>
