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
</div>
<script>
var ID=<?= json_encode($id) ?>, TOK=<?= json_encode(me() ? token() : '') ?>, since=0;
function cmod(action){ var u=document.getElementById('muser').value.trim(); if(!u)return;
  fetch('/channel/mod',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({token:TOK,id:ID,action:action,user:u})}).then(function(r){return r.json();}).then(function(j){ alert(j.ok?j.result:(j.error||'Ошибка')); if(j.ok)document.getElementById('muser').value=''; }); }
function setmod(remove){ var u=document.getElementById('moduser').value.trim(); if(!u)return;
  fetch('/channel/mods',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({token:TOK,id:ID,user:u,remove:remove})}).then(function(r){return r.json();}).then(function(j){ if(j.ok){document.getElementById('modlist').textContent=(j.mods&&j.mods.length?'модераторы: '+j.mods.join(', '):'');document.getElementById('moduser').value='';}else alert(j.error||'Ошибка'); }); }
function esc(s){var d=document.createElement('div');d.textContent=s;return d.innerHTML;}
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
