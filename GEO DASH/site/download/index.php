<?php
require_once __DIR__ . '/../_inc/api.php';
$dir = __DIR__;
$exe = file_exists("$dir/GEO_DASH.exe") ? filesize("$dir/GEO_DASH.exe") : 0;
$apk = file_exists("$dir/GEO_DASH.apk") ? filesize("$dir/GEO_DASH.apk") : 0;
$setup = file_exists("$dir/GEO_DASH_Setup.exe") ? filesize("$dir/GEO_DASH_Setup.exe") : 0;
$auto  = file_exists("$dir/autoupdate.exe") ? filesize("$dir/autoupdate.exe") : 0;
$rel = file_exists("$dir/.release") ? trim(file_get_contents("$dir/.release")) : '';
$upd = file_exists("$dir/.updated") ? trim(file_get_contents("$dir/.updated")) : '';
$mb  = fn($b) => $b ? number_format($b / 1048576, 1, ',', ' ') . ' МБ' : '—';
$host = $_SERVER['HTTP_HOST'] ?? 'geodash.winnigames2024.ru';
gd_head('Скачать', 'download');
?>
<section class="section"><div class="container">
  <h2>Скачать GEO DASH</h2>
  <p class="muted">Версия: <b><?= h($rel ?: 'dev') ?></b><?= $upd ? ' · обновлено ' . h(substr($upd, 0, 16)) : '' ?></p>

  <div class="card" style="margin-top:18px;border-color:#ffd24a">
    <h3>Установщик для Windows — так проще всего</h3>
    <p class="muted">
      Ставит игру, делает ярлыки, привязывает файлы уровней <b>.gdl</b> и включает службу автообновления.
      Если интернета нет — поставит из файла <b>GEO_DASH.exe</b>, положенного рядом с установщиком;
      если интернет есть — скачает свежую версию с GitHub сам. Рядом с игрой появится
      <b>autoupdate.exe</b>: он сверяет <b>version.txt</b> с <b>update.json</b> и качает новую версию,
      когда та выше.
    </p>
    <p class="muted">Размер: <?= $mb($setup) ?></p>
    <p style="margin-top:14px">
      <?php if ($setup): ?>
        <a class="btn gold" href="/download/GEO_DASH_Setup.exe">СКАЧАТЬ УСТАНОВЩИК</a>
        <?php if ($auto): ?>
          <a class="btn" href="/download/autoupdate.exe">autoupdate.exe отдельно (<?= $mb($auto) ?>)</a>
        <?php endif; ?>
      <?php else: ?>
        <span class="badge">установщик ещё не выложен</span>
      <?php endif; ?>
    </p>
  </div>

  <div class="grid c2" style="margin-top:18px">
    <div class="card">
      <h3>Windows — только игра, без установки</h3>
      <p class="muted">Один файл .exe, ничего ставить не надо. Уровни, музыка и сохранения лежат в папке <b>assets</b> рядом с игрой.</p>
      <p class="muted">Размер: <?= $mb($exe) ?></p>
      <p style="margin-top:14px">
        <?php if ($exe): ?>
          <a class="btn green" href="/download/GEO_DASH.exe">СКАЧАТЬ .EXE БЕЗ УСТАНОВКИ</a>
        <?php else: ?>
          <span class="badge">сборка ещё не выложена</span>
        <?php endif; ?>
      </p>
    </div>

    <div class="card">
      <h3>Android</h3>
      <p class="muted">APK с тем же движком и тем же аккаунтом. Уровни и песни сохраняются в <b>assets</b> приложения и играются офлайн.</p>
      <p class="muted">Размер: <?= $mb($apk) ?></p>
      <p style="margin-top:14px">
        <?php if ($apk): ?>
          <a class="btn pink" href="/download/GEO_DASH.apk">СКАЧАТЬ .APK</a>
        <?php else: ?>
          <span class="badge">сборка ещё не выложена</span>
        <?php endif; ?>
      </p>
      <p class="muted" style="margin-top:10px">При установке разреши «Установка из неизвестных источников».</p>
    </div>
  </div>

  <div class="card" style="margin-top:18px">
    <h3>Как играть без интернета</h3>
    <p class="muted">
      Основные уровни (Easy / Hard / Insane) и их музыка уже внутри игры. Онлайн-уровни можно сохранить
      кнопкой «Сохранить (SAVED)» — они лягут в assets вместе с треком. Daily, Weekly и офлайн-паки,
      отмеченные админом, качаются сами. Прогресс, монеты и загрузки уровней копятся офлайн и уезжают
      на сервер, как только появится сеть.
    </p>
  </div>
</div></section>
<?php gd_foot();
