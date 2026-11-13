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
  <!-- шапка канала: обложка + аватар + @username -->
  <div class="ch-banner"<?= !empty($info['cover']) ? ' style="background-image:url(\''.h($info['cover']).'\')"' : '' ?>></div>
  <div class="ch-head">
    <div class="ch-ava"<?= !empty($info['avatar']) ? ' style="background-image:url(\''.h($info['avatar']).'\');background-size:cover"' : '' ?>><?= empty($info['avatar']) ? h(mb_substr($info['name'] ?? '?', 0, 1)) : '' ?></div>
    <div class="ch-meta">
      <h1 style="margin:0"># <?= h($info['name'] ?? '') ?></h1>
      <?php if (!empty($info['username'])): ?><div class="muted">@<?= h($info['username']) ?></div><?php endif; ?>
      <?php if (!empty($info['desc'])): ?><p class="muted" style="margin:4px 0 0"><?= h($info['desc']) ?></p><?php endif; ?>
    </div>
    <?php if (!empty($info['isOwner'])): ?>
    <div class="ch-actions">
      <button class="btn ghost" type="button" onclick="document.getElementById('ch-edit').style.display=document.getElementById('ch-edit').style.display==='none'?'block':'none'">✎ Оформление</button>
      <form method="post" onsubmit="return confirm('Удалить канал и весь чат?')" style="display:inline"><input type="hidden" name="act" value="del"><button class="btn ghost" type="submit">Удалить</button></form>
    </div>
    <?php endif; ?>
  </div>
  <?php if (!empty($info['isOwner'])): ?>
  <div class="panel" id="ch-edit" style="display:none;margin-bottom:12px">
    <h3 style="margin-top:0">Оформление канала</h3>
    <label class="fl">Название</label><input id="e-name" class="fi" maxlength="60" value="<?= h($info['name'] ?? '') ?>">
    <label class="fl" style="margin-top:8px">@username (англ., уникальный)</label><input id="e-user" class="fi" maxlength="24" value="<?= h($info['username'] ?? '') ?>">
    <label class="fl" style="margin-top:8px">Описание</label><textarea id="e-desc" class="fi" rows="2" maxlength="300"><?= h($info['desc'] ?? '') ?></textarea>
    <div class="row-form" style="margin-top:8px">
      <label class="ck">Аватар <input type="file" id="e-ava" accept="image/*"></label>
      <label class="ck">Обложка <input type="file" id="e-cov" accept="image/*"></label>
    </div>
    <button class="btn" id="e-save" style="margin-top:10px">Сохранить</button>
    <span class="muted" id="e-msg"></span>
  </div>
  <?php endif; ?>
  <!-- разделы канала -->
  <div class="tabs" id="chtabs">
    <button class="tab active" type="button" data-t="home">🏠 Главная</button>
    <button class="tab" type="button" data-t="videos">🎬 Видео</button>
    <button class="tab" type="button" data-t="recs">⏺ Записи</button>
    <button class="tab" type="button" data-t="shorts">⚡ Shorts</button>
    <button class="tab" type="button" data-t="photos">🖼 Фото</button>
    <button class="tab" type="button" data-t="chat">💬 Чат</button>
  </div>

  <div class="tab-pane active" data-pane="home"><div id="ch-home"><p class="muted">Загрузка…</p></div></div>
  <div class="tab-pane" data-pane="videos"><div id="ch-videos" class="mgrid"></div></div>
  <div class="tab-pane" data-pane="recs"><div id="ch-recs" class="mgrid"></div></div>
  <div class="tab-pane" data-pane="shorts"><div id="ch-shorts" class="mgrid shorts"></div></div>
  <div class="tab-pane" data-pane="photos"><div id="ch-photos" class="mgrid"></div></div>

  <div class="tab-pane" data-pane="chat">
  <?php if (!empty($info['canMod'])): ?>
  <div class="row-form" style="margin-bottom:8px">
    <input id="muser" placeholder="Ник для модерации" maxlength="16" style="flex:1">
    <button class="btn ghost" type="button" onclick="cmod('mute')">Мьют 5м</button>
    <button class="btn ghost" type="button" onclick="cmod('ban')">Бан (не зайдёт)</button>
    <button class="btn ghost" type="button" onclick="cmod('unban')">Разбан</button>
  </div>
  <?php endif; ?>
  <?php if (!empty($info['isOwner'])): ?>
  <div class="row-form" style="margin-bottom:8px">
    <input id="moduser" placeholder="Ник модератора" maxlength="16" style="flex:1">
    <button class="btn ghost" type="button" onclick="setmod(false)">Назначить мод.</button>
    <button class="btn ghost" type="button" onclick="setmod(true)">Снять мод.</button>
    <span class="muted" id="modlist"><?= !empty($info['mods']) ? 'модераторы: ' . h(implode(', ', $info['mods'])) : '' ?></span>
  </div>
  <?php endif; ?>
  <div id="chat" class="lchat" style="height:420px"></div>
  <?php if (me()): ?>
  <div class="row-form" style="margin-top:8px"><input id="ct" placeholder="Сообщение в канал" maxlength="600" style="flex:1"><button class="btn" id="cs">Отправить</button></div>
  <?php else: ?><p class="muted"><a href="/login/">Войдите</a>, чтобы писать.</p><?php endif; ?>
  <p class="muted" style="margin-top:10px">ID канала для ботов: <code>channel:<?= h($id) ?></code></p>
  </div><!-- /chat pane -->
</div>
<script>
var ID=<?= json_encode($id) ?>, TOK=<?= json_encode(me() ? token() : '') ?>, since=0;
var OWNER=<?= json_encode($info['owner'] ?? '') ?>;
function cmod(action){ var u=document.getElementById('muser').value.trim(); if(!u)return;
  fetch('/channel/mod',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({token:TOK,id:ID,action:action,user:u})}).then(function(r){return r.json();}).then(function(j){ alert(j.ok?j.result:(j.error||'Ошибка')); if(j.ok)document.getElementById('muser').value=''; }); }
function setmod(remove){ var u=document.getElementById('moduser').value.trim(); if(!u)return;
  fetch('/channel/mods',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({token:TOK,id:ID,user:u,remove:remove})}).then(function(r){return r.json();}).then(function(j){ if(j.ok){document.getElementById('modlist').textContent=(j.mods&&j.mods.length?'модераторы: '+j.mods.join(', '):'');document.getElementById('moduser').value='';}else alert(j.error||'Ошибка'); }); }
function esc(s){var d=document.createElement('div');d.textContent=s;return d.innerHTML;}
function fmtMsg(t){ t=String(t==null?'':t); var out='',last=0,re=/```([a-zA-Z0-9+#]*)\n?([\s\S]*?)```/g,m;
  while((m=re.exec(t))){ out+=esc(t.slice(last,m.index)); var lang=(m[1]||'код'), code=m[2].replace(/\n$/,'');
    out+='<div class="codecard"><div class="cc-head"><span>'+esc(lang)+'</span><button class="cc-copy" type="button" onclick="copyCode(this)">копировать</button></div><pre class="cc-body">'+esc(code)+'</pre></div>';
    last=re.lastIndex; } out+=esc(t.slice(last)); return out; }
function copyCode(btn){ try{ var card=btn.closest('.codecard'); var code=card.querySelector('.cc-body').textContent;
  navigator.clipboard.writeText(code).then(function(){ btn.textContent='скопировано ✓'; setTimeout(function(){btn.textContent='копировать';},1500); }); }catch(e){} }
function node(p,d){return fetch(p,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(d)}).then(function(r){return r.json();});}
// оформление канала (владелец)
(function(){ var save=document.getElementById('e-save'); if(!save)return;
  function readImg(el,cb){ var f=el&&el.files&&el.files[0]; if(!f){cb(undefined);return;} if(f.size>1100000){alert('Картинка до 1 МБ');cb(undefined);return;} var r=new FileReader(); r.onload=function(){cb(r.result);}; r.readAsDataURL(f); }
  save.onclick=function(){ var msg=document.getElementById('e-msg'); msg.textContent='Сохраняю…';
    readImg(document.getElementById('e-ava'),function(av){ readImg(document.getElementById('e-cov'),function(cv){
      var body={token:TOK,id:ID,name:document.getElementById('e-name').value.trim(),username:document.getElementById('e-user').value.trim(),desc:document.getElementById('e-desc').value};
      if(av!==undefined)body.avatar=av; if(cv!==undefined)body.cover=cv;
      node('/channel/update',body).then(function(j){ if(j.ok){ msg.textContent='Готово!'; location.reload(); } else msg.textContent=j.error||'Ошибка'; });
    }); }); };
})();
// --- разделы канала ---
Array.prototype.forEach.call(document.querySelectorAll('#chtabs .tab'),function(t){t.addEventListener('click',function(){
  var name=this.getAttribute('data-t');
  Array.prototype.forEach.call(document.querySelectorAll('#chtabs .tab'),function(x){x.classList.toggle('active',x===t);});
  Array.prototype.forEach.call(document.querySelectorAll('.tab-pane'),function(p){p.classList.toggle('active',p.getAttribute('data-pane')===name);});
});});
function fmtDur(s){s=Math.round(s);var m=Math.floor(s/60),x=s%60;return m+':'+(x<10?'0':'')+x;}
function vthumb(v){ return v.thumb?('style="background-image:url(\''+String(v.thumb).replace(/'/g,'')+'\');background-size:cover;background-position:center"'):''; }
function vcard(v){ return '<a class="mcard" href="/watch/'+encodeURIComponent(v.id)+'/"><div class="mthumb vid" '+vthumb(v)+'>'+(v.dur?'<span class="dur">'+fmtDur(v.dur)+'</span>':'')+'</div><div class="mmeta"><div class="mtitle">'+esc(v.title||'')+'</div><div class="muted">'+(v.views||0)+' просмотров</div></div></a>'; }
function pcard(p){ return '<a class="mcard" href="/media/photo.php?p='+encodeURIComponent(p.id)+'"><div class="mthumb" style="background-image:url(\'/photo/img/'+encodeURIComponent(p.id)+'\');background-size:cover;background-position:center"></div><div class="mmeta"><div class="mtitle">'+esc(p.title||'Фото')+'</div></div></a>'; }
var VIDS=[],PHOTOS=[];
function isRec(v){return (v.tags||[]).indexOf('стрим')>=0;}
function isShort(v){return !isRec(v)&&((v.tags||[]).indexOf('shorts')>=0||(v.dur>0&&v.dur<=60));}
function fill(){
  var vids=VIDS.filter(function(v){return !isRec(v)&&!isShort(v);}), recs=VIDS.filter(isRec), shorts=VIDS.filter(isShort);
  document.getElementById('ch-videos').innerHTML=vids.length?vids.map(vcard).join(''):'<p class="muted">Нет видео.</p>';
  document.getElementById('ch-recs').innerHTML=recs.length?recs.map(vcard).join(''):'<p class="muted">Нет записей эфиров.</p>';
  document.getElementById('ch-shorts').innerHTML=shorts.length?shorts.map(vcard).join(''):'<p class="muted">Нет shorts.</p>';
  document.getElementById('ch-photos').innerHTML=PHOTOS.length?PHOTOS.map(pcard).join(''):'<p class="muted">Нет фото.</p>';
  function sect(title,arr,render){ return arr.length?('<h3>'+title+'</h3><div class="mgrid">'+arr.slice(0,8).map(render).join('')+'</div>'):''; }
  var liveBlock='';
  if(LIVE){
    var cov=LIVE.cover?('data:image/jpeg;base64,'+LIVE.cover):'';
    liveBlock='<h3>🔴 В эфире сейчас</h3><a class="mcard livecard" href="'+esc(LIVE.link||('/live/watch.php?s='+LIVE.id))+'">'+
      '<div class="mthumb vid" style="'+(cov?('background-image:url(\''+cov+'\');background-size:cover;background-position:center'):'')+'">'+
      '<span class="livebadge">● В ЭФИРЕ</span><span class="dur">👁 '+(LIVE.viewers||0)+'</span></div>'+
      '<div class="mmeta"><div class="mtitle">'+esc(LIVE.title||'Прямой эфир')+'</div><div class="muted">смотреть трансляцию</div></div></a>';
  }
  var home=liveBlock+sect('Видео',vids,vcard)+sect('Записи эфиров',recs,vcard)+sect('Shorts',shorts,vcard)+sect('Фото',PHOTOS,pcard);
  document.getElementById('ch-home').innerHTML=home||'<p class="muted">На канале пока пусто.</p>';
}
var LIVE=null;
if(OWNER){
  fetch('/video/list?channel='+encodeURIComponent(OWNER)).then(function(r){return r.json();}).then(function(j){ VIDS=(j.videos||[]); fill(); }).catch(function(){});
  fetch('/photo/list?nick='+encodeURIComponent(OWNER)).then(function(r){return r.json();}).then(function(j){ PHOTOS=(j.photos||[]); fill(); }).catch(function(){});
  fetch('/stream/list').then(function(r){return r.json();}).then(function(j){
    var ow=OWNER.toLowerCase();
    LIVE=(j.streams||[]).filter(function(s){ return (s.channel||'').toLowerCase()===ow || (s.user||'').toLowerCase()===ow; })[0]||null;
    fill();
  }).catch(function(){});
}

function poll(){ node('/channel/chat/get',{id:ID,since:since}).then(function(j){
  if(j.ok&&j.msgs&&j.msgs.length){ var box=document.getElementById('chat');
    j.msgs.forEach(function(m){var d=document.createElement('div');d.className='lmsg'+(m.bot?' bot':'');d.innerHTML='<b>'+esc(m.from)+':</b> '+fmtMsg(m.text);box.appendChild(d);});
    since=j.total; box.scrollTop=box.scrollHeight; }
}).catch(function(){}).finally(function(){setTimeout(poll,1500);}); }
poll();
var cs=document.getElementById('cs');
cs&&cs.addEventListener('click',function(){var t=document.getElementById('ct').value.trim();if(!t)return;
  node('/channel/chat/send',{token:TOK,id:ID,text:t}).then(function(j){if(j.ok)document.getElementById('ct').value='';});});
document.getElementById('ct')&&document.getElementById('ct').addEventListener('keydown',function(e){if(e.key==='Enter')cs.click();});
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
