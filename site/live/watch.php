<?php
require_once __DIR__ . '/../_inc/config.php';
require_login();                 // смотреть эфиры можно только из аккаунта
$id = $_GET['s'] ?? '';
$PAGE_TITLE = 'Эфир — NareLauncher';
require __DIR__ . '/../_inc/head.php';
?>
<div class="watch" id="watchbox">
  <h1 style="margin:0 0 2px" id="title">Эфир</h1>
  <div class="muted" style="margin-bottom:12px"><span id="user"></span> · <span id="vw">0</span> смотрят · <span id="cat"></span> · <span id="st" class="badge">подключение…</span></div>

  <div class="tabs" id="tabs">
    <button class="tab active" type="button" data-t="live">📺 Эфир</button>
    <button class="tab" type="button" data-t="chat">💬 Чат</button>
    <button class="tab" type="button" data-t="viewers">👥 Зрители <span id="vcount" class="vbadge">0</span></button>
    <button class="tab" type="button" data-t="about">ℹ️ Об эфире</button>
  </div>

  <!-- вкладка: эфир -->
  <div class="tab-pane active" data-pane="live">
    <div class="rc-screen-wrap" id="wrap">
      <video id="vid" autoplay muted playsinline style="display:none"></video>
      <img id="scr" alt="эфир" style="display:none">
      <div id="off" class="rc-off">Подключение…</div>
    </div>
    <div class="btn-row" style="margin-top:8px">
      <button class="btn" id="sound">🔊 Включить звук</button>
      <button class="btn ghost" id="size">⤢ Размер: обычный</button>
    </div>
    <div class="btn-row" style="margin-top:6px" id="reacts">
      <?php foreach (['❤️','🔥','😂','👏','😮','🎉'] as $e): ?><button class="btn ghost react" type="button" data-e="<?= $e ?>" style="font-size:18px;padding:6px 12px"><?= $e ?></button><?php endforeach; ?>
    </div>
  </div>

  <!-- вкладка: чат -->
  <div class="tab-pane" data-pane="chat">
    <div class="row-form" id="modrow" style="display:none;margin-bottom:8px">
      <input id="muser" placeholder="Ник для модерации" maxlength="16" style="flex:1">
      <button class="btn ghost" type="button" onclick="smod('mute')">Мьют 5м</button>
      <button class="btn ghost" type="button" onclick="smod('ban')">Бан</button>
      <button class="btn ghost" type="button" onclick="smod('unban')">Разбан</button>
    </div>
    <div id="chat" class="lchat"></div>
    <div class="row-form" style="margin-top:8px"><input id="ct" placeholder="Сообщение" maxlength="400" style="flex:1"><button class="btn" id="cs">→</button></div>
  </div>

  <!-- вкладка: зрители -->
  <div class="tab-pane" data-pane="viewers">
    <p class="muted" style="margin-top:0">Кто сейчас смотрит эфир. Нажми «написать», чтобы отправить личное сообщение.</p>
    <div id="viewers" class="vlist"></div>
  </div>

  <!-- вкладка: об эфире -->
  <div class="tab-pane" data-pane="about">
    <div class="about-grid">
      <div><span class="muted">Стример</span><div id="aUser">—</div></div>
      <div><span class="muted">Категория</span><div id="aCat">—</div></div>
      <div><span class="muted">Зрителей</span><div id="aVw">0</div></div>
      <div><span class="muted">Статус</span><div id="aSt">—</div></div>
    </div>
    <div class="btn-row" style="margin-top:12px">
      <button class="btn ghost" id="share">🔗 Поделиться</button>
      <button class="btn ghost" id="follow" style="display:none">+ Подписаться</button>
    </div>
  </div>
</div>

<!-- страница ошибки в стиле github 404 -->
<div id="notfound" class="gh404" style="display:none">
  <div class="gh404-code">1.456</div>
  <div class="gh404-big">СТРИМ НЕ НАЙДЕН</div>
  <p class="gh404-sub">Эфир с таким адресом не найден — возможно, он никогда не запускался, уже завершён или ссылка устарела.</p>
  <div class="btn-row" style="justify-content:center">
    <a class="btn" href="/live/">Все эфиры</a>
    <a class="btn ghost" href="/home/">На главную</a>
  </div>
</div>

<script>
var ID=<?= json_encode($id) ?>, TOK=<?= json_encode(token()) ?>, ME=<?= json_encode(me() ?: '') ?>;
function node(p,d){return fetch(p,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(d)}).then(function(r){return r.json();});}
function esc(s){var d=document.createElement('div');d.textContent=s;return d.innerHTML;}

// вкладки
Array.prototype.forEach.call(document.querySelectorAll('#tabs .tab'),function(t){ t.addEventListener('click',function(){
  var name=this.getAttribute('data-t');
  Array.prototype.forEach.call(document.querySelectorAll('#tabs .tab'),function(x){x.classList.toggle('active',x===t);});
  Array.prototype.forEach.call(document.querySelectorAll('.tab-pane'),function(p){p.classList.toggle('active',p.getAttribute('data-pane')===name);});
}); });

function smod(action){ var u=document.getElementById('muser').value.trim(); if(!u)return;
  node('/stream/mod',{token:TOK,id:ID,action:action,user:u}).then(function(j){ alert(j.ok?j.result:(j.error||'Ошибка')); if(j.ok)document.getElementById('muser').value=''; }); }

// ---------- МЕТА + статус + зрители + запасной кадр ----------
var vid=document.getElementById('vid'), scr=document.getElementById('scr'), streamerNick=null;
function show404(){ document.getElementById('watchbox').style.display='none'; document.getElementById('notfound').style.display='flex'; }
function meta(){ node('/stream/frame',{token:TOK,id:ID}).then(function(j){
  if(j.needLogin){ location.href='/login/'; return; }
  if(j.found===false){ show404(); return; }
  var off=document.getElementById('off');
  var stTxt=j.online?'в эфире':(j.lost?'связь потеряна':'не в сети');
  document.getElementById('st').textContent=stTxt; document.getElementById('st').className='badge '+(j.online?'on':'');
  if(j.title)document.getElementById('title').textContent=j.title;
  if(j.user)document.getElementById('user').textContent=j.user;
  document.getElementById('vw').textContent=j.viewers||0;
  var catTxt=(j.subcategory||j.category||''); document.getElementById('cat').textContent=catTxt;
  document.getElementById('aUser').textContent=j.user||'—';
  document.getElementById('aCat').textContent=catTxt||'—';
  document.getElementById('aVw').textContent=j.viewers||0;
  document.getElementById('aSt').textContent=stTxt;
  document.getElementById('vcount').textContent=j.viewers||0;
  if(j.nick)offerFollow(j.nick);
  if(j.nick&&ME&&j.nick.toLowerCase()===ME.toLowerCase()){ document.getElementById('modrow').style.display='flex'; }
  renderViewers(j.viewerList||[]);
  // режим: настоящее видео или запасной кадр
  if(j.hasVideo){ vid.style.display='block'; scr.style.display='none'; }
  else { vid.style.display='none'; scr.style.display='block'; if(j.jpg)scr.src='data:image/jpeg;base64,'+j.jpg; }
  // наложение статуса
  var offText=j.online?'':(j.lost?'Пропало соединение у стримера. Ждём, когда он вернётся…':'Стрим не запущен. Ожидайте, пока вернётся тот, кто его запустил.');
  off.textContent=offText; off.style.display=j.online?'none':'grid';
}).catch(function(){}).finally(function(){ setTimeout(meta,1500); }); }
meta();

// список зрителей + кнопка «написать»
function renderViewers(list){ var box=document.getElementById('viewers'); if(!box)return;
  if(!list.length){ box.innerHTML='<p class="muted">Пока никто не смотрит.</p>'; return; }
  box.innerHTML=''; list.forEach(function(v){ var row=document.createElement('div'); row.className='vrow';
    var me=ME&&v.nick.toLowerCase()===ME.toLowerCase();
    row.innerHTML='<span class="vav">'+esc((v.user||'?').slice(0,1).toUpperCase())+'</span><span class="vname">'+esc(v.user||v.nick)+(me?' <span class="muted">(вы)</span>':'')+'</span>';
    if(!me){ var b=document.createElement('a'); b.className='btn ghost'; b.style.padding='4px 10px'; b.textContent='✉ написать'; b.href='/messages/?with='+encodeURIComponent(v.nick); row.appendChild(b); }
    box.appendChild(row); }); }

// ---------- НАСТОЯЩЕЕ ВИДЕО через MSE ----------
var MIME='video/webm;codecs="vp8,opus"', mseOk=('MediaSource'in window)&&window.MediaSource.isTypeSupported&&MediaSource.isTypeSupported(MIME);
var media=null, sb=null, queue=[], epoch=-1, since=0, opening=false;
function b64buf(b64){ var bin=atob(b64),n=bin.length,u=new Uint8Array(n); for(var i=0;i<n;i++)u[i]=bin.charCodeAt(i); return u; }
function pump(){ if(!sb||sb.updating||!queue.length)return; try{ sb.appendBuffer(queue.shift()); }catch(e){ if(e&&e.name==='QuotaExceededError'){ try{ if(sb.buffered.length)sb.remove(sb.buffered.start(0),Math.max(sb.buffered.start(0),vid.currentTime-10)); }catch(_){}}}}
function resetMse(){ try{ if(vid.src)URL.revokeObjectURL(vid.src); }catch(e){} media=null;sb=null;queue=[];since=0;opening=false; }
function startMse(){ if(opening||media)return; opening=true; media=new MediaSource(); vid.src=URL.createObjectURL(media);
  media.addEventListener('sourceopen',function(){ opening=false; try{ sb=media.addSourceBuffer(MIME); sb.mode='sequence';
    sb.addEventListener('updateend',function(){ pump();
      try{ if(sb.buffered.length){ var s0=sb.buffered.start(0),e0=sb.buffered.end(sb.buffered.length-1); if(e0-s0>30&&!sb.updating)sb.remove(s0,e0-20); } }catch(e){} });
    pump(); }catch(e){ mseOk=false; } }); }
function catchup(){ try{ if(sb&&sb.buffered.length){ var end=sb.buffered.end(sb.buffered.length-1); if(end-vid.currentTime>5)vid.currentTime=end-0.4; } }catch(e){} }
setInterval(catchup,2500);
function videoLoop(){ if(!mseOk){ return; }
  node('/stream/media/get',{token:TOK,id:ID,since:since}).then(function(j){
    if(j.needLogin){ location.href='/login/'; return; }
    if(j.found===false||!j.hasVideo){ return; }      // запасной кадр покажет meta()
    if(j.epoch!==epoch){ epoch=j.epoch; resetMse(); startMse(); if(j.init)queue.push(b64buf(j.init)); }
    else if(!media){ startMse(); }
    (j.chunks||[]).forEach(function(c){ queue.push(b64buf(c.chunk)); if(c.seq>since)since=c.seq; });
    pump();
  }).catch(function(){}).finally(function(){ setTimeout(videoLoop, 900); }); }
videoLoop();

// ---------- звук ----------
document.getElementById('sound').addEventListener('click',function(){ vid.muted=!vid.muted; this.textContent=vid.muted?'🔊 Включить звук':'🔇 Выключить звук'; if(!vid.muted)vid.play().catch(function(){}); });

// ---------- чат ----------
var since2=0;
function chat(){ node('/stream/chat/get',{id:ID,since:since2}).then(function(j){
  if(j.ok&&j.msgs&&j.msgs.length){ var box=document.getElementById('chat');
    j.msgs.forEach(function(m){ var d=document.createElement('div'); d.className='lmsg'+(m.bot?' bot':''); d.innerHTML='<b>'+esc(m.from)+':</b> '+esc(m.text); box.appendChild(d); });
    since2=j.total; box.scrollTop=box.scrollHeight; }
}).catch(function(){}).finally(function(){setTimeout(chat,1500);}); }
chat();
var cs=document.getElementById('cs');
cs.addEventListener('click',function(){var t=document.getElementById('ct').value.trim();if(!t)return;
  node('/stream/chat/send',{token:TOK,id:ID,text:t}).then(function(j){if(j.ok)document.getElementById('ct').value='';});});
document.getElementById('ct').addEventListener('keydown',function(e){if(e.key==='Enter')cs.click();});

// ---------- размер ----------
var big=false;
document.getElementById('size').addEventListener('click',function(){ big=!big; var w=document.getElementById('wrap');
  w.style.maxWidth=big?'100%':''; this.textContent=big?'⤢ Размер: большой':'⤢ Размер: обычный';
  document.querySelector('.watch').style.maxWidth=big?'1200px':''; });

// ---------- поделиться ----------
document.getElementById('share').addEventListener('click',function(){ var u=location.href;
  if(navigator.share){navigator.share({title:document.getElementById('title').textContent,url:u}).catch(function(){});}
  else{navigator.clipboard.writeText(u);this.textContent='Скопировано ✓';} });

// ---------- реакции ----------
Array.prototype.forEach.call(document.querySelectorAll('.react'),function(b){ b.addEventListener('click',function(){
  node('/stream/chat/send',{token:TOK,id:ID,text:this.getAttribute('data-e')}); }); });

// ---------- подписка ----------
var followBtn=document.getElementById('follow');
function offerFollow(nick){ if(!nick||streamerNick)return; streamerNick=nick; if(ME&&nick.toLowerCase()===ME.toLowerCase())return; followBtn.style.display='inline-block';
  node('/subscribed',{token:TOK}).then(function(j){ if(j.ok){ var f=(j.channels||[]).some(function(c){return c.nick.toLowerCase()===nick.toLowerCase();}); followBtn.textContent=f?'Вы подписаны ✓':'+ Подписаться'; } }); }
followBtn.addEventListener('click',function(){ node('/subscribe',{token:TOK,nick:streamerNick}).then(function(j){ if(j.ok)followBtn.textContent=j.following?'Вы подписаны ✓':'+ Подписаться'; }); });
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
