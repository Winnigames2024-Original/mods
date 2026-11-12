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
<link rel="stylesheet" href="/assets/app.css">
</head>
<body>
<header class="nav">
  <a class="brand" href="/home/"><span class="logo">N</span> NareLauncher</a>
  <nav class="tabs">
    <a href="/home/">Главная</a>
    <a href="/media/">Медиа</a>
    <a href="/live/">Эфиры</a>
    <a href="/servers/">Сервера</a>
    <a href="/channels/">Каналы</a>
    <a href="/download/">Скачать</a>
    <?php if (me()): ?>
      <a href="/subscriptions/">Подписки</a>
      <a href="/servers/mine/">Мои серверы</a>
      <a href="/bots/">Боты</a>
      <a href="/remote/">Удалёнка</a>
      <?php if (!empty($_SESSION['admin'])): ?><a href="/admin/">Админ</a><?php endif; ?>
      <a href="/profile/my/">Профиль</a>
    <?php else: ?>
      <a href="/login/">Вход</a>
      <a class="cta" href="/register/">Регистрация</a>
    <?php endif; ?>
    <button type="button" class="design-toggle" id="designToggle" title="Переключить дизайн">◑ Дизайн</button>
  </nav>
</header>
<main class="wrap">
