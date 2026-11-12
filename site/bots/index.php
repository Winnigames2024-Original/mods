<?php
require_once __DIR__ . '/../_inc/config.php';
require_login();
$msg = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST' && ($_POST['act'] ?? '') === 'create') {
    $r = api_post('/bot/create', ['token' => token(), 'name' => $_POST['name'] ?? 'bot']);
    if (!empty($r['ok'])) { header('Location: /bots/edit.php?id=' . urlencode($r['id'])); exit; }
    $msg = $r['error'] ?? 'Ошибка';
}
$list = api_post('/bot/list', ['token' => token()]);
$bots = $list['bots'] ?? [];
$PAGE_TITLE = 'Боты — NareLauncher';
require __DIR__ . '/../_inc/head.php';
?>
<h1>Боты для чатов</h1>
<p class="lead">Пишите ботов на Python прямо здесь. Бот отвечает в чатах каналов и трансляций через наш API. Файлов может быть сколько угодно.</p>

<form method="post" class="row-form" style="margin:12px 0">
  <input type="hidden" name="act" value="create">
  <input name="name" placeholder="Имя бота" maxlength="40" required style="flex:1">
  <button class="btn" type="submit">Создать бота</button>
</form>
<?php if ($msg): ?><div class="msg"><?= h($msg) ?></div><?php endif; ?>

<?php if (!$bots): ?><p class="muted">У вас пока нет ботов.</p><?php else: ?>
<div class="srv-list">
  <?php foreach ($bots as $b): ?>
  <div class="srv">
    <div class="srv-main"><div class="srv-ico">🤖</div><div class="srv-body">
      <div class="srv-name"><?= h($b['name']) ?></div>
      <div class="muted"><?= count($b['files']) ?> файлов · цели: <?= $b['targets'] ? h(implode(', ', $b['targets'])) : '—' ?></div>
    </div></div>
    <div class="srv-act"><a class="btn" href="/bots/edit.php?id=<?= urlencode($b['id']) ?>">Редактор</a></div>
  </div>
  <?php endforeach; ?>
</div>
<?php endif; ?>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Как запустить бота</h2>
  <ol style="line-height:1.9">
    <li>Создайте бота и напишите код в редакторе (несколько файлов можно).</li>
    <li>Скачайте файлы бота и библиотеку <a href="/bots/narebot.py" download>narebot.py</a>.</li>
    <li>Привяжите бота к каналу или трансляции (в редакторе, цель вида <code>channel:ID</code> или <code>stream:ID</code>).</li>
    <li>Запустите: <code>python bot.py</code> (нужен Python 3). Бот начнёт отвечать в чатах.</li>
  </ol>
  <h3>API бота</h3>
  <p class="muted">Все запросы — POST JSON на <code><?= SITE ?></code>:</p>
  <ul style="line-height:1.8">
    <li><code>/bot/events</code> {botToken, since} → новые сообщения чатов бота</li>
    <li><code>/bot/send</code> {botToken, target, text} → отправить сообщение</li>
    <li><code>/bot/me</code> {botToken} → имя и цели бота</li>
  </ul>
</div>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
