<?php
require_once __DIR__ . '/../_inc/config.php';
$id = $_GET['p'] ?? '';
$resp = api_get('/photo/get?id=' . urlencode($id));
$p = $resp['photo'] ?? null;
if (!$p) { require __DIR__ . '/../_inc/head.php'; echo '<div class="panel"><h1>Фото не найдено</h1></div>'; require __DIR__ . '/../_inc/foot.php'; exit; }
$PAGE_TITLE = $p['title'] . ' — NareLauncher';
require __DIR__ . '/../_inc/head.php';
?>
<div class="watch">
  <div style="text-align:center"><img src="/photo/img/<?= urlencode($p['id']) ?>" alt="<?= h($p['title']) ?>" style="max-width:100%;border-radius:12px"></div>
  <h1 style="margin:14px 0 4px"><?= h($p['title']) ?></h1>
  <div class="muted"><a href="/profile/view/?user=<?= urlencode($p['nick']) ?>"><?= h($p['user']) ?></a></div>
  <div class="btn-row" style="margin:12px 0"><button class="btn ghost" id="like">👍 <span id="likes"><?= (int)$p['likes'] ?></span></button></div>
  <?php if (!empty($p['desc'])): ?><div class="panel" style="white-space:pre-wrap"><?= h($p['desc']) ?></div><?php endif; ?>
  <h2 style="margin-top:20px">Комментарии</h2>
  <?php if (me()): ?>
  <div class="row-form"><input id="ctext" placeholder="Ваш комментарий" maxlength="500" style="flex:1"><button class="btn" id="csend">Отправить</button></div>
  <?php else: ?><p class="muted"><a href="/login/">Войдите</a>, чтобы комментировать.</p><?php endif; ?>
  <div id="clist" style="margin-top:12px"></div>
</div>
<script>
var ID=<?= json_encode($p['id']) ?>, TOK=<?= json_encode(token()) ?>;
function node(path,data){return fetch(path,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(data)}).then(function(r){return r.json();});}
function esc(s){var d=document.createElement('div');d.textContent=s;return d.innerHTML;}
function render(cs){document.getElementById('clist').innerHTML=(cs||[]).map(function(c){return '<div class="cmt"><b>'+esc(c.user||c.nick)+'</b> '+esc(c.text)+'</div>';}).join('')||'<p class="muted">Пока нет комментариев.</p>';}
fetch('/photo/get?id='+encodeURIComponent(ID)).then(r=>r.json()).then(function(j){if(j.photo)render(j.photo.comments);});
document.getElementById('like').addEventListener('click',function(){if(!TOK){location.href='/login/';return;}node('/photo/like',{token:TOK,id:ID}).then(function(j){if(j.ok)document.getElementById('likes').textContent=j.likes;});});
var cs=document.getElementById('csend');
cs&&cs.addEventListener('click',function(){var t=document.getElementById('ctext').value.trim();if(!t)return;node('/photo/comment',{token:TOK,id:ID,text:t}).then(function(j){if(j.ok){document.getElementById('ctext').value='';fetch('/photo/get?id='+encodeURIComponent(ID)).then(r=>r.json()).then(function(x){if(x.photo)render(x.photo.comments);});}});});
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
