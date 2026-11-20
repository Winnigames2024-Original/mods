<?php
/**
 * Инструкция по настройке оплаты через ЮMoney — для админа.
 * Страница сама проверяет, что уже сделано, и показывает, что осталось.
 */
require_once __DIR__ . '/../_inc/api.php';
require_once __DIR__ . '/../_inc/pay.php';
gd_login_required();
if (!gd_is_admin()) {
    http_response_code(403);
    gd_head('ЮMoney');
    echo '<section class="section"><div class="container"><h2>Только для ' . h(GD_ADMIN) . '</h2></div></section>';
    gd_foot();
    exit;
}

$host   = $_SERVER['HTTP_HOST'] ?? 'geodash.winnigames2024.ru';
$notify = 'https://' . $host . '/api.php?action=yoomoney';

$conf     = gd_pay_conf();
$hasFile  = is_readable(GD_PAY_FILE);
$hasRecv  = $conf['receiver'] !== '';
$hasSecr  = $conf['secret'] !== '';
$ready    = gd_pay_ready();
[$apiOk, $stats] = gd_pay_stats();
$events   = gd_pay_events();

/** Номер кошелька показываем не целиком. */
$mask = function (string $s): string {
    $n = strlen($s);
    return $n <= 6 ? str_repeat('•', $n) : substr($s, 0, 4) . str_repeat('•', max(0, $n - 8)) . substr($s, -4);
};
$dot = fn(bool $ok) => $ok
    ? '<span class="badge" style="background:#1d6;color:#042">готово</span>'
    : '<span class="badge" style="background:#a33">нужно сделать</span>';

gd_head('ЮMoney — настройка', 'admin');
?>
<style>
  .step { border-left:4px solid #4af; padding:2px 0 2px 16px; margin:26px 0 }
  .step.done { border-color:#1d6 } .step.wait { border-color:#a33 }
  .step h3 { margin:0 0 8px }
  .ym-code { background:#0d0b22; border:2px solid #000; border-radius:10px; padding:14px 16px;
             overflow-x:auto; font-size:14px; line-height:1.55; color:#cfe3ff; white-space:pre }
  .ym-copy { cursor:pointer }
  .ym-row { display:flex; gap:10px; align-items:center; flex-wrap:wrap }
</style>

<section class="section"><div class="container" style="max-width:900px">
  <p><a class="btn small ghost" href="/admin/">← В админку</a>
     <a class="btn small ghost" href="/admin/docs.php">Документация</a></p>

  <h2>Оплата через ЮMoney</h2>
  <p class="muted" style="margin-top:-8px">
    Игрок платит на странице <a href="/buy/">/buy/</a>, ЮMoney присылает нам уведомление,
    и ключ сам появляется в его аккаунте. Ниже — что для этого нужно, по шагам.
  </p>

  <div class="card">
    <div class="stats" style="margin:0">
      <div class="stat"><b><?= $ready ? 'ВКЛ' : 'ВЫКЛ' ?></b><span>продажа ключей</span></div>
      <div class="stat"><b><?= (int)$conf['price'] ?> ₽</b><span>цена ключа</span></div>
      <div class="stat"><b><?= $hasRecv ? h($mask($conf['receiver'])) : '—' ?></b><span>кошелёк</span></div>
      <div class="stat"><b><?= $apiOk ? 'есть' : 'нет' ?></b><span>связь с API игры</span></div>
    </div>
    <?php if (!$ready): ?>
      <p class="muted" style="margin-top:14px">Пока продажа выключена, страница покупки честно пишет об этом,
        а ключи выдаются ссылкой и из админки — игра работает как обычно.</p>
    <?php endif; ?>
  </div>

  <!-- 1 -->
  <div class="step <?= $hasRecv ? 'done' : 'wait' ?>">
    <h3>Шаг 1. Номер кошелька <?= $dot($hasRecv) ?></h3>
    <p>Нужен номер счёта ЮMoney — 16 цифр, начинается на <code>4100</code>. Он виден в кошельке
      на <a href="https://yoomoney.ru" target="_blank" rel="noopener">yoomoney.ru</a> и в приложении,
      в разделе со счётом. Это тот же номер, на который вам переводят деньги.</p>
    <p class="muted">Кошелёк должен быть хотя бы «именной»: у анонимного есть ограничения на приём переводов.</p>
  </div>

  <!-- 2 -->
  <div class="step <?= $hasSecr ? 'done' : 'wait' ?>">
    <h3>Шаг 2. Включить HTTP-уведомления и взять секрет <?= $dot($hasSecr) ?></h3>
    <p>Откройте настройки уведомлений кошелька:
      <a href="https://yoomoney.ru/transfer/myservices/http-notification" target="_blank" rel="noopener">
        yoomoney.ru/transfer/myservices/http-notification</a>.
      Если страница называется иначе, ищите в настройках пункт <b>HTTP-уведомления</b>.</p>
    <p>В поле адреса вставьте ровно это:</p>
    <div class="ym-row">
      <div class="ym-code" id="ymUrl"><?= h($notify) ?></div>
      <button class="btn small ghost ym-copy" data-copy="ymUrl" type="button">Скопировать</button>
    </div>
    <p style="margin-top:12px">Включите уведомления, поставьте галочки на <b>оба</b> вида:
      переводы с кошелька и с банковской карты — иначе половина оплат не дойдёт.
      Рядом будет <b>секрет</b> (секретное слово / секретный ключ) — скопируйте его, он нужен на шаге 3.</p>
    <p class="muted">Секрет — это пароль уведомлений. По нему мы проверяем, что оплату
      подтвердила именно ЮMoney, а не кто-то посторонний. Никому его не показывайте.</p>
  </div>

  <!-- 3 -->
  <div class="step <?= ($hasFile && $ready) ? 'done' : 'wait' ?>">
    <h3>Шаг 3. Положить настройки на сервер <?= $dot($hasFile && $ready) ?></h3>
    <p>Настройки оплаты не лежат в репозитории — только на сервере, в файле
      <code><?= h(GD_PAY_FILE) ?></code>. Зайдите на сервер и выполните, подставив свои значения:</p>
    <div class="ym-code" id="ymConf">mkdir -p /etc/geodash
cat &gt; /etc/geodash/pay.json &lt;&lt;'EOF'
{
  "receiver": "4100XXXXXXXXXXX",
  "secret":   "СЕКРЕТ_ИЗ_ШАГА_2",
  "price":    <?= (int)$conf['price'] ?>

}
EOF
chown root:www-data /etc/geodash/pay.json
chmod 640 /etc/geodash/pay.json</div>
    <p class="ym-row" style="margin-top:10px">
      <button class="btn small ghost ym-copy" data-copy="ymConf" type="button">Скопировать команды</button>
      <span class="muted">права 640 — файл читает только сайт, другим пользователям сервера он не виден</span>
    </p>
    <p>Цену ключа задаёт <code>price</code> — меняется в этом же файле, пересборка не нужна.
      После правки просто обновите эту страницу.</p>
    <p class="muted">Файл сейчас: <?= $hasFile
        ? 'читается — кошелёк ' . ($hasRecv ? 'указан' : '<b>не указан</b>') . ', секрет ' . ($hasSecr ? 'указан' : '<b>не указан</b>')
        : '<b>не найден или недоступен сайту</b>' ?>.</p>
  </div>

  <!-- 4 -->
  <div class="step">
    <h3>Шаг 4. Проверить</h3>
    <p>На странице уведомлений ЮMoney есть кнопка проверки — она пришлёт тестовое уведомление.
      Оно должно появиться в журнале ниже со словами «подпись сошлась».</p>
    <p>Потом купите ключ сами за <?= (int)$conf['price'] ?> ₽ на странице
      <a href="/buy/">/buy/</a>: ключ должен прийти в аккаунт за несколько секунд.
      Состояние заказа всегда можно посмотреть так:</p>
    <div class="ym-code">curl -s "https://<?= h($host) ?>/api.php?action=order&amp;order=НОМЕР_ЗАКАЗА"</div>
    <p class="muted" style="margin-top:10px">Номер заказа виден на странице оплаты и в таблице ключей в админке.</p>
  </div>

  <div class="card">
    <h3>Журнал уведомлений</h3>
    <?php if (!$apiOk): ?>
      <p class="muted">Нет связи с API игры — журнал недоступен.</p>
    <?php elseif (!$events): ?>
      <p class="muted">Уведомлений ещё не было. Как только ЮMoney пришлёт первое — оно появится здесь.</p>
    <?php else: ?>
      <table class="lv">
        <tr><th>Когда</th><th>Что</th><th>Заказ</th><th>Сумма</th><th>Подпись</th><th>Подробности</th></tr>
        <?php foreach ($events as $e): ?>
          <tr>
            <td class="muted"><?= h(substr((string)($e['at'] ?? ''), 0, 16)) ?></td>
            <td><b><?= h($e['kind'] ?? '') ?></b></td>
            <td class="muted"><?= h($e['order'] ?? '') ?></td>
            <td><?= !empty($e['sum']) ? h($e['sum']) . ' ₽' : '' ?></td>
            <td class="muted"><?= h($e['sign'] ?? '') ?></td>
            <td class="muted"><?= h($e['note'] ?? '') ?></td>
          </tr>
        <?php endforeach; ?>
      </table>
    <?php endif; ?>
    <p style="margin-top:12px"><a class="btn small ghost" href="/admin/yoomoney.php">Обновить</a></p>
  </div>

  <div class="card">
    <h3>Если что-то не так</h3>
    <table class="lv">
      <tr><th>Видно</th><th>Что делать</th></tr>
      <tr><td>в журнале «подпись не сошлась»</td>
          <td>секрет в <code>pay.json</code> не тот, что в кошельке. Скопируйте его заново, без пробелов по краям</td></tr>
      <tr><td>журнал пустой, а оплата прошла</td>
          <td>в ЮMoney не включены уведомления или указан другой адрес. Сверьте адрес из шага 2 знак в знак</td></tr>
      <tr><td>«платёж без заказа»</td>
          <td>деньги пришли обычным переводом, мимо страницы покупки. Ключ выдайте вручную из админки</td></tr>
      <tr><td>«недоплата»</td>
          <td>пришло меньше цены. Допуск 10% на процент карты; ключ не выдан, выдайте вручную или попросите доплатить</td></tr>
      <tr><td>«повтор уведомления»</td>
          <td>так и должно быть: ЮMoney иногда присылает уведомление дважды, второй ключ не выдаётся</td></tr>
      <tr><td>продажа «ВЫКЛ» после правки файла</td>
          <td>сайт не смог прочитать <code>pay.json</code> — проверьте <code>chown root:www-data</code> и <code>chmod 640</code></td></tr>
    </table>
    <p class="muted" style="margin-top:12px">Подпись мы принимаем и новую (<code>sign</code>, HMAC-SHA256),
      и старую (<code>sha1_hash</code>) — какую пришлёт ЮMoney, такую и проверим.</p>
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
