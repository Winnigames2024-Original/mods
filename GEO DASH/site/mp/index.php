<?php
// Живые комнаты мультиплеера
require_once __DIR__ . '/../_inc/api.php';

[$ok, $rooms] = gd_get('/api/mp/rooms');
if (!$ok || !is_array($rooms)) $rooms = [];

// подтянем названия уровней
$names = [];
foreach ($rooms as $r) {
    $id = (int)($r['levelId'] ?? 0);
    if ($id && !isset($names[$id])) {
        [$lok, $lv] = gd_get('/api/levels/' . $id);
        $names[$id] = $lok && is_array($lv) ? ($lv['name'] ?? ('Уровень ' . $id)) : ('Уровень ' . $id);
    }
}
$total = 0;
foreach ($rooms as $r) $total += (int)($r['players'] ?? 0);

gd_head('Мультиплеер', 'mp');
?>
<div class="container">
  <h1>Мультиплеер</h1>
  <p class="lead">Уровень с типом «мультиплеер» пускает в одну карту сразу несколько игроков —
     вы видите кубики друг друга и бежите вместе. Тип уровня и максимум игроков ставятся
     в редакторе: <b>НАСТРОЙКИ УРОВНЯ → Мультиплеер</b>.</p>

  <div class="panel">
    <h3>Сейчас играют: <?= $total ?></h3>
    <?php if (!$rooms): ?>
      <p class="dim">Пустых комнат нет — зайди в любой мультиплеерный уровень, и здесь появится твоя.</p>
    <?php else: ?>
      <table>
        <thead><tr><th>Уровень</th><th>Игроков</th><th>Вместимость</th><th></th></tr></thead>
        <tbody>
        <?php foreach ($rooms as $r): $id = (int)($r['levelId'] ?? 0); ?>
          <tr>
            <td><b><?= h($names[$id] ?? ('Уровень ' . $id)) ?></b> <span class="dim">#<?= $id ?></span></td>
            <td class="rank"><?= (int)($r['players'] ?? 0) ?></td>
            <td class="dim">до <?= (int)($r['max'] ?? 0) ?></td>
            <td><a class="btn small ghost" href="/level/?id=<?= $id ?>">Об уровне</a></td>
          </tr>
        <?php endforeach; ?>
        </tbody>
      </table>
    <?php endif; ?>
  </div>

  <div class="card">
    <h3>Как играть вместе</h3>
    <ol>
      <li>В редакторе включи «мультиплеер» и поставь максимум игроков.</li>
      <li>Опубликуй уровень кнопкой «ОПУБЛИКОВАТЬ» — нужен ID на сервере.</li>
      <li>Скажи друзьям его номер: <b>CREATOR → SEARCH → номер</b>.</li>
      <li>Все, кто запустил уровень, попадают в одну карту, если есть места.</li>
    </ol>
  </div>
</div>
<?php gd_foot();
