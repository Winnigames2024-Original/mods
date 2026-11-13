<?php
require_once __DIR__ . '/../_inc/config.php';
require_login();
$PAGE_TITLE = 'Сообщения — NareLauncher';
require __DIR__ . '/../_inc/head.php';
$with = $_GET['with'] ?? '';
?>
<h1 style="margin-bottom:12px">Личные сообщения</h1>
<div class="dm-wrap">
  <div class="dm-threads panel" id="threads"><p class="muted">Загрузка…</p></div>
  <div class="dm-conv panel" id="conv">
    <div id="conv-head" class="dm-head muted">Выберите диалог или откройте профиль игрока → «написать».</div>
    <div id="conv-body" class="dm-body"></div>
    <div class="row-form dm-foot" id="conv-foot" style="display:none">
      <input id="dm-in" placeholder="Сообщение…" maxlength="1000" style="flex:1"><button class="btn" id="dm-send">→</button>
    </div>
  </div>
</div>
<script>
var TOK=<?= json_encode(token()) ?>, ME=<?= json_encode(me() ?: '') ?>, WITH=<?= json_encode($with) ?>;
function node(p,d){return fetch(p,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(d)}).then(function(r){return r.json();});}
function esc(s){var d=document.createElement('div');d.textContent=s;return d.innerHTML;}
function when(ts){var d=new Date(ts);return d.toLocaleString('ru-RU',{hour:'2-digit',minute:'2-digit',day:'2-digit',month:'2-digit'});}

function loadThreads(){ node('/dm/threads',{token:TOK}).then(function(j){ var box=document.getElementById('threads');
  if(!j.ok){box.innerHTML='<p class="muted">Ошибка</p>';return;}
  if(!j.threads.length){ box.innerHTML='<p class="muted">Пока нет переписок. Откройте профиль игрока или список зрителей эфира и нажмите «написать».</p>'; return; }
  box.innerHTML=''; j.threads.forEach(function(t){ var d=document.createElement('div'); d.className='dm-thread'+(WITH&&t.with.toLowerCase()===WITH.toLowerCase()?' active':'');
    d.innerHTML='<span class="vav">'+esc((t.withUser||'?').slice(0,1).toUpperCase())+'</span><span style="flex:1;min-width:0"><b>'+esc(t.withUser)+'</b><br><span class="muted dm-last">'+esc(t.last)+'</span></span>'+(t.unread?('<span class="vbadge">'+t.unread+'</span>'):'');
    d.onclick=function(){ WITH=t.with; openConv(); loadThreads(); }; box.appendChild(d); }); }); }

var convTimer=null;
function openConv(){ if(!WITH)return; document.getElementById('conv-foot').style.display='flex';
  node('/dm/thread',{token:TOK,with:WITH}).then(function(j){ if(!j.ok){ document.getElementById('conv-head').textContent=j.error||'Ошибка'; return; }
    document.getElementById('conv-head').innerHTML='Диалог с <b>'+esc(j.withUser)+'</b>';
    var body=document.getElementById('conv-body'); body.innerHTML='';
    j.msgs.forEach(function(m){ var mine=m.from.toLowerCase()===ME.toLowerCase(); var d=document.createElement('div');
      d.className='dm-msg '+(mine?'me':'them'); d.innerHTML=esc(m.text)+'<span class="dm-time">'+when(m.at)+'</span>'; body.appendChild(d); });
    body.scrollTop=body.scrollHeight; });
  if(convTimer)clearTimeout(convTimer); convTimer=setTimeout(openConv,3000);
}
document.getElementById('dm-send').onclick=function(){ var inp=document.getElementById('dm-in'); var t=inp.value.trim(); if(!t||!WITH)return;
  node('/dm/send',{token:TOK,to:WITH,text:t}).then(function(j){ if(j.ok){ inp.value=''; openConv(); loadThreads(); } else alert(j.error||'Ошибка'); }); };
document.getElementById('dm-in').addEventListener('keydown',function(e){ if(e.key==='Enter')document.getElementById('dm-send').onclick(); });

loadThreads(); if(WITH)openConv();
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
