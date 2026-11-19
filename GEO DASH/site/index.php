<?php
require_once __DIR__ . '/_inc/api.php';

[$okD, $daily]  = gd_get('/api/daily');
[$okW, $weekly] = gd_get('/api/weekly');
[$okL, $levels] = gd_get('/api/levels?sort=popular');
[$okP, $ping]   = gd_get('/api/ping');

$exe = file_exists(__DIR__ . '/download/GEO_DASH.exe');
$apk = file_exists(__DIR__ . '/download/GEO_DASH.apk');

gd_head('Главная', 'home');
?>
<section class="hero"><div class="container">
  <h1>GEO <em>DASH</em></h1>
  <p class="lead">
    Ритм-платформер на реакции: куб, корабль, шар, НЛО, волна, робот, паук — и новый режим
    <b>червяк</b>, который проходит сквозь блоки и по клику переворачивает гравитацию.
    Скорости до <b>6x</b>, полный редактор с триггерами, свои уровни, друзья, daily и weekly.
    Работает и без интернета.
  </p>
  <div class="cta">
    <a class="btn green" href="/download/">СКАЧАТЬ ИГРУ</a>
    <?php if ($exe): ?><a class="btn" href="/download/GEO_DASH.exe">Windows (.exe)</a><?php endif; ?>
    <?php if ($apk): ?><a class="btn pink" href="/download/GEO_DASH.apk">Android (.apk)</a><?php endif; ?>
    <a class="btn ghost" href="/levels/">Уровни игроков</a>
  </div>
  <div class="badges">
    <span class="badge">8 режимов игры</span>
    <span class="badge">скорости 0.5x → 6x</span>
    <span class="badge">38 типов триггеров</span>
    <span class="badge">музыка с Newgrounds по ID</span>
    <span class="badge">офлайн-режим</span>
    <span class="badge">защита уровней паролем</span>
    <?php if ($okP): ?><span class="badge">сервер онлайн · игроков: <?= (int)($ping['users'] ?? 0) ?> · уровней: <?= (int)($ping['levels'] ?? 0) ?></span><?php endif; ?>
  </div>
</div></section>

<section class="section"><div class="container">
  <h2>Уровень дня и недели</h2>
  <div class="grid c2">
    <?php foreach ([['DAILY', $daily], ['WEEKLY', $weekly]] as [$label, $d]):
      $lv = $d['level'] ?? null; ?>
      <div class="card">
        <h3><?= $label ?></h3>
        <?php if ($lv): ?>
          <div class="flex" style="margin:10px 0">
            <span class="diff <?= gd_diff_class((int)$lv['difficulty']) ?>"><?= h(gd_diff_name((int)$lv['difficulty'])) ?></span>
            <b style="font-size:19px"><?= h($lv['name']) ?></b>
          </div>
          <p class="muted">автор <a href="/profile/?user=<?= urlencode($lv['author']) ?>"><?= h($lv['author']) ?></a>
            · ★ <?= (int)$lv['stars'] ?> · ♥ <?= (int)$lv['likes'] ?> · ↓ <?= (int)$lv['downloads'] ?></p>
          <p style="margin-top:12px"><a class="btn small" href="/levels/view.php?id=<?= (int)$lv['id'] ?>">Открыть уровень</a></p>
        <?php else: ?>
          <p class="muted">Пока не назначен.</p>
        <?php endif; ?>
      </div>
    <?php endforeach; ?>
  </div>
</div></section>

<section class="section"><div class="container">
  <h2>Что внутри</h2>
  <div class="grid c3">
    <div class="card"><h3>Все режимы и порталы</h3><p>Куб, корабль, шар, НЛО, волна, робот, паук и червяк. Порталы режимов, гравитации, мини, дуал, зеркало и скорости от 0.5x до 6x.</p></div>
    <div class="card"><h3>Редактор с триггерами</h3><p>Move, Rotate, Scale, Alpha, Toggle, Spawn, Pulse, Color, Shake, Follow, Count, Collision, Random, Timer, Camera, Song, Gravity и ещё десяток — с группами и плавностями.</p></div>
    <div class="card"><h3>Музыка по ID</h3><p>Любой трек Newgrounds по номеру: сервер скачивает его и раздаёт игре, трек ложится в assets и играет офлайн. В основных уровнях — свой саундтрек.</p></div>
    <div class="card"><h3>Офлайн</h3><p>Без сети игра работает: свои уровни, SAVED, монеты и прогресс. Всё, что сделано офлайн, уезжает на сервер, как только появится интернет.</p></div>
    <div class="card"><h3>Аккаунты и друзья</h3><p>Регистрация, звёзды, монеты, пройденные демоны, запросы в друзья — из игры и с сайта.</p></div>
    <div class="card"><h3>Защита уровней</h3><p>Уровень можно закрыть от копирования, открыть по паролю или сделать свободным для копирования.</p></div>
  </div>
</div></section>

<section class="section"><div class="container">
  <h2>Популярные уровни</h2>
  <table class="lv">
    <tr><th>ID</th><th>Название</th><th>Автор</th><th>Сложность</th><th>★</th><th>♥</th><th>↓</th></tr>
    <?php foreach (array_slice($levels ?: [], 0, 10) as $l): ?>
      <tr>
        <td class="muted">#<?= (int)$l['id'] ?></td>
        <td class="lvname"><a href="/levels/view.php?id=<?= (int)$l['id'] ?>"><?= h($l['name']) ?></a></td>
        <td><a href="/profile/?user=<?= urlencode($l['author']) ?>"><?= h($l['author']) ?></a></td>
        <td><span class="diff <?= gd_diff_class((int)$l['difficulty']) ?>"><?= h(gd_diff_name((int)$l['difficulty'])) ?></span></td>
        <td><?= (int)$l['stars'] ?></td><td><?= (int)$l['likes'] ?></td><td><?= (int)$l['downloads'] ?></td>
      </tr>
    <?php endforeach; ?>
    <?php if (!$levels): ?><tr><td colspan="7" class="muted">Пока пусто — загрузи первый уровень из редактора.</td></tr><?php endif; ?>
  </table>
  <p><a class="btn small ghost" href="/levels/">Все уровни</a></p>
</div></section>

<section class="section"><div class="container">
  <h2>Свой сервер</h2>
  <p class="muted">Debian 12 / Ubuntu — одна команда:</p>
  <pre class="cmd">curl -fsSL http://<?= h($_SERVER['HTTP_HOST'] ?? 'geodash.winnigames2024.ru') ?>/install.sh | sudo bash</pre>
</div></section>
<?php gd_foot();
