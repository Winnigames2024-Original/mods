<?php
// GEO DASH — общие функции сайта (обращение к API на 127.0.0.1:37534)
if (session_status() === PHP_SESSION_NONE) session_start();

define('GD_API', getenv('GEODASH_API') ?: 'http://127.0.0.1:37534');
define('GD_BRAND', 'Winnigames2024');
define('GD_ADMIN', 'Winnigames2024');

function gd_call(string $method, string $path, $body = null, ?string $token = null) {
    $ch = curl_init(GD_API . $path);
    $headers = ['Accept: application/json'];
    if ($token) $headers[] = 'Authorization: Bearer ' . $token;
    if ($body !== null) {
        $headers[] = 'Content-Type: application/json';
        curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($body, JSON_UNESCAPED_UNICODE));
    }
    curl_setopt_array($ch, [
        CURLOPT_CUSTOMREQUEST => $method,
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_HTTPHEADER => $headers,
        CURLOPT_TIMEOUT => 20,
    ]);
    $raw = curl_exec($ch);
    $code = curl_getinfo($ch, CURLINFO_HTTP_CODE);
    curl_close($ch);
    if ($raw === false) return [false, ['error' => 'Сервер игры не отвечает'], 0];
    $json = json_decode($raw, true);
    return [$code >= 200 && $code < 300, $json, $code];
}

function gd_get(string $path, ?string $token = null)   { return gd_call('GET', $path, null, $token); }
function gd_post(string $path, $body, ?string $token = null) { return gd_call('POST', $path, $body, $token); }

function gd_token(): ?string { return $_SESSION['gd_token'] ?? null; }
function gd_user(): ?string  { return $_SESSION['gd_user'] ?? null; }
function gd_is_admin(): bool { return gd_user() !== null && strcasecmp(gd_user(), GD_ADMIN) === 0; }

function gd_login_required() {
    if (!gd_token()) { header('Location: /login/'); exit; }
}

function gd_me() {
    static $me = null;
    if ($me !== null) return $me;
    if (!gd_token()) return null;
    [$ok, $data] = gd_get('/api/me', gd_token());
    $me = $ok ? $data : null;
    return $me;
}

const GD_DIFFS = ['Без оценки','Auto','Easy','Normal','Hard','Harder','Insane',
                  'Easy Demon','Medium Demon','Hard Demon','Insane Demon','Extreme Demon'];

function gd_diff_name(int $d): string { return GD_DIFFS[$d] ?? '?'; }

function gd_diff_class(int $d): string {
    if ($d >= 7) return 'd-demon';
    return match ($d) { 2 => 'd-easy', 3 => 'd-normal', 4 => 'd-hard',
                        5 => 'd-harder', 6 => 'd-insane', 1 => 'd-normal', default => 'd-na' };
}

function h($s): string { return htmlspecialchars((string)$s, ENT_QUOTES, 'UTF-8'); }

/** Выбранный стиль сайта: classic (как был), dark или light. */
function gd_theme(): string {
    $t = $_COOKIE['gdtheme'] ?? 'classic';
    return in_array($t, ['classic', 'dark', 'light'], true) ? $t : 'classic';
}

function gd_head(string $title, string $active = '') {
    $user = gd_user();
    $theme = gd_theme();
    ?><!doctype html>
<html lang="ru" data-theme="<?= h($theme) ?>">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title><?= h($title) ?> — GEO DASH</title>
<meta name="description" content="GEO DASH — ритм-платформер от Winnigames2024: уровни, редактор, друзья, daily и weekly. Скачать для Windows и Android.">
<link rel="stylesheet" href="/assets/style.css?v=7">
<link rel="icon" href="/favicon.ico" sizes="any">
<link rel="icon" type="image/png" href="/assets/icon.png">
<link rel="apple-touch-icon" href="/assets/icon.png">
<meta name="theme-color" content="<?= $theme === 'dark' ? '#12122a' : ($theme === 'light' ? '#eaf0ff' : '#287dff') ?>">
<script>
// стиль сайта переключается сразу, без перезагрузки, и помнится год
(function () {
  document.addEventListener('click', function (e) {
    var b = e.target.closest('[data-set-theme]');
    if (!b) return;
    var t = b.getAttribute('data-set-theme');
    document.documentElement.setAttribute('data-theme', t);
    document.cookie = 'gdtheme=' + t + ';path=/;max-age=31536000;samesite=lax';
    document.querySelectorAll('[data-set-theme]').forEach(function (x) {
      x.setAttribute('aria-pressed', x === b ? 'true' : 'false');
    });
    var m = document.querySelector('meta[name="theme-color"]');
    if (m) m.content = t === 'dark' ? '#12122a' : (t === 'light' ? '#eaf0ff' : '#287dff');
  });
})();
</script>
</head>
<body>
<header class="top"><div class="container nav">
  <a class="logo" href="/">GEO<span> DASH</span></a>
  <a class="link<?= $active==='levels'?' active':'' ?>" href="/levels/">Уровни</a>
  <a class="link<?= $active==='top'?' active':'' ?>" href="/top/">Топ</a>
  <a class="link<?= $active==='mp'?' active':'' ?>" href="/mp/">Мультиплеер</a>
  <a class="link<?= $active==='music'?' active':'' ?>" href="/music/">Музыка</a>
  <a class="link<?= $active==='images'?' active':'' ?>" href="/images/">Toolbox</a>
  <a class="link<?= $active==='download'?' active':'' ?>" href="/download/">Скачать</a>
  <?php if ($user): ?>
    <a class="link<?= $active==='friends'?' active':'' ?>" href="/friends/">Друзья</a>
    <a class="link<?= $active==='account'?' active':'' ?>" href="/account/">Аккаунт</a>
    <?php if (gd_is_admin()): ?><a class="link" href="/admin/">Админка</a><?php endif; ?>
  <?php endif; ?>
  <span class="spacer"></span>
  <span class="themes" role="group" aria-label="Стиль сайта">
    <button type="button" data-set-theme="classic" title="Обычный стиль"
            aria-pressed="<?= $theme === 'classic' ? 'true' : 'false' ?>">◆</button>
    <button type="button" data-set-theme="dark" title="Тёмный стиль"
            aria-pressed="<?= $theme === 'dark' ? 'true' : 'false' ?>">☾</button>
    <button type="button" data-set-theme="light" title="Светлый стиль"
            aria-pressed="<?= $theme === 'light' ? 'true' : 'false' ?>">☀</button>
  </span>
  <?php if ($user): ?>
    <span class="pill"><?= h($user) ?></span>
    <a class="btn small ghost" href="/logout.php">Выйти</a>
  <?php else: ?>
    <a class="btn small ghost" href="/login/">Вход</a>
    <a class="btn small" href="/register/">Регистрация</a>
  <?php endif; ?>
</div></header>
<main>
<?php
}

function gd_foot() {
    ?></main>
<footer><div class="container flex">
  <div>GEO DASH — <?= h(GD_BRAND) ?>. Движок, графика и встроенный саундтрек сделаны для этой игры.</div>
  <div class="right"><a href="/download/">Скачать</a> · <a href="/levels/">Уровни</a> · <a href="/images/">Toolbox</a></div>
</div></footer>
</body></html><?php
}
