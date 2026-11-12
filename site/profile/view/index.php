<?php
require_once __DIR__ . '/../../_inc/config.php';
$user = $_GET['user'] ?? '';
$p = $user !== '' ? api_get('/profile/get?nick=' . urlencode($user)) : [];
$found = !empty($p['ok']) || !empty($p['nick']);
$name = $found ? ($p['username'] ?? $user) : $user;
$PAGE_TITLE = $found ? ('Профиль ' . $name . ' — NareLauncher') : 'Профиль не найден — NareLauncher';
$PAGE_DESC  = $found ? ('Публичный профиль игрока ' . $name . ' в NareLauncher.') : 'Такого игрока нет в NareLauncher.';
$CANON = SITE . '/profile/view/?user=' . urlencode($user);
require __DIR__ . '/../../_inc/head.php';
?>
<?php if (!$found): ?>
  <div class="panel">
    <h1>Профиль не найден</h1>
    <p class="muted">Игрока «<?= h($user) ?>» нет. Проверьте ник.</p>
    <p style="margin-top:12px"><a class="btn ghost" href="/home/">На главную</a></p>
  </div>
<?php else:
  $letter = initial($name); ?>
  <div class="panel">
    <div class="prof">
      <div class="ava"><?= h($letter) ?></div>
      <div>
        <h1 style="margin:0"><?= h($name) ?>
          <?php if (!empty($p['admin'])): ?><span class="badge">админ</span>
          <?php elseif (!empty($p['moderator'])): ?><span class="badge">модератор</span><?php endif; ?>
          <?php if (!empty($p['online'])): ?><span class="badge on">онлайн</span><?php endif; ?>
        </h1>
        <div class="muted">@<?= h($p['nick'] ?? $user) ?> <?php if (!empty($p['tag'])): ?>· <span class="tag"><?= render_tag($p["tag"]) ?></span><?php endif; ?></div>
        <?php if (!empty($p['status'])): ?><div style="margin-top:6px"><?= h($p['status']) ?></div><?php endif; ?>
      </div>
    </div>
    <div class="grid" style="margin-top:18px">
      <div class="card"><h3>Подписчики</h3><p style="font-size:22px" id="subcnt"><?= (int)($p['subscribers'] ?? 0) ?></p></div>
      <div class="card"><h3>Монеты</h3><p class="coins" style="font-size:22px"><?= (int)($p['coins'] ?? 0) ?></p></div>
      <div class="card"><h3>Регистрация</h3><p><?= !empty($p['since']) ? h(date('d.m.Y', (int)($p['since']/1000))) : '—' ?></p></div>
    </div>
    <?php if (me() && me() !== ($p['nick'] ?? $user)): ?>
      <p style="margin-top:16px"><button class="btn" id="subbtn" type="button">Подписаться</button></p>
    <?php elseif (!me()): ?>
      <p style="margin-top:16px"><a class="btn" href="/login/">Войдите, чтобы подписаться</a></p>
    <?php endif; ?>
  </div>

  <?php if (me() && me() !== ($p['nick'] ?? $user)): ?>
  <script>
  var NICK=<?= json_encode($p['nick'] ?? $user) ?>, TOK=<?= json_encode(token()) ?>;
  var btn=document.getElementById('subbtn');
  function paint(f){ btn.textContent=f?'Вы подписаны ✓':'Подписаться'; btn.className='btn'+(f?' ghost':''); }
  // начальное состояние: есть ли канал среди моих подписок
  fetch('/subscribed',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({token:TOK})})
    .then(function(r){return r.json();}).then(function(j){ if(j.ok){ paint((j.channels||[]).some(function(c){return c.nick.toLowerCase()===NICK.toLowerCase();})); } });
  btn.addEventListener('click',function(){
    fetch('/subscribe',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({token:TOK,nick:NICK})})
      .then(function(r){return r.json();}).then(function(j){ if(j.ok){ paint(j.following); document.getElementById('subcnt').textContent=j.subscribers; } else alert(j.error||'Ошибка'); });
  });
  </script>
  <?php endif; ?>
<?php endif; ?>
<?php require __DIR__ . '/../../_inc/foot.php'; ?>
