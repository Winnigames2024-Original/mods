<?php
// Музыка: залить свой трек и взять его ID для уровня
require_once __DIR__ . '/../_inc/api.php';

$msg = null; $err = null; $newId = null;

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_FILES['song'])) {
    if (!gd_token()) {
        $err = 'Сначала войди в аккаунт';
    } elseif ($_FILES['song']['error'] !== UPLOAD_ERR_OK) {
        $err = 'Файл не загрузился (код ' . (int)$_FILES['song']['error'] . ')';
    } else {
        $bytes = file_get_contents($_FILES['song']['tmp_name']);
        $name  = trim($_POST['name'] ?? '') ?: pathinfo($_FILES['song']['name'], PATHINFO_FILENAME);

        $ch = curl_init(GD_API . '/api/music/upload');
        curl_setopt_array($ch, [
            CURLOPT_POST => true,
            CURLOPT_POSTFIELDS => $bytes,
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_TIMEOUT => 120,
            CURLOPT_HTTPHEADER => [
                'Authorization: Bearer ' . gd_token(),
                'Content-Type: application/octet-stream',
                'X-Song-Name: ' . rawurlencode($name),
                'X-Song-Artist: ' . rawurlencode(gd_user() ?? ''),
            ],
        ]);
        $raw = curl_exec($ch);
        curl_close($ch);
        $res = json_decode((string)$raw, true);
        if (!empty($res['ok'])) { $newId = (int)$res['id']; $msg = 'Готово! ID трека: ' . $newId; }
        else $err = $res['error'] ?? 'Сервер не принял файл';
    }
}

[$ok, $songs] = gd_get('/api/music');
if (!$ok || !is_array($songs)) $songs = [];

gd_head('Музыка', 'music');
?>
<div class="container">
  <h1>Музыка</h1>
  <p class="lead">Залей свой трек — он получит ID, и этот ID можно поставить уровню в настройках
     или включить триггером «музыка» прямо посреди уровня. Чужие треки с Newgrounds тоже работают по их ID.</p>

  <?php if ($msg): ?><div class="msg ok"><?= h($msg) ?></div><?php endif; ?>
  <?php if ($err): ?><div class="msg err"><?= h($err) ?></div><?php endif; ?>

  <div class="panel">
    <h3>Залить свой трек</h3>
    <?php if (gd_token()): ?>
      <form method="post" enctype="multipart/form-data">
        <div class="form-row">
          <div>
            <label>Название</label>
            <input type="text" name="name" placeholder="Например: Neon Rush">
          </div>
          <div>
            <label>Файл mp3 (до 40 МБ)</label>
            <input type="file" name="song" accept=".mp3,audio/mpeg" required>
          </div>
        </div>
        <p style="margin-top:16px"><button class="btn green" type="submit">ЗАЛИТЬ ТРЕК</button></p>
      </form>
    <?php else: ?>
      <p>Чтобы заливать музыку, <a href="/login/">войди в аккаунт</a>.</p>
    <?php endif; ?>
  </div>

  <?php if ($newId): ?>
    <div class="card">
      <h3>Как поставить этот трек в уровень</h3>
      <p>В игре: <b>Редактор → НАСТРОЙКИ УРОВНЯ → МУЗЫКА</b> и ввести ID <b><?= $newId ?></b>,
         либо поставить триггер «музыка» с этим же ID в нужном месте уровня.</p>
      <p><a class="btn small" href="/api/music/<?= $newId ?>/file">Послушать</a></p>
    </div>
  <?php endif; ?>

  <div class="panel">
    <h3>Треки на сервере (<?= count($songs) ?>)</h3>
    <?php if (!$songs): ?>
      <p class="dim">Пока пусто.</p>
    <?php else: ?>
      <table>
        <thead><tr><th>ID</th><th>Название</th><th>Кто залил</th><th>Размер</th><th></th></tr></thead>
        <tbody>
        <?php foreach ($songs as $s): ?>
          <tr>
            <td class="rank">#<?= (int)($s['id'] ?? 0) ?></td>
            <td><?= h($s['name'] ?? '') ?></td>
            <td><?= h($s['artist'] ?? '') ?></td>
            <td class="dim"><?= number_format(((int)($s['size'] ?? 0)) / 1048576, 1, ',', ' ') ?> МБ</td>
            <td><a class="btn small ghost" href="/api/music/<?= (int)($s['id'] ?? 0) ?>/file">Слушать</a></td>
          </tr>
        <?php endforeach; ?>
        </tbody>
      </table>
    <?php endif; ?>
  </div>
</div>
<?php gd_foot();
