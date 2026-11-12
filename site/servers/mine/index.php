<?php
require_once __DIR__ . '/../../_inc/config.php';
require_login();
$nick = me();
$tok  = token();

$hosted = api_post('/host/list', ['token' => $tok]);
$hostServers = $hosted['servers'] ?? [];           // [{id, folder, port}]
$ads = api_post('/servers/mine', ['token' => $tok]);
$adServers = $ads['servers'] ?? [];                // [{id,name,ip,status,reason}]

$PAGE_TITLE = 'Мои серверы — NareLauncher';
$PAGE_DESC  = 'Ваши сервера NareLauncher: адрес для входа и статус публикации.';
require __DIR__ . '/../../_inc/head.php';

function dedic_ip($nick, $id) {
    return strtolower($nick) . '-' . strtolower($id) . '.narelauncher.winnigames2024.ru';
}
$statusText = ['approved' => 'опубликован', 'pending' => 'на модерации', 'rejected' => 'отклонён'];
?>
<h1>Мои серверы</h1>
<p class="lead">Сервера с вашего ПК и заявки на публикацию. Сервером управляют в приложении NareLauncher (вкладка «Мои серверы»).</p>

<div class="panel">
  <h2 style="margin-top:0">Сервера с вашего ПК</h2>
  <?php if (!$hostServers): ?>
    <p class="muted">Серверов не найдено. Создайте сервер в лаунчере — он появится здесь.</p>
  <?php else: ?>
    <div class="srv-list">
      <?php foreach ($hostServers as $s): $ip = dedic_ip($nick, $s['id']); ?>
      <div class="srv">
        <div class="srv-main">
          <div class="srv-ico"><?= h(initial($s['id'])) ?></div>
          <div class="srv-body">
            <div class="srv-name"><?= h($s['id']) ?> <span class="badge">порт <?= (int)($s['port'] ?? 0) ?></span></div>
            <div class="srv-ip">Адрес: <code><?= h($ip) ?></code></div>
            <div class="muted">Доступен, пока сервер запущен на ПК.</div>
          </div>
        </div>
        <div class="srv-act">
          <a class="btn" href="narelauncher://play?server=<?= urlencode($ip) ?>">Играть</a>
          <button class="btn ghost" type="button" onclick="copyIp('<?= h(addslashes($ip)) ?>', this)">Скопировать</button>
        </div>
      </div>
      <?php endforeach; ?>
    </div>
  <?php endif; ?>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Заявки на публикацию</h2>
  <?php if (!$adServers): ?>
    <p class="muted">Вы ещё не публиковали серверов в общем списке. Опубликовать можно из лаунчера.</p>
  <?php else: ?>
    <table class="tbl">
      <tr><th>Название</th><th>Адрес</th><th>Статус</th></tr>
      <?php foreach ($adServers as $s): ?>
      <tr>
        <td><?= h($s['name'] ?? '') ?></td>
        <td><?= !empty($s['hideIp']) ? '<span class="muted">скрыт</span>' : h($s['ip'] ?? '') ?></td>
        <td>
          <span class="badge <?= ($s['status'] ?? '') === 'approved' ? 'on' : '' ?>"><?= h($statusText[$s['status'] ?? ''] ?? $s['status'] ?? '') ?></span>
          <?php if (!empty($s['reason'])): ?><div class="muted"><?= h($s['reason']) ?></div><?php endif; ?>
        </td>
      </tr>
      <?php endforeach; ?>
    </table>
  <?php endif; ?>
</div>

<script>
function copyIp(ip, btn){
  navigator.clipboard.writeText(ip).then(function(){
    var t = btn.textContent; btn.textContent = 'Скопировано ✓';
    setTimeout(function(){ btn.textContent = t; }, 1500);
  });
}
</script>
<?php require __DIR__ . '/../../_inc/foot.php'; ?>
