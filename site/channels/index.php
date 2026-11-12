<?php
require_once __DIR__ . '/../_inc/config.php';
$msg = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST' && ($_POST['act'] ?? '') === 'create') {
    if (!me()) { header('Location: /login/'); exit; }
    $r = api_post('/channel/create', ['token' => token(), 'name' => $_POST['name'] ?? '', 'desc' => $_POST['desc'] ?? '']);
    if (!empty($r['ok'])) { header('Location: /channels/chat.php?c=' . urlencode($r['id'])); exit; }
    $msg = $r['error'] ?? 'Ошибка';
}
$PAGE_TITLE = 'Каналы — NareLauncher';
$PAGE_DESC  = 'Каналы с общим чатом сообщества NareLauncher.';
require __DIR__ . '/../_inc/head.php';
$channels = api_get('/channel/list')['channels'] ?? [];
?>
<h1>Каналы</h1>
<?php if (me()): ?>
<form method="post" style="margin:12px 0">
  <input type="hidden" name="act" value="create">
  <div class="row-form">
    <input name="name" placeholder="Название нового канала" maxlength="60" required style="flex:1">
    <button class="btn" type="submit">Создать</button>
  </div>
  <input name="desc" placeholder="Описание (необязательно)" maxlength="300" style="width:100%;margin-top:8px;background:#0a0d14;border:1px solid var(--border);color:var(--text);border-radius:9px;padding:10px 12px">
</form>
<?php if ($msg): ?><div class="msg"><?= h($msg) ?></div><?php endif; ?>
<?php endif; ?>
<?php if (!$channels): ?><p class="muted">Пока нет каналов.</p><?php else: ?>
<div class="srv-list">
  <?php foreach ($channels as $c): ?>
  <a class="srv" href="/channels/chat.php?c=<?= urlencode($c['id']) ?>" style="text-decoration:none;color:inherit">
    <div class="srv-main"><div class="srv-ico">#</div><div class="srv-body">
      <div class="srv-name"><?= h($c['name']) ?></div>
      <?php if (!empty($c['desc'])): ?><div class="muted"><?= h($c['desc']) ?></div><?php endif; ?>
      <div class="muted">владелец @<?= h($c['owner']) ?> · <?= (int)$c['msgs'] ?> сообщений</div>
    </div></div>
    <div class="srv-act"><span class="btn ghost">Открыть чат</span></div>
  </a>
  <?php endforeach; ?>
</div>
<?php endif; ?>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
