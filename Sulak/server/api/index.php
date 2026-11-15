<?php
declare(strict_types=1);
require __DIR__ . '/lib.php';

$issued = null;
$error  = null;

if (($_SERVER['REQUEST_METHOD'] ?? 'GET') === 'POST' && cfg()['free_keys']) {
    $owner = trim((string)($_POST['owner'] ?? ''));
    if ($owner === '') {
        $error = 'Напиши свой ник.';
    } else {
        $ip = client_ip();
        $st = db()->prepare('SELECT COUNT(*) FROM keys WHERE ip = ? AND created > ?');
        $st->execute([$ip, time() - 86400]);
        if ((int)$st->fetchColumn() >= (int)cfg()['keys_per_ip_day']) {
            $error = 'С этого адреса уже выдано слишком много ключей. Попробуй завтра.';
        } else {
            $issued = new_key($owner, $ip);
        }
    }
}
?><!doctype html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Sulak AI API — winnigames2024</title>
<style>
  :root { color-scheme: dark; }
  * { box-sizing: border-box; }
  body { margin:0; background:#0d0b12; color:#e8e4f0;
         font:16px/1.6 ui-sans-serif,system-ui,"Segoe UI",Roboto,sans-serif; }
  .wrap { max-width:760px; margin:0 auto; padding:48px 20px 80px; }
  h1 { font-size:30px; margin:0 0 6px; letter-spacing:-.02em; }
  h2 { font-size:19px; margin:40px 0 10px; color:#c9b6ff; }
  .sub { color:#8d85a3; margin:0 0 32px; }
  .card { background:#161222; border:1px solid #2a2140; border-radius:14px; padding:22px; }
  input[type=text] { width:100%; padding:12px 14px; border-radius:10px;
    border:1px solid #2f2749; background:#0f0d18; color:#e8e4f0; font-size:16px; }
  button { margin-top:12px; padding:12px 20px; border:0; border-radius:10px;
    background:#7c4dff; color:#fff; font-size:16px; font-weight:600; cursor:pointer; }
  button:hover { background:#8d63ff; }
  code, pre { font-family:ui-monospace,"JetBrains Mono",Consolas,monospace; font-size:14px; }
  pre { background:#0f0d18; border:1px solid #241d38; border-radius:10px;
        padding:14px; overflow:auto; }
  .key { font-size:17px; word-break:break-all; background:#101a12;
         border:1px solid #1f4d2c; color:#9bf0b4; padding:14px; border-radius:10px; }
  .err { background:#1d1013; border:1px solid #5a2230; color:#ffb3b3;
         padding:12px; border-radius:10px; }
  table { width:100%; border-collapse:collapse; margin-top:8px; }
  td, th { text-align:left; padding:7px 10px; border-bottom:1px solid #241d38; vertical-align:top; }
  th { color:#9b8fc0; font-weight:600; }
  .foot { margin-top:48px; color:#6b6481; font-size:14px; }
</style>
</head>
<body>
<div class="wrap">
  <h1>Sulak AI API</h1>
  <p class="sub">Бесплатный шлюз для мода Sulak и других проектов winnigames2024.</p>

  <div class="card">
    <?php if ($issued): ?>
      <p>Твой ключ (сохрани, он показывается один раз):</p>
      <div class="key"><?= htmlspecialchars($issued, ENT_QUOTES) ?></div>
      <p style="margin-top:14px">В игре: <code>/sulak key <?= htmlspecialchars($issued, ENT_QUOTES) ?></code><br>
         Или в <code>config/sulak-common.toml</code> в поле <code>apiKey</code>.</p>
    <?php else: ?>
      <?php if ($error): ?><p class="err"><?= htmlspecialchars($error, ENT_QUOTES) ?></p><?php endif; ?>
      <form method="post">
        <label for="owner">Ник в Minecraft</label>
        <input type="text" id="owner" name="owner" placeholder="Steve" maxlength="40" required>
        <button type="submit">Получить бесплатный ключ</button>
      </form>
    <?php endif; ?>
  </div>

  <h2>Как дёргать</h2>
  <pre>GET/POST https://ai.winnigames2024.ru/api/api.php
    ?dbtoken=&lt;uuid игрока — это и есть память ИИ&gt;
    &amp;system_prompt=&lt;системный промпт&gt;
    &amp;user_promps=&lt;что написал игрок&gt;
    &amp;api_key=&lt;ключ отсюда&gt;</pre>

  <h2>Параметры</h2>
  <table>
    <tr><th>dbtoken</th><td>UUID игрока. Для каждого dbtoken сервер хранит свою историю диалога, так что у каждого игрока — своя память.</td></tr>
    <tr><th>system_prompt</th><td>Роль и список разрешённых действий. Мод шлёт его сам.</td></tr>
    <tr><th>user_promps</th><td>Текст желания игрока.</td></tr>
    <tr><th>api_key</th><td>Ключ с этой страницы.</td></tr>
  </table>

  <h2>Ответ</h2>
  <pre>{
  "ok": true,
  "say": "Держи. Но помни, кто дал.",
  "actions": [
    {"type":"give","target":"@owner","item":"minecraft:diamond","count":64}
  ],
  "memory": 8
}</pre>

  <h2>Ошибки</h2>
  <pre>{"ok": false, "error": "неизвестный api_key"}</pre>

  <p class="foot">winnigames2024 · лимит <?= (int)cfg()['rate_per_minute'] ?> запросов в минуту на ключ</p>
</div>
</body>
</html>
