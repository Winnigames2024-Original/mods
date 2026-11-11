<?php
require_once __DIR__ . '/../_inc/config.php';
$PAGE_TITLE = 'Запуск Minecraft — NareLauncher';
$PAGE_DESC  = 'Как запустить Minecraft через NareLauncher: вход, выбор версии, игра. Свои сервера с модами и плагинами.';
require __DIR__ . '/../_inc/head.php';
?>
<h1>Запуск Minecraft</h1>
<p class="lead">Игра запускается из приложения NareLauncher на вашем ПК. Сайт — для аккаунта, профиля и скачивания.</p>

<ol style="line-height:2;margin-top:12px">
  <li>Скачайте и установите лаунчер на странице <a href="/download/">Скачать</a>.</li>
  <li>Войдите в аккаунт (тот же, что на сайте) или создайте новый.</li>
  <li>Выберите версию и, если нужно, поставьте моды или плагины.</li>
  <li>Нажмите «Играть». Чтобы поднять свой сервер — вкладка «Мои серверы».</li>
</ol>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Уже установлен?</h2>
  <p class="muted">Откройте NareLauncher на компьютере и нажмите «Играть». Запуск игры из браузера технически невозможен — нужен установленный лаунчер.</p>
  <?php if (me()): ?>
    <p style="margin-top:12px"><a class="btn ghost" href="/profile/my/">Мой профиль</a></p>
  <?php else: ?>
    <p style="margin-top:12px"><a class="btn" href="/register/">Создать аккаунт</a> <a class="btn ghost" href="/login/">Войти</a></p>
  <?php endif; ?>
</div>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
