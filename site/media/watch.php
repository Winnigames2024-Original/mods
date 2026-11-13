<?php
require_once __DIR__ . '/../_inc/config.php';
require_login();                 // смотреть видео можно только из аккаунта
$id = $_GET['v'] ?? '';
$resp = api_get('/video/get?id=' . urlencode($id));
$v = $resp['video'] ?? null;
if (!$v) { require __DIR__ . '/../_inc/head.php'; echo '<div class="panel"><h1>Видео не найдено</h1></div>'; require __DIR__ . '/../_inc/foot.php'; exit; }
$PAGE_TITLE = $v['title'] . ' — NareLauncher';
$PAGE_DESC  = mb_strimwidth(strip_tags($v['desc'] ?? ''), 0, 150, '…');
require __DIR__ . '/../_inc/head.php';
?>
<div class="watch">
  <video controls autoplay playsinline style="width:100%;border-radius:12px;background:#000" src="/video/stream/<?= urlencode($v['id']) ?>"></video>
  <h1 style="margin:14px 0 4px"><?= h($v['title']) ?></h1>
  <div class="muted"><a href="/profile/view/?user=<?= urlencode($v['nick']) ?>"><?= h($v['user']) ?></a> · <span id="views"><?= (int)$v['views'] ?></span> просмотров</div>
  <div class="btn-row" style="margin:12px 0">
    <button class="btn ghost" id="like">👍 <span id="likes"><?= (int)$v['likes'] ?></span></button>
  </div>
  <?php if (!empty($v['tags']) && is_array($v['tags'])): ?>
  <div class="tagrow"><?php foreach ($v['tags'] as $tg): ?><a class="tagchip" href="/search/?q=<?= urlencode('#'.$tg) ?>">#<?= h($tg) ?></a><?php endforeach; ?></div>
  <?php endif; ?>
  <?php if (!empty($v['desc'])): ?><div class="panel" style="white-space:pre-wrap"><?= h($v['desc']) ?></div><?php endif; ?>

  <h2 style="margin-top:20px">Комментарии</h2>
  <?php if (me()): ?>
  <div class="row-form"><input id="ctext" placeholder="Ваш комментарий" maxlength="500" style="flex:1"><button class="btn" id="csend">Отправить</button></div>
  <?php else: ?><p class="muted"><a href="/login/">Войдите</a>, чтобы комментировать.</p><?php endif; ?>
  <div id="clist" style="margin-top:12px"></div>
</div>
<script>
var ID = <?= json_encode($v['id']) ?>;
var TOK = <?= json_encode(token()) ?>;
fetch('/video/view', {method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({id:ID})});
function node(path,data){ return fetch(path,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(data)}).then(function(r){return r.json();}); }
function esc(s){var d=document.createElement('div');d.textContent=s;return d.innerHTML;}
function renderComments(cs){ document.getElementById('clist').innerHTML = (cs||[]).map(function(c){
  return '<div class="cmt"><b>'+esc(c.user||c.nick)+'</b> '+esc(c.text)+'</div>'; }).join('') || '<p class="muted">Пока нет комментариев.</p>'; }
fetch('/video/get?id='+encodeURIComponent(ID)).then(r=>r.json()).then(function(j){ if(j.video) renderComments(j.video.comments); });
document.getElementById('like') && document.getElementById('like').addEventListener('click',function(){
  if(!TOK){location.href='/login/';return;}
  node('/video/like',{token:TOK,id:ID}).then(function(j){ if(j.ok) document.getElementById('likes').textContent=j.likes; });
});
var cs=document.getElementById('csend');
cs && cs.addEventListener('click',function(){
  var t=document.getElementById('ctext').value.trim(); if(!t)return;
  node('/video/comment',{token:TOK,id:ID,text:t}).then(function(j){ if(j.ok){document.getElementById('ctext').value='';
    fetch('/video/get?id='+encodeURIComponent(ID)).then(r=>r.json()).then(function(x){ if(x.video) renderComments(x.video.comments); }); }});
});
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
