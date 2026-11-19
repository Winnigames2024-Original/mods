<?php
require_once __DIR__ . '/../_inc/api.php';
gd_login_required();
if (!gd_is_admin()) { http_response_code(403); gd_head('Админка'); echo '<section class="section"><div class="container"><h2>Только для ' . h(GD_ADMIN) . '</h2></div></section>'; gd_foot(); exit; }

$msg = ''; $err = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
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
        default     => [false, ['error' => 'неизвестное действие']],
    };
    if ($ok) $msg = 'Готово'; else $err = $r['error'] ?? 'Ошибка';
}

[$okU, $users]  = gd_get('/api/admin/users', gd_token());
[$okL, $levels] = gd_get('/api/levels?sort=recent', gd_token());
[$okS, $stats]  = gd_get('/api/admin/stats', gd_token());
gd_head('Админка', 'admin');
?>
<section class="section"><div class="container">
  <h2>Админка</h2>
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
    <h3>Уровни</h3>
    <table class="lv">
      <tr><th>ID</th><th>Название</th><th>Автор</th><th>Сложность / флаги</th><th>Действия</th></tr>
      <?php foreach (array_slice($levels ?: [], 0, 60) as $l): ?>
        <tr>
          <td class="muted">#<?= (int)$l['id'] ?></td>
          <td class="lvname"><a href="/levels/view.php?id=<?= (int)$l['id'] ?>"><?= h($l['name']) ?></a></td>
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
