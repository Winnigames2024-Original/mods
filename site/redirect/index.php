<?php
require_once __DIR__ . '/../_inc/config.php';

$url = $_GET['url'] ?? '';
$captcha = ($_GET['captcha'] ?? '') === 'true';

// Разрешаем только безопасные схемы. url открывается только после подтверждения
// (если captcha=true) и всегда с явной кнопкой — как на GameBanana.
function safe_url($u) {
    $u = trim($u);
    if ($u === '') return '';
    $scheme = strtolower(parse_url($u, PHP_URL_SCHEME) ?: '');
    if (in_array($scheme, ['http', 'https', 'narelauncher'], true)) return $u;
    return '';
}
$dest = safe_url($url);

$token = '';
if ($dest !== '' && $captcha) {
    $r = api_post('/verify/new', ['purpose' => 'redirect', 'url' => $dest]);
    $token = $r['token'] ?? '';
}

$PAGE_TITLE = 'Переход — NareLauncher';
$PAGE_DESC  = 'Переход по внешней ссылке.';
require __DIR__ . '/../_inc/head.php';
?>
<?php if ($dest === ''): ?>
  <div class="panel"><h1>Неверная ссылка</h1><p class="muted">Ссылка пустая или использует недопустимую схему. Разрешены http(s) и narelauncher://.</p>
  <p><a class="btn ghost" href="/home/">На главную</a></p></div>
<?php elseif ($captcha && $token !== ''): ?>
  <div class="panel" style="max-width:520px;text-align:center">
    <h1>Подтвердите, что вы человек</h1>
    <p class="muted">Отсканируйте QR телефоном или откройте код на <b>/qrverify</b> и нажмите «Подтвердить». После этого откроется переход.</p>
    <div class="qr-box"><canvas id="qr"></canvas></div>
    <p>Код подтверждения: <code id="code"><?= h($token) ?></code></p>
    <p class="muted" id="st">Ожидаю подтверждения…</p>
    <a id="go" class="btn" style="display:none" href="<?= h($dest) ?>">Перейти →</a>
    <p class="muted" style="margin-top:14px;font-size:13px;word-break:break-all">Назначение: <?= h($dest) ?></p>
  </div>
  <script src="/assets/qr.js"></script>
  <script>
    var link = <?= json_encode(SITE . '/qrverify/?t=' . $token) ?>;
    NareQR.draw(document.getElementById('qr'), link, {scale:6});
    var dest = <?= json_encode($dest) ?>;
    var token = <?= json_encode($token) ?>;
    var done = false;
    function poll(){
      if (done) return;
      fetch('/verify/status.php?t=' + encodeURIComponent(token)).then(function(r){return r.json();}).then(function(j){
        if (j && j.verified){
          done = true;
          document.getElementById('st').textContent = 'Подтверждено ✓ Переходим…';
          document.getElementById('go').style.display = 'inline-block';
          setTimeout(function(){ location.href = dest; }, 800);
        }
      }).catch(function(){}).finally(function(){ if(!done) setTimeout(poll, 1500); });
    }
    poll();
  </script>
<?php else: ?>
  <div class="panel" style="max-width:520px;text-align:center">
    <h1>Переход на внешний ресурс</h1>
    <p class="muted">Вы покидаете NareLauncher. Проверьте ссылку перед переходом.</p>
    <p style="margin:14px 0;word-break:break-all"><code><?= h($dest) ?></code></p>
    <a class="btn" href="<?= h($dest) ?>" id="go">Перейти →</a>
    <p class="muted" id="auto" style="margin-top:12px">Автопереход через <span id="n">4</span> с…</p>
  </div>
  <script>
    var dest = <?= json_encode($dest) ?>;
    var n = 4, el = document.getElementById('n');
    var t = setInterval(function(){ n--; if(el) el.textContent = n; if(n<=0){ clearInterval(t); location.href = dest; } }, 1000);
  </script>
<?php endif; ?>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
