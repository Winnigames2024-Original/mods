<?php
require_once __DIR__ . '/../_inc/config.php';
// Встраиваемый плеер эфира (для iframe). Шапки/подвала сайта нет.
$id = $_GET['s'] ?? '';
?><!doctype html>
<html lang="ru"><head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Эфир</title>
<style>
  html,body{margin:0;height:100%;background:#05070c;color:#e6edf3;font-family:system-ui,Segoe UI,Roboto,sans-serif}
  .wrap{position:relative;width:100%;height:100%;display:flex;align-items:center;justify-content:center;overflow:hidden}
  video,img{width:100%;height:100%;object-fit:contain;background:#05070c;display:none}
  .off{position:absolute;inset:0;display:grid;place-items:center;text-align:center;padding:16px;color:#8b949e}
  .bar{position:absolute;left:0;right:0;bottom:0;display:flex;gap:8px;align-items:center;justify-content:space-between;
    padding:8px 10px;background:linear-gradient(transparent,rgba(0,0,0,.6))}
  .bar a,.bar button{background:rgba(0,0,0,.5);color:#fff;border:1px solid rgba(255,255,255,.2);border-radius:8px;
    padding:5px 10px;font:600 12px/1 inherit;cursor:pointer;text-decoration:none}
  .live{display:inline-block;background:#e23;color:#fff;font-size:11px;font-weight:700;padding:2px 7px;border-radius:5px}
  .need{text-align:center}
  .need a{color:#5b8cff}
</style>
</head><body>
<?php if (!me()): ?>
  <div class="wrap"><div class="need">Чтобы смотреть эфир, <a href="/login/" target="_top">войдите в аккаунт</a>.</div></div>
<?php else: ?>
<div class="wrap">
  <video id="vid" autoplay muted playsinline></video>
  <img id="scr" alt="эфир">
  <div id="off" class="off">Подключение…</div>
  <div class="bar">
    <span id="ttl" style="font-size:13px;font-weight:600;overflow:hidden;text-overflow:ellipsis;white-space:nowrap"></span>
    <span style="display:flex;gap:8px;align-items:center">
      <button id="sound">🔊 звук</button>
      <a id="open" href="/live/watch.php?s=<?= htmlspecialchars($id, ENT_QUOTES) ?>" target="_top">Открыть ↗</a>
    </span>
  </div>
</div>
<script src="https://cdn.jsdelivr.net/npm/hls.js@1"></script>
<script>
var ID=<?= json_encode($id) ?>, TOK=<?= json_encode(token()) ?>;
function node(p,d){return fetch(p,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(d)}).then(function(r){return r.json();});}
var vid=document.getElementById('vid'), scr=document.getElementById('scr'), off=document.getElementById('off');
var hls=null, hlsUrl='';
function setupHls(u){ if(hlsUrl===u)return; hlsUrl=u;
  if(vid.canPlayType('application/vnd.apple.mpegurl')){ vid.src=u; vid.play().catch(function(){}); }
  else if(window.Hls&&Hls.isSupported()){ if(hls)hls.destroy(); hls=new Hls({liveSyncDurationCount:3,lowLatencyMode:true}); hls.loadSource(u); hls.attachMedia(vid); hls.on(Hls.Events.MANIFEST_PARSED,function(){vid.play().catch(function(){});}); }
}
// MSE для webm-эфиров
var MIME='video/webm;codecs="vp8,opus"', mseOk=('MediaSource'in window)&&window.MediaSource.isTypeSupported&&MediaSource.isTypeSupported(MIME);
var media=null,sb=null,queue=[],epoch=-1,since=0,opening=false;
function b64buf(b){var s=atob(b),n=s.length,u=new Uint8Array(n);for(var i=0;i<n;i++)u[i]=s.charCodeAt(i);return u;}
function pump(){ if(!sb||sb.updating||!queue.length)return; try{sb.appendBuffer(queue.shift());}catch(e){} }
function startMse(){ if(opening||media)return; opening=true; media=new MediaSource(); vid.src=URL.createObjectURL(media);
  media.addEventListener('sourceopen',function(){ opening=false; try{ sb=media.addSourceBuffer(MIME); sb.mode='sequence'; sb.addEventListener('updateend',pump); pump(); }catch(e){mseOk=false;} }); }
function videoLoop(){ if(!mseOk)return;
  node('/stream/media/get',{token:TOK,id:ID,since:since}).then(function(j){
    if(!j||j.found===false||!j.hasVideo)return;
    if(j.epoch!==epoch){ epoch=j.epoch; try{if(vid.src)URL.revokeObjectURL(vid.src);}catch(e){} media=null;sb=null;queue=[];since=0;opening=false; startMse(); if(j.init)queue.push(b64buf(j.init)); }
    else if(!media){ startMse(); }
    (j.chunks||[]).forEach(function(c){ queue.push(b64buf(c.chunk)); if(c.seq>since)since=c.seq; });
    pump();
  }).catch(function(){}).finally(function(){ setTimeout(videoLoop,900); }); }
videoLoop();
function meta(){ node('/stream/frame',{token:TOK,id:ID}).then(function(j){
  if(j.needLogin){ off.innerHTML='<a href="/login/" target="_top" style="color:#5b8cff">Войдите</a>, чтобы смотреть'; off.style.display='grid'; return; }
  if(j.found===false){ off.textContent='ошибка 1.456 — эфир не найден'; off.style.display='grid'; vid.style.display='none'; scr.style.display='none'; return; }
  document.getElementById('ttl').textContent=(j.title||'Эфир')+' · '+(j.viewers||0)+' смотрят';
  if(j.hls){ setupHls(j.hls); vid.style.display='block'; scr.style.display='none'; }
  else if(j.hasVideo){ vid.style.display='block'; scr.style.display='none'; }
  else { vid.style.display='none'; scr.style.display='block'; if(j.jpg)scr.src='data:image/jpeg;base64,'+j.jpg; }
  off.textContent=j.online?'':(j.lost?'Пропало соединение у стримера…':'Эфир не запущен. Ожидайте.');
  off.style.display=j.online?'none':'grid';
}).catch(function(){}).finally(function(){ setTimeout(meta,1500); }); }
meta();
document.getElementById('sound').addEventListener('click',function(){ vid.muted=!vid.muted; this.textContent=vid.muted?'🔊 звук':'🔇 звук'; if(!vid.muted)vid.play().catch(function(){}); });
</script>
<?php endif; ?>
</body></html>
