<?php
// Страница одного уровня: описание, рекорды, комментарии
require_once __DIR__ . '/../_inc/api.php';

$id = (int)($_GET['id'] ?? 0);
if ($id <= 0) { header('Location: /levels/'); exit; }

$msg = null; $err = null;

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['text'])) {
    if (!gd_token()) $err = 'Войди, чтобы писать комментарии';
    else {
        [$ok2, $r] = gd_post("/api/levels/$id/comments", ['text' => trim($_POST['text'])], gd_token());
        if ($ok2) $msg = 'Комментарий добавлен';
        else $err = $r['error'] ?? 'Не отправилось';
    }
}

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['delc'])) {
    [$ok2, $r] = gd_post("/api/levels/$id/comments/delete", ['commentId' => $_POST['delc']], gd_token());
    if ($ok2) $msg = 'Комментарий удалён'; else $err = $r['error'] ?? 'Не вышло удалить';
}

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['like'])) {
    gd_post("/api/levels/$id/like", ['like' => true], gd_token());
    header("Location: /level/?id=$id"); exit;
}

[$ok, $lv] = gd_get("/api/levels/$id");
if (!$ok || !is_array($lv)) { gd_head('Уровень', 'levels'); ?>
  <div class="container"><h1>Уровень не найден</h1>
  <p><a class="btn" href="/levels/">К списку уровней</a></p></div>
<?php gd_foot(); exit; }

[$sok, $scores] = gd_get("/api/levels/$id/scores");
if (!$sok || !is_array($scores)) $scores = [];
[$cok, $comments] = gd_get("/api/levels/$id/comments");
if (!$cok || !is_array($comments)) $comments = [];

$diff = (int)($lv['difficulty'] ?? 0);
gd_head($lv['name'] ?? 'Уровень', 'levels');
?>
<div class="container">
  <h1><?= h($lv['name'] ?? '') ?> <span class="dim">#<?= $id ?></span></h1>
  <p class="lead">автор: <b><?= h($lv['author'] ?? '') ?></b>
     <span class="diff <?= gd_diff_class($diff) ?>"><?= h(gd_diff_name($diff)) ?></span>
     <?php if (!empty($lv['featured'])): ?><span class="badge">FEATURED</span><?php endif; ?>
     <?php if (!empty($lv['epic'])): ?><span class="badge epic">EPIC</span><?php endif; ?>
  </p>

  <?php if ($msg): ?><div class="msg ok"><?= h($msg) ?></div><?php endif; ?>
  <?php if ($err): ?><div class="msg err"><?= h($err) ?></div><?php endif; ?>

  <div class="grid c2">
    <div class="card">
      <h3>Об уровне</h3>
      <?php if (!empty($lv['desc'])): ?><p><?= h($lv['desc']) ?></p><?php endif; ?>
      <div class="flex" style="margin-top:12px">
        <span class="stat">★ <b><?= (int)($lv['stars'] ?? 0) ?></b></span>
        <span class="stat">👍 <b><?= (int)($lv['likes'] ?? 0) ?></b></span>
        <span class="stat">▶ <b><?= (int)($lv['plays'] ?? 0) ?></b></span>
        <span class="stat">⬇ <b><?= (int)($lv['downloads'] ?? 0) ?></b></span>
      </div>
      <p class="dim" style="margin-top:10px">
        музыка: <?= ((int)($lv['songId'] ?? 0)) === 0 ? 'нет'
                   : (((int)$lv['songId']) < 0 ? 'встроенная' : '#' . (int)$lv['songId']) ?>
        <?= !empty($lv['songName']) ? ' · ' . h($lv['songName']) : '' ?>
      </p>
      <p style="margin-top:14px" class="flex">
        <?php if (gd_token()): ?>
          <form method="post"><input type="hidden" name="like" value="1">
            <button class="btn small green" type="submit">НРАВИТСЯ</button></form>
        <?php endif; ?>
        <a class="btn small" href="/api/levels/<?= $id ?>/data">СКАЧАТЬ .GDL</a>
      </p>
      <p class="dim">В игре этот уровень ищется так: <b>CREATOR → SEARCH → <?= $id ?></b></p>
    </div>

    <div class="card">
      <h3>Рекорды</h3>
      <?php if (!$scores): ?>
        <p class="dim">Пока никто не прошёл.</p>
      <?php else: ?>
        <table>
          <thead><tr><th>#</th><th>Игрок</th><th>Процент</th><th>Монеты</th></tr></thead>
          <tbody>
          <?php foreach (array_slice($scores, 0, 20) as $i => $s): ?>
            <tr>
              <td class="rank"><?= $i + 1 ?></td>
              <td><a href="/profile/?name=<?= urlencode($s['name'] ?? '') ?>"><?= h($s['name'] ?? '') ?></a></td>
              <td><b><?= (int)($s['percent'] ?? 0) ?>%</b></td>
              <td class="dim"><?= (int)($s['coins'] ?? 0) ?>/3</td>
            </tr>
          <?php endforeach; ?>
          </tbody>
        </table>
      <?php endif; ?>
    </div>
  </div>

  <div class="panel">
    <h3>Комментарии (<?= count($comments) ?>)</h3>
    <?php if (gd_token()): ?>
      <form method="post" class="flex" style="margin-bottom:16px">
        <input type="text" name="text" maxlength="300" placeholder="Что скажешь об уровне?" required style="flex:1">
        <button class="btn small green" type="submit">ОТПРАВИТЬ</button>
      </form>
    <?php else: ?>
      <p class="dim"><a href="/login/">Войди</a>, чтобы оставить комментарий.</p>
    <?php endif; ?>

    <?php if (!$comments): ?>
      <p class="dim">Комментариев пока нет — будь первым.</p>
    <?php else: foreach ($comments as $c): ?>
      <div class="lv">
        <?php if (!empty($c['avatar'])): ?>
          <img src="/api/images/<?= (int)$c['avatar'] ?>/file" alt=""
               style="width:46px;height:46px;object-fit:cover;border:3px solid #000;border-radius:10px">
        <?php endif; ?>
        <div style="flex:1">
          <div class="lvname"><a href="/profile/?name=<?= urlencode($c['author'] ?? '') ?>"><?= h($c['author'] ?? '') ?></a></div>
          <div><?= h($c['text'] ?? '') ?></div>
          <div class="dim"><?= h(substr((string)($c['created'] ?? ''), 0, 16)) ?></div>
        </div>
        <?php if (gd_user() && (strcasecmp(gd_user(), (string)($c['author'] ?? '')) === 0
                 || strcasecmp(gd_user(), (string)($lv['author'] ?? '')) === 0 || gd_is_admin())): ?>
          <form method="post"><input type="hidden" name="delc" value="<?= h($c['id'] ?? '') ?>">
            <button class="btn small red" type="submit">×</button></form>
        <?php endif; ?>
      </div>
    <?php endforeach; endif; ?>
  </div>

  <p><a class="btn ghost" href="/levels/">← ко всем уровням</a></p>
</div>
<?php gd_foot();
