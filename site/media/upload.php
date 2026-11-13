<?php
require_once __DIR__ . '/../_inc/config.php';
require_login();
$PAGE_TITLE = 'Загрузка — NareLauncher';
require __DIR__ . '/../_inc/head.php';
?>
<div class="panel" style="max-width:560px">
  <h1>Загрузить</h1>
  <div class="pagetabs" style="margin-top:4px">
    <button class="on" id="tabv" type="button">Видео</button>
    <button id="tabp" type="button">Фото</button>
  </div>
  <label style="display:block;color:var(--dim);font-size:13px;margin:10px 0 4px">Название</label>
  <input id="title" maxlength="120" style="width:100%;background:#0a0d14;border:1px solid var(--border);color:var(--text);border-radius:9px;padding:11px 12px">
  <label style="display:block;color:var(--dim);font-size:13px;margin:10px 0 4px">Описание</label>
  <textarea id="desc" rows="3" style="width:100%;background:#0a0d14;border:1px solid var(--border);color:var(--text);border-radius:9px;padding:11px 12px"></textarea>
  <label style="display:block;color:var(--dim);font-size:13px;margin:10px 0 4px">Категория</label>
  <select id="cat" style="width:100%;background:#0a0d14;border:1px solid var(--border);color:var(--text);border-radius:9px;padding:11px 12px">
    <option value="games">Игры</option><option value="just-chatting">Разговоры</option>
    <option value="music">Музыка</option><option value="creative">Творчество</option><option value="">Без категории</option>
  </select>
  <label style="display:block;color:var(--dim);font-size:13px;margin:10px 0 4px">Подкатегория (например Minecraft)</label>
  <input id="sub" maxlength="40" placeholder="Minecraft" list="subs" style="width:100%;background:#0a0d14;border:1px solid var(--border);color:var(--text);border-radius:9px;padding:11px 12px">
  <datalist id="subs"><option value="Minecraft"><option value="CS2"><option value="GTA V"><option value="Dota 2"><option value="Fortnite"><option value="Roblox"></datalist>
  <label style="display:block;color:var(--dim);font-size:13px;margin:10px 0 4px">Теги (через пробел или запятую, с # или без)</label>
  <input id="tags" maxlength="200" placeholder="#minecraft обзор #топ" style="width:100%;background:#0a0d14;border:1px solid var(--border);color:var(--text);border-radius:9px;padding:11px 12px">
  <label class="ck" style="display:flex;align-items:center;gap:8px;margin-top:10px"><input type="checkbox" id="isShort"> ⚡ Это Shorts (короткое видео)</label>
  <label style="display:block;color:var(--dim);font-size:13px;margin:10px 0 4px">Файл</label>
  <input id="file" type="file" accept="video/*" style="width:100%">
  <button class="btn" id="go" style="margin-top:16px">Загрузить</button>
  <div class="msg" id="st" style="display:none"></div>
  <div style="height:8px;background:#0a0d14;border-radius:6px;margin-top:10px;overflow:hidden;display:none" id="pw"><div id="pb" style="height:100%;width:0;background:var(--accent)"></div></div>
</div>
<script>
var TOK=<?= json_encode(token()) ?>, kind='video';
var fileEl=document.getElementById('file'), st=document.getElementById('st');
function setKind(k){ kind=k; fileEl.setAttribute('accept', k==='video'?'video/*':'image/*');
  document.getElementById('tabv').classList.toggle('on',k==='video'); document.getElementById('tabp').classList.toggle('on',k==='photo'); }
document.getElementById('tabv').onclick=function(){setKind('video');};
document.getElementById('tabp').onclick=function(){setKind('photo');};
function msg(t,ok){ st.style.display='block'; st.className='msg'+(ok?' ok':''); st.textContent=t; }
document.getElementById('go').onclick=function(){
  var f=fileEl.files[0]; if(!f){msg('Выберите файл');return;}
  var title=document.getElementById('title').value.trim()||f.name;
  var desc=document.getElementById('desc').value;
  var createPath=kind==='video'?'/video/create':'/photo/create';
  var rawBase=kind==='video'?'/video/raw/':'/photo/raw/';
  msg('Создаю…');
  var cat=document.getElementById('cat').value, sub=document.getElementById('sub').value.trim();
  var tags=document.getElementById('tags')?document.getElementById('tags').value:'';
  var sh=document.getElementById('isShort'); if(sh&&sh.checked) tags='#shorts '+tags;
  fetch(createPath,{method:'POST',headers:{'Content-Type':'application/json'},
    body:JSON.stringify({token:TOK,title:title,desc:desc,mime:f.type,category:cat,subcategory:sub,tags:tags})}).then(function(r){return r.json();}).then(function(j){
    if(!j.ok){msg(j.error||'Ошибка');return;}
    var id=j.id; msg('Загружаю файл…');
    document.getElementById('pw').style.display='block';
    var xhr=new XMLHttpRequest();
    xhr.open('PUT', rawBase+id, true);
    xhr.setRequestHeader('X-Nare-Token', TOK);
    xhr.upload.onprogress=function(e){ if(e.lengthComputable) document.getElementById('pb').style.width=Math.round(e.loaded/e.total*100)+'%'; };
    xhr.onload=function(){ var ok=false; try{ok=JSON.parse(xhr.responseText).ok;}catch(e){}
      if(ok){ msg('Готово! Открываю…',true); location.href=(kind==='video'?'/media/watch.php?v=':'/media/photo.php?p=')+id; }
      else msg('Ошибка загрузки файла'); };
    xhr.onerror=function(){ msg('Сеть прервалась'); };
    xhr.send(f);
  }).catch(function(){msg('Ошибка сети');});
};
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
