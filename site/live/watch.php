<?php
require_once __DIR__ . '/../_inc/config.php';
$id = $_GET['s'] ?? '';
$PAGE_TITLE = 'Эфир — NareLauncher';
require __DIR__ . '/../_inc/head.php';
?>
<div class="watch">
  <div class="rc-screen-wrap"><img id="scr" alt="эфир"><div id="off" class="rc-off">Эфир не в сети.</div></div>
  <h1 style="margin:12px 0 2px" id="title">Эфир</h1>
  <div class="muted"><span id="user"></span> · <span id="vw">0</span> смотрят · <span id="st" class="badge">подключение…</span></div>

  <h2 style="margin-top:18px">Чат</h2>
  <div id="chat" class="lchat"></div>
  <?php if (me()): ?>
  <div class="row-form" style="margin-top:8px"><input id="ct" placeholder="Сообщение" maxlength="400" style="flex:1"><button class="btn" id="cs">→</button></div>
  <?php else: ?><p class="muted"><a href="/login/">Войдите</a>, чтобы писать в чат.</p><?php endif; ?>
</div>
<script>
var ID=<?= json_encode($id) ?>, TOK=<?= json_encode(me() ? token() : '') ?>;
function esc(s){var d=document.createElement('div');d.textContent=s;return d.innerHTML;}
function node(p,d){return fetch(p,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(d)}).then(function(r){return r.json();});}
function frame(){ node('/stream/frame',{id:ID}).then(function(j){
  if(j.ok){ document.getElementById('st').textContent=j.online?'в эфире':'не в сети'; document.getElementById('st').className='badge '+(j.online?'on':'');
    document.getElementById('off').style.display=j.online?'none':'grid';
    if(j.title)document.getElementById('title').textContent=j.title; if(j.user)document.getElementById('user').textContent=j.user;
    document.getElementById('vw').textContent=j.viewers||0;
    if(j.jpg)document.getElementById('scr').src='data:image/jpeg;base64,'+j.jpg; }
}).catch(function(){}).finally(function(){setTimeout(frame,600);}); }
frame();
var since=0;
function chat(){ node('/stream/chat/get',{id:ID,since:since}).then(function(j){
  if(j.ok&&j.msgs&&j.msgs.length){ var box=document.getElementById('chat');
    j.msgs.forEach(function(m){ var d=document.createElement('div'); d.className='lmsg'+(m.bot?' bot':''); d.innerHTML='<b>'+esc(m.from)+':</b> '+esc(m.text); box.appendChild(d); });
    since=j.total; box.scrollTop=box.scrollHeight; }
}).catch(function(){}).finally(function(){setTimeout(chat,1500);}); }
chat();
var cs=document.getElementById('cs');
cs&&cs.addEventListener('click',function(){var t=document.getElementById('ct').value.trim();if(!t)return;
  node('/stream/chat/send',{token:TOK,id:ID,text:t}).then(function(j){if(j.ok)document.getElementById('ct').value='';});});
document.getElementById('ct')&&document.getElementById('ct').addEventListener('keydown',function(e){if(e.key==='Enter')cs.click();});
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
