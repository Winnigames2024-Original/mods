<?php
require_once __DIR__ . '/../_inc/config.php';
require_login();
$tok = token();
$state = api_post('/admin/state', ['token' => $tok]);
if (empty($state['ok'])) {
    require __DIR__ . '/../_inc/head.php';
    echo '<div class="panel"><h1>Нет доступа</h1><p class="muted">Только для администраторов.</p></div>';
    require __DIR__ . '/../_inc/foot.php'; exit;
}
$all = api_post('/admin/servers/all', ['token' => $tok]);
$servers = $all['servers'] ?? [];
$PAGE_TITLE = 'Серверы игроков — админ · NareLauncher';
require __DIR__ . '/../_inc/head.php';
?>
<div class="media-head"><h1>Серверы игроков</h1><a class="btn ghost" href="/admin/">← В админку</a></div>
<p class="lead">Управление серверами всех игроков: консоль, команды, запуск/остановка. Команды уходят в ServerManager владельца.</p>

<?php if (!$servers): ?><p class="muted">Серверов пока нет (ни у кого не сохранено серверов).</p><?php else: ?>
<div class="srv-list">
  <?php foreach ($servers as $s): ?>
  <div class="srv">
    <div class="srv-main"><div class="srv-ico">⚙</div><div class="srv-body">
      <div class="srv-name"><?= h($s['id']) ?> <span class="badge <?= !empty($s['online']) ? 'on' : '' ?>"><?= !empty($s['online']) ? 'онлайн' : 'офлайн' ?></span></div>
      <div class="muted">владелец @<?= h($s['owner']) ?> · порт <?= (int)$s['port'] ?>
        <?php if (!empty($s['stats'])): ?> · игроков <?= (int)$s['stats']['players'] ?> · RAM <?= (int)$s['stats']['ram'] ?>МБ<?php endif; ?></div>
    </div></div>
    <div class="srv-act"><button class="btn" type="button" onclick="openCon('<?= h(addslashes($s['name'])) ?>','<?= h(addslashes($s['id'])) ?>')">Консоль</button></div>
  </div>
  <?php endforeach; ?>
</div>

<div class="panel" id="con" style="display:none;margin-top:18px">
  <h2 style="margin-top:0">Консоль: <span id="conName"></span></h2>
  <div class="btn-row" style="margin-bottom:8px">
    <button class="btn ghost" type="button" onclick="ctl('start')">▶ Запустить</button>
    <button class="btn ghost" type="button" onclick="ctl('restart')">⟳ Перезапустить</button>
    <button class="btn ghost" type="button" onclick="ctl('stop')">■ Остановить</button>
  </div>
  <div id="log" class="lchat" style="height:360px;font-family:ui-monospace,Menlo,Consolas,monospace;font-size:12.5px"></div>
  <div class="row-form" style="margin-top:8px"><input id="cmd" placeholder="Команда серверу (без /)" style="flex:1"><button class="btn" id="send" type="button">Выполнить</button></div>
</div>
<?php endif; ?>
<script>
var TOK=<?= json_encode($tok) ?>, CUR=null, timer=null;
function node(p,d){return fetch(p,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(d)}).then(function(r){return r.json();});}
function esc(s){var d=document.createElement('div');d.textContent=s;return d.innerHTML;}
function openCon(name,id){ CUR=name; document.getElementById('con').style.display='block';
  document.getElementById('conName').textContent=id; document.getElementById('log').innerHTML='';
  document.getElementById('con').scrollIntoView({behavior:'smooth'});
  if(timer)clearInterval(timer); pollLog(); timer=setInterval(pollLog,2000); }
function pollLog(){ if(!CUR)return; node('/server/weblog',{token:TOK,name:CUR}).then(function(j){
  if(j.ok){ var box=document.getElementById('log'); box.innerHTML=(j.lines||[]).map(function(l){return esc(l);}).join('<br>');
    box.scrollTop=box.scrollHeight; } }).catch(function(){}); }
function ctl(action){ if(!CUR)return; node('/server/control',{token:TOK,name:CUR,action:action}).then(function(j){
  if(!j.ok)alert(j.error||'Ошибка'); }); }
document.addEventListener('click',function(e){ if(e.target&&e.target.id==='send'){ var t=document.getElementById('cmd').value.trim(); if(!t||!CUR)return;
  node('/server/webcmd',{token:TOK,name:CUR,text:t}).then(function(j){ if(j.ok)document.getElementById('cmd').value=''; else alert(j.error||'Ошибка'); }); }});
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
