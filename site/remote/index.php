<?php
require_once __DIR__ . '/../_inc/config.php';
require_login();
$tok = token();

if (isset($_GET['exit'])) { unset($_SESSION['rtoken']); header('Location: /remote/'); exit; }

$err = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['password'])) {
    $r = api_post('/remote/auth', ['token' => $tok, 'password' => $_POST['password']]);
    if (!empty($r['ok'])) { $_SESSION['rtoken'] = $r['rtoken']; header('Location: /remote/'); exit; }
    $err = $r['error'] ?? 'Не удалось войти в пульт';
}

$state = api_post('/remote/state', ['token' => $tok]);
$hasPass = !empty($state['hasPassword']);
$online  = !empty($state['online']);
$authed  = !empty($_SESSION['rtoken']);

$PAGE_TITLE = 'Удалённое управление — NareLauncher';
$PAGE_DESC  = 'Управляйте лаунчером NareLauncher со своего телефона или другого компьютера: мышь, клики, клавиатура.';
require __DIR__ . '/../_inc/head.php';
?>
<h1>Удалённое управление</h1>

<?php if (!$authed): ?>
  <div class="panel" style="max-width:460px">
    <p class="muted" style="margin-top:0">
      Статус лаунчера:
      <?php if ($online): ?><span class="badge on">на связи</span><?php else: ?><span class="badge">не в сети</span><?php endif; ?>
    </p>
    <?php if (!$hasPass): ?>
      <p>Сначала задайте пароль удалённого управления в приложении: <b>Настройки → Удалённое управление</b>, включите тумблер и нажмите «Задать пароль».</p>
    <?php else: ?>
      <form method="post">
        <label style="display:block;color:var(--dim);font-size:13px;margin-bottom:4px">Пароль удалённого управления</label>
        <input name="password" type="password" autocomplete="off" required
               style="width:100%;background:#0a0d14;border:1px solid var(--border);color:var(--text);border-radius:9px;padding:11px 12px;font-size:15px">
        <button class="btn" type="submit" style="margin-top:14px">Подключиться</button>
        <?php if ($err): ?><div class="msg"><?= h($err) ?></div><?php endif; ?>
      </form>
      <p class="muted" style="margin-top:12px">Пароль вы задали в лаунчере. Это второй фактор — без него управлять нельзя.</p>
    <?php endif; ?>
  </div>
<?php else: ?>
  <div class="rc-bar">
    <span id="rc-status" class="badge">соединение…</span>
    <div class="rc-keys">
      <input id="rc-text" placeholder="Текст — Enter отправит в лаунчер" />
      <button class="btn ghost" type="button" onclick="rcKey('enter')">Enter</button>
      <button class="btn ghost" type="button" onclick="rcKey('backspace')">⌫</button>
      <button class="btn ghost" type="button" onclick="rcKey('esc')">Esc</button>
      <a class="btn ghost" href="/remote/?exit=1">Выйти из пульта</a>
    </div>
  </div>
  <div class="rc-screen-wrap">
    <img id="rc-screen" alt="экран лаунчера" draggable="false">
    <div id="rc-off" class="rc-off">Лаунчер не в сети. Откройте NareLauncher на ПК и включите удалённое управление в настройках.</div>
  </div>
  <p class="muted" style="margin-top:10px">Кликайте прямо по картинке — курсор на ПК повторит. Колесо мыши и правый клик тоже работают. Небольшая задержка — это нормально.</p>

<script>
var screenImg = document.getElementById('rc-screen');
var statusEl  = document.getElementById('rc-status');
var offEl     = document.getElementById('rc-off');
var queue = [];
var lastMove = 0;

function push(ev){ queue.push(ev); if (queue.length > 40) queue.shift(); flush(); }
var flushing = false;
function flush(){
  if (flushing || !queue.length) return;
  flushing = true;
  var batch = queue; queue = [];
  fetch('/remote/input.php', {method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify({events: batch})})
    .catch(function(){}).finally(function(){ flushing = false; if (queue.length) flush(); });
}

function frac(e){
  var r = screenImg.getBoundingClientRect();
  var p = (e.touches && e.touches[0]) ? e.touches[0] : e;
  return { x: Math.max(0, Math.min(1, (p.clientX - r.left) / r.width)),
           y: Math.max(0, Math.min(1, (p.clientY - r.top) / r.height)) };
}

screenImg.addEventListener('mousemove', function(e){
  var now = Date.now(); if (now - lastMove < 110) return; lastMove = now;
  var f = frac(e); push({t:'move', x:f.x, y:f.y});
});
screenImg.addEventListener('click', function(e){ var f=frac(e); push({t:'click', x:f.x, y:f.y}); });
screenImg.addEventListener('dblclick', function(e){ var f=frac(e); push({t:'dbl', x:f.x, y:f.y}); });
screenImg.addEventListener('contextmenu', function(e){ e.preventDefault(); var f=frac(e); push({t:'rclick', x:f.x, y:f.y}); });
screenImg.addEventListener('wheel', function(e){ e.preventDefault(); var f=frac(e); push({t:'scroll', x:f.x, y:f.y, dy: e.deltaY>0?-1:1}); }, {passive:false});

// текст и спец-клавиши
var textBox = document.getElementById('rc-text');
textBox.addEventListener('keydown', function(e){
  if (e.key === 'Enter'){
    e.preventDefault();
    if (textBox.value){ push({t:'text', text:textBox.value}); textBox.value=''; }
    push({t:'key', key:'enter'});
  }
});
function rcKey(k){ push({t:'key', key:k}); }
window.rcKey = rcKey;

// опрос кадра
function poll(){
  fetch('/remote/frame.php').then(function(r){return r.json();}).then(function(j){
    if (j && j.ok){
      statusEl.textContent = j.online ? 'на связи' : 'не в сети';
      statusEl.className = 'badge ' + (j.online ? 'on' : '');
      offEl.style.display = j.online ? 'none' : 'block';
      if (j.jpg){ screenImg.src = 'data:image/jpeg;base64,' + j.jpg; }
    }
  }).catch(function(){ statusEl.textContent='нет связи с сервером'; })
    .finally(function(){ setTimeout(poll, 480); });
}
poll();
</script>
<?php endif; ?>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
