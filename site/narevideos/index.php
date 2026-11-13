<?php
require_once __DIR__ . '/../_inc/config.php';
$PAGE_TITLE = 'NareVideos — видео, клипы, эфиры и фото';
$PAGE_DESC  = 'NareVideos: видео, клипы, записи эфиров, каналы и фото сообщества NareLauncher.';
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
    echo '<a class="vcard" href="/watch/' . urlencode($v['id']) . '/">'
       . '<div class="vthumb"' . $thumb . '>' . (empty($v['thumb']) ? '<span>▶</span>' : '')
       . (!empty($v['dur']) ? '<span class="dur">' . h(nv_dur($v['dur'])) . '</span>' : '') . '</div>'
       . '<div class="vinfo"><div class="vtitle">' . h($v['title']) . '</div>'
       . '<div class="vmeta">' . h($v['user']) . ' · ' . (int)$v['views'] . ' просмотров</div></div></a>';
}
function nv_pcard($p) {
    echo '<a class="vcard" href="/media/photo.php?p=' . urlencode($p['id']) . '">'
       . '<div class="vthumb photo" style="background-image:url(\'/photo/img/' . urlencode($p['id']) . '\');background-size:cover;background-position:center"></div>'
       . '<div class="vinfo"><div class="vtitle">' . h($p['title']) . '</div>'
       . '<div class="vmeta">' . h($p['user']) . ' · ' . (int)$p['views'] . ' просмотров</div></div></a>';
}
function nv_ccard($c) {
    $ava = !empty($c['avatar']) ? ' style="background-image:url(\'' . h($c['avatar']) . '\');background-size:cover;background-position:center"' : '';
    echo '<a class="ccard" href="/channels/chat.php?c=' . urlencode($c['id']) . '">'
       . '<div class="cc-ava"' . $ava . '>' . (empty($c['avatar']) ? h(initial($c['name'] ?? '?')) : '') . '</div>'
       . '<div class="cc-name"># ' . h($c['name']) . '</div>'
       . '<div class="vmeta">' . (!empty($c['username']) ? '@' . h($c['username']) : (int)($c['msgs'] ?? 0) . ' сообщ.') . '</div></a>';
}
function nv_scard($s) {
    $href = !empty($s['link']) ? $s['link'] : ('/live/watch.php?s=' . urlencode($s['id']));
    $cov = !empty($s['cover']) ? ' style="background-image:url(\'data:image/jpeg;base64,' . h($s['cover']) . '\');background-size:cover;background-position:center"' : '';
    echo '<a class="vcard" href="' . h($href) . '">'
       . '<div class="vthumb"' . $cov . '><span class="live-dot">● LIVE</span></div>'
       . '<div class="vinfo"><div class="vtitle">' . h($s['title']) . '</div>'
       . '<div class="vmeta">' . h($s['user']) . ' · ' . (int)$s['viewers'] . ' смотрят</div></div></a>';
}
$nav = [
  ['home', '🏠', 'Главная'], ['videos', '🎬', 'Видео'], ['shorts', '⚡', 'Клипы'],
  ['streams', '🔴', 'Эфиры'], ['records', '⏺', 'Записи'], ['channels', '📺', 'Каналы'], ['photos', '🖼', 'Фото'],
];
?>
<div class="vk">
  <aside class="vk-side">
    <div class="vk-brand"><span class="nv-logo">▶</span> NareVideos</div>
    <nav class="vk-nav">
      <?php foreach ($nav as $i => $n): ?>
      <button class="vk-item<?= $i === 0 ? ' active' : '' ?>" type="button" data-t="<?= $n[0] ?>"><span class="vk-ic"><?= $n[1] ?></span><?= $n[2] ?></button>
      <?php endforeach; ?>
      <?php if (me()): ?>
      <div class="vk-sep"></div>
      <a class="vk-item" href="/subscriptions/"><span class="vk-ic">⭐</span>Подписки</a>
      <a class="vk-item" href="/media/upload.php"><span class="vk-ic">⬆</span>Загрузить</a>
      <a class="vk-item" href="/live/go.php"><span class="vk-ic">📡</span>Начать эфир</a>
      <?php endif; ?>
    </nav>
  </aside>

  <main class="vk-main">
    <div class="vk-search"><input id="nvq" type="search" placeholder="Поиск видео, клипов, каналов и эфиров" autocomplete="off"></div>

    <!-- результаты поиска -->
    <div id="nv-results" style="display:none">
      <div id="nvr-chans"></div>
      <div id="nvr-streams"></div>
      <div id="nvr-videos"></div>
      <div id="nvr-photos"></div>
      <p class="muted" id="nvr-empty" style="display:none">Ничего не найдено.</p>
    </div>

    <div id="nv-browse">
      <!-- Главная -->
      <div class="pane active" data-pane="home">
        <?php if ($recs): ?><h2>Рекомендации</h2><div class="vgrid"><?php foreach ($recs as $v) nv_vcard($v); ?></div><?php endif; ?>
        <?php if ($videos): ?><h2>Видео</h2><div class="vgrid"><?php foreach (array_slice($videos, 0, 12) as $v) nv_vcard($v); ?></div><?php endif; ?>
        <?php if ($shorts): ?><h2>Клипы</h2><div class="vgrid clips"><?php foreach (array_slice($shorts, 0, 12) as $v) nv_vcard($v); ?></div><?php endif; ?>
        <?php if ($streams): ?><h2>Прямые эфиры</h2><div class="vgrid"><?php foreach ($streams as $s) nv_scard($s); ?></div><?php endif; ?>
        <?php if ($records): ?><h2>Записи эфиров</h2><div class="vgrid"><?php foreach (array_slice($records, 0, 8) as $v) nv_vcard($v); ?></div><?php endif; ?>
        <?php if ($chans): ?><h2>Каналы</h2><div class="cgrid"><?php foreach (array_slice($chans, 0, 12) as $c) nv_ccard($c); ?></div><?php endif; ?>
        <?php if ($phs): ?><h2>Фото</h2><div class="vgrid"><?php foreach (array_slice($phs, 0, 8) as $p) nv_pcard($p); ?></div><?php endif; ?>
        <?php if (!$recs && !$videos && !$shorts && !$streams && !$records && !$chans && !$phs): ?><p class="muted">Пока пусто. <?php if (me()): ?><a href="/media/upload.php">Загрузите первое видео</a>.<?php endif; ?></p><?php endif; ?>
      </div>
      <div class="pane" data-pane="videos"><?php if (!$videos): ?><p class="muted">Нет видео.</p><?php else: ?><div class="vgrid"><?php foreach ($videos as $v) nv_vcard($v); ?></div><?php endif; ?></div>
      <div class="pane" data-pane="shorts"><?php if (!$shorts): ?><p class="muted">Нет клипов.</p><?php else: ?><div class="vgrid clips"><?php foreach ($shorts as $v) nv_vcard($v); ?></div><?php endif; ?></div>
      <div class="pane" data-pane="streams"><?php if (!$streams): ?><p class="muted">Сейчас нет прямых эфиров. <a href="/live/go.php">Начать эфир</a>.</p><?php else: ?><div class="vgrid"><?php foreach ($streams as $s) nv_scard($s); ?></div><?php endif; ?></div>
      <div class="pane" data-pane="records"><?php if (!$records): ?><p class="muted">Нет записей эфиров — появляются после завершения эфира (настоящим видео).</p><?php else: ?><div class="vgrid"><?php foreach ($records as $v) nv_vcard($v); ?></div><?php endif; ?></div>
      <div class="pane" data-pane="channels"><?php if (!$chans): ?><p class="muted">Нет каналов. <a href="/channels/">Создать</a>.</p><?php else: ?><div class="cgrid"><?php foreach ($chans as $c) nv_ccard($c); ?></div><?php endif; ?></div>
      <div class="pane" data-pane="photos"><?php if (!$phs): ?><p class="muted">Нет фото.</p><?php else: ?><div class="vgrid"><?php foreach ($phs as $p) nv_pcard($p); ?></div><?php endif; ?></div>
    </div>
  </main>
</div>

<script>
var NVDATA = {
  videos: <?= json_encode($all, JSON_UNESCAPED_UNICODE) ?>,
  photos: <?= json_encode($phs, JSON_UNESCAPED_UNICODE) ?>,
  channels: <?= json_encode($chans, JSON_UNESCAPED_UNICODE) ?>,
  streams: <?= json_encode($streams, JSON_UNESCAPED_UNICODE) ?>
};
function esc(s){var d=document.createElement('div');d.textContent=s;return d.innerHTML;}
function fmtDur(s){s=Math.round(s);var m=Math.floor(s/60),x=s%60;return m+':'+(x<10?'0':'')+x;}
function vcard(v){ var th=v.thumb?(' style="background-image:url(\''+String(v.thumb).replace(/'/g,'')+'\');background-size:cover;background-position:center"'):''; return '<a class="vcard" href="/watch/'+encodeURIComponent(v.id)+'/"><div class="vthumb"'+th+'>'+(v.thumb?'':'<span>▶</span>')+(v.dur?'<span class="dur">'+fmtDur(v.dur)+'</span>':'')+'</div><div class="vinfo"><div class="vtitle">'+esc(v.title||'')+'</div><div class="vmeta">'+esc(v.user||'')+' · '+(v.views||0)+' просмотров</div></div></a>'; }
function pcard(p){ return '<a class="vcard" href="/media/photo.php?p='+encodeURIComponent(p.id)+'"><div class="vthumb photo" style="background-image:url(\'/photo/img/'+encodeURIComponent(p.id)+'\');background-size:cover;background-position:center"></div><div class="vinfo"><div class="vtitle">'+esc(p.title||'Фото')+'</div><div class="vmeta">'+esc(p.user||'')+'</div></div></a>'; }
function ccard(c){ var av=c.avatar?(' style="background-image:url(\''+String(c.avatar).replace(/'/g,'')+'\');background-size:cover;background-position:center"'):''; return '<a class="ccard" href="/channels/chat.php?c='+encodeURIComponent(c.id)+'"><div class="cc-ava"'+av+'>'+(c.avatar?'':esc((c.name||'?').slice(0,1).toUpperCase()))+'</div><div class="cc-name"># '+esc(c.name||'')+'</div><div class="vmeta">'+(c.username?'@'+esc(c.username):(c.msgs||0)+' сообщ.')+'</div></a>'; }
function scard(s){ var href=s.link||('/live/watch.php?s='+encodeURIComponent(s.id)); var cov=s.cover?(' style="background-image:url(\'data:image/jpeg;base64,'+String(s.cover).replace(/'/g,'')+'\');background-size:cover;background-position:center"'):''; return '<a class="vcard" href="'+href+'"><div class="vthumb"'+cov+'><span class="live-dot">● LIVE</span></div><div class="vinfo"><div class="vtitle">'+esc(s.title||'')+'</div><div class="vmeta">'+esc(s.user||'')+' · '+(s.viewers||0)+' смотрят</div></div></a>'; }

// боковое меню переключает разделы
Array.prototype.forEach.call(document.querySelectorAll('.vk-item[data-t]'),function(t){ t.addEventListener('click',function(){
  var name=this.getAttribute('data-t');
  Array.prototype.forEach.call(document.querySelectorAll('.vk-item[data-t]'),function(x){x.classList.toggle('active',x===t);});
  Array.prototype.forEach.call(document.querySelectorAll('.pane'),function(p){p.classList.toggle('active',p.getAttribute('data-pane')===name);});
  window.scrollTo(0,0);
}); });

// поиск
function sect(box,title,items,render,grid){ var el=document.getElementById(box); if(!items.length){el.innerHTML='';return 0;} el.innerHTML='<h2>'+title+'</h2><div class="'+(grid||'vgrid')+'">'+items.map(render).join('')+'</div>'; return items.length; }
function runSearch(q){ q=q.trim().toLowerCase();
  var browse=document.getElementById('nv-browse'), res=document.getElementById('nv-results');
  if(!q){ res.style.display='none'; browse.style.display='block'; return; }
  browse.style.display='none'; res.style.display='block';
  var has=function(s){return String(s||'').toLowerCase().indexOf(q)>=0;};
  var n=0;
  n+=sect('nvr-chans','Каналы',NVDATA.channels.filter(function(c){return has(c.name)||has(c.username)||has(c.desc);}),ccard,'cgrid');
  n+=sect('nvr-streams','Эфиры',NVDATA.streams.filter(function(s){return has(s.title)||has(s.user)||has(s.subcategory)||has(s.category);}),scard);
  n+=sect('nvr-videos','Видео',NVDATA.videos.filter(function(v){return has(v.title)||has(v.user)||(v.tags||[]).some(has)||has(v.subcategory);}),vcard);
  n+=sect('nvr-photos','Фото',NVDATA.photos.filter(function(p){return has(p.title)||has(p.user);}),pcard);
  document.getElementById('nvr-empty').style.display=n?'none':'block';
}
var nvq=document.getElementById('nvq'),nvt=null;
nvq.addEventListener('input',function(){ clearTimeout(nvt); var v=this.value; nvt=setTimeout(function(){runSearch(v);},150); });
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
