<?php
require_once __DIR__ . '/../_inc/config.php';

$t = trim($_GET['t'] ?? '');
$info = null;
if ($t !== '') $info = api_post('/verify/status', ['token' => $t]);

$PAGE_TITLE = 'Подтверждение — NareLauncher';
$PAGE_DESC  = 'Подтверждение QR-кода: капча и вход.';
require __DIR__ . '/../_inc/head.php';

function purpose_text($p) {
    if ($p === 'login2fa') return 'Подтверждение входа в аккаунт';
    return 'Подтверждение перехода (капча)';
}
?>
<div class="panel" style="max-width:480px">
  <h1>Подтверждение</h1>

  <?php if ($t === '' || empty($info['exists'])): ?>
    <?php if ($t !== '' && empty($info['exists'])): ?>
      <div class="msg">Код не найден или устарел.</div>
    <?php endif; ?>
    <p class="muted">Введите код, показанный под QR-кодом, или отсканируйте QR камерой телефона.</p>
    <form method="get" class="row-form" style="margin-top:10px">
      <input name="t" placeholder="Код подтверждения" maxlength="24" required style="flex:1">
      <button class="btn" type="submit">Открыть</button>
    </form>
  <?php elseif (!empty($info['verified'])): ?>
    <div class="msg ok" style="font-size:16px">✓ Уже подтверждено. Можно вернуться на прежнюю вкладку.</div>
  <?php else: ?>
    <p class="lead" style="margin-top:0"><?= h(purpose_text($info['purpose'] ?? '')) ?></p>
    <?php if (($info['purpose'] ?? '') === 'login2fa' && !empty($info['nick'])): ?>
      <p>Аккаунт: <b>@<?= h($info['nick']) ?></b></p>
    <?php elseif (!empty($info['url'])): ?>
      <p class="muted" style="word-break:break-all">Назначение: <?= h($info['url']) ?></p>
    <?php endif; ?>
    <p class="muted">Нажмите, только если вы сами сейчас это запросили.</p>
    <button id="ok" class="btn" type="button" style="margin-top:8px">Подтвердить</button>
    <div id="res" class="msg ok" style="display:none">✓ Подтверждено! Вернитесь на прежнюю вкладку — она продолжит сама.</div>
    <script>
      document.getElementById('ok').addEventListener('click', function(){
        var b = this; b.disabled = true; b.textContent = 'Подтверждаю…';
        fetch('/verify/confirm.php', {method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify({token: <?= json_encode($t) ?>})})
          .then(function(r){return r.json();}).then(function(j){
            if (j && j.ok){ b.style.display='none'; document.getElementById('res').style.display='block'; }
            else { b.disabled=false; b.textContent='Подтвердить'; alert(j && j.error ? j.error : 'Не удалось'); }
          }).catch(function(){ b.disabled=false; b.textContent='Подтвердить'; });
      });
    </script>
  <?php endif; ?>
</div>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
