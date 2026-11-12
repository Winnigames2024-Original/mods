<?php
require_once __DIR__ . '/../_inc/config.php';
require_login();
$id = $_GET['id'] ?? '';
$b = api_post('/bot/get', ['token' => token(), 'id' => $id]);
if (empty($b['ok'])) { require __DIR__ . '/../_inc/head.php'; echo '<div class="panel"><h1>Бот не найден</h1></div>'; require __DIR__ . '/../_inc/foot.php'; exit; }
$PAGE_TITLE = 'Редактор бота — NareLauncher';
require __DIR__ . '/../_inc/head.php';
?>
<div class="media-head"><h1>Бот: <?= h($b['name']) ?></h1>
  <a class="btn ghost" href="/bots/">← К списку</a></div>

<div class="panel" style="margin-bottom:14px">
  <div>Токен бота: <code id="btok"><?= h($b['botToken']) ?></code> <button class="btn ghost" id="regen" type="button">Сменить</button></div>
  <div style="margin-top:10px">Привязать к чату: <input id="target" placeholder="channel:ID или stream:ID" style="background:#0a0d14;border:1px solid var(--border);color:var(--text);border-radius:8px;padding:7px 10px">
    <button class="btn ghost" id="join" type="button">Привязать</button>
    <button class="btn ghost" id="leave" type="button">Отвязать</button></div>
  <div class="muted" style="margin-top:8px">Цели: <span id="targets"><?= $b['targets'] ? h(implode(', ', $b['targets'])) : '—' ?></span></div>
</div>

<div class="beditor">
  <div class="bfiles" id="tabs"></div>
  <textarea id="code" spellcheck="false"></textarea>
  <div class="btn-row" style="margin-top:10px">
    <button class="btn" id="save" type="button">Сохранить</button>
    <button class="btn ghost" id="addf" type="button">+ Файл</button>
    <button class="btn ghost" id="delf" type="button">Удалить файл</button>
    <a class="btn ghost" href="/bots/narebot.py" download>Скачать narebot.py</a>
    <span class="muted" id="st"></span>
  </div>
</div>
<script>
var TOK=<?= json_encode(token()) ?>, ID=<?= json_encode($id) ?>;
var files=<?= json_encode($b['files'] ?: (object)[], JSON_UNESCAPED_UNICODE) ?>;
var cur=Object.keys(files)[0]||'bot.py';
if(!files[cur])files[cur]='';
var code=document.getElementById('code'), tabs=document.getElementById('tabs');
function renderTabs(){ tabs.innerHTML=''; Object.keys(files).forEach(function(name){
  var b=document.createElement('button'); b.textContent=name; b.className=(name===cur?'on':'');
  b.onclick=function(){ files[cur]=code.value; cur=name; code.value=files[cur]; renderTabs(); }; tabs.appendChild(b); }); }
code.value=files[cur]; renderTabs();
function st(t){document.getElementById('st').textContent=t;}
function node(p,d){return fetch(p,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(d)}).then(function(r){return r.json();});}
document.getElementById('save').onclick=function(){ files[cur]=code.value;
  node('/bot/save',{token:TOK,id:ID,files:files}).then(function(j){ st(j.ok?'Сохранено ✓':(j.error||'Ошибка')); }); };
document.getElementById('addf').onclick=function(){ var n=prompt('Имя файла (например utils.py):'); if(!n)return; n=n.replace(/[^\w.\-\/]/g,''); if(!n)return;
  files[cur]=code.value; files[n]=files[n]||''; cur=n; code.value=files[cur]; renderTabs(); };
document.getElementById('delf').onclick=function(){ if(Object.keys(files).length<=1){st('Нельзя удалить последний файл');return;}
  if(!confirm('Удалить '+cur+'?'))return; delete files[cur]; cur=Object.keys(files)[0]; code.value=files[cur]; renderTabs(); };
document.getElementById('regen').onclick=function(){ if(!confirm('Сменить токен? Старый перестанет работать.'))return;
  node('/bot/regen',{token:TOK,id:ID}).then(function(j){ if(j.ok){document.getElementById('btok').textContent=j.botToken; st('Токен обновлён');} }); };
function setT(leave){ var t=document.getElementById('target').value.trim(); if(!t)return;
  node('/bot/join',{token:TOK,id:ID,target:t,leave:leave}).then(function(j){ if(j.ok){document.getElementById('targets').textContent=(j.targets.length?j.targets.join(', '):'—');st('Готово');} else st(j.error||'Ошибка'); }); }
document.getElementById('join').onclick=function(){setT(false);};
document.getElementById('leave').onclick=function(){setT(true);};
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
