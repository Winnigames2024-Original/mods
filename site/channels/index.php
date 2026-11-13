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
<div class="nv-search" style="margin-top:4px"><input id="chq" type="search" placeholder="Поиск по каналам…" autocomplete="off"></div>
<?php if (!$channels): ?><p class="muted">Пока нет каналов.</p><?php else: ?>
<div class="mgrid chans" id="chgrid">
  <?php foreach ($channels as $c):
    $ava = !empty($c['avatar']) ? ' style="background-image:url(\''.h($c['avatar']).'\');background-size:cover;background-position:center"' : '';
    $s = strtolower(($c['name'] ?? '').' '.($c['username'] ?? '').' '.($c['desc'] ?? '')); ?>
  <a class="mcard chan" href="/channels/chat.php?c=<?= urlencode($c['id']) ?>" data-s="<?= h($s) ?>">
    <div class="cc-ava"<?= $ava ?>><?= empty($c['avatar']) ? h(initial($c['name'] ?? '?')) : '' ?></div>
    <div class="mmeta"><div class="mtitle"># <?= h($c['name']) ?></div>
      <div class="muted"><?= !empty($c['username']) ? '@'.h($c['username']).' · ' : '' ?><?= (int)$c['msgs'] ?> сообщений</div></div>
  </a>
  <?php endforeach; ?>
</div>
<p class="muted" id="chempty" style="display:none">Ничего не найдено.</p>
<script>
(function(){ var q=document.getElementById('chq'); if(!q)return;
  q.addEventListener('input',function(){ var v=this.value.trim().toLowerCase(),n=0;
    Array.prototype.forEach.call(document.querySelectorAll('#chgrid .chan'),function(c){
      var show=!v||c.getAttribute('data-s').indexOf(v)>=0; c.style.display=show?'':'none'; if(show)n++; });
    document.getElementById('chempty').style.display=n?'none':'block'; }); })();
</script>
<?php endif; ?>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
