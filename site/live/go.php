<?php
require_once __DIR__ . '/../_inc/config.php';
require_login();
$PAGE_TITLE = 'Начать трансляцию — NareLauncher';
require __DIR__ . '/../_inc/head.php';
?>
<div class="watch">
  <h1>Прямой эфир</h1>
  <div id="setup" class="panel" style="max-width:560px">
    <label class="fl">Название</label>
    <input id="title" maxlength="120" placeholder="Мой стрим" class="fi">
    <label class="fl" style="margin-top:10px">ID стрима (для ссылки ?s=…; пусто — сгенерируется сам)</label>
    <input id="sid" class="fi" maxlength="40" placeholder="напр. my-live-1 (англ., цифры, дефис)">
    <div class="row-form" style="margin-top:10px">
      <select id="cat" class="fi" style="flex:1">
        <option value="games">Игры</option><option value="just-chatting">Разговоры</option>
        <option value="music">Музыка</option><option value="creative">Творчество</option><option value="stream">Другое</option>
      </select>
      <input id="sub" class="fi" style="flex:1" maxlength="40" placeholder="Подкатегория (Minecraft)" list="subs">
      <datalist id="subs"><option value="Minecraft"><option value="CS2"><option value="GTA V"><option value="Dota 2"></datalist>
    </div>
    <label class="fl" style="margin-top:10px">Постоянное имя трансляции (англ., для ссылки)</label>
    <input id="name" class="fi" maxlength="40" placeholder="my-stream — ссылка будет всегда одна">
    <p class="muted" style="font-size:13px;margin-top:4px">Ссылка: narelauncher.winnigames2024.ru/live/&lt;категория&gt;/&lt;подкатегория&gt;/&lt;это-имя&gt;</p>
    <label class="fl" style="margin-top:10px">Качество</label>
    <select id="q" class="fi">
      <option value="720">720p · обычное</option>
      <option value="1080" selected>1080p · хорошее</option>
      <option value="1440">1440p · максимум (нагрузка выше)</option>
    </select>
    <div style="margin-top:12px;display:flex;gap:16px;flex-wrap:wrap">
      <label class="ck"><input type="checkbox" id="useScreen" checked> 🖥 Экран/окно</label>
      <label class="ck"><input type="checkbox" id="useCam"> 🎥 Камера</label>
      <label class="ck"><input type="checkbox" id="useMic"> 🎤 Микрофон</label>
    </div>
    <button class="btn" id="start" style="margin-top:16px">Выйти в эфир</button>
    <div class="msg" id="st" style="display:none"></div>
    <p class="muted" style="margin-top:10px">Идёт настоящим видео (webm) со звуком — как живой эфир, с небольшой задержкой в пару секунд. Это нормально.</p>
  </div>

  <div id="live" style="display:none">
    <div class="rc-screen-wrap"><canvas id="cv"></canvas></div>
    <video id="screenV" style="display:none" autoplay muted playsinline></video>
    <video id="camV" style="display:none" autoplay muted playsinline></video>
    <div class="btn-row" style="margin-top:12px">
      <span class="badge on">● В ЭФИРЕ</span><span class="muted" id="vw"></span>
      <button class="btn ghost" id="stop">Завершить</button>
      <a class="btn ghost" id="watch" target="_blank">Страница эфира</a>
      <button class="btn ghost" id="copy">Скопировать ссылку</button>
    </div>
    <div class="panel" style="margin-top:12px">
      <div class="btn-row">
        <button class="btn ghost" id="camToggle">🎥 Камера: выкл</button>
        <select id="camDev" class="fi" style="max-width:220px;display:none"></select>
        <span class="muted">позиция:</span>
        <button class="btn ghost campos" data-p="tl">↖</button>
        <button class="btn ghost campos" data-p="tr">↗</button>
        <button class="btn ghost campos" data-p="bl">↙</button>
        <button class="btn ghost campos" data-p="br">↘</button>
        <button class="btn ghost" id="camBig">＋</button>
        <button class="btn ghost" id="camSmall">－</button>
      </div>
      <div class="btn-row" style="margin-top:8px">
        <button class="btn ghost" id="micToggle">🎤 Микрофон: выкл</button>
        <select id="micDev" class="fi" style="max-width:220px;display:none"></select>
        <span class="muted">качество:</span>
        <select id="q2" class="fi" style="max-width:160px">
          <option value="720">720p</option><option value="1080" selected>1080p</option><option value="1440">1440p</option>
        </select>
      </div>
    </div>
  </div>
</div>
<script>
var TOK=<?= json_encode(token()) ?>, SID=null, LINK='';
var screenStream=null, camStream=null, micStream=null;
var camOn=false, micOn=false, camPos='br', camScale=0.26, maxW=1920;
function node(p,d){return fetch(p,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(d)}).then(function(r){return r.json();});}
function msg(t){var e=document.getElementById('st');e.style.display='block';e.textContent=t;}
function qToW(q){ return q==='1440'?2560:(q==='720'?1280:1920); }

document.getElementById('start').onclick=function(){
  var wantS=document.getElementById('useScreen').checked, wantC=document.getElementById('useCam').checked, wantM=document.getElementById('useMic').checked;
  if(!wantS&&!wantC){ msg('Выберите экран и/или камеру'); return; }
  maxW=qToW(document.getElementById('q').value);
  var chain=Promise.resolve();
  if(wantS) chain=chain.then(function(){return navigator.mediaDevices.getDisplayMedia({video:{frameRate:15}}).then(function(ms){screenStream=ms;document.getElementById('screenV').srcObject=ms;ms.getVideoTracks()[0].addEventListener('ended',stop);});});
  if(wantC) chain=chain.then(function(){return navigator.mediaDevices.getUserMedia({video:true}).then(function(ms){camStream=ms;camOn=true;document.getElementById('camV').srcObject=ms;});});
  if(wantM) chain=chain.then(function(){return navigator.mediaDevices.getUserMedia({audio:true}).then(function(ms){micStream=ms;micOn=true;});});
  chain.then(function(){ return node('/stream/start',{token:TOK,id:document.getElementById('sid').value.trim(),title:(document.getElementById('title').value.trim()||'Эфир'),category:document.getElementById('cat').value,subcategory:document.getElementById('sub').value.trim(),name:document.getElementById('name').value.trim()}); })
  .then(function(j){ if(!j.ok){msg(j.error||'Ошибка');return;} SID=j.id; LINK=location.origin+(j.watch||('/live/watch.php?s='+j.id));
    document.getElementById('setup').style.display='none'; document.getElementById('live').style.display='block';
    document.getElementById('watch').href=j.watch||('/live/watch.php?s='+j.id);
    document.getElementById('camToggle').textContent='🎥 Камера: '+(camOn?'вкл':'выкл');
    document.getElementById('micToggle').textContent='🎤 Микрофон: '+(micOn?'вкл':'выкл');
    listDevices(); drawLoop(); startRecorder(); pollv();
  }).catch(function(e){ msg('Не удалось: '+e.message); });
};

// плавная отрисовка на canvas (экран + камера PiP), размер = исходный кадр (без обрезки)
var rafId=null;
function drawLoop(){
  var cv=document.getElementById('cv'), ctx=cv.getContext('2d');
  var sV=document.getElementById('screenV'), cV=document.getElementById('camV');
  function tick(){
    var baseW=0,baseH=0;
    if(screenStream&&sV.videoWidth){ baseW=sV.videoWidth; baseH=sV.videoHeight; }
    else if(camStream&&cV.videoWidth){ baseW=cV.videoWidth; baseH=cV.videoHeight; }
    if(baseW){
      var scale=baseW>maxW?maxW/baseW:1; var W=Math.round(baseW*scale), H=Math.round(baseH*scale);
      if(cv.width!==W||cv.height!==H){ cv.width=W; cv.height=H; }
      if(screenStream&&sV.videoWidth) ctx.drawImage(sV,0,0,W,H);
      else { ctx.fillStyle='#000'; ctx.fillRect(0,0,W,H); }
      if(camOn&&camStream&&cV.videoWidth){
        var cw=Math.round(W*camScale), ch=Math.round(cw*cV.videoHeight/cV.videoWidth), m=Math.round(W*0.015);
        var x=(camPos==='tl'||camPos==='bl')?m:W-cw-m, y=(camPos==='tl'||camPos==='tr')?m:H-ch-m;
        ctx.drawImage(cV,x,y,cw,ch); ctx.strokeStyle='#fff'; ctx.lineWidth=2; ctx.strokeRect(x,y,cw,ch);
      }
    }
    rafId=requestAnimationFrame(tick);
  }
  tick();
}

// НАСТОЯЩЕЕ ВИДЕО: пишем canvas (+микрофон) в webm и шлём чанками на сервер
var rec=null, recFirst=true, combined=null;
function videoMime(){ var m=['video/webm;codecs=vp8,opus','video/webm;codecs=vp9,opus','video/webm']; for(var i=0;i<m.length;i++) if(window.MediaRecorder&&MediaRecorder.isTypeSupported(m[i])) return m[i]; return ''; }
function vBitrate(){ return maxW>=2560?6000000:(maxW>=1920?4000000:2500000); }
function startRecorder(){
  try{ if(rec){ rec.onstop=null; rec.stop(); } }catch(e){}
  var cv=document.getElementById('cv');
  // дождёмся, пока у canvas появится размер
  if(!cv.width){ setTimeout(startRecorder,200); return; }
  var canvasStream=cv.captureStream(30);
  var tracks=canvasStream.getVideoTracks();
  if(micOn&&micStream){ micStream.getAudioTracks().forEach(function(t){ tracks.push(t); }); }
  combined=new MediaStream(tracks);
  var mime=videoMime(); if(!mime){ msg('Браузер не умеет записывать видео'); return; }
  recFirst=true;
  try{
    rec=new MediaRecorder(combined,{mimeType:mime,videoBitsPerSecond:vBitrate()});
    rec.ondataavailable=function(e){ if(!e.data||!e.data.size||!SID)return;
      var fr=new FileReader(); fr.onload=function(){ var b64=(''+fr.result).split(',')[1]; if(!b64)return;
        node('/stream/media/push',{token:TOK,id:SID,init:recFirst,chunk:b64}); recFirst=false; };
      fr.readAsDataURL(e.data); };
    rec.start(1000);   // чанк раз в секунду
  }catch(e){ msg('Запись видео: '+e.message); }
}
function restartRecorder(){ startRecorder(); }   // после смены микрофона/качества — перезапуск с новым init

function pollv(){ if(!SID)return; node('/stream/viewers',{id:SID}).then(function(j){ if(j.ok){ var n=j.count||0; var names=(j.viewers||[]).map(function(v){return v.user;}).join(', '); document.getElementById('vw').textContent=' · '+n+' смотрят'+(names?(' ('+names+')'):''); } }).finally(function(){setTimeout(pollv,4000);}); }

function listDevices(){ navigator.mediaDevices.enumerateDevices().then(function(ds){
  var cs=document.getElementById('camDev'), ms=document.getElementById('micDev'); cs.innerHTML='';ms.innerHTML='';
  ds.forEach(function(d){ if(d.kind==='videoinput'){var o=document.createElement('option');o.value=d.deviceId;o.textContent=d.label||'Камера';cs.appendChild(o);} if(d.kind==='audioinput'){var o2=document.createElement('option');o2.value=d.deviceId;o2.textContent=d.label||'Микрофон';ms.appendChild(o2);} });
  cs.style.display=cs.options.length>1?'inline-block':'none'; ms.style.display=ms.options.length>1?'inline-block':'none';
}); }

document.getElementById('camToggle').onclick=function(){
  if(camOn){ camOn=false; this.textContent='🎥 Камера: выкл'; }
  else { var dev=document.getElementById('camDev').value;
    navigator.mediaDevices.getUserMedia({video:dev?{deviceId:{exact:dev}}:true}).then(function(ms){ if(camStream)camStream.getTracks().forEach(function(t){t.stop();}); camStream=ms; document.getElementById('camV').srcObject=ms; camOn=true; document.getElementById('camToggle').textContent='🎥 Камера: вкл'; listDevices(); }).catch(function(e){alert('Камера: '+e.message);}); }
};
document.getElementById('camDev').onchange=function(){ if(!camOn)return; var dev=this.value; navigator.mediaDevices.getUserMedia({video:{deviceId:{exact:dev}}}).then(function(ms){ if(camStream)camStream.getTracks().forEach(function(t){t.stop();}); camStream=ms; document.getElementById('camV').srcObject=ms; }); };
Array.prototype.forEach.call(document.querySelectorAll('.campos'),function(b){ b.onclick=function(){ camPos=this.getAttribute('data-p'); }; });
document.getElementById('camBig').onclick=function(){ camScale=Math.min(0.5,camScale+0.05); };
document.getElementById('camSmall').onclick=function(){ camScale=Math.max(0.12,camScale-0.05); };
document.getElementById('micToggle').onclick=function(){
  if(micOn){ micOn=false; this.textContent='🎤 Микрофон: выкл'; restartRecorder(); }
  else { var dev=document.getElementById('micDev').value;
    navigator.mediaDevices.getUserMedia({audio:dev?{deviceId:{exact:dev}}:true}).then(function(ms){ if(micStream)micStream.getTracks().forEach(function(t){t.stop();}); micStream=ms; micOn=true; document.getElementById('micToggle').textContent='🎤 Микрофон: вкл'; listDevices(); restartRecorder(); }).catch(function(e){alert('Микрофон: '+e.message);}); }
};
document.getElementById('micDev').onchange=function(){ if(!micOn)return; var dev=this.value; navigator.mediaDevices.getUserMedia({audio:{deviceId:{exact:dev}}}).then(function(ms){ if(micStream)micStream.getTracks().forEach(function(t){t.stop();}); micStream=ms; restartRecorder(); }); };
document.getElementById('q2').onchange=function(){ maxW=qToW(this.value); restartRecorder(); };

function stop(){ if(rafId)cancelAnimationFrame(rafId); try{if(rec){rec.onstop=null;rec.stop();}}catch(e){} if(SID)node('/stream/stop',{token:TOK,id:SID});
  [screenStream,camStream,micStream].forEach(function(s){ if(s)s.getTracks().forEach(function(t){t.stop();}); }); location.href='/live/'; }
document.getElementById('stop').onclick=stop;
document.getElementById('copy').onclick=function(){ navigator.clipboard.writeText(LINK); this.textContent='Скопировано ✓'; };
window.addEventListener('beforeunload',function(){ if(SID)node('/stream/stop',{token:TOK,id:SID}); });
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
