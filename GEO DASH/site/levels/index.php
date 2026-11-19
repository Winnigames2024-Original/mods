<?php
require_once __DIR__ . '/../_inc/api.php';
$q    = trim($_GET['q'] ?? '');
$sort = $_GET['sort'] ?? 'recent';
$diff = isset($_GET['diff']) && $_GET['diff'] !== '' ? (int)$_GET['diff'] : -1;
$page = max(0, (int)($_GET['page'] ?? 0));

[$ok, $levels] = gd_get('/api/levels?q=' . urlencode($q) . '&sort=' . urlencode($sort) .
                        '&diff=' . $diff . '&page=' . $page);
gd_head('Уровни', 'levels');
?>
<section class="section"><div class="container">
  <h2>Уровни игроков</h2>
  <form class="card flex" method="get" style="gap:12px;align-items:flex-end;margin-bottom:18px">
    <div style="flex:1;min-width:200px"><label>Поиск</label>
      <input type="text" name="q" value="<?= h($q) ?>" placeholder="название или #id"></div>
    <div><label>Сортировка</label>
      <select name="sort">
        <?php foreach (['recent'=>'Новые','popular'=>'Популярные','top'=>'Лучшие','featured'=>'Featured','epic'=>'Epic'] as $k=>$v): ?>
          <option value="<?= $k ?>"<?= $sort===$k?' selected':'' ?>><?= $v ?></option>
        <?php endforeach; ?>
      </select></div>
    <div><label>Сложность</label>
      <select name="diff">
        <option value="-1">любая</option>
        <?php foreach (GD_DIFFS as $i=>$d): ?>
          <option value="<?= $i ?>"<?= $diff===$i?' selected':'' ?>><?= h($d) ?></option>
        <?php endforeach; ?>
      </select></div>
    <div><button class="btn small" type="submit">ИСКАТЬ</button></div>
  </form>

  <table class="lv">
    <tr><th>ID</th><th>Название</th><th>Автор</th><th>Сложность</th><th>★</th><th>♥</th><th>↓</th><th></th></tr>
    <?php foreach ($levels ?: [] as $l): ?>
      <tr>
        <td class="muted">#<?= (int)$l['id'] ?></td>
        <td class="lvname">
          <a href="/level/?id=<?= (int)$l['id'] ?>"><?= h($l['name']) ?></a>
          <?php if (!empty($l['daily'])): ?> <span class="badge">DAILY</span><?php endif; ?>
          <?php if (!empty($l['weekly'])): ?> <span class="badge">WEEKLY</span><?php endif; ?>
          <?php if (!empty($l['epic'])): ?> <span class="badge" style="background:#5c1d4d">EPIC</span>
          <?php elseif (!empty($l['featured'])): ?> <span class="badge" style="background:#5c4a1d">FEATURED</span><?php endif; ?>
          <?php if (!empty($l['offline'])): ?> <span class="badge" style="background:#1d5c3c">OFFLINE</span><?php endif; ?>
        </td>
        <td><a href="/profile/?user=<?= urlencode($l['author']) ?>"><?= h($l['author']) ?></a></td>
        <td><span class="diff <?= gd_diff_class((int)$l['difficulty']) ?>"><?= h(gd_diff_name((int)$l['difficulty'])) ?></span></td>
        <td><?= (int)$l['stars'] ?></td><td><?= (int)$l['likes'] ?></td><td><?= (int)$l['downloads'] ?></td>
        <td><a class="btn small ghost" href="/level/?id=<?= (int)$l['id'] ?>">Открыть</a></td>
      </tr>
    <?php endforeach; ?>
    <?php if (!$levels): ?><tr><td colspan="8" class="muted">Ничего не найдено.</td></tr><?php endif; ?>
  </table>

  <div class="flex" style="margin-top:18px">
    <?php if ($page > 0): ?>
      <a class="btn small ghost" href="?<?= h(http_build_query(array_merge($_GET, ['page'=>$page-1]))) ?>">← назад</a>
    <?php endif; ?>
    <?php if (count($levels ?: []) >= 50): ?>
      <a class="btn small ghost" href="?<?= h(http_build_query(array_merge($_GET, ['page'=>$page+1]))) ?>">вперёд →</a>
    <?php endif; ?>
  </div>
</div></section>
<?php gd_foot();
