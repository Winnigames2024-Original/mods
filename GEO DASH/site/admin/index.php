<?php
require_once __DIR__ . '/../_inc/api.php';
require_once __DIR__ . '/../_inc/pay.php';
gd_login_required();
if (!gd_is_admin()) { http_response_code(403); gd_head('Админка'); echo '<section class="section"><div class="container"><h2>Только для ' . h(GD_ADMIN) . '</h2></div></section>'; gd_foot(); exit; }

$msg = ''; $err = '';

// загрузка mp3 для ID (если Newgrounds не отдаёт трек серверу)
if ($_SERVER['REQUEST_METHOD'] === 'POST' && ($_POST['act'] ?? '') === 'song' && !empty($_FILES['song']['tmp_name'])) {
    $sid = (int)($_POST['songid'] ?? 0);
    if ($sid <= 0) $err = 'Укажи ID трека';
    else {
        $bin = file_get_contents($_FILES['song']['tmp_name']);
        $ch = curl_init(GD_API . "/api/music/$sid/upload");
        curl_setopt_array($ch, [
            CURLOPT_POST => true,
            CURLOPT_POSTFIELDS => $bin,
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_TIMEOUT => 120,
            CURLOPT_HTTPHEADER => ['Content-Type: audio/mpeg', 'Authorization: Bearer ' . gd_token()],
        ]);
        $raw = curl_exec($ch);
        $code = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);
        if ($code >= 200 && $code < 300) $msg = "Трек #$sid загружен (" . number_format(strlen($bin) / 1048576, 1) . " МБ)";
        else $err = 'Не вышло загрузить трек: ' . h((string)$raw);
    }
}

if ($_SERVER['REQUEST_METHOD'] === 'POST' && ($_POST['act'] ?? '') !== 'song') {
    $act = $_POST['act'] ?? '';
    $id  = (int)($_POST['id'] ?? 0);
    $name = trim($_POST['name'] ?? '');
    [$ok, $r] = match ($act) {
        'ban'       => gd_post('/api/admin/ban', ['name' => $name, 'ban' => true], gd_token()),
        'unban'     => gd_post('/api/admin/ban', ['name' => $name, 'ban' => false], gd_token()),
        'deluser'   => gd_post('/api/admin/delete-user', ['name' => $name], gd_token()),
        'rate'      => gd_post('/api/admin/rate', ['id' => $id, 'difficulty' => (int)($_POST['difficulty'] ?? 0),
                                'featured' => !empty($_POST['featured']), 'epic' => !empty($_POST['epic'])], gd_token()),
        'daily'     => gd_post('/api/admin/daily', ['id' => $id], gd_token()),
        'weekly'    => gd_post('/api/admin/weekly', ['id' => $id], gd_token()),
        'promote'   => gd_post('/api/admin/daily-to-weekly', [], gd_token()),
        'offon'     => gd_post('/api/admin/offline', ['id' => $id, 'offline' => true], gd_token()),
        'offoff'    => gd_post('/api/admin/offline', ['id' => $id, 'offline' => false], gd_token()),
        'dellevel'  => gd_post("/api/levels/$id/delete", [], gd_token()),
        'news'      => gd_post('/api/admin/broadcast', ['text' => $_POST['text'] ?? ''], gd_token()),
        'newkeys'   => gd_post('/api/admin/licence/new', ['count' => (int)($_POST['count'] ?? 1),
                                'note' => trim($_POST['note'] ?? '')], gd_token()),
        'grantkey'  => gd_post('/api/admin/licence/grant', ['name' => $name,
                                'note' => 'выдано из админки'], gd_token()),
        'revoke'    => gd_post('/api/admin/licence/revoke', ['key' => trim($_POST['key'] ?? ''),
                                'revoked' => true], gd_token()),
        'unrevoke'  => gd_post('/api/admin/licence/revoke', ['key' => trim($_POST['key'] ?? ''),
                                'revoked' => false], gd_token()),
        default     => [false, ['error' => 'неизвестное действие']],
    };
    if ($ok) {
        $msg = 'Готово';
        // новые ключи показываем сразу — их нужно скопировать и отдать игроку
        if (!empty($r['keys'])) $msg = 'Выдано: ' . implode('   ', (array)$r['keys']);
        elseif (!empty($r['key'])) $msg = 'Ключ ' . $r['key'] . (!empty($r['licensed']) ? ' — активирован на ' . $name : '');
    } else $err = $r['error'] ?? 'Ошибка';
}

[$okU, $users]  = gd_get('/api/admin/users', gd_token());
[$okL, $levels] = gd_get('/api/levels?sort=recent', gd_token());
[$okS, $stats]  = gd_get('/api/admin/stats', gd_token());
[$okK, $lic]    = gd_get('/api/admin/licences', gd_token());
gd_head('Админка', 'admin');
?>
<section class="section"><div class="container">
  <h2>Админка</h2>
  <p class="muted" style="margin-top:-8px">Как всё устроено, ключи, оплата и обновления —
    <a href="/admin/docs.php"><b>документация админа</b></a>.</p>
  <?php if ($msg): ?><div class="alert ok"><?= h($msg) ?></div><?php endif; ?>
  <?php if ($err): ?><div class="alert err"><?= h($err) ?></div><?php endif; ?>

  <?php if ($okS && $stats): ?>
  <div class="stats" style="margin-bottom:20px">
    <div class="stat"><b><?= (int)$stats['users'] ?></b><span>игроков</span></div>
    <div class="stat"><b><?= (int)$stats['levels'] ?></b><span>уровней</span></div>
    <div class="stat"><b><?= (int)$stats['songs'] ?></b><span>треков в кэше</span></div>
    <div class="stat"><b><?= (int)($stats['uptime'] ?? 0) ?>с</b><span>аптайм API</span></div>
  </div>
  <?php endif; ?>

  <div class="card" style="margin-bottom:18px">
    <h3>Быстрые действия</h3>
    <form method="post" style="display:inline"><input type="hidden" name="act" value="promote">
      <button class="btn small gold" type="submit">DAILY → WEEKLY</button></form>
    <form method="post" class="flex" style="margin-top:12px;max-width:640px;gap:10px">
      <input type="hidden" name="act" value="news">
      <input type="text" name="text" placeholder="объявление для всех игроков">
      <button class="btn small" type="submit">ОТПРАВИТЬ</button>
    </form>
  </div>

  <div class="card" style="margin-bottom:18px">
    <h3>Лицензионные ключи</h3>
    <?php if ($okK && is_array($lic)): ?>
      <div class="stats" style="margin:0 0 14px">
        <div class="stat"><b><?= (int)($lic['total'] ?? 0) ?></b><span>всего</span></div>
        <div class="stat"><b><?= (int)($lic['used'] ?? 0) ?></b><span>активировано</span></div>
        <div class="stat"><b><?= (int)($lic['free'] ?? 0) ?></b><span>свободно</span></div>
      </div>
    <?php endif; ?>

    <div class="flex" style="gap:18px;flex-wrap:wrap;align-items:flex-end">
      <form method="post" class="flex" style="gap:8px">
        <input type="hidden" name="act" value="newkeys">
        <input type="number" name="count" value="1" min="1" max="50" style="max-width:90px" title="сколько ключей">
        <input type="text" name="note" placeholder="пометка: кому и за что" style="max-width:260px">
        <button class="btn small gold" type="submit">ВЫДАТЬ КЛЮЧИ</button>
      </form>
      <form method="post" class="flex" style="gap:8px">
        <input type="hidden" name="act" value="grantkey">
        <input type="text" name="name" placeholder="игрок" style="max-width:200px" required>
        <button class="btn small" type="submit">ОТКРЫТЬ ИГРУ ИГРОКУ</button>
      </form>
    </div>
    <p class="muted" style="margin-top:12px">
      Ту же выдачу можно получить ссылкой, без входа в админку:
      <code>/api.php?action=getlicencekey&amp;value=key:АДМИНКЛЮЧ;format:json;</code> —
      см. <a href="/admin/docs.php">документацию</a>.
    </p>
    <p style="margin-top:6px">
      <?php $payOk = gd_pay_ready(); ?>
      <a class="btn small <?= $payOk ? 'ghost' : 'gold' ?>" href="/admin/pay.php">
        ОПЛАТА: <?= h(gd_pay_name()) ?><?= $payOk ? '' : ' — НАСТРОИТЬ' ?></a>
      <span class="muted">продажа ключей на сайте: <b><?= $payOk ? 'включена' : 'выключена' ?></b></span>
    </p>

    <?php if ($okK && !empty($lic['licences'])): ?>
      <table class="lv" style="margin-top:14px">
        <tr><th>Ключ</th><th>Выдан</th><th>Кому</th><th>Пометка</th><th></th></tr>
        <?php foreach (array_slice($lic['licences'], 0, 80) as $k): ?>
          <tr>
            <td class="lvname"><code><?= h($k['key']) ?></code></td>
            <td class="muted"><?= h(substr((string)$k['created'], 0, 16)) ?></td>
            <td><?= $k['user'] ? '<a href="/profile/?user=' . urlencode($k['user']) . '">' . h($k['user']) . '</a>'
                              : '<span class="muted">свободен</span>' ?></td>
            <td class="muted"><?= h($k['note'] ?? '') ?><?= !empty($k['order']) ? ' · заказ ' . h($k['order']) : '' ?></td>
            <td>
              <form method="post">
                <input type="hidden" name="act" value="<?= !empty($k['revoked']) ? 'unrevoke' : 'revoke' ?>">
                <input type="hidden" name="key" value="<?= h($k['key']) ?>">
                <button class="btn small <?= !empty($k['revoked']) ? 'ghost' : 'red' ?>" type="submit">
                  <?= !empty($k['revoked']) ? 'Вернуть' : 'Отозвать' ?></button>
              </form>
            </td>
          </tr>
        <?php endforeach; ?>
      </table>
    <?php endif; ?>
  </div>

  <div class="card" style="margin-bottom:18px">
    <h3>Музыка по ID</h3>
    <p class="muted">Сервер сам качает треки с Newgrounds по ID. Если он их не отдаёт (403 с серверных IP) —
      загрузи mp3 вручную: он ляжет в библиотеку сервера и будет раздаваться игре как <code>/songs/&lt;id&gt;.mp3</code>.</p>
    <form method="post" enctype="multipart/form-data" class="flex" style="gap:10px;margin-top:10px;max-width:640px">
      <input type="hidden" name="act" value="song">
      <input type="number" name="songid" placeholder="ID трека" style="max-width:160px" required>
      <input type="file" name="song" accept="audio/mpeg,.mp3" required>
      <button class="btn small gold" type="submit">ЗАГРУЗИТЬ</button>
    </form>
    <?php
    [$okM, $songs] = gd_get('/api/music');
    if ($okM && $songs): ?>
      <p class="muted" style="margin-top:12px">В библиотеке: <?= count($songs) ?> шт. —
        <?php foreach (array_slice($songs, 0, 20) as $s): ?>
          <a href="/songs/<?= (int)$s['id'] ?>.mp3">#<?= (int)$s['id'] ?></a>
        <?php endforeach; ?>
      </p>
    <?php endif; ?>
  </div>

  <div class="card" style="margin-bottom:18px">
    <h3>Уровни</h3>
    <table class="lv">
      <tr><th>ID</th><th>Название</th><th>Автор</th><th>Сложность / флаги</th><th>Действия</th></tr>
      <?php foreach (array_slice($levels ?: [], 0, 60) as $l): ?>
        <tr>
          <td class="muted">#<?= (int)$l['id'] ?></td>
          <td class="lvname"><a href="/level/?id=<?= (int)$l['id'] ?>"><?= h($l['name']) ?></a></td>
          <td><?= h($l['author']) ?></td>
          <td>
            <form method="post" class="flex" style="gap:6px">
              <input type="hidden" name="act" value="rate"><input type="hidden" name="id" value="<?= (int)$l['id'] ?>">
              <select name="difficulty" style="width:150px">
                <?php foreach (GD_DIFFS as $i => $d): ?>
                  <option value="<?= $i ?>"<?= (int)$l['difficulty'] === $i ? ' selected' : '' ?>><?= h($d) ?></option>
                <?php endforeach; ?>
              </select>
              <label style="margin:0"><input type="checkbox" name="featured"<?= !empty($l['featured'])?' checked':'' ?>> F</label>
              <label style="margin:0"><input type="checkbox" name="epic"<?= !empty($l['epic'])?' checked':'' ?>> E</label>
              <button class="btn small" type="submit">OK</button>
            </form>
          </td>
          <td class="flex" style="gap:6px">
            <form method="post"><input type="hidden" name="act" value="daily"><input type="hidden" name="id" value="<?= (int)$l['id'] ?>"><button class="btn small ghost" type="submit">Daily</button></form>
            <form method="post"><input type="hidden" name="act" value="weekly"><input type="hidden" name="id" value="<?= (int)$l['id'] ?>"><button class="btn small ghost" type="submit">Weekly</button></form>
            <form method="post"><input type="hidden" name="act" value="<?= !empty($l['offline']) ? 'offoff' : 'offon' ?>"><input type="hidden" name="id" value="<?= (int)$l['id'] ?>">
              <button class="btn small <?= !empty($l['offline']) ? 'gold' : 'ghost' ?>" type="submit">Offline</button></form>
            <form method="post" onsubmit="return confirm('Удалить уровень?')"><input type="hidden" name="act" value="dellevel"><input type="hidden" name="id" value="<?= (int)$l['id'] ?>"><button class="btn small red" type="submit">×</button></form>
          </td>
        </tr>
      <?php endforeach; ?>
    </table>
  </div>

  <div class="card">
    <h3>Игроки</h3>
    <table class="lv">
      <tr><th>Игрок</th><th>★</th><th>Монеты</th><th>Демоны</th><th>Статус</th><th>Действия</th></tr>
      <?php foreach ($users ?: [] as $u): ?>
        <tr>
          <td class="lvname"><a href="/profile/?user=<?= urlencode($u['name']) ?>"><?= h($u['name']) ?></a></td>
          <td><?= (int)$u['stars'] ?></td><td><?= (int)$u['coins'] ?></td><td><?= (int)$u['demons'] ?></td>
          <td><?= !empty($u['banned']) ? '<span class="badge" style="background:#5c1d28">бан</span>' : (!empty($u['admin']) ? '<span class="badge">админ</span>' : 'ок') ?></td>
          <td class="flex" style="gap:6px">
            <form method="post"><input type="hidden" name="act" value="<?= !empty($u['banned']) ? 'unban' : 'ban' ?>"><input type="hidden" name="name" value="<?= h($u['name']) ?>">
              <button class="btn small ghost" type="submit"><?= !empty($u['banned']) ? 'Разбан' : 'Бан' ?></button></form>
            <form method="post" onsubmit="return confirm('Удалить игрока и его уровни?')"><input type="hidden" name="act" value="deluser"><input type="hidden" name="name" value="<?= h($u['name']) ?>">
              <button class="btn small red" type="submit">×</button></form>
          </td>
        </tr>
      <?php endforeach; ?>
    </table>
  </div>
</div></section>
<?php gd_foot();
