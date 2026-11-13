<?php
require_once __DIR__ . '/../_inc/config.php';
require_login();
$tok = token();

// Проверяем права: берём состояние (needStaff). Если не админ/модератор — 403 от API.
$state = api_post('/admin/state', ['token' => $tok]);
if (empty($state['ok'])) {
    $PAGE_TITLE = 'Админ-панель — NareLauncher';
    require __DIR__ . '/../_inc/head.php';
    echo '<div class="panel"><h1>Нет доступа</h1><p class="muted">Эта страница только для администраторов и модераторов.</p><p><a class="btn ghost" href="/profile/my/">В профиль</a></p></div>';
    require __DIR__ . '/../_inc/foot.php';
    exit;
}

$msg = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $act = $_POST['act'] ?? '';
    if ($act === 'broadcast') {
        $r = api_post('/admin/broadcast', ['token' => $tok, 'text' => $_POST['text'] ?? '', 'seconds' => (int)($_POST['seconds'] ?? 0)]);
        $msg = !empty($r['ok']) ? 'Объявление отправлено.' : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'restart') {
        $r = api_post('/admin/restart', ['token' => $tok, 'what' => $_POST['what'] ?? '']);
        $msg = !empty($r['ok']) ? 'Перезапуск запрошен.' : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'command') {
        $r = api_post('/admin/command', ['token' => $tok, 'text' => $_POST['cmd'] ?? '']);
        $msg = !empty($r['ok']) ? 'Команда поставлена в очередь.' : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'approve') {
        $r = api_post('/admin/server/approve', ['token' => $tok, 'id' => $_POST['id'] ?? '']);
        $msg = !empty($r['ok']) ? 'Сервер одобрен.' : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'reject') {
        $r = api_post('/admin/server/reject', ['token' => $tok, 'id' => $_POST['id'] ?? '', 'reason' => $_POST['reason'] ?? '']);
        $msg = !empty($r['ok']) ? 'Сервер отклонён.' : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'streamban') {
        $r = api_post('/admin/stream/ban', ['token' => $tok, 'nick' => $_POST['nick'] ?? '', 'on' => ($_POST['on'] ?? '') === '1']);
        $msg = !empty($r['ok']) ? ($r['banned'] ? 'Трансляции запрещены игроку.' : 'Запрет на трансляции снят.') : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'apicode') {
        $r = api_post('/admin/apicode', ['token' => $tok, 'regen' => true]);
        $msg = !empty($r['ok']) ? 'Новый API-код сгенерирован.' : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'emupdate') {
        $r = api_post('/admin/update', ['token' => $tok, 'version' => $_POST['version'] ?? '', 'changelog' => $_POST['changelog'] ?? '', 'critical' => ($_POST['critical'] ?? '') === '1']);
        $msg = !empty($r['ok']) ? 'Обновление объявлено всем.' : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'twofaoff') {
        $r = api_post('/admin/twofa/off', ['token' => $tok, 'nick' => $_POST['nick'] ?? '']);
        $msg = !empty($r['ok']) ? ('2FA снята: ' . h($r['cleared'])) : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'aiconfig') {
        $r = api_post('/admin/ai/config', ['token' => $tok, 'set' => true,
            'provider' => $_POST['provider'] ?? '', 'model' => $_POST['model'] ?? '',
            'base' => $_POST['base'] ?? '', 'key' => $_POST['key'] ?? '',
            'enabled' => ($_POST['enabled'] ?? '') === '1']);
        $msg = !empty($r['ok']) ? 'Настройки ИИ сохранены.' : ($r['error'] ?? 'Ошибка');
    } elseif ($act === 'aiforget') {
        $r = api_post('/admin/ai/config', ['token' => $tok, 'clearMemory' => true]);
        $msg = !empty($r['ok']) ? 'Память ИИ очищена.' : ($r['error'] ?? 'Ошибка');
    }
    // перечитываем состояние после действия
    $state = api_post('/admin/state', ['token' => $tok]);
}
$apiCode = api_post('/admin/apicode', ['token' => $tok])['code'] ?? '';
$aiCfg = api_post('/admin/ai/config', ['token' => $tok])['ai'] ?? [];
$srvResp = api_post('/admin/servers', ['token' => $tok]);
$allServers = $srvResp['servers'] ?? [];
$pending = array_values(array_filter($allServers, fn($x) => ($x['status'] ?? '') === 'pending'));
$st = $state['stats'] ?? [];

$PAGE_TITLE = 'Админ-панель — NareLauncher';
$PAGE_DESC  = 'Управление NareLauncher: объявления, перезапуск, модерация серверов.';
require __DIR__ . '/../_inc/head.php';
?>
<h1>Админ-панель</h1>
<?php if ($msg): ?><div class="msg ok"><?= h($msg) ?></div><?php endif; ?>
<p style="margin:10px 0"><a class="btn" href="/admin/servers.php">⚙ Серверы игроков (консоль и управление)</a></p>

<div class="grid" style="margin-top:12px">
  <div class="card"><h3>Аккаунты</h3><p style="font-size:22px"><?= (int)($st['accounts'] ?? 0) ?></p></div>
  <div class="card"><h3>Онлайн</h3><p style="font-size:22px"><?= (int)($st['online'] ?? 0) ?></p></div>
  <div class="card"><h3>Баны</h3><p style="font-size:22px"><?= (int)($st['bans'] ?? 0) ?></p></div>
  <div class="card"><h3>Серверы на модерации</h3><p style="font-size:22px"><?= (int)($st['pendingServers'] ?? 0) ?></p></div>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Объявление всем</h2>
  <p class="muted">Текст появится вверху лаунчера у всех. Ваш ник и время подставятся сами.</p>
  <form method="post" class="row-form">
    <input type="hidden" name="act" value="broadcast">
    <input name="text" placeholder="Текст объявления" maxlength="300" required style="flex:1">
    <input name="seconds" type="number" min="0" max="3600" value="30" title="сколько секунд показывать (0 — пока не уберёте)" style="width:120px">
    <button class="btn" type="submit">Отправить</button>
  </form>
  <form method="post" style="margin-top:8px">
    <input type="hidden" name="act" value="broadcast">
    <input type="hidden" name="text" value="">
    <button class="btn ghost" type="submit">Убрать объявление</button>
  </form>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Перезапуск</h2>
  <div class="btn-row">
    <form method="post"><input type="hidden" name="act" value="restart"><input type="hidden" name="what" value="launchers"><button class="btn ghost" type="submit">Перезапустить лаунчеры</button></form>
    <form method="post"><input type="hidden" name="act" value="restart"><input type="hidden" name="what" value="servers"><button class="btn ghost" type="submit">Перезапустить сервера</button></form>
    <form method="post" onsubmit="return confirm('Перезапустить всё — лаунчеры и сервера?')"><input type="hidden" name="act" value="restart"><input type="hidden" name="what" value="all"><button class="btn" type="submit">Перезапустить всё</button></form>
  </div>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">🤖 Настоящий ИИ (Наря)</h2>
  <p class="muted">Подключи любой <b>бесплатный</b> провайдер ИИ — и помощник на сайте оживёт: будет отвечать как живой, искать по базе, создавать каналы/ботов, смотреть присланные картинки и учиться в файл на сервере.</p>
  <ul class="muted" style="margin:6px 0 12px;font-size:14px;line-height:1.6">
    <li><b>Groq</b> (быстрый, бесплатный): провайдер <code>openai</code>, адрес <code>https://api.groq.com/openai/v1</code>, модель напр. <code>llama-3.3-70b-versatile</code>. Ключ — на console.groq.com.</li>
    <li><b>OpenRouter</b> (много бесплатных моделей): провайдер <code>openai</code>, адрес <code>https://openrouter.ai/api/v1</code>, модель <code>openrouter/free</code> (сама выбирает любую свободную бесплатную). Ключ — на openrouter.ai. <i>Бесплатные модели часто лимитируются — сервер перебирает запасные автоматически.</i></li>
    <li><b>Google Gemini</b> (бесплатный ключ): провайдер <code>gemini</code>, модель напр. <code>gemini-1.5-flash</code>, адрес не нужен. Ключ — на aistudio.google.com.</li>
  </ul>
  <p class="muted" style="font-size:13px">Сейчас:
    <b><?= $aiCfg ? h(($aiCfg['provider'] ?: '—')) : '—' ?></b>,
    модель <b><?= h(($aiCfg['model'] ?? '') ?: '—') ?></b>,
    ключ <b><?= !empty($aiCfg['hasKey']) ? h($aiCfg['keyHint']) : 'не задан' ?></b>,
    <b><?= !empty($aiCfg['enabled']) ? 'включён' : 'выключен' ?></b>.
    <?php if (!empty($aiCfg['brain'])): $br=$aiCfg['brain']; ?>
    <br>Локальный мозг: фраз <b><?= (int)$br['phrases'] ?></b>, пар вопрос→ответ <b><?= (int)$br['qa'] ?></b>, обработано сообщений <b><?= (int)$br['seen'] ?></b>.
    <?php endif; ?>
  </p>
  <div style="margin:4px 0 10px;display:flex;gap:8px;flex-wrap:wrap">
    <button type="button" class="btn ghost" onclick="aiPreset('local')">🧠 Локальный (бесплатно)</button>
    <button type="button" class="btn ghost" onclick="aiPreset('openrouter')">⚡ OpenRouter</button>
    <button type="button" class="btn ghost" onclick="aiPreset('groq')">🚀 Groq</button>
    <button type="button" class="btn ghost" onclick="aiPreset('gemini')">✨ Gemini</button>
  </div>
  <form method="post" style="display:grid;gap:8px;max-width:560px" id="aiForm">
    <input type="hidden" name="act" value="aiconfig">
    <label>Провайдер
      <select name="provider" id="aiProvider" style="width:100%">
        <option value="local"<?= (($aiCfg['provider'] ?? '')==='local')?' selected':'' ?>>local — локальный самообучающийся (без ключа)</option>
        <option value="openai"<?= (($aiCfg['provider'] ?? '')==='openai')?' selected':'' ?>>openai-совместимый (Groq / OpenRouter / …)</option>
        <option value="gemini"<?= (($aiCfg['provider'] ?? '')==='gemini')?' selected':'' ?>>gemini (Google)</option>
      </select>
    </label>
    <label>Модель <input name="model" id="aiModel" value="<?= h($aiCfg['model'] ?? '') ?>" placeholder="meta-llama/llama-3.3-70b-instruct:free" style="width:100%"></label>
    <label>Адрес API (для openai-совместимых) <input name="base" id="aiBase" value="<?= h($aiCfg['base'] ?? '') ?>" placeholder="https://openrouter.ai/api/v1" style="width:100%"></label>
    <label>Ключ API <input name="key" type="password" id="aiKey" placeholder="<?= !empty($aiCfg['hasKey']) ? 'оставь пустым, чтобы не менять' : 'вставь ключ (sk-or-v1-… для OpenRouter)' ?>" style="width:100%"></label>
    <label style="display:flex;align-items:center;gap:8px"><input type="checkbox" name="enabled" value="1"<?= !empty($aiCfg['enabled'])?' checked':'' ?>> Включить ИИ</label>
    <div style="display:flex;gap:8px"><button class="btn" type="submit">Сохранить</button></div>
  </form>
  <script>
  function aiPreset(p){
    var prov=document.getElementById('aiProvider'), model=document.getElementById('aiModel'),
        base=document.getElementById('aiBase'), en=document.querySelector('#aiForm input[name=enabled]');
    if(p==='local'){ prov.value='local'; model.value=''; base.value=''; }
    else if(p==='openrouter'){ prov.value='openai'; base.value='https://openrouter.ai/api/v1'; model.value='openrouter/free'; }
    else if(p==='groq'){ prov.value='openai'; base.value='https://api.groq.com/openai/v1'; model.value='llama-3.3-70b-versatile'; }
    else if(p==='gemini'){ prov.value='gemini'; base.value=''; model.value='gemini-1.5-flash'; }
    if(en) en.checked=true;
    document.getElementById('aiKey').focus();
  }
  </script>
  <div style="margin-top:12px;display:flex;gap:8px;align-items:center;flex-wrap:wrap">
    <button type="button" class="btn" id="aiTestBtn">🔍 Проверить ИИ</button>
    <span id="aiTestSt" class="muted" style="font-size:13px"></span>
  </div>
  <div id="aiTestOut" style="display:none;margin-top:8px;padding:12px;border:1px solid var(--border);border-radius:10px;background:rgba(128,128,128,.06);font-size:14px"></div>

  <?php $aiLogs = api_post('/admin/ai/config', ['token' => $tok])['logs'] ?? []; ?>
  <div style="margin-top:14px">
    <b>📜 Журнал ИИ (последние запросы):</b>
    <?php if (!$aiLogs): ?>
      <p class="muted" style="font-size:13px">Пока пусто — задай вопрос Наря или нажми «Проверить ИИ».</p>
    <?php else: ?>
      <div style="margin-top:6px;max-height:260px;overflow:auto;border:1px solid var(--border);border-radius:10px">
        <table class="tbl" style="margin:0;font-size:13px">
          <tr><th>Время</th><th>Кто ответил</th><th>Детали</th></tr>
          <?php foreach ($aiLogs as $lg):
            $t = date('H:i:s', (int)(($lg['at'] ?? 0)/1000));
            $ok = !empty($lg['ok']);
            if (($lg['provider'] ?? '') === 'local') { $who = '🧠 локальный'; $det = $lg['reason'] ?? ''; }
            elseif ($ok) { $who = '✅ внешний ИИ'; $det = 'модель: '.h($lg['used'] ?: ($lg['model'] ?? '')); }
            else { $who = '❌ ошибка внешнего'; $det = (isset($lg['code'])?('HTTP '.$lg['code'].' · '):'').h($lg['error'] ?? ''); }
          ?>
            <tr><td><?= $t ?></td><td><?= $who ?></td><td style="max-width:360px;word-break:break-word"><?= $det ?></td></tr>
          <?php endforeach; ?>
        </table>
      </div>
    <?php endif; ?>
  </div>
  <script>
  (function(){
    var TOK=<?= json_encode($tok) ?>;
    var btn=document.getElementById('aiTestBtn'), st=document.getElementById('aiTestSt'), out=document.getElementById('aiTestOut');
    btn.onclick=function(){
      btn.disabled=true; st.textContent='Отправляю тестовый запрос…'; out.style.display='none';
      var q='Привет! Это тест. Ответь одним коротким предложением, что ты работаешь.';
      fetch('/ai/chat',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({token:TOK,text:q,history:[]})})
        .then(function(r){return r.json();})
        .then(function(j){
          btn.disabled=false;
          var badge, color;
          if(j && j.local){ badge='🧠 Ответил ЛОКАЛЬНЫЙ мозг'+(j.note?(' ('+j.note+')'):' (внешний ИИ не задан или недоступен)'); color='#ffd24a'; }
          else if(j && j.configured){ badge='✅ Ответил ВНЕШНИЙ ИИ (OpenRouter/Groq/Gemini)'; color='#4bb34b'; }
          else { badge='⚠ ИИ не настроен'; color='#ff6b74'; }
          st.innerHTML='<b style="color:'+color+'">'+badge+'</b>';
          out.style.display='block';
          var extra='';
          if(j && j.extError){ extra='<div style="margin-top:8px;padding:8px;border-radius:8px;background:rgba(255,107,116,.12);color:#ff6b74;font-size:13px"><b>Ошибка внешнего ИИ:</b> '+String(j.extError).replace(/[<>]/g,'')+'</div>'; }
          out.innerHTML='<div class="muted" style="font-size:12px;margin-bottom:4px">Ответ Наря:</div>'+((j&&j.reply)?String(j.reply).replace(/[<>]/g,''):'(пусто)')+extra;
        })
        .catch(function(){ btn.disabled=false; st.innerHTML='<b style="color:#ff6b74">Ошибка сети — сервер ИИ не ответил</b>'; });
    };
  })();
  </script>
  <form method="post" style="margin-top:8px" onsubmit="return confirm('Очистить всё, что ИИ выучил?')">
    <input type="hidden" name="act" value="aiforget">
    <button class="btn ghost" type="submit">Очистить память ИИ</button>
  </form>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Снять 2FA с игрока</h2>
  <p class="muted">Если игрок (или ты сам) застрял на подтверждении входа — убери у него защиту входа. Можно <code>all</code>, чтобы снять у всех.</p>
  <form method="post" class="row-form">
    <input type="hidden" name="act" value="twofaoff">
    <input name="nick" placeholder="Ник игрока (или all)" maxlength="16" required style="flex:1">
    <button class="btn" type="submit">Снять 2FA</button>
  </form>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Запрет трансляций</h2>
  <p class="muted">Запретить или разрешить игроку вести прямые эфиры.</p>
  <form method="post" class="row-form">
    <input type="hidden" name="act" value="streamban">
    <input name="nick" placeholder="Ник игрока" maxlength="16" required style="flex:1">
    <button class="btn" type="submit" name="on" value="1">Запретить</button>
    <button class="btn ghost" type="submit" name="on" value="0">Разрешить</button>
  </form>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Экстренное обновление всем</h2>
  <p class="muted">Все лаунчеры получат уведомление об обновлении. «Критическое» — обязательное, без него не пустит в игру.</p>
  <form method="post" class="row-form">
    <input type="hidden" name="act" value="emupdate">
    <input name="version" placeholder="Версия (например 1.90)" maxlength="32" required style="width:180px">
    <input name="changelog" placeholder="Что нового" maxlength="400" style="flex:1">
    <label class="muted" style="display:flex;align-items:center;gap:6px"><input type="checkbox" name="critical" value="1"> критическое</label>
    <button class="btn" type="submit">Объявить</button>
  </form>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Управление по URL (API-код)</h2>
  <p class="muted">Секретный код, которым можно перезапустить/обновить всех прямо по ссылке — из закладки или скрипта, без входа. Держите его в тайне.</p>
  <p>Код: <code id="apicode"><?= h($apiCode ?: '—') ?></code>
     <button class="btn ghost" type="button" onclick="navigator.clipboard.writeText(document.getElementById('apicode').textContent)">Скопировать</button></p>
  <form method="post" onsubmit="return confirm('Сменить код? Старые ссылки перестанут работать.')" style="margin-bottom:12px">
    <input type="hidden" name="act" value="apicode"><button class="btn ghost" type="submit">Сгенерировать новый код</button>
  </form>
  <?php $base = SITE; $c = urlencode($apiCode); ?>
  <p class="muted">Готовые ссылки (открой в браузере):</p>
  <ul style="line-height:1.9;word-break:break-all">
    <li>Перезапустить всё: <code><?= h($base) ?>/admin/api/restart?code=<?= $c ?>&what=all</code></li>
    <li>Перезапустить только лаунчеры: <code><?= h($base) ?>/admin/api/restart?code=<?= $c ?>&what=launchers</code></li>
    <li>Перезапустить только серверы: <code><?= h($base) ?>/admin/api/restart?code=<?= $c ?>&what=servers</code></li>
    <li>Экстренное обновление: <code><?= h($base) ?>/admin/api/update?code=<?= $c ?>&version=1.90&critical=1</code></li>
    <li>Объявление: <code><?= h($base) ?>/admin/api/broadcast?code=<?= $c ?>&text=Привет&seconds=30</code></li>
    <li>Снять 2FA (аварийно): <code><?= h($base) ?>/admin/api/twofa?code=<?= $c ?>&nick=all</code></li>
    <li>Закрыть ServerManager у всех: <code><?= h($base) ?>/admin/api/sm?code=<?= $c ?>&nick=all</code></li>
  </ul>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Команда серверу</h2>
  <p class="muted">Выполнится на сервере WMine (например: <code>say привет</code>, <code>weather clear</code>).</p>
  <form method="post" class="row-form">
    <input type="hidden" name="act" value="command">
    <input name="cmd" placeholder="Команда без /" maxlength="300" required style="flex:1">
    <button class="btn" type="submit">Выполнить</button>
  </form>
</div>

<div class="panel" style="margin-top:18px">
  <h2 style="margin-top:0">Заявки на публикацию серверов <?= $pending ? '(' . count($pending) . ')' : '' ?></h2>
  <?php if (!$pending): ?>
    <p class="muted">Нет заявок на модерации.</p>
  <?php else: foreach ($pending as $s): ?>
    <div class="srv">
      <div class="srv-main">
        <div class="srv-ico"><?= h(initial($s['name'] ?? '?')) ?></div>
        <div class="srv-body">
          <div class="srv-name"><?= h($s['name'] ?? '') ?> <?php if (!empty($s['version'])): ?><span class="badge"><?= h($s['version'] . ' ' . ($s['loader'] ?? '')) ?></span><?php endif; ?></div>
          <div class="muted">от @<?= h($s['owner'] ?? '') ?> · <?= !empty($s['hideIp']) ? 'IP скрыт' : h($s['ip'] ?? '') ?></div>
          <?php if (!empty($s['desc'])): ?><div class="srv-desc"><?= h($s['desc']) ?></div><?php endif; ?>
        </div>
      </div>
      <div class="srv-act">
        <form method="post"><input type="hidden" name="act" value="approve"><input type="hidden" name="id" value="<?= h($s['id']) ?>"><button class="btn" type="submit">Одобрить</button></form>
        <form method="post" onsubmit="this.reason.value=prompt('Причина отказа (необязательно):')||'';return true;"><input type="hidden" name="act" value="reject"><input type="hidden" name="id" value="<?= h($s['id']) ?>"><input type="hidden" name="reason" value=""><button class="btn ghost" type="submit">Отклонить</button></form>
      </div>
    </div>
  <?php endforeach; endif; ?>
</div>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
