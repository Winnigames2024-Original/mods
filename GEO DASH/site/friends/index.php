<?php
require_once __DIR__ . '/../_inc/api.php';
gd_login_required();
$msg = ''; $err = '';

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $act  = $_POST['act'] ?? '';
    $name = trim($_POST['name'] ?? '');
    $map = [
        'send'    => '/api/friends/request',
        'accept'  => '/api/friends/accept',
        'decline' => '/api/friends/decline',
        'remove'  => '/api/friends/remove',
    ];
    if (isset($map[$act]) && $name !== '') {
        [$ok, $r] = gd_post($map[$act], ['name' => $name], gd_token());
        if ($ok) $msg = match ($act) {
            'send' => 'Запрос отправлен игроку ' . $name,
            'accept' => 'Теперь вы друзья с ' . $name,
            'decline' => 'Запрос от ' . $name . ' отклонён',
            default => $name . ' удалён из друзей',
        };
        else $err = $r['error'] ?? 'Не вышло';
    }
}

[$okF, $friends]  = gd_get('/api/friends', gd_token());
[$okR, $requests] = gd_get('/api/friends/requests', gd_token());
$me = gd_user();
$incoming = array_filter($requests ?: [], fn($x) => strcasecmp($x['to'] ?? '', $me) === 0);
$outgoing = array_filter($requests ?: [], fn($x) => strcasecmp($x['from'] ?? '', $me) === 0);

gd_head('Друзья', 'friends');
?>
<section class="section"><div class="container">
  <h2>Друзья</h2>
  <?php if ($msg): ?><div class="alert ok"><?= h($msg) ?></div><?php endif; ?>
  <?php if ($err): ?><div class="alert err"><?= h($err) ?></div><?php endif; ?>

  <div class="card" style="margin-bottom:18px">
    <h3>Отправить запрос</h3>
    <form method="post" class="flex" style="gap:10px;max-width:560px">
      <input type="hidden" name="act" value="send">
      <input type="text" name="name" placeholder="имя игрока" required>
      <button class="btn small green" type="submit">ОТПРАВИТЬ</button>
    </form>
  </div>

  <div class="grid c2">
    <div class="card">
      <h3>Входящие запросы (<?= count($incoming) ?>)</h3>
      <?php foreach ($incoming as $x): ?>
        <div class="flex" style="padding:8px 0;border-bottom:1px solid #ffffff14">
          <b><?= h($x['from']) ?></b>
          <span class="right"></span>
          <form method="post" style="display:inline">
            <input type="hidden" name="act" value="accept"><input type="hidden" name="name" value="<?= h($x['from']) ?>">
            <button class="btn small green" type="submit">Принять</button>
          </form>
          <form method="post" style="display:inline">
            <input type="hidden" name="act" value="decline"><input type="hidden" name="name" value="<?= h($x['from']) ?>">
            <button class="btn small red" type="submit">Отклонить</button>
          </form>
        </div>
      <?php endforeach; ?>
      <?php if (!$incoming): ?><p class="muted">Нет входящих запросов.</p><?php endif; ?>

      <h3 style="margin-top:18px">Исходящие (<?= count($outgoing) ?>)</h3>
      <?php foreach ($outgoing as $x): ?>
        <div class="flex" style="padding:6px 0"><span class="muted">→ <?= h($x['to']) ?></span></div>
      <?php endforeach; ?>
      <?php if (!$outgoing): ?><p class="muted">Нет исходящих.</p><?php endif; ?>
    </div>

    <div class="card">
      <h3>Мои друзья (<?= count($friends ?: []) ?>)</h3>
      <?php foreach ($friends ?: [] as $f): ?>
        <div class="flex" style="padding:8px 0;border-bottom:1px solid #ffffff14">
          <a href="/profile/?user=<?= urlencode($f['name']) ?>"><b><?= h($f['name']) ?></b></a>
          <span class="muted">★ <?= (int)$f['stars'] ?></span>
          <span class="right"></span>
          <form method="post" style="display:inline">
            <input type="hidden" name="act" value="remove"><input type="hidden" name="name" value="<?= h($f['name']) ?>">
            <button class="btn small ghost" type="submit">Удалить</button>
          </form>
        </div>
      <?php endforeach; ?>
      <?php if (!$friends): ?><p class="muted">Пока никого.</p><?php endif; ?>
    </div>
  </div>
</div></section>
<?php gd_foot();
