<?php
require_once __DIR__ . '/../_inc/config.php';
$ver = launcher_version();
$PAGE_TITLE = 'Скачать NareLauncher — бесплатный лаунчер Minecraft';
$PAGE_DESC  = 'Скачать NareLauncher бесплатно. Установщик сам поставит лаунчер Minecraft и обновления. Последняя версия ' . $ver . '.';
require __DIR__ . '/../_inc/head.php';
?>
<section class="hero">
  <h1>Скачать NareLauncher</h1>
  <p class="lead">Бесплатный лаунчер Minecraft для Windows. Установщик поставит лаунчер, ярлыки и автообновление. Текущая версия — <b><?= h($ver) ?></b>.</p>
  <div class="row">
    <a class="btn" href="<?= h(GH_INSTALLER) ?>">Скачать установщик (.exe)</a>
    <a class="btn ghost" href="/versions/">Какие версии поддерживаются</a>
  </div>
  <p class="muted">Windows 10/11 · 64-бит. Если антивирус ругается на новый exe — это ложное срабатывание из-за свежей подписи.</p>
</section>

<h2>Что внутри</h2>
<div class="grid">
  <div class="card"><h3>Любые версии</h3><p>Ванильные и кастомные версии в один клик.</p></div>
  <div class="card"><h3>Свои сервера</h3><p>Forge, Fabric, Paper и гибрид — моды и плагины.</p></div>
  <div class="card"><h3>Автообновление</h3><p>Лаунчер сам обновляется до свежей версии при запуске.</p></div>
</div>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
