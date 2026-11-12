<?php
require_once __DIR__ . '/../_inc/config.php';
$PAGE_TITLE = 'Сервера Minecraft — NareLauncher';
$PAGE_DESC  = 'Список серверов NareLauncher: сервера с модами и плагинами, адрес для входа, версия и ядро. Заходи и играй бесплатно.';
require __DIR__ . '/../_inc/head.php';

$resp = api_get('/servers');
$servers = $resp['servers'] ?? [];
// живые выделенные — вперёд
usort($servers, function ($a, $b) {
    return (($b['dedic'] ?? false) ? 1 : 0) - (($a['dedic'] ?? false) ? 1 : 0);
});
?>
<h1>Сервера</h1>
<p class="lead">Сервера игроков NareLauncher. Нажми «Играть», чтобы открыть сервер в лаунчере, или скопируй адрес и вставь в Minecraft.</p>

<?php if (!$servers): ?>
  <div class="panel"><p class="muted">Пока нет опубликованных серверов. Подними свой во вкладке «Мои серверы» в лаунчере — он появится здесь, пока запущен.</p></div>
<?php else: ?>
<div class="srv-list">
  <?php foreach ($servers as $s):
      $ip   = trim($s['ip'] ?? '');
      $hide = !empty($s['hideIp']);
      $name = $s['name'] ?? 'Сервер';
      $ver  = trim(($s['version'] ?? '') . ' ' . ($s['loader'] ?? ''));
  ?>
  <div class="srv">
    <div class="srv-main">
      <div class="srv-ico"><?= h(initial($name)) ?></div>
      <div class="srv-body">
        <div class="srv-name">
          <?= h($name) ?>
          <?php if (!empty($s['dedic'])): ?><span class="badge on">онлайн</span><?php endif; ?>
          <?php if ($ver): ?><span class="badge"><?= h($ver) ?></span><?php endif; ?>
        </div>
        <?php if (!empty($s['owner'])): ?><div class="muted">владелец: <a href="/profile/view/?user=<?= urlencode($s['owner']) ?>">@<?= h($s['owner']) ?></a></div><?php endif; ?>
        <?php if (!empty($s['desc'])): ?><div class="srv-desc"><?= h($s['desc']) ?></div><?php endif; ?>
        <?php if ($ip && !$hide): ?>
          <div class="srv-ip">Адрес: <code id="ip-<?= h($s['id']) ?>"><?= h($ip) ?></code></div>
        <?php elseif ($hide): ?>
          <div class="muted">Адрес скрыт владельцем</div>
        <?php endif; ?>
      </div>
    </div>
    <div class="srv-act">
      <?php if ($ip && !$hide): ?>
        <a class="btn" href="narelauncher://play?server=<?= urlencode($ip) ?>">Играть</a>
        <button class="btn ghost" type="button" onclick="copyIp('<?= h(addslashes($ip)) ?>', this)">Скопировать адрес</button>
      <?php else: ?>
        <span class="muted">—</span>
      <?php endif; ?>
    </div>
  </div>
  <?php endforeach; ?>
</div>
<?php endif; ?>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Как зайти на сервер</h2>
  <p class="muted">«Играть» откроет сервер в установленном лаунчере NareLauncher (игра запускается на вашем ПК — из браузера напрямую нельзя). Либо скопируйте адрес и в Minecraft выберите «Сетевая игра» → «Добавить сервер».</p>
</div>

<script>
function copyIp(ip, btn){
  navigator.clipboard.writeText(ip).then(function(){
    var t = btn.textContent; btn.textContent = 'Скопировано ✓';
    setTimeout(function(){ btn.textContent = t; }, 1500);
  });
}
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
