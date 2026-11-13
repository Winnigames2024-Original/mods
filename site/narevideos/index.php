<?php
require_once __DIR__ . '/../_inc/config.php';
$PAGE_TITLE = 'NareVideos — видео, Shorts, эфиры и фото';
$PAGE_DESC  = 'NareVideos: видео, Shorts, записи эфиров и фото сообщества NareLauncher.';
require __DIR__ . '/../_inc/head.php';

$recs = api_get('/recommend' . (me() ? '?token=' . urlencode(token()) : ''))['videos'] ?? [];
$all  = api_get('/video/list')['videos'] ?? [];
$phs  = api_get('/photos')['photos'] ?? [];

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
?>
<div class="nv-top">
  <div class="nv-brand"><span class="nv-logo">▶</span> <b>NareVideos</b></div>
  <div class="btn-row">
    <a class="btn ghost" href="/live/">🔴 Эфиры</a>
    <?php if (me()): ?><a class="btn ghost" href="/subscriptions/">Подписки</a><a class="btn" href="/media/upload.php">Загрузить</a><?php endif; ?>
  </div>
</div>

<div class="tabs" id="nvtabs">
  <button class="tab active" type="button" data-t="home">🏠 Главная</button>
  <button class="tab" type="button" data-t="videos">🎬 Видео</button>
  <button class="tab" type="button" data-t="shorts">⚡ Shorts</button>
  <button class="tab" type="button" data-t="records">⏺ Записи эфиров</button>
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

<!-- Фото -->
<div class="tab-pane" data-pane="photos">
  <?php if (!$phs): ?><p class="muted">Нет фото.</p><?php else: ?><div class="mgrid"><?php foreach ($phs as $p) nv_pcard($p); ?></div><?php endif; ?>
</div>

<script>
Array.prototype.forEach.call(document.querySelectorAll('#nvtabs .tab'),function(t){ t.addEventListener('click',function(){
  var name=this.getAttribute('data-t');
  Array.prototype.forEach.call(document.querySelectorAll('#nvtabs .tab'),function(x){x.classList.toggle('active',x===t);});
  Array.prototype.forEach.call(document.querySelectorAll('.tab-pane'),function(p){p.classList.toggle('active',p.getAttribute('data-pane')===name);});
}); });
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
