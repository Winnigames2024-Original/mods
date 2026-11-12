<?php
require_once __DIR__ . '/../_inc/config.php';
require_login();
$PAGE_TITLE = 'Начать трансляцию — NareLauncher';
require __DIR__ . '/../_inc/head.php';
?>
<div class="watch">
  <h1>Прямой эфир</h1>
  <div id="setup" class="panel" style="max-width:480px">
    <label style="display:block;color:var(--dim);font-size:13px;margin-bottom:4px">Название эфира</label>
    <input id="title" maxlength="120" placeholder="Мой стрим" style="width:100%;background:#0a0d14;border:1px solid var(--border);color:var(--text);border-radius:9px;padding:11px 12px">
    <button class="btn" id="start" style="margin-top:14px">Выйти в эфир (с камеры)</button>
    <div class="msg" id="st" style="display:none"></div>
    <p class="muted" style="margin-top:10px">Транслируется изображение с вашей камеры. Разрешите доступ к камере в браузере.</p>
  </div>
  <div id="live" style="display:none">
    <video id="cam" autoplay muted playsinline style="width:100%;max-width:640px;border-radius:12px;background:#000"></video>
    <canvas id="cv" style="display:none"></canvas>
    <div class="btn-row" style="margin-top:12px">
      <span class="badge on" id="ind">● В ЭФИРЕ</span>
      <button class="btn ghost" id="stop">Завершить</button>
      <a class="btn ghost" id="watch" target="_blank">Открыть страницу эфира</a>
    </div>
  </div>
</div>
<script>
var TOK=<?= json_encode(token()) ?>, SID=null, timer=null, stream=null;
function node(p,d){return fetch(p,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(d)}).then(function(r){return r.json();});}
function msg(t){var e=document.getElementById('st');e.style.display='block';e.textContent=t;}
document.getElementById('start').onclick=function(){
  var title=document.getElementById('title').value.trim()||'Эфир';
  navigator.mediaDevices.getUserMedia({video:{width:854,height:480},audio:false}).then(function(ms){
    stream=ms; document.getElementById('cam').srcObject=ms;
    return node('/stream/start',{token:TOK,title:title});
  }).then(function(j){
    if(!j.ok){msg(j.error||'Не удалось начать');return;}
    SID=j.id;
    document.getElementById('setup').style.display='none';
    document.getElementById('live').style.display='block';
    document.getElementById('watch').href='/live/watch.php?s='+SID;
    var cv=document.getElementById('cv'), cam=document.getElementById('cam');
    timer=setInterval(function(){
      if(!cam.videoWidth)return;
      cv.width=854; cv.height=Math.round(854*cam.videoHeight/cam.videoWidth);
      cv.getContext('2d').drawImage(cam,0,0,cv.width,cv.height);
      var data=cv.toDataURL('image/jpeg',0.5).split(',')[1];
      node('/stream/push',{token:TOK,id:SID,jpg:data});
    },800);
  }).catch(function(e){msg('Нет доступа к камере: '+e.message);});
};
function stop(){ if(timer)clearInterval(timer); if(SID)node('/stream/stop',{token:TOK,id:SID});
  if(stream)stream.getTracks().forEach(function(t){t.stop();}); location.href='/live/'; }
document.getElementById('stop').onclick=stop;
window.addEventListener('beforeunload',function(){ if(SID)navigator.sendBeacon&&node('/stream/stop',{token:TOK,id:SID}); });
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
