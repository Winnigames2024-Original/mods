<?php
require_once __DIR__ . '/../_inc/config.php';
$tab = $_GET['tab'] ?? 'main';
$PAGE_TITLE = 'NareLauncher — скачать бесплатный лаунчер Minecraft';
$PAGE_DESC  = 'Скачайте NareLauncher — бесплатный лаунчер Minecraft с любыми версиями, своими серверами с модами и плагинами, голосовым чатом, скинами и профилями.';
require __DIR__ . '/../_inc/head.php';
$ver = launcher_version();
?>
<section class="hero">
  <h1>NareLauncher — твой Minecraft, без границ</h1>
  <p class="lead">Бесплатный лаунчер Minecraft: любые и кастомные версии, свои сервера с модами и плагинами, голосовой чат, скины и профили. Текущая версия лаунчера — <b><?= h($ver) ?></b>.</p>
  <div class="row">
    <a class="btn" href="/download/">Скачать лаунчер</a>
    <a class="btn ghost" href="/versions/">Все версии</a>
  </div>
</section>

<nav class="pagetabs">
  <a href="?tab=main"     class="<?= $tab==='main'?'on':'' ?>">Главная</a>
  <a href="?tab=features" class="<?= $tab==='features'?'on':'' ?>">Возможности</a>
  <a href="?tab=versions" class="<?= $tab==='versions'?'on':'' ?>">Версии</a>
  <a href="?tab=download" class="<?= $tab==='download'?'on':'' ?>">Скачать</a>
</nav>

<?php if ($tab === 'features'): ?>
  <div class="grid">
    <div class="card"><h3>Любые версии</h3><p>Ванильные и кастомные версии Minecraft в один клик, со своими сборками.</p></div>
    <div class="card"><h3>Свои сервера</h3><p>Запусти сервер с модами или плагинами прямо с ПК — он доступен по адресу-поддомену.</p></div>
    <div class="card"><h3>Моды и плагины</h3><p>Forge, Fabric, Paper и гибрид (Arclight) — моды и плагины на одном сервере.</p></div>
    <div class="card"><h3>Голосовой чат</h3><p>Общайся в игре голосом — на серверах и в совместных мирах.</p></div>
    <div class="card"><h3>Скины и профили</h3><p>Свой скин, профиль, статус и монеты — переносятся между компьютерами.</p></div>
    <div class="card"><h3>Кликер и награды</h3><p>Зарабатывай монеты, забирай ежедневные и ежеминутные награды, трать в магазине.</p></div>
  </div>
<?php elseif ($tab === 'versions'): ?>
  <?php include __DIR__ . '/../versions/index.php'; // встраиваем страницу версий ?>
<?php elseif ($tab === 'download'): ?>
  <div class="panel">
    <h2>Скачать NareLauncher <?= h($ver) ?></h2>
    <p class="muted">Установщик сам поставит лаунчер и всё необходимое.</p>
    <p style="margin-top:14px"><a class="btn" href="/download/">Перейти к скачиванию</a></p>
  </div>
<?php else: ?>
  <div class="grid">
    <div class="card"><h3>1. Скачай</h3><p>Загрузи установщик и запусти — лаунчер поставится сам.</p></div>
    <div class="card"><h3>2. Войди</h3><p>Создай аккаунт или войди — профиль, скины и монеты уже с тобой.</p></div>
    <div class="card"><h3>3. Играй</h3><p>Выбирай версию, ставь моды, запускай свой сервер и зови друзей.</p></div>
  </div>
<?php endif; ?>

<?php require __DIR__ . '/../_inc/foot.php'; ?>
