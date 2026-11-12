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
$worlds = api_post('/admin/worlds', ['token' => $tok])['worlds'] ?? [];
$PAGE_TITLE = 'Серверы игроков — админ · NareLauncher';
require __DIR__ . '/../_inc/head.php';
?>
<div class="media-head"><h1>Серверы игроков</h1><a class="btn ghost" href="/admin/">← В админку</a></div>
<p class="lead">Управление серверами всех игроков: консоль, команды, запуск/остановка. Команды уходят в ServerManager владельца.</p>
<p style="margin:8px 0"><button class="btn ghost" type="button" onclick="smClose('all')">⨯ Закрыть ServerManager у ВСЕХ (остановит их серверы)</button></p>

<?php if (!$servers): ?><p class="muted">Серверов пока нет (ни у кого не сохранено серверов).</p><?php else: ?>
<div class="srv-list">
  <?php foreach ($servers as $s): ?>
  <div class="srv">
    <div class="srv-main"><div class="srv-ico">⚙</div><div class="srv-body">
      <div class="srv-name"><?= h($s['id']) ?> <span class="badge <?= !empty($s['online']) ? 'on' : '' ?>"><?= !empty($s['online']) ? 'онлайн' : 'офлайн' ?></span></div>
      <div class="muted">владелец @<?= h($s['owner']) ?> · порт <?= (int)$s['port'] ?>
        <?php if (!empty($s['stats'])): ?> · игроков <?= (int)$s['stats']['players'] ?> · RAM <?= (int)$s['stats']['ram'] ?>МБ<?php endif; ?></div>
    </div></div>
    <div class="srv-act">
      <button class="btn" type="button" onclick="openCon('<?= h(addslashes($s['name'])) ?>','<?= h(addslashes($s['id'])) ?>')">Консоль</button>
      <button class="btn ghost" type="button" onclick="smClose('<?= h(addslashes($s['owner'])) ?>')">Закрыть ServerManager</button>
    </div>
  </div>
  <?php endforeach; ?>
</div>

<h2 style="margin-top:24px">Сетевые миры «Вместе»</h2>
<p class="muted">Команды выполняются в мире хоста с правами оператора. Выдать OP: впиши ник игрока и нажми «OP».</p>
<?php if (!$worlds): ?><p class="muted">Сейчас нет открытых сетевых миров.</p><?php else: ?>
<div class="srv-list">
  <?php foreach ($worlds as $w): ?>
  <div class="srv" style="flex-direction:column;align-items:stretch;gap:10px">
    <div class="srv-main"><div class="srv-ico">🌐</div><div class="srv-body">
      <div class="srv-name"><?= h($w['user']) ?> <span class="badge on">в сети</span> <?php if (!empty($w['version'])): ?><span class="badge"><?= h($w['version']) ?></span><?php endif; ?></div>
      <div class="muted">хост @<?= h($w['host']) ?> · режим <?= h($w['mode']) ?></div>
    </div></div>
    <div class="row-form">
      <input placeholder="Команда без /" data-host="<?= h($w['host']) ?>" class="wcmd" style="flex:1">
      <button class="btn" type="button" onclick="wsend(this)">Выполнить</button>
    </div>
    <div class="row-form">
      <input placeholder="Ник игрока для OP" data-host="<?= h($w['host']) ?>" class="wop" style="flex:1">
      <button class="btn ghost" type="button" onclick="wop(this,true)">OP</button>
      <button class="btn ghost" type="button" onclick="wop(this,false)">снять OP</button>
    </div>
  </div>
  <?php endforeach; ?>
</div>
<?php endif; ?>

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
function smClose(nick){ if(!confirm(nick==='all'?'Закрыть ServerManager у всех игроков? Их серверы остановятся.':'Закрыть ServerManager у '+nick+'? Его сервер остановится.'))return;
  node('/admin/sm/close',{token:TOK,nick:nick}).then(function(j){ alert(j.ok?('Команда отправлена: '+j.closed):(j.error||'Ошибка')); }); }
function wsend(btn){ var inp=btn.previousElementSibling, host=inp.getAttribute('data-host'), t=inp.value.trim(); if(!t)return;
  node('/admin/worldcommand',{token:TOK,host:host,text:t}).then(function(j){ if(j.ok){inp.value='';btn.textContent='✓';setTimeout(function(){btn.textContent='Выполнить';},1200);}else alert(j.error||'Ошибка'); }); }
function wop(btn,give){ var inp=btn.parentNode.querySelector('.wop'), host=inp.getAttribute('data-host'), p=inp.value.trim(); if(!p)return;
  node('/admin/worldcommand',{token:TOK,host:host,text:(give?'op ':'deop ')+p}).then(function(j){ if(j.ok){inp.value='';alert((give?'OP выдан: ':'OP снят: ')+p);}else alert(j.error||'Ошибка'); }); }
document.addEventListener('click',function(e){ if(e.target&&e.target.id==='send'){ var t=document.getElementById('cmd').value.trim(); if(!t||!CUR)return;
  node('/server/webcmd',{token:TOK,name:CUR,text:t}).then(function(j){ if(j.ok)document.getElementById('cmd').value=''; else alert(j.error||'Ошибка'); }); }});
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
