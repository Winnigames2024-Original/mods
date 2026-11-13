<?php
require_once __DIR__ . '/../_inc/config.php';
$PAGE_TITLE = 'Трансляции — NareLauncher';
$PAGE_DESC  = 'Прямые трансляции сообщества NareLauncher.';
require __DIR__ . '/../_inc/head.php';
$streams = api_get('/stream/list')['streams'] ?? [];
?>
<div class="media-head">
  <h1>Трансляции</h1>
  <?php if (me()): ?><a class="btn" href="/live/go.php">🔴 Начать эфир с браузера</a><?php else: ?><a class="btn ghost" href="/login/">Войдите, чтобы вести эфир</a><?php endif; ?>
</div>
<?php if (me()): ?><p class="muted" style="margin-top:-6px">Можно вести эфир прямо из браузера — <b>приложение не нужно</b>. Браузер спросит разрешение на демонстрацию экрана/окна. Либо стримьте из лаунчера (студия как в OBS).</p><?php endif; ?>

<!-- смотреть эфир по ID прямо здесь (встраивание, без перехода) -->
<div class="embed-box">
  <input id="emid" placeholder="ID эфира" autocomplete="off">
  <button class="btn" id="embtn">▶ Смотреть здесь</button>
  <button class="btn ghost" id="emclose" style="display:none">✕ Закрыть</button>
</div>
<div id="emwrap" class="embed-wrap" style="display:none">
  <iframe id="emframe" allow="autoplay; fullscreen" allowfullscreen></iframe>
</div>
<script>
(function(){
  var inp=document.getElementById('emid'), wrap=document.getElementById('emwrap'), fr=document.getElementById('emframe'), cl=document.getElementById('emclose');
  function show(){ var v=inp.value.trim(); if(!v)return; fr.src='/live/embed.php?s='+encodeURIComponent(v); wrap.style.display='block'; cl.style.display='inline-block'; }
  document.getElementById('embtn').onclick=show;
  inp.addEventListener('keydown',function(e){ if(e.key==='Enter')show(); });
  cl.onclick=function(){ fr.src='about:blank'; wrap.style.display='none'; cl.style.display='none'; };
})();
</script>
<?php if (!$streams): ?>
  <p class="muted">Сейчас нет прямых эфиров.</p>
<?php else: ?>
<div class="mgrid">
  <?php foreach ($streams as $s): $href = !empty($s['link']) ? $s['link'] : ('/live/watch.php?s=' . urlencode($s['id'])); ?>
  <a class="mcard" href="<?= h($href) ?>">
    <div class="mthumb vid"<?= !empty($s['cover']) ? ' style="background-image:url(\'data:image/jpeg;base64,'.h($s['cover']).'\');background-size:cover;background-position:center"' : '' ?>><span class="live-dot">● LIVE</span></div>
    <div class="mmeta"><div class="mtitle"><?= h($s['title']) ?></div>
      <div class="muted"><?= h($s['user']) ?> · <?= (int)$s['viewers'] ?> смотрят<?php if (!empty($s['subcategory'])): ?> · <?= h($s['subcategory']) ?><?php elseif (!empty($s['category'])): ?> · <?= h($s['category']) ?><?php endif; ?></div></div>
  </a>
  <?php endforeach; ?>
</div>
<?php endif; ?>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
