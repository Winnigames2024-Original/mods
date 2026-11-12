<?php
require_once __DIR__ . '/../_inc/config.php';
$msg = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST' && ($_POST['act'] ?? '') === 'create') {
    if (!me()) { header('Location: /login/'); exit; }
    $r = api_post('/channel/create', ['token' => token(), 'name' => $_POST['name'] ?? '']);
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
<form method="post" class="row-form" style="margin:12px 0">
  <input type="hidden" name="act" value="create">
  <input name="name" placeholder="Название нового канала" maxlength="60" required style="flex:1">
  <button class="btn" type="submit">Создать</button>
</form>
<?php if ($msg): ?><div class="msg"><?= h($msg) ?></div><?php endif; ?>
<?php endif; ?>
<?php if (!$channels): ?><p class="muted">Пока нет каналов.</p><?php else: ?>
<div class="srv-list">
  <?php foreach ($channels as $c): ?>
  <a class="srv" href="/channels/chat.php?c=<?= urlencode($c['id']) ?>" style="text-decoration:none;color:inherit">
    <div class="srv-main"><div class="srv-ico">#</div><div class="srv-body">
      <div class="srv-name"><?= h($c['name']) ?></div>
      <div class="muted">владелец @<?= h($c['owner']) ?> · <?= (int)$c['msgs'] ?> сообщений</div>
    </div></div>
    <div class="srv-act"><span class="btn ghost">Открыть чат</span></div>
  </a>
  <?php endforeach; ?>
</div>
<?php endif; ?>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
