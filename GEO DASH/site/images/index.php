<?php
// Toolbox: картинки для уровней — поиск, категории, ID для игры
require_once __DIR__ . '/../_inc/api.php';

$msg = null; $err = null; $newId = null;

// ---- загрузка картинки
if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_FILES['pic'])) {
    if (!gd_token()) {
        $err = 'Сначала войди в аккаунт';
    } elseif ($_FILES['pic']['error'] !== UPLOAD_ERR_OK) {
        $err = 'Файл не загрузился (код ' . (int)$_FILES['pic']['error'] . ')';
    } else {
        $bytes = file_get_contents($_FILES['pic']['tmp_name']);
        $name  = trim($_POST['name'] ?? '') ?: pathinfo($_FILES['pic']['name'], PATHINFO_FILENAME);
        $cat   = trim($_POST['category'] ?? '') ?: 'прочее';

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
                'X-Image-Category: ' . rawurlencode($cat),
            ],
        ]);
        $raw = curl_exec($ch);
        curl_close($ch);
        $res = json_decode((string)$raw, true);
        if (!empty($res['ok'])) { $newId = (int)$res['id']; $msg = 'Готово! ID картинки: ' . $newId; }
        else $err = $res['error'] ?? 'Сервер не принял картинку';
    }
}

// ---- удаление и смена категории
if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['del'])) {
    [$dok, $dres] = gd_post('/api/images/' . (int)$_POST['del'] . '/delete', [], gd_token());
    if ($dok) $msg = 'Картинка удалена'; else $err = $dres['error'] ?? 'Не вышло удалить';
}
if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['recat'])) {
    [$eok, $eres] = gd_post('/api/images/' . (int)$_POST['recat'] . '/edit',
        ['category' => trim($_POST['newcat'] ?? '')], gd_token());
    if ($eok) $msg = 'Категория изменена'; else $err = $eres['error'] ?? 'Не вышло';
}

$q   = trim($_GET['q'] ?? '');
$cat = trim($_GET['category'] ?? '');

$url = '/api/images?limit=300';
if ($q !== '')   $url .= '&q=' . urlencode($q);
if ($cat !== '') $url .= '&category=' . urlencode($cat);

[$ok, $images] = gd_get($url);
if (!$ok || !is_array($images)) $images = [];
[$cok, $cats] = gd_get('/api/images/categories');
if (!$cok || !is_array($cats)) $cats = [];

gd_head('Toolbox', 'images');
?>
<div class="container">
  <h1>Toolbox</h1>
  <p class="lead">Картинки для уровней: ищи по названию или категории, запоминай <b>ID</b> —
     в игре он вводится в редакторе (<b>BUILD → Картинки → С САЙТА</b>) и картинка встаёт в уровень.
     Игра скачивает её один раз и дальше показывает офлайн.</p>

  <?php if ($msg): ?><div class="msg ok"><?= h($msg) ?></div><?php endif; ?>
  <?php if ($err): ?><div class="msg err"><?= h($err) ?></div><?php endif; ?>

  <!-- поиск -->
  <div class="panel">
    <form method="get" class="flex">
      <input type="search" name="q" value="<?= h($q) ?>" placeholder="название, автор, категория или ID" style="flex:1">
      <?php if ($cat !== ''): ?><input type="hidden" name="category" value="<?= h($cat) ?>"><?php endif; ?>
      <button class="btn small" type="submit">ИСКАТЬ</button>
      <?php if ($q !== '' || $cat !== ''): ?>
        <a class="btn small ghost" href="/images/">СБРОС</a>
      <?php endif; ?>
    </form>

    <div class="flex" style="margin-top:14px">
      <a class="btn small <?= $cat === '' ? '' : 'ghost' ?>"
         href="/images/<?= $q !== '' ? '?q=' . urlencode($q) : '' ?>">Все<?php
         $total = 0; foreach ($cats as $c) $total += (int)($c['count'] ?? 0); ?> (<?= $total ?>)</a>
      <?php foreach ($cats as $c): $cn = (string)($c['name'] ?? ''); ?>
        <a class="btn small <?= strcasecmp($cn, $cat) === 0 ? '' : 'ghost' ?>"
           href="/images/?category=<?= urlencode($cn) ?><?= $q !== '' ? '&q=' . urlencode($q) : '' ?>">
          <?= h($cn) ?> (<?= (int)($c['count'] ?? 0) ?>)
        </a>
      <?php endforeach; ?>
    </div>
  </div>

  <!-- загрузка -->
  <div class="panel">
    <h3>Залить свою картинку</h3>
    <?php if (gd_token()): ?>
      <form method="post" enctype="multipart/form-data">
        <div class="form-row">
          <div>
            <label>Название</label>
            <input type="text" name="name" placeholder="например: кирпичная стена">
          </div>
          <div>
            <label>Категория</label>
            <input type="text" name="category" list="cats" placeholder="фон, блоки, декор, логотип…">
            <datalist id="cats">
              <?php foreach ($cats as $c): ?><option value="<?= h((string)($c['name'] ?? '')) ?>"><?php endforeach; ?>
            </datalist>
          </div>
          <div>
            <label>Файл (png, jpg, gif — до 8 МБ)</label>
            <input type="file" name="pic" accept="image/png,image/jpeg,image/gif" required>
          </div>
        </div>
        <p style="margin-top:16px"><button class="btn green" type="submit">ЗАЛИТЬ</button></p>
      </form>
    <?php else: ?>
      <p>Чтобы заливать картинки, <a href="/login/">войди в аккаунт</a>.</p>
    <?php endif; ?>
  </div>

  <!-- сетка картинок -->
  <div class="panel">
    <h3>
      <?= $cat !== '' ? 'Категория «' . h($cat) . '»' : 'Все картинки' ?>
      <?= $q !== '' ? ' · поиск «' . h($q) . '»' : '' ?>
      (<?= count($images) ?>)
    </h3>

    <?php if (!$images): ?>
      <p class="dim">Ничего не нашлось. Попробуй другой запрос или залей свою картинку.</p>
    <?php else: ?>
      <div class="grid c3">
        <?php foreach ($images as $i): $id = (int)($i['id'] ?? 0); ?>
          <div class="card">
            <img src="/api/images/<?= $id ?>/file" alt="" loading="lazy"
                 style="width:100%;height:150px;object-fit:contain;border:3px solid #000;border-radius:12px;background:#120e30">
            <h3 style="margin-top:12px"><?= h($i['name'] ?? '') ?></h3>
            <p class="flex" style="gap:8px;margin:6px 0">
              <span class="badge">ID #<?= $id ?></span>
              <a class="btn small ghost" href="/images/?category=<?= urlencode((string)($i['category'] ?? '')) ?>">
                <?= h((string)($i['category'] ?? 'прочее')) ?></a>
            </p>
            <p class="dim"><?= h($i['author'] ?? '') ?> · <?= number_format(((int)($i['size'] ?? 0)) / 1024, 0, ',', ' ') ?> КБ</p>

            <?php if (gd_user() && (strcasecmp(gd_user(), (string)($i['author'] ?? '')) === 0 || gd_is_admin())): ?>
              <form method="post" class="flex" style="gap:6px;margin-top:8px">
                <input type="hidden" name="recat" value="<?= $id ?>">
                <input type="text" name="newcat" placeholder="новая категория" style="flex:1">
                <button class="btn small" type="submit">OK</button>
              </form>
              <form method="post" onsubmit="return confirm('Удалить картинку?')" style="margin-top:6px">
                <input type="hidden" name="del" value="<?= $id ?>">
                <button class="btn small red" type="submit">Удалить</button>
              </form>
            <?php endif; ?>
          </div>
        <?php endforeach; ?>
      </div>
    <?php endif; ?>
  </div>

  <div class="card">
    <h3>Как поставить картинку в уровень</h3>
    <ol>
      <li>Запомни ID из карточки выше, например <b>#12</b>.</li>
      <li>В игре: редактор → <b>BUILD</b> → вкладка <b>Картинки</b> → кнопка <b>С САЙТА</b>.</li>
      <li>Выбери картинку из списка (или введи ID вручную) и поставь её кликом по полю.</li>
      <li>Размер и поворот меняются в <b>ПРАВИТЬ ОБЪЕКТ</b>, там же можно сменить ID картинки.</li>
    </ol>
  </div>
</div>
<?php gd_foot();
