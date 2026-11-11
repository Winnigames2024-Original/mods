<?php
require_once __DIR__ . '/../_inc/config.php';
$embedded = defined('NARE_HEAD');   // true, если нас включили в home.php

if (!$embedded) {
    $PAGE_TITLE = 'Версии Minecraft — NareLauncher';
    $PAGE_DESC  = 'Поддерживаемые версии Minecraft в NareLauncher: ванильные, Forge, Fabric, Paper и гибрид. Скачать любую версию майнкрафт.';
    require __DIR__ . '/../_inc/head.php';
}

// Список типов ядра и версий — то, что умеет лаунчер.
$cores = [
  ['Vanilla',  'Чистый сервер/клиент Mojang', 'любые релизы'],
  ['Forge',    'Моды Forge', '1.21.4 и другие'],
  ['Fabric',   'Лёгкие моды Fabric', '1.21.x и другие'],
  ['Paper',    'Плагины, оптимизация', '1.21.x'],
  ['Гибрид (Arclight)', 'Моды + плагины на одном сервере', '1.21.1 / 1.21 / 1.20.4'],
];
?>
<h2>Версии и ядра</h2>
<p class="muted">NareLauncher ставит любые версии Minecraft и разные ядра серверов. Моды и плагины — из встроенного каталога.</p>
<div class="vlist">
  <?php foreach ($cores as $c): ?>
    <div class="vrow">
      <div><b><?= h($c[0]) ?></b> <span class="muted">— <?= h($c[1]) ?></span></div>
      <span class="badge"><?= h($c[2]) ?></span>
    </div>
  <?php endforeach; ?>
</div>
<p style="margin-top:16px"><a class="btn" href="/download/">Скачать лаунчер</a></p>

<?php if (!$embedded) require __DIR__ . '/../_inc/foot.php'; ?>
