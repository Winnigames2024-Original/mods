<?php
// Общий <head> с SEO + шапка-навигация. Перед include задай:
//   $PAGE_TITLE, $PAGE_DESC, (необязательно) $CANON, $PAGE_OG_IMAGE
require_once __DIR__ . '/config.php';
if (!defined('NARE_HEAD')) define('NARE_HEAD', 1);   // метка: шапка уже выведена
$t = $PAGE_TITLE ?? 'NareLauncher — бесплатный лаунчер Minecraft';
$d = $PAGE_DESC ?? 'NareLauncher — бесплатный лаунчер Minecraft: любые и кастомные версии, свои сервера с модами и плагинами, голосовой чат, скины и профили. Скачать майнкрафт лаунчер.';
$canon = $CANON ?? (SITE . ($_SERVER['REQUEST_URI'] ?? '/'));
$ogimg = $PAGE_OG_IMAGE ?? (SITE . '/assets/cover.png');
?><!doctype html>
<html lang="ru">
<head>
<script>try{if(localStorage.getItem('nareDesign')==='simple')document.documentElement.classList.add('simple');}catch(e){}</script>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title><?= h($t) ?></title>
<meta name="description" content="<?= h($d) ?>">
<meta name="keywords" content="minecraft launcher, майнкрафт скачать, лаунчер майнкрафт, скачать майнкрафт лаунчер, бесплатный лаунчер minecraft, nare launcher, narelauncher, сервер майнкрафт с модами и плагинами, майнкрафт с модами">
<meta name="robots" content="index, follow">
<link rel="manifest" href="/manifest.json">
<meta name="theme-color" content="#2787f5">
<link rel="icon" type="image/png" sizes="32x32" href="/assets/favicon-32.png">
<link rel="apple-touch-icon" href="/assets/icon-180.png">
<meta name="apple-mobile-web-app-capable" content="yes">
<meta name="mobile-web-app-capable" content="yes">
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent">
<meta name="apple-mobile-web-app-title" content="NareLauncher">
<?php $gsc = @trim(@file_get_contents(__DIR__ . '/gsc.txt')); if ($gsc): ?>
<meta name="google-site-verification" content="<?= h($gsc) ?>">
<?php endif; ?>
<link rel="canonical" href="<?= h($canon) ?>">
<meta property="og:type" content="website">
<meta property="og:site_name" content="NareLauncher">
<meta property="og:title" content="<?= h($t) ?>">
<meta property="og:description" content="<?= h($d) ?>">
<meta property="og:url" content="<?= h($canon) ?>">
<meta property="og:image" content="<?= h($ogimg) ?>">
<meta name="twitter:card" content="summary_large_image">
<meta name="twitter:title" content="<?= h($t) ?>">
<meta name="twitter:description" content="<?= h($d) ?>">
<meta name="twitter:image" content="<?= h($ogimg) ?>">
<link rel="icon" href="/assets/favicon.png">
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "SoftwareApplication",
  "name": "NareLauncher",
  "applicationCategory": "GameApplication",
  "operatingSystem": "Windows",
  "offers": { "@type": "Offer", "price": "0", "priceCurrency": "RUB" },
  "url": "<?= SITE ?>",
  "downloadUrl": "<?= SITE ?>/download/",
  "description": "Бесплатный лаунчер Minecraft: любые версии, свои сервера с модами и плагинами, голосовой чат, скины и профили."
}
</script>
<link rel="stylesheet" href="/assets/app.css?v=<?= @filemtime(__DIR__ . '/../assets/app.css') ?: date('Ymd') ?>">
</head>
<body>
<?php
// Пункты меню (иконка, подпись, ссылка) — для выезжающего слева списка
$NAV_MAIN = [
  ['🏠','Главная','/home/'],
  ['▶','NareVideos','/narevideos/'],
  ['📡','Эфиры','/live/'],
  ['🔥','Популярное','/popular/'],
  ['🎮','Сервера','/servers/'],
  ['👥','Каналы','/channels/'],
  ['🔍','Поиск','/search/'],
  ['⬇','Скачать','/download/'],
];
$NAV_ME = me() ? [
  ['🔔','Подписки','/subscriptions/'],
  ['✉','Сообщения','/messages/'],
  ['🖥','Мои серверы','/servers/mine/'],
  ['🤖','Боты','/bots/'],
  ['🕹','Удалёнка','/remote/'],
  ['👤','Профиль','/profile/my/'],
] : [];
?>
<header class="nav" id="siteHeader">
  <button type="button" class="menu-btn" id="menuBtn" aria-label="Меню">
    <span></span><span></span><span></span>
  </button>
  <a class="brand" href="/home/"><span class="logo">N</span> <span class="brand-tx">NareLauncher</span></a>
  <form class="nav-search" action="/search/" method="get" role="search">
    <span class="nav-search-ic">🔍</span>
    <input type="search" name="q" placeholder="Поиск видео, каналов, эфиров…" value="<?= h($_GET['q'] ?? '') ?>" autocomplete="off">
  </form>
  <div class="nav-right">
    <?php if (me()): ?>
      <button type="button" class="nav-bell" id="navBell" title="Уведомления">🔔<span class="bell-dot" id="bellDot" style="display:none"></span></button>
    <?php else: ?>
      <a class="nav-login" href="/login/">Вход</a>
      <a class="tabs-cta" href="/register/">Регистрация</a>
    <?php endif; ?>
    <button type="button" class="design-toggle" id="designToggle" title="Переключить дизайн">◑</button>
  </div>
</header>

<!-- Выезжающее слева меню (как в VK) -->
<div class="drawer-ov" id="navOverlay"></div>
<aside class="drawer" id="navDrawer" aria-hidden="true">
  <div class="drawer-head">
    <span class="logo">N</span> <b>NareLauncher</b>
    <button type="button" class="drawer-x" id="drawerX" aria-label="Закрыть">✕</button>
  </div>
  <nav class="drawer-nav">
    <?php foreach ($NAV_MAIN as $it): ?>
      <a class="drawer-item" href="<?= $it[2] ?>"><span class="di-ic"><?= $it[0] ?></span><?= h($it[1]) ?></a>
    <?php endforeach; ?>
    <?php if ($NAV_ME): ?>
      <div class="drawer-sep"></div>
      <?php foreach ($NAV_ME as $it): ?>
        <a class="drawer-item" href="<?= $it[2] ?>"><span class="di-ic"><?= $it[0] ?></span><?= h($it[1]) ?></a>
      <?php endforeach; ?>
      <?php if (!empty($_SESSION['admin'])): ?>
        <a class="drawer-item" href="/admin/"><span class="di-ic">⚙</span>Админ</a>
      <?php endif; ?>
      <a class="drawer-item logout" href="/profile/my/?logout=1" onclick="return confirm('Выйти из аккаунта?')"><span class="di-ic">🚪</span>Выйти</a>
    <?php else: ?>
      <div class="drawer-sep"></div>
      <a class="drawer-item" href="/login/"><span class="di-ic">🔑</span>Вход</a>
      <a class="drawer-item accent" href="/register/"><span class="di-ic">✨</span>Регистрация</a>
    <?php endif; ?>
  </nav>
</aside>
<main class="wrap">
