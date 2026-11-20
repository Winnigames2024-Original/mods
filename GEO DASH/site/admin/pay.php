<?php
/**
 * Оплата ключей — настройка для админа.
 *
 * Всё держится на приложении ЮMoney: кошелёк принимает деньги, а приложение
 * даёт сайту право спросить у ЮMoney, пришёл ли платёж с меткой заказа.
 * HTTP-уведомления не нужны — они остались как запасной путь и настраиваются
 * только файлом на сервере, если когда-нибудь понадобятся.
 */
require_once __DIR__ . '/../_inc/api.php';
require_once __DIR__ . '/../_inc/pay.php';
gd_login_required();
if (!gd_is_admin()) {
    http_response_code(403);
    gd_head('Оплата');
    echo '<section class="section"><div class="container"><h2>Только для ' . h(GD_ADMIN) . '</h2></div></section>';
    gd_foot();
    exit;
}

$saved = ''; $failed = '';

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $act = $_POST['act'] ?? '';

    if ($act === 'save') {
        $patch = ['price' => max(1, (int)($_POST['price'] ?? 199))];

        $recv = preg_replace('/\D+/', '', (string)($_POST['receiver'] ?? ''));
        if ($recv !== '' && strlen($recv) < 11) {
            $failed = 'Номер кошелька — 16 цифр, обычно начинается на 4100';
        } else {
            $patch['receiver'] = $recv;
        }

        // пустое поле — оставить прежнее значение, а не стереть его
        $cid = trim((string)($_POST['clientId'] ?? ''));
        if ($cid !== '') $patch['clientId'] = $cid;
        $cs = trim((string)($_POST['clientSecret'] ?? ''));
        if ($cs !== '') $patch['clientSecret'] = $cs;

        if ($failed === '') {
            [$ok, $r] = gd_pay_store_set($patch);
            if ($ok) $saved = 'Сохранено и уже работает';
            else $failed = (string)($r['error'] ?? 'Сервер игры не отвечает — не сохранено');
        }
    }

    if ($act === 'forget') {
        [$ok, $r] = gd_pay_store_set(['clientId' => null, 'clientSecret' => null, 'ymToken' => null]);
        if ($ok) $saved = 'Приложение отключено'; else $failed = 'Не вышло отключить';
    }

    if ($act === 'offsale') {
        [$ok, $r] = gd_pay_store_set(['receiver' => null]);
        if ($ok) $saved = 'Продажа выключена'; else $failed = 'Не вышло выключить';
    }

    if ($act === 'checkorder') {
        [$pok, $ptext] = gd_pay_poll(trim((string)($_POST['order'] ?? '')));
        if ($pok) $saved = $ptext; else $failed = $ptext;
    }
}

$host  = $_SERVER['HTTP_HOST'] ?? 'geodash.winnigames2024.ru';
$conf  = gd_pay_conf(true);
$ready = gd_pay_ready();
$app   = gd_ym_ready();

$hasRecv = (string)$conf['receiver'] !== '';
$hasId   = !empty($conf['clientId']);
$hasSec  = !empty($conf['clientSecret']);

[$apiOk, $stats] = gd_pay_stats();
$events = gd_pay_events();

$mask = function (string $s): string {
    $n = strlen($s);
    return $n <= 6 ? str_repeat('•', $n) : substr($s, 0, 4) . str_repeat('•', max(0, $n - 8)) . substr($s, -4);
};
$dot = fn(bool $ok) => $ok
    ? '<span class="badge" style="background:#1d6;color:#042">готово</span>'
    : '<span class="badge" style="background:#a33">нужно сделать</span>';

// ссылка разрешения доступа — обычная, по ней просто переходят
$authUrl = 'https://yoomoney.ru/oauth/authorize?' . http_build_query([
    'client_id' => (string)($conf['clientId'] ?? ''),
    'response_type' => 'code',
    'redirect_uri' => gd_ym_redirect($host),
    'scope' => GD_YM_SCOPE,
]);

gd_head('Оплата ключей', 'admin');
?>
<style>
  .step { border-left:4px solid #4af; padding:2px 0 2px 16px; margin:26px 0 }
  .step.done { border-color:#1d6 } .step.wait { border-color:#a33 }
  .step h3 { margin:0 0 8px }
  .ym-code { background:#0d0b22; border:2px solid #000; border-radius:10px; padding:12px 14px;
             overflow-x:auto; font-size:14px; line-height:1.5; color:#cfe3ff; white-space:pre;
             display:inline-block; vertical-align:middle }
  .ym-copy { cursor:pointer }
  .ok-line { color:#8f8 } .no-line { color:#f99 }
</style>

<section class="section"><div class="container" style="max-width:900px">
  <p><a class="btn small ghost" href="/admin/">← В админку</a>
     <a class="btn small ghost" href="/admin/docs.php">Документация</a></p>

  <h2>Оплата ключей — ЮMoney</h2>
  <?php if ($saved): ?><div class="alert ok"><?= h($saved) ?></div><?php endif; ?>
  <?php if ($failed): ?><div class="alert err"><?= h($failed) ?></div><?php endif; ?>

  <p class="muted" style="margin-top:-4px">
    Игрок платит на странице <a href="/buy/">/buy/</a> — картой или кошельком, выбор на его стороне.
    Деньги приходят на ваш кошелёк, а приложение ЮMoney даёт сайту право спросить у неё,
    пришёл ли платёж, и выдать ключ. Больше ничего настраивать не нужно.
  </p>

  <div class="card">
    <div class="stats" style="margin:0">
      <div class="stat"><b><?= $ready ? 'ВКЛ' : 'ВЫКЛ' ?></b><span>продажа ключей</span></div>
      <div class="stat"><b><?= $app ? 'ДА' : 'НЕТ' ?></b><span>приложение подключено</span></div>
      <div class="stat"><b><?= (int)$conf['price'] ?> ₽</b><span>цена ключа</span></div>
      <div class="stat"><b><?= $hasRecv ? h($mask((string)$conf['receiver'])) : '—' ?></b><span>кошелёк</span></div>
    </div>
    <?php if (!$ready): ?>
      <p class="muted" style="margin-top:14px">Пока продажа выключена, страница покупки честно пишет об этом,
        а ключи выдаются ссылкой и из админки — игра работает как обычно.</p>
    <?php elseif (!$app): ?>
      <p style="margin-top:14px;color:#ffd24a">Кошелёк указан, но приложение не подключено:
        оплату придётся подтверждать вручную — кнопкой «Спросить у ЮMoney» внизу или выдачей ключа из админки.</p>
    <?php endif; ?>
  </div>

  <!-- 1 -->
  <div class="step <?= $hasId ? 'done' : 'wait' ?>">
    <h3>Шаг 1. Зарегистрировать приложение <?= $dot($hasId) ?></h3>
    <p>Откройте <a href="https://yoomoney.ru/myservices/new" target="_blank" rel="noopener">yoomoney.ru/myservices/new</a>
      и заполните так:</p>
    <table class="lv">
      <tr><th>Поле</th><th>Что вписать</th></tr>
      <tr><td>Название для пользователей</td>
          <td><code id="ymName">Оплата</code>
              <button class="btn small ghost ym-copy" data-copy="ymName" type="button">Скопировать</button></td></tr>
      <tr><td>Адрес сайта</td>
          <td><code id="ymSite">https://<?= h($host) ?></code>
              <button class="btn small ghost ym-copy" data-copy="ymSite" type="button">Скопировать</button></td></tr>
      <tr><td>Почта для связи</td><td>ваша почта</td></tr>
      <tr><td>Redirect URI</td>
          <td><code id="ymRedir"><?= h(gd_ym_redirect($host)) ?></code>
              <button class="btn small ghost ym-copy" data-copy="ymRedir" type="button">Скопировать</button></td></tr>
      <tr><td>Notification URI</td>
          <td><code id="ymNotif"><?= h(gd_ym_notify_url($host)) ?></code>
              <button class="btn small ghost ym-copy" data-copy="ymNotif" type="button">Скопировать</button></td></tr>
      <tr><td>Логотип</td><td>по желанию: <code>https://<?= h($host) ?>/assets/icon.png</code></td></tr>
      <tr><td>Проверять подлинность (client_secret)</td><td>поставьте галочку</td></tr>
    </table>
    <p class="muted">Redirect URI ЮMoney сверяет знак в знак — лучше копировать кнопкой.
      Это та же страница, куда возвращается покупатель после оплаты: она понимает и код
      авторизации, и одноразовый код выдачи ключа.</p>
  </div>

  <!-- 2 -->
  <div class="step <?= ($hasRecv && $hasId) ? 'done' : 'wait' ?>">
    <h3>Шаг 2. Ввести данные <?= $dot($hasRecv && $hasId) ?></h3>
    <p>Номер кошелька — куда приходят деньги. client_id и client_secret ЮMoney показывает
      сразу после регистрации приложения.</p>

    <form class="form" method="post">
      <input type="hidden" name="act" value="save">
      <div class="grid c2" style="gap:16px">
        <div>
          <label>Номер кошелька ЮMoney</label>
          <input type="text" name="receiver" value="<?= h((string)$conf['receiver']) ?>"
                 placeholder="4100XXXXXXXXXXX" inputmode="numeric" autocomplete="off">
          <p class="muted" style="margin:6px 0 0">16 цифр, видны в кошельке на yoomoney.ru.</p>
        </div>
        <div>
          <label>Цена ключа, ₽</label>
          <input type="number" name="price" min="1" max="100000" value="<?= (int)$conf['price'] ?>">
        </div>
        <div>
          <label>client_id</label>
          <input type="text" name="clientId" value="<?= h((string)($conf['clientId'] ?? '')) ?>"
                 autocomplete="off" placeholder="из регистрации приложения">
        </div>
        <div>
          <label>client_secret</label>
          <input type="password" name="clientSecret" autocomplete="new-password"
                 placeholder="<?= $hasSec ? 'сохранён — оставьте пустым' : 'из регистрации приложения' ?>">
          <p class="muted" style="margin:6px 0 0">
            <?= $hasSec
                ? '<span class="ok-line">сохранён ✓</span> — поле пустое не потому, что он сбросился:
                   обратно он не показывается. Заполняйте, только если меняете.'
                : '<span class="no-line">не задан</span>' ?>
          </p>
        </div>
      </div>
      <p style="margin-top:18px">
        <button class="btn green" type="submit">ПРИМЕНИТЬ</button>
        <?php if ($hasRecv): ?>
          <button class="btn small ghost" type="submit" name="act" value="offsale" formnovalidate>Выключить продажу</button>
        <?php endif; ?>
        <?php if ($hasId || $app): ?>
          <button class="btn small ghost" type="submit" name="act" value="forget" formnovalidate>Отключить приложение</button>
        <?php endif; ?>
      </p>
    </form>
  </div>

  <!-- 3 -->
  <div class="step <?= $app ? 'done' : 'wait' ?>">
    <h3>Шаг 3. Разрешить доступ <?= $dot($app) ?></h3>
    <?php if (!$hasId): ?>
      <p class="muted">Сначала впишите client_id на шаге 2.</p>
    <?php else: ?>
      <p>Нажмите кнопку — откроется страница ЮMoney, там нужно нажать «Разрешить».
        Вернётесь на сайт уже подключённым.</p>
      <p>
        <a class="btn gold" href="<?= h($authUrl) ?>" target="_blank" rel="noopener">
          <?= $app ? 'ПОДКЛЮЧИТЬ ЗАНОВО' : 'РАЗРЕШИТЬ ДОСТУП' ?></a>
      </p>
      <details style="margin-top:12px">
        <summary class="muted" style="cursor:pointer">Не открылось или ЮMoney ругается — есть запасной способ</summary>
        <form method="POST" action="https://yoomoney.ru/oauth/authorize" target="_blank" style="margin-top:10px">
          <input type="hidden" name="client_id" value="<?= h((string)$conf['clientId']) ?>">
          <input type="hidden" name="response_type" value="code">
          <input type="hidden" name="redirect_uri" value="<?= h(gd_ym_redirect($host)) ?>">
          <input type="hidden" name="scope" value="<?= h(GD_YM_SCOPE) ?>">
          <button class="btn small" type="submit">ОТКРЫТЬ ФОРМОЙ</button>
        </form>
        <p class="muted" style="margin-top:10px">Или скопируйте ссылку и вставьте в браузер:</p>
        <div class="ym-code" id="ymAuth"><?= h($authUrl) ?></div>
        <button class="btn small ghost ym-copy" data-copy="ymAuth" type="button">Скопировать ссылку</button>
      </details>
      <p class="muted" style="margin-top:12px">Запрашиваем два права: <code>account-info</code> и
        <code>operation-history</code> — только чтение. Ни переводов, ни списаний приложение делать не может.</p>
    <?php endif; ?>
  </div>

  <?php if ($app): ?>
  <div class="card">
    <h3>Проверить оплату вручную</h3>
    <p class="muted">Обычно ключ выдаётся сам, когда покупатель возвращается на сайт.
      Если он закрыл страницу — спросите у ЮMoney по номеру заказа.</p>
    <form method="post" class="flex" style="gap:8px;flex-wrap:wrap">
      <input type="hidden" name="act" value="checkorder">
      <input type="text" name="order" placeholder="gd…" style="max-width:300px"
             value="<?= h((string)($_POST['order'] ?? '')) ?>">
      <button class="btn small" type="submit">СПРОСИТЬ У ЮMONEY</button>
    </form>
  </div>
  <?php endif; ?>

  <div class="card">
    <h3>Журнал оплат</h3>
    <?php if (!$apiOk): ?>
      <p class="muted">Нет связи с API игры — журнал недоступен.</p>
    <?php elseif (!$events): ?>
      <p class="muted">Записей пока нет. Как только пройдёт первая оплата — она появится здесь.</p>
    <?php else: ?>
      <table class="lv">
        <tr><th>Когда</th><th>Что</th><th>Заказ</th><th>Сумма</th><th>Откуда</th><th>Подробности</th></tr>
        <?php foreach ($events as $e): ?>
          <tr>
            <td class="muted"><?= h(substr((string)($e['at'] ?? ''), 0, 16)) ?></td>
            <td><b><?= h($e['kind'] ?? '') ?></b></td>
            <td class="muted"><?= h($e['order'] ?? '') ?></td>
            <td><?= !empty($e['sum']) ? h($e['sum']) . ' ₽' : '' ?></td>
            <td class="muted"><?= h(($e['sign'] ?? '') === 'kassa' ? 'ЮKassa' : ($e['sign'] ?? '')) ?></td>
            <td class="muted"><?= h($e['note'] ?? '') ?></td>
          </tr>
        <?php endforeach; ?>
      </table>
    <?php endif; ?>
    <p style="margin-top:12px"><a class="btn small ghost" href="/admin/pay.php">Обновить</a></p>
  </div>

  <div class="card">
    <h3>Если что-то не так</h3>
    <table class="lv">
      <tr><th>Что видно</th><th>Что делать</th></tr>
      <tr><td>«Токен больше не действует»</td>
          <td>нажмите «Подключить заново» на шаге 3</td></tr>
      <tr><td>ЮMoney не принимает Redirect URI</td>
          <td>скопируйте его кнопкой из шага 1 — он должен совпадать знак в знак</td></tr>
      <tr><td>деньги пришли, ключа нет</td>
          <td>«Спросить у ЮMoney» по номеру заказа; если платёж был мимо страницы покупки —
              выдайте ключ вручную в админке</td></tr>
      <tr><td>«недоплата»</td>
          <td>пришло меньше цены: ключ не выдан, выдайте вручную или попросите доплатить</td></tr>
      <tr><td>client_secret будто сбросился</td>
          <td>так и должно быть: обратно он не показывается. Если под полем написано
              «сохранён ✓» — он на месте</td></tr>
    </table>
  </div>
</div></section>

<script>
  document.querySelectorAll('.ym-copy').forEach(function (b) {
    b.addEventListener('click', function () {
      var el = document.getElementById(b.dataset.copy);
      if (!el) return;
      var t = el.innerText;
      var done = function () { var o = b.textContent; b.textContent = 'Скопировано'; setTimeout(function () { b.textContent = o; }, 1200); };
      if (navigator.clipboard) { navigator.clipboard.writeText(t).then(done, done); return; }
      var a = document.createElement('textarea');
      a.value = t; document.body.appendChild(a); a.select();
      try { document.execCommand('copy'); } catch (e) { }
      document.body.removeChild(a); done();
    });
  });
</script>
<?php gd_foot();
