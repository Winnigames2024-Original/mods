<?php
// Toolbox: картинки, которые можно ставить в уровень объектом «картинка»
require_once __DIR__ . '/../_inc/api.php';

$msg = null; $err = null; $newId = null;

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_FILES['pic'])) {
    if (!gd_token()) {
        $err = 'Сначала войди в аккаунт';
    } elseif ($_FILES['pic']['error'] !== UPLOAD_ERR_OK) {
        $err = 'Файл не загрузился (код ' . (int)$_FILES['pic']['error'] . ')';
    } else {
        $bytes = file_get_contents($_FILES['pic']['tmp_name']);
        $name  = trim($_POST['name'] ?? '') ?: pathinfo($_FILES['pic']['name'], PATHINFO_FILENAME);

        $ch = curl_init(GD_API . '/api/images/upload');
        curl_setopt_array($ch, [
            CURLOPT_POST => true,
            CURLOPT_POSTFIELDS => $bytes,
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_TIMEOUT => 60,
            CURLOPT_HTTPHEADER => [
                'Authorization: Bearer ' . gd_token(),
                'Content-Type: application/octet-stream',
                'X-Image-Name: ' . rawurlencode($name),
            ],
        ]);
        $raw = curl_exec($ch);
        curl_close($ch);
        $res = json_decode((string)$raw, true);
        if (!empty($res['ok'])) { $newId = (int)$res['id']; $msg = 'Готово! ID картинки: ' . $newId; }
        else $err = $res['error'] ?? 'Сервер не принял картинку';
    }
}

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['del'])) {
    [$dok, $dres] = gd_post('/api/images/' . (int)$_POST['del'] . '/delete', [], gd_token());
    if ($dok) $msg = 'Картинка удалена'; else $err = $dres['error'] ?? 'Не вышло удалить';
}

[$ok, $images] = gd_get('/api/images');
if (!$ok || !is_array($images)) $images = [];

gd_head('Картинки', 'images');
?>
<div class="container">
  <h1>Картинки (toolbox)</h1>
  <p class="lead">Залей png, jpg или gif — картинка получит ID, и её можно поставить в уровень
     объектом «картинка» из вкладки <b>Картинки</b> в редакторе. Игра скачает её сама и будет
     показывать даже без интернета.</p>

  <?php if ($msg): ?><div class="msg ok"><?= h($msg) ?></div><?php endif; ?>
  <?php if ($err): ?><div class="msg err"><?= h($err) ?></div><?php endif; ?>

  <div class="panel">
    <h3>Залить картинку</h3>
    <?php if (gd_token()): ?>
      <form method="post" enctype="multipart/form-data">
        <div class="form-row">
          <div>
            <label>Название</label>
            <input type="text" name="name" placeholder="Например: логотип">
          </div>
          <div>
            <label>Файл (до 8 МБ)</label>
            <input type="file" name="pic" accept="image/png,image/jpeg,image/gif" required>
          </div>
        </div>
        <p style="margin-top:16px"><button class="btn green" type="submit">ЗАЛИТЬ КАРТИНКУ</button></p>
      </form>
    <?php else: ?>
      <p>Чтобы заливать картинки, <a href="/login/">войди в аккаунт</a>.</p>
    <?php endif; ?>
  </div>

  <?php if ($newId): ?>
    <div class="card">
      <h3>Как поставить её в уровень</h3>
      <p>В редакторе: <b>BUILD → вкладка «Картинки» → КАРТИНКА С САЙТА</b> и ввести ID <b><?= $newId ?></b>.</p>
    </div>
  <?php endif; ?>

  <div class="panel">
    <h3>Все картинки (<?= count($images) ?>)</h3>
    <?php if (!$images): ?>
      <p class="dim">Пока пусто.</p>
    <?php else: ?>
      <div class="grid c3">
        <?php foreach ($images as $i): $id = (int)($i['id'] ?? 0); ?>
          <div class="card">
            <img src="/api/images/<?= $id ?>/file" alt=""
                 style="width:100%;height:150px;object-fit:contain;border:3px solid #000;border-radius:12px;background:#120e30">
            <h3 style="margin-top:12px"><?= h($i['name'] ?? '') ?></h3>
            <p class="dim">ID <b>#<?= $id ?></b> · <?= h($i['author'] ?? '') ?>
               · <?= number_format(((int)($i['size'] ?? 0)) / 1024, 0, ',', ' ') ?> КБ</p>
            <?php if (gd_user() && (strcasecmp(gd_user(), (string)($i['author'] ?? '')) === 0 || gd_is_admin())): ?>
              <form method="post" onsubmit="return confirm('Удалить картинку?')">
                <input type="hidden" name="del" value="<?= $id ?>">
                <button class="btn small red" type="submit">Удалить</button>
              </form>
            <?php endif; ?>
          </div>
        <?php endforeach; ?>
      </div>
    <?php endif; ?>
  </div>
</div>
<?php gd_foot();
