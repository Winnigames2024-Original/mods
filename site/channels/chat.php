<?php
require_once __DIR__ . '/../_inc/config.php';
$id = $_GET['c'] ?? '';
if (($_POST['act'] ?? '') === 'del' && me()) {
    api_post('/channel/delete', ['token' => token(), 'id' => $id]);
    header('Location: /channels/'); exit;
}
$info = api_post('/channel/chat/get', ['id' => $id, 'since' => 0, 'token' => me() ? token() : '']);
if (empty($info['ok'])) { require __DIR__ . '/../_inc/head.php'; echo '<div class="panel"><h1>Канал не найден</h1></div>'; require __DIR__ . '/../_inc/foot.php'; exit; }
$PAGE_TITLE = ($info['name'] ?? 'Канал') . ' — NareLauncher';
require __DIR__ . '/../_inc/head.php';
?>
<div class="watch">
  <div class="media-head">
    <h1># <?= h($info['name'] ?? '') ?></h1>
    <?php if (!empty($info['isOwner'])): ?>
    <form method="post" onsubmit="return confirm('Удалить канал и весь чат?')"><input type="hidden" name="act" value="del"><button class="btn ghost" type="submit">Удалить канал</button></form>
    <?php endif; ?>
  </div>
  <?php if (!empty($info['desc'])): ?><p class="muted"><?= h($info['desc']) ?></p><?php endif; ?>
  <div id="chat" class="lchat" style="height:420px"></div>
  <?php if (me()): ?>
  <div class="row-form" style="margin-top:8px"><input id="ct" placeholder="Сообщение в канал" maxlength="600" style="flex:1"><button class="btn" id="cs">Отправить</button></div>
  <?php else: ?><p class="muted"><a href="/login/">Войдите</a>, чтобы писать.</p><?php endif; ?>
  <p class="muted" style="margin-top:10px">ID канала для ботов: <code>channel:<?= h($id) ?></code></p>
</div>
<script>
var ID=<?= json_encode($id) ?>, TOK=<?= json_encode(me() ? token() : '') ?>, since=0;
function esc(s){var d=document.createElement('div');d.textContent=s;return d.innerHTML;}
function node(p,d){return fetch(p,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(d)}).then(function(r){return r.json();});}
function poll(){ node('/channel/chat/get',{id:ID,since:since}).then(function(j){
  if(j.ok&&j.msgs&&j.msgs.length){ var box=document.getElementById('chat');
    j.msgs.forEach(function(m){var d=document.createElement('div');d.className='lmsg'+(m.bot?' bot':'');d.innerHTML='<b>'+esc(m.from)+':</b> '+esc(m.text);box.appendChild(d);});
    since=j.total; box.scrollTop=box.scrollHeight; }
}).catch(function(){}).finally(function(){setTimeout(poll,1500);}); }
poll();
var cs=document.getElementById('cs');
cs&&cs.addEventListener('click',function(){var t=document.getElementById('ct').value.trim();if(!t)return;
  node('/channel/chat/send',{token:TOK,id:ID,text:t}).then(function(j){if(j.ok)document.getElementById('ct').value='';});});
document.getElementById('ct')&&document.getElementById('ct').addEventListener('keydown',function(e){if(e.key==='Enter')cs.click();});
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
