<?php
require_once __DIR__ . '/../_inc/config.php';
require_login();
$tok = token();

// Проверяем права: берём состояние (needStaff). Если не админ/модератор — 403 от API.
$state = api_post('/admin/state', ['token' => $tok]);
if (empty($state['ok'])) {
    $PAGE_TITLE = 'Админ-панель — NareLauncher';
    require __DIR__ . '/../_inc/head.php';
    echo '<div class="panel"><h1>Нет доступа</h1><p class="muted">Эта страница только для администраторов и модераторов.</p><p><a class="btn ghost" href="/profile/my/">В профиль</a></p></div>';
    require __DIR__ . '/../_inc/foot.php';
    exit;
}

$msg = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $act = $_POST['act'] ?? '';
    if ($act === 'broadcast') {
        $r = api_post('/admin/broadcast', ['token' => $tok, 'text' => $_POST['text'] ?? '', 'seconds' => (int)($_POST['seconds'] ?? 0)]);
        $msg = !empty($r['ok']) ? 'Объявление отправлено.' : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'restart') {
        $r = api_post('/admin/restart', ['token' => $tok, 'what' => $_POST['what'] ?? '']);
        $msg = !empty($r['ok']) ? 'Перезапуск запрошен.' : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'command') {
        $r = api_post('/admin/command', ['token' => $tok, 'text' => $_POST['cmd'] ?? '']);
        $msg = !empty($r['ok']) ? 'Команда поставлена в очередь.' : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'approve') {
        $r = api_post('/admin/server/approve', ['token' => $tok, 'id' => $_POST['id'] ?? '']);
        $msg = !empty($r['ok']) ? 'Сервер одобрен.' : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'reject') {
        $r = api_post('/admin/server/reject', ['token' => $tok, 'id' => $_POST['id'] ?? '', 'reason' => $_POST['reason'] ?? '']);
        $msg = !empty($r['ok']) ? 'Сервер отклонён.' : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'streamban') {
        $r = api_post('/admin/stream/ban', ['token' => $tok, 'nick' => $_POST['nick'] ?? '', 'on' => ($_POST['on'] ?? '') === '1']);
        $msg = !empty($r['ok']) ? ($r['banned'] ? 'Трансляции запрещены игроку.' : 'Запрет на трансляции снят.') : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'apicode') {
        $r = api_post('/admin/apicode', ['token' => $tok, 'regen' => true]);
        $msg = !empty($r['ok']) ? 'Новый API-код сгенерирован.' : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'emupdate') {
        $r = api_post('/admin/update', ['token' => $tok, 'version' => $_POST['version'] ?? '', 'changelog' => $_POST['changelog'] ?? '', 'critical' => ($_POST['critical'] ?? '') === '1']);
        $msg = !empty($r['ok']) ? 'Обновление объявлено всем.' : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'twofaoff') {
        $r = api_post('/admin/twofa/off', ['token' => $tok, 'nick' => $_POST['nick'] ?? '']);
        $msg = !empty($r['ok']) ? ('2FA снята: ' . h($r['cleared'])) : ($r['error'] ?? 'Ошибка');
    }
    // перечитываем состояние после действия
    $state = api_post('/admin/state', ['token' => $tok]);
}
$apiCode = api_post('/admin/apicode', ['token' => $tok])['code'] ?? '';
$srvResp = api_post('/admin/servers', ['token' => $tok]);
$allServers = $srvResp['servers'] ?? [];
$pending = array_values(array_filter($allServers, fn($x) => ($x['status'] ?? '') === 'pending'));
$st = $state['stats'] ?? [];

$PAGE_TITLE = 'Админ-панель — NareLauncher';
$PAGE_DESC  = 'Управление NareLauncher: объявления, перезапуск, модерация серверов.';
require __DIR__ . '/../_inc/head.php';
?>
<h1>Админ-панель</h1>
<?php if ($msg): ?><div class="msg ok"><?= h($msg) ?></div><?php endif; ?>
<p style="margin:10px 0"><a class="btn" href="/admin/servers.php">⚙ Серверы игроков (консоль и управление)</a></p>

<div class="grid" style="margin-top:12px">
  <div class="card"><h3>Аккаунты</h3><p style="font-size:22px"><?= (int)($st['accounts'] ?? 0) ?></p></div>
  <div class="card"><h3>Онлайн</h3><p style="font-size:22px"><?= (int)($st['online'] ?? 0) ?></p></div>
  <div class="card"><h3>Баны</h3><p style="font-size:22px"><?= (int)($st['bans'] ?? 0) ?></p></div>
  <div class="card"><h3>Серверы на модерации</h3><p style="font-size:22px"><?= (int)($st['pendingServers'] ?? 0) ?></p></div>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Объявление всем</h2>
  <p class="muted">Текст появится вверху лаунчера у всех. Ваш ник и время подставятся сами.</p>
  <form method="post" class="row-form">
    <input type="hidden" name="act" value="broadcast">
    <input name="text" placeholder="Текст объявления" maxlength="300" required style="flex:1">
    <input name="seconds" type="number" min="0" max="3600" value="30" title="сколько секунд показывать (0 — пока не уберёте)" style="width:120px">
    <button class="btn" type="submit">Отправить</button>
  </form>
  <form method="post" style="margin-top:8px">
    <input type="hidden" name="act" value="broadcast">
    <input type="hidden" name="text" value="">
    <button class="btn ghost" type="submit">Убрать объявление</button>
  </form>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Перезапуск</h2>
  <div class="btn-row">
    <form method="post"><input type="hidden" name="act" value="restart"><input type="hidden" name="what" value="launchers"><button class="btn ghost" type="submit">Перезапустить лаунчеры</button></form>
    <form method="post"><input type="hidden" name="act" value="restart"><input type="hidden" name="what" value="servers"><button class="btn ghost" type="submit">Перезапустить сервера</button></form>
    <form method="post" onsubmit="return confirm('Перезапустить всё — лаунчеры и сервера?')"><input type="hidden" name="act" value="restart"><input type="hidden" name="what" value="all"><button class="btn" type="submit">Перезапустить всё</button></form>
  </div>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Снять 2FA с игрока</h2>
  <p class="muted">Если игрок (или ты сам) застрял на подтверждении входа — убери у него защиту входа. Можно <code>all</code>, чтобы снять у всех.</p>
  <form method="post" class="row-form">
    <input type="hidden" name="act" value="twofaoff">
    <input name="nick" placeholder="Ник игрока (или all)" maxlength="16" required style="flex:1">
    <button class="btn" type="submit">Снять 2FA</button>
  </form>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Запрет трансляций</h2>
  <p class="muted">Запретить или разрешить игроку вести прямые эфиры.</p>
  <form method="post" class="row-form">
    <input type="hidden" name="act" value="streamban">
    <input name="nick" placeholder="Ник игрока" maxlength="16" required style="flex:1">
    <button class="btn" type="submit" name="on" value="1">Запретить</button>
    <button class="btn ghost" type="submit" name="on" value="0">Разрешить</button>
  </form>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Экстренное обновление всем</h2>
  <p class="muted">Все лаунчеры получат уведомление об обновлении. «Критическое» — обязательное, без него не пустит в игру.</p>
  <form method="post" class="row-form">
    <input type="hidden" name="act" value="emupdate">
    <input name="version" placeholder="Версия (например 1.90)" maxlength="32" required style="width:180px">
    <input name="changelog" placeholder="Что нового" maxlength="400" style="flex:1">
    <label class="muted" style="display:flex;align-items:center;gap:6px"><input type="checkbox" name="critical" value="1"> критическое</label>
    <button class="btn" type="submit">Объявить</button>
  </form>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Управление по URL (API-код)</h2>
  <p class="muted">Секретный код, которым можно перезапустить/обновить всех прямо по ссылке — из закладки или скрипта, без входа. Держите его в тайне.</p>
  <p>Код: <code id="apicode"><?= h($apiCode ?: '—') ?></code>
     <button class="btn ghost" type="button" onclick="navigator.clipboard.writeText(document.getElementById('apicode').textContent)">Скопировать</button></p>
  <form method="post" onsubmit="return confirm('Сменить код? Старые ссылки перестанут работать.')" style="margin-bottom:12px">
    <input type="hidden" name="act" value="apicode"><button class="btn ghost" type="submit">Сгенерировать новый код</button>
  </form>
  <?php $base = SITE; $c = urlencode($apiCode); ?>
  <p class="muted">Готовые ссылки (открой в браузере):</p>
  <ul style="line-height:1.9;word-break:break-all">
    <li>Перезапустить всё: <code><?= h($base) ?>/admin/api/restart?code=<?= $c ?>&what=all</code></li>
    <li>Перезапустить только лаунчеры: <code><?= h($base) ?>/admin/api/restart?code=<?= $c ?>&what=launchers</code></li>
    <li>Перезапустить только серверы: <code><?= h($base) ?>/admin/api/restart?code=<?= $c ?>&what=servers</code></li>
    <li>Экстренное обновление: <code><?= h($base) ?>/admin/api/update?code=<?= $c ?>&version=1.90&critical=1</code></li>
    <li>Объявление: <code><?= h($base) ?>/admin/api/broadcast?code=<?= $c ?>&text=Привет&seconds=30</code></li>
    <li>Снять 2FA (аварийно): <code><?= h($base) ?>/admin/api/twofa?code=<?= $c ?>&nick=all</code></li>
    <li>Закрыть ServerManager у всех: <code><?= h($base) ?>/admin/api/sm?code=<?= $c ?>&nick=all</code></li>
  </ul>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Команда серверу</h2>
  <p class="muted">Выполнится на сервере WMine (например: <code>say привет</code>, <code>weather clear</code>).</p>
  <form method="post" class="row-form">
    <input type="hidden" name="act" value="command">
    <input name="cmd" placeholder="Команда без /" maxlength="300" required style="flex:1">
    <button class="btn" type="submit">Выполнить</button>
  </form>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Заявки на публикацию серверов <?= $pending ? '(' . count($pending) . ')' : '' ?></h2>
  <?php if (!$pending): ?>
    <p class="muted">Нет заявок на модерации.</p>
  <?php else: foreach ($pending as $s): ?>
    <div class="srv">
      <div class="srv-main">
        <div class="srv-ico"><?= h(initial($s['name'] ?? '?')) ?></div>
        <div class="srv-body">
          <div class="srv-name"><?= h($s['name'] ?? '') ?> <?php if (!empty($s['version'])): ?><span class="badge"><?= h($s['version'] . ' ' . ($s['loader'] ?? '')) ?></span><?php endif; ?></div>
          <div class="muted">от @<?= h($s['owner'] ?? '') ?> · <?= !empty($s['hideIp']) ? 'IP скрыт' : h($s['ip'] ?? '') ?></div>
          <?php if (!empty($s['desc'])): ?><div class="srv-desc"><?= h($s['desc']) ?></div><?php endif; ?>
        </div>
      </div>
      <div class="srv-act">
        <form method="post"><input type="hidden" name="act" value="approve"><input type="hidden" name="id" value="<?= h($s['id']) ?>"><button class="btn" type="submit">Одобрить</button></form>
        <form method="post" onsubmit="this.reason.value=prompt('Причина отказа (необязательно):')||'';return true;"><input type="hidden" name="act" value="reject"><input type="hidden" name="id" value="<?= h($s['id']) ?>"><input type="hidden" name="reason" value=""><button class="btn ghost" type="submit">Отклонить</button></form>
      </div>
    </div>
  <?php endforeach; endif; ?>
</div>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
