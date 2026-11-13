<?php
require_once __DIR__ . '/../../_inc/config.php';
if (isset($_GET['logout'])) { if (token()) api_post('/account/logout', ['token' => token()]); nare_clear_auth(); session_destroy(); header('Location: /home/'); exit; }
require_login();
$nick = me();

$twofaMsg = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST' && ($_POST['act'] ?? '') === 'twofa') {
    $on = ($_POST['on'] ?? '') === '1';
    $r = api_post('/twofa/set', ['token' => token(), 'on' => $on]);
    $twofaMsg = !empty($r['ok']) ? ($r['on'] ? 'Подтверждение входа по QR включено.' : 'Подтверждение входа по QR выключено.') : 'Не удалось изменить настройку.';
}
$tf = api_post('/twofa/get', ['token' => token()]);
$twofaOn = !empty($tf['on']);

$p = api_get('/profile/get?nick=' . urlencode($nick));
$PAGE_TITLE = 'Мой профиль — NareLauncher';
$PAGE_DESC  = 'Ваш профиль NareLauncher: статус, монеты, тег.';
require __DIR__ . '/../../_inc/head.php';
$letter = initial($p['username'] ?? $nick);
?>
<div class="panel">
  <div class="prof">
    <div class="ava"><?= h($letter) ?></div>
    <div>
      <h1 style="margin:0"><?= h($p['username'] ?? $nick) ?>
        <?php if (!empty($p['admin'])): ?><span class="badge">админ</span>
        <?php elseif (!empty($p['moderator'])): ?><span class="badge">модератор</span><?php endif; ?>
        <?php if (!empty($p['online'])): ?><span class="badge on">онлайн</span><?php endif; ?>
      </h1>
      <div class="muted">@<?= h($nick) ?> <?php if (!empty($p['tag'])): ?>· <span class="tag"><?= render_tag($p["tag"]) ?></span><?php endif; ?></div>
      <?php if (!empty($p['status'])): ?><div style="margin-top:6px"><?= h($p['status']) ?></div><?php endif; ?>
    </div>
  </div>
  <div class="grid" style="margin-top:18px">
    <div class="card"><h3>Монеты</h3><p class="coins" style="font-size:22px"><?= (int)($p['coins'] ?? 0) ?></p></div>
    <div class="card"><h3>Ник на сервере</h3><p><?= h($p['chatName'] ?: $nick) ?></p></div>
    <div class="card"><h3>Регистрация</h3><p><?= $p['since'] ? h(date('d.m.Y', (int)($p['since']/1000))) : '—' ?></p></div>
  </div>
  <p style="margin-top:18px">
    <a class="btn ghost" href="/profile/view/?user=<?= urlencode($nick) ?>">Открыть публичный профиль</a>
    <a class="btn ghost" href="/profile/my/?logout=1">Выйти</a>
  </p>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Защита входа (2FA)</h2>
  <p class="muted">
    Если включить, при входе на сайт нужно будет подтвердить его по QR-коду (отсканировать телефоном или открыть код на /qrverify и нажать «Подтвердить»). Это дополнительный шаг-подтверждение.
  </p>
  <?php if ($twofaMsg): ?><div class="msg ok"><?= h($twofaMsg) ?></div><?php endif; ?>
  <p style="margin-top:10px">
    Сейчас: <span class="badge <?= $twofaOn ? 'on' : '' ?>"><?= $twofaOn ? 'включено' : 'выключено' ?></span>
  </p>
  <form method="post" style="margin-top:8px">
    <input type="hidden" name="act" value="twofa">
    <input type="hidden" name="on" value="<?= $twofaOn ? '0' : '1' ?>">
    <button class="btn <?= $twofaOn ? 'ghost' : '' ?>" type="submit"><?= $twofaOn ? 'Выключить' : 'Включить' ?></button>
  </form>
</div>
<?php require __DIR__ . '/../../_inc/foot.php'; ?>
