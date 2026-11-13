<?php
require_once __DIR__ . '/../_inc/config.php';
$PAGE_TITLE = 'NareVideos — видео, Shorts, эфиры и фото';
$PAGE_DESC  = 'NareVideos: видео, Shorts, записи эфиров и фото сообщества NareLauncher.';
require __DIR__ . '/../_inc/head.php';

$recs = api_get('/recommend' . (me() ? '?token=' . urlencode(token()) : ''))['videos'] ?? [];
$all  = api_get('/video/list')['videos'] ?? [];
$phs  = api_get('/photos')['photos'] ?? [];
$chans   = api_get('/channel/list')['channels'] ?? [];
$streams = api_get('/stream/list')['streams'] ?? [];

$isRec   = function ($v) { return in_array('стрим', $v['tags'] ?? [], true); };
$isShort = function ($v) use ($isRec) { return !$isRec($v) && (in_array('shorts', $v['tags'] ?? [], true) || ((($v['dur'] ?? 0) > 0) && ($v['dur'] ?? 0) <= 60)); };
$videos = array_values(array_filter($all, function ($v) use ($isRec, $isShort) { return !$isRec($v) && !$isShort($v); }));
$shorts = array_values(array_filter($all, $isShort));
$records = array_values(array_filter($all, $isRec));

function nv_dur($s) { $s = (int)round($s); return floor($s / 60) . ':' . str_pad($s % 60, 2, '0', STR_PAD_LEFT); }
function nv_vcard($v) {
    $thumb = !empty($v['thumb']) ? ' style="background-image:url(\'' . h($v['thumb']) . '\');background-size:cover;background-position:center"' : '';
    echo '<a class="mcard" href="/watch/' . urlencode($v['id']) . '/">'
       . '<div class="mthumb vid"' . $thumb . '>' . (empty($v['thumb']) ? '<span>▶</span>' : '')
       . (!empty($v['dur']) ? '<span class="dur">' . h(nv_dur($v['dur'])) . '</span>' : '') . '</div>'
       . '<div class="mmeta"><div class="mtitle">' . h($v['title']) . '</div>'
       . '<div class="muted">' . h($v['user']) . ' · ' . (int)$v['views'] . ' просм.</div></div></a>';
}
function nv_pcard($p) {
    echo '<a class="mcard" href="/media/photo.php?p=' . urlencode($p['id']) . '">'
       . '<div class="mthumb" style="background-image:url(\'/photo/img/' . urlencode($p['id']) . '\');background-size:cover;background-position:center"></div>'
       . '<div class="mmeta"><div class="mtitle">' . h($p['title']) . '</div>'
       . '<div class="muted">' . h($p['user']) . ' · ' . (int)$p['views'] . ' просм.</div></div></a>';
}
function nv_ccard($c) {
    $ava = !empty($c['avatar']) ? ' style="background-image:url(\'' . h($c['avatar']) . '\');background-size:cover;background-position:center"' : '';
    echo '<a class="mcard chan" href="/channels/chat.php?c=' . urlencode($c['id']) . '">'
       . '<div class="cc-ava"' . $ava . '>' . (empty($c['avatar']) ? h(initial($c['name'] ?? '?')) : '') . '</div>'
       . '<div class="mmeta"><div class="mtitle"># ' . h($c['name']) . '</div>'
       . '<div class="muted">' . (!empty($c['username']) ? '@' . h($c['username']) . ' · ' : '') . (int)($c['msgs'] ?? 0) . ' сообщений</div></div></a>';
}
function nv_scard($s) {
    $href = !empty($s['link']) ? $s['link'] : ('/live/watch.php?s=' . urlencode($s['id']));
    $cov = !empty($s['cover']) ? ' style="background-image:url(\'data:image/jpeg;base64,' . h($s['cover']) . '\');background-size:cover;background-position:center"' : '';
    echo '<a class="mcard" href="' . h($href) . '">'
       . '<div class="mthumb vid"' . $cov . '><span class="live-dot">● LIVE</span></div>'
       . '<div class="mmeta"><div class="mtitle">' . h($s['title']) . '</div>'
       . '<div class="muted">' . h($s['user']) . ' · ' . (int)$s['viewers'] . ' смотрят</div></div></a>';
}
?>
<div class="nv-top">
  <div class="nv-brand"><span class="nv-logo">▶</span> <b>NareVideos</b></div>
  <div class="btn-row">
    <a class="btn ghost" href="/live/">🔴 Эфиры</a>
    <?php if (me()): ?><a class="btn ghost" href="/subscriptions/">Подписки</a><a class="btn" href="/media/upload.php">Загрузить</a><?php endif; ?>
  </div>
</div>

<div class="nv-search">
  <input id="nvq" type="search" placeholder="Поиск: видео, фото, каналы, эфиры…" autocomplete="off">
</div>

<!-- результаты поиска -->
<div id="nv-results" style="display:none">
  <h2>Результаты поиска</h2>
  <div id="nvr-chans"></div>
  <div id="nvr-streams"></div>
  <div id="nvr-videos"></div>
  <div id="nvr-photos"></div>
  <p class="muted" id="nvr-empty" style="display:none">Ничего не найдено.</p>
</div>

<div id="nv-browse">
<div class="tabs" id="nvtabs">
  <button class="tab active" type="button" data-t="home">🏠 Главная</button>
  <button class="tab" type="button" data-t="videos">🎬 Видео</button>
  <button class="tab" type="button" data-t="shorts">⚡ Shorts</button>
  <button class="tab" type="button" data-t="records">⏺ Записи</button>
  <button class="tab" type="button" data-t="channels">📺 Каналы</button>
  <button class="tab" type="button" data-t="streams">🔴 Эфиры</button>
  <button class="tab" type="button" data-t="photos">🖼 Фото</button>
</div>

<!-- Главная -->
<div class="tab-pane active" data-pane="home">
  <?php if ($recs): ?>
  <h2>Рекомендации</h2>
  <div class="mgrid"><?php foreach ($recs as $v) nv_vcard($v); ?></div>
  <?php endif; ?>
  <?php if ($videos): ?><h2>Видео</h2><div class="mgrid"><?php foreach (array_slice($videos, 0, 12) as $v) nv_vcard($v); ?></div><?php endif; ?>
  <?php if ($shorts): ?><h2>Shorts</h2><div class="mgrid shorts"><?php foreach (array_slice($shorts, 0, 12) as $v) nv_vcard($v); ?></div><?php endif; ?>
  <?php if ($records): ?><h2>Записи эфиров</h2><div class="mgrid"><?php foreach (array_slice($records, 0, 8) as $v) nv_vcard($v); ?></div><?php endif; ?>
  <?php if ($phs): ?><h2>Фото</h2><div class="mgrid"><?php foreach (array_slice($phs, 0, 8) as $p) nv_pcard($p); ?></div><?php endif; ?>
  <?php if (!$recs && !$videos && !$shorts && !$records && !$phs): ?><p class="muted">Пока пусто. <?php if (me()): ?><a href="/media/upload.php">Загрузите первое видео</a>.<?php endif; ?></p><?php endif; ?>
</div>

<!-- Видео -->
<div class="tab-pane" data-pane="videos">
  <?php if (!$videos): ?><p class="muted">Нет видео.</p><?php else: ?><div class="mgrid"><?php foreach ($videos as $v) nv_vcard($v); ?></div><?php endif; ?>
</div>

<!-- Shorts -->
<div class="tab-pane" data-pane="shorts">
  <?php if (!$shorts): ?><p class="muted">Нет Shorts.</p><?php else: ?><div class="mgrid shorts"><?php foreach ($shorts as $v) nv_vcard($v); ?></div><?php endif; ?>
</div>

<!-- Записи эфиров -->
<div class="tab-pane" data-pane="records">
  <?php if (!$records): ?><p class="muted">Нет записей эфиров. Они появляются автоматически после завершения эфира (настоящим видео).</p><?php else: ?><div class="mgrid"><?php foreach ($records as $v) nv_vcard($v); ?></div><?php endif; ?>
</div>

<!-- Каналы -->
<div class="tab-pane" data-pane="channels">
  <?php if (!$chans): ?><p class="muted">Нет каналов. <a href="/channels/">Создать канал</a>.</p><?php else: ?><div class="mgrid"><?php foreach ($chans as $c) nv_ccard($c); ?></div><?php endif; ?>
</div>

<!-- Эфиры -->
<div class="tab-pane" data-pane="streams">
  <?php if (!$streams): ?><p class="muted">Сейчас нет прямых эфиров. <a href="/live/go.php">Начать эфир</a>.</p><?php else: ?><div class="mgrid"><?php foreach ($streams as $s) nv_scard($s); ?></div><?php endif; ?>
</div>

<!-- Фото -->
<div class="tab-pane" data-pane="photos">
  <?php if (!$phs): ?><p class="muted">Нет фото.</p><?php else: ?><div class="mgrid"><?php foreach ($phs as $p) nv_pcard($p); ?></div><?php endif; ?>
</div>
</div><!-- /nv-browse -->

<script>
var NVDATA = {
  videos: <?= json_encode($all, JSON_UNESCAPED_UNICODE) ?>,
  photos: <?= json_encode($phs, JSON_UNESCAPED_UNICODE) ?>,
  channels: <?= json_encode($chans, JSON_UNESCAPED_UNICODE) ?>,
  streams: <?= json_encode($streams, JSON_UNESCAPED_UNICODE) ?>
};
Array.prototype.forEach.call(document.querySelectorAll('#nvtabs .tab'),function(t){ t.addEventListener('click',function(){
  var name=this.getAttribute('data-t');
  Array.prototype.forEach.call(document.querySelectorAll('#nvtabs .tab'),function(x){x.classList.toggle('active',x===t);});
  Array.prototype.forEach.call(document.querySelectorAll('.tab-pane'),function(p){p.classList.toggle('active',p.getAttribute('data-pane')===name);});
}); });

// --- поиск по видео / фото / каналам / эфирам ---
function esc(s){var d=document.createElement('div');d.textContent=s;return d.innerHTML;}
function fmtDur(s){s=Math.round(s);var m=Math.floor(s/60),x=s%60;return m+':'+(x<10?'0':'')+x;}
function vcard(v){ var th=v.thumb?(' style="background-image:url(\''+String(v.thumb).replace(/'/g,'')+'\');background-size:cover;background-position:center"'):''; return '<a class="mcard" href="/watch/'+encodeURIComponent(v.id)+'/"><div class="mthumb vid"'+th+'>'+(v.thumb?'':'<span>▶</span>')+(v.dur?'<span class="dur">'+fmtDur(v.dur)+'</span>':'')+'</div><div class="mmeta"><div class="mtitle">'+esc(v.title||'')+'</div><div class="muted">'+esc(v.user||'')+' · '+(v.views||0)+' просм.</div></div></a>'; }
function pcard(p){ return '<a class="mcard" href="/media/photo.php?p='+encodeURIComponent(p.id)+'"><div class="mthumb" style="background-image:url(\'/photo/img/'+encodeURIComponent(p.id)+'\');background-size:cover;background-position:center"></div><div class="mmeta"><div class="mtitle">'+esc(p.title||'Фото')+'</div><div class="muted">'+esc(p.user||'')+'</div></div></a>'; }
function ccard(c){ var av=c.avatar?(' style="background-image:url(\''+String(c.avatar).replace(/'/g,'')+'\');background-size:cover;background-position:center"'):''; return '<a class="mcard chan" href="/channels/chat.php?c='+encodeURIComponent(c.id)+'"><div class="cc-ava"'+av+'>'+(c.avatar?'':esc((c.name||'?').slice(0,1).toUpperCase()))+'</div><div class="mmeta"><div class="mtitle"># '+esc(c.name||'')+'</div><div class="muted">'+(c.username?'@'+esc(c.username)+' · ':'')+(c.msgs||0)+' сообщений</div></div></a>'; }
function scard(s){ var href=s.link||('/live/watch.php?s='+encodeURIComponent(s.id)); var cov=s.cover?(' style="background-image:url(\'data:image/jpeg;base64,'+String(s.cover).replace(/'/g,'')+'\');background-size:cover;background-position:center"'):''; return '<a class="mcard" href="'+href+'"><div class="mthumb vid"'+cov+'><span class="live-dot">● LIVE</span></div><div class="mmeta"><div class="mtitle">'+esc(s.title||'')+'</div><div class="muted">'+esc(s.user||'')+' · '+(s.viewers||0)+' смотрят</div></div></a>'; }

function sect(box,title,items,render){ var el=document.getElementById(box); if(!items.length){el.innerHTML='';return 0;} el.innerHTML='<h3>'+title+'</h3><div class="mgrid'+(box==='nvr-chans'?' chans':'')+'">'+items.map(render).join('')+'</div>'; return items.length; }
function runSearch(q){
  q=q.trim().toLowerCase();
  var browse=document.getElementById('nv-browse'), res=document.getElementById('nv-results');
  if(!q){ res.style.display='none'; browse.style.display='block'; return; }
  browse.style.display='none'; res.style.display='block';
  var has=function(s){return String(s||'').toLowerCase().indexOf(q)>=0;};
  var vids=NVDATA.videos.filter(function(v){return has(v.title)||has(v.user)||(v.tags||[]).some(has)||has(v.subcategory);});
  var phs=NVDATA.photos.filter(function(p){return has(p.title)||has(p.user);});
  var chs=NVDATA.channels.filter(function(c){return has(c.name)||has(c.username)||has(c.desc);});
  var strs=NVDATA.streams.filter(function(s){return has(s.title)||has(s.user)||has(s.subcategory)||has(s.category);});
  var n=0;
  n+=sect('nvr-chans','Каналы',chs,ccard);
  n+=sect('nvr-streams','Эфиры',strs,scard);
  n+=sect('nvr-videos','Видео',vids,vcard);
  n+=sect('nvr-photos','Фото',phs,pcard);
  document.getElementById('nvr-empty').style.display=n?'none':'block';
}
var nvq=document.getElementById('nvq'); var nvt=null;
nvq.addEventListener('input',function(){ clearTimeout(nvt); var v=this.value; nvt=setTimeout(function(){runSearch(v);},150); });
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
