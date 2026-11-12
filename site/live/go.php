<?php
require_once __DIR__ . '/../_inc/config.php';
require_login();
$PAGE_TITLE = 'Начать трансляцию — NareLauncher';
require __DIR__ . '/../_inc/head.php';
?>
<div class="watch">
  <h1>Прямой эфир</h1>
  <div id="setup" class="panel" style="max-width:520px">
    <label style="display:block;color:var(--dim);font-size:13px;margin-bottom:4px">Название эфира</label>
    <input id="title" maxlength="120" placeholder="Мой стрим" style="width:100%;background:#0a0d14;border:1px solid var(--border);color:var(--text);border-radius:9px;padding:11px 12px">
    <label style="display:block;color:var(--dim);font-size:13px;margin:10px 0 4px">Категория</label>
    <select id="cat" style="width:100%;background:#0a0d14;border:1px solid var(--border);color:var(--text);border-radius:9px;padding:11px 12px">
      <option value="games">Игры</option><option value="just-chatting">Разговоры</option>
      <option value="music">Музыка</option><option value="creative">Творчество</option><option value="stream">Другое</option>
    </select>
    <div class="btn-row" style="margin-top:16px">
      <button class="btn" id="screen">🖥 Экран / окно / приложение</button>
      <button class="btn ghost" id="camera">🎥 Камера</button>
    </div>
    <div class="msg" id="st" style="display:none"></div>
    <p class="muted" style="margin-top:12px">«Экран / окно / приложение» — браузер спросит, что показывать (весь экран, окно или вкладку).</p>
    <hr style="border:0;border-top:1px solid var(--border);margin:16px 0">
    <p class="muted">Нужен полноценный режим как в OBS (захват приложения, без браузера)? Транслируйте через приложение:</p>
    <a class="btn ghost" href="narelauncher://stream">Открыть студию в NareLauncher</a>
    <p class="muted" style="font-size:13px;margin-top:6px">Работает, если установлен NareLauncher.</p>
  </div>
  <div id="live" style="display:none">
    <video id="cam" autoplay muted playsinline style="width:100%;max-width:720px;border-radius:12px;background:#000"></video>
    <canvas id="cv" style="display:none"></canvas>
    <div class="btn-row" style="margin-top:12px">
      <span class="badge on">● В ЭФИРЕ</span><span class="muted" id="vw"></span>
      <button class="btn ghost" id="stop">Завершить</button>
      <a class="btn ghost" id="watch" target="_blank">Страница эфира</a>
      <button class="btn ghost" id="copy">Скопировать ссылку</button>
    </div>
  </div>
</div>
<script>
var TOK=<?= json_encode(token()) ?>, SID=null, timer=null, stream=null, LINK='';
function node(p,d){return fetch(p,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(d)}).then(function(r){return r.json();});}
function msg(t){var e=document.getElementById('st');e.style.display='block';e.textContent=t;}
function begin(getMedia){
  var title=document.getElementById('title').value.trim()||'Эфир';
  var cat=document.getElementById('cat').value;
  getMedia().then(function(ms){
    stream=ms; document.getElementById('cam').srcObject=ms;
    ms.getVideoTracks()[0].addEventListener('ended', stop);   // пользователь нажал «прекратить доступ»
    return node('/stream/start',{token:TOK,title:title,category:cat});
  }).then(function(j){
    if(!j.ok){msg(j.error||'Не удалось начать');return;}
    SID=j.id; LINK=location.origin+(j.link||('/live/watch.php?s='+j.id));
    document.getElementById('setup').style.display='none';
    document.getElementById('live').style.display='block';
    document.getElementById('watch').href=j.link||('/live/watch.php?s='+j.id);
    var cv=document.getElementById('cv'), cam=document.getElementById('cam');
    timer=setInterval(function(){
      if(!cam.videoWidth)return;
      var w=Math.min(1280,cam.videoWidth); cv.width=w; cv.height=Math.round(w*cam.videoHeight/cam.videoWidth);
      cv.getContext('2d').drawImage(cam,0,0,cv.width,cv.height);
      node('/stream/push',{token:TOK,id:SID,jpg:cv.toDataURL('image/jpeg',0.5).split(',')[1]});
    },800);
    pollv();
  }).catch(function(e){msg('Не удалось: '+e.message);});
}
function pollv(){ if(!SID)return; node('/stream/frame',{id:SID}).then(function(j){ if(j.ok)document.getElementById('vw').textContent=' · '+(j.viewers||0)+' смотрят'; }).finally(function(){setTimeout(pollv,4000);}); }
document.getElementById('screen').onclick=function(){ begin(function(){return navigator.mediaDevices.getDisplayMedia({video:{frameRate:15},audio:false});}); };
document.getElementById('camera').onclick=function(){ begin(function(){return navigator.mediaDevices.getUserMedia({video:{width:1280,height:720},audio:false});}); };
function stop(){ if(timer){clearInterval(timer);timer=null;} if(SID)node('/stream/stop',{token:TOK,id:SID}); if(stream)stream.getTracks().forEach(function(t){t.stop();}); location.href='/live/'; }
document.getElementById('stop').onclick=stop;
document.getElementById('copy').onclick=function(){ navigator.clipboard.writeText(LINK); this.textContent='Скопировано ✓'; };
window.addEventListener('beforeunload',function(){ if(SID)node('/stream/stop',{token:TOK,id:SID}); });
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
