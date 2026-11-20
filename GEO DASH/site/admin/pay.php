<?php
/**
 * Настройка оплаты ключей — для админа. Две вкладки: ЮKassa (магазин) и
 * ЮMoney (кошелёк). Страница сама проверяет, что уже сделано.
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

// ----- сохранение настроек прямо из админки -----
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $act = $_POST['act'] ?? '';
    $patch = [];

    if ($act === 'kassa' || $act === 'money') {
        $patch['price'] = max(1, (int)($_POST['price'] ?? 199));
    }

    if ($act === 'kassa') {
        $patch['shopId']  = trim((string)($_POST['shopId'] ?? ''));
        $patch['vatCode'] = max(0, (int)($_POST['vatCode'] ?? 0));
        // пустое поле секрета — оставить прежний
        $sk = trim((string)($_POST['secretKey'] ?? ''));
        if ($sk !== '') $patch['secretKey'] = $sk;
        if ($patch['shopId'] !== '' && !preg_match('/^\d{4,12}$/', $patch['shopId']))
            $failed = 'shopId — это только цифры, как в кабинете ЮKassa';
    }

    if ($act === 'money') {
        $patch['receiver'] = preg_replace('/\D+/', '', (string)($_POST['receiver'] ?? ''));
        $patch['askEmail'] = !empty($_POST['askEmail']);
        $se = trim((string)($_POST['secret'] ?? ''));
        if ($se !== '') $patch['secret'] = $se;
        if ($patch['receiver'] !== '' && strlen($patch['receiver']) < 11)
            $failed = 'Номер кошелька — 16 цифр, обычно начинается на 4100';
    }

    if ($act === 'offkassa') { $patch = ['shopId' => null, 'secretKey' => null]; }
    if ($act === 'offmoney') { $patch = ['receiver' => null, 'secret' => null]; }

    if ($failed === '' && $patch) {
        [$ok, $r] = gd_pay_store_set($patch);
        if ($ok) $saved = match ($act) {
            'offkassa' => 'Приём через ЮKassa выключен',
            'offmoney' => 'Приём через кошелёк выключен',
            default    => 'Настройки сохранены и уже работают',
        };
        else $failed = (string)($r['error'] ?? 'Сервер игры не отвечает — настройки не сохранены');
    }
}

$host    = $_SERVER['HTTP_HOST'] ?? 'geodash.winnigames2024.ru';
$nKassa  = 'https://' . $host . '/api.php?action=yookassa';
$nMoney  = 'https://' . $host . '/api.php?action=yoomoney';

$conf    = gd_pay_conf();
$how     = gd_pay_provider();
$tab     = ($_GET['p'] ?? '') === 'money' ? 'money' : (($_GET['p'] ?? '') === 'kassa' ? 'kassa'
           : ($how === 'yoomoney' ? 'money' : 'kassa'));

$hasFile = is_readable(GD_PAY_FILE);
$hasShop = $conf['shopId'] !== '';
$hasKey  = $conf['secretKey'] !== '';
$hasRecv = $conf['receiver'] !== '';
$hasSecr = $conf['secret'] !== '';
$testKey = $hasKey && stripos($conf['secretKey'], 'test_') === 0;

[$apiOk, $stats] = gd_pay_stats();
$events = gd_pay_events();

$mask = function (string $s): string {
    $n = strlen($s);
    return $n <= 6 ? str_repeat('•', $n) : substr($s, 0, 4) . str_repeat('•', max(0, $n - 8)) . substr($s, -4);
};
$dot = fn(bool $ok) => $ok
    ? '<span class="badge" style="background:#1d6;color:#042">готово</span>'
    : '<span class="badge" style="background:#a33">нужно сделать</span>';

gd_head('Оплата ключей', 'admin');
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

  <h2>Оплата ключей</h2>
  <?php if ($saved): ?><div class="alert ok"><?= h($saved) ?></div><?php endif; ?>
  <?php if ($failed): ?><div class="alert err"><?= h($failed) ?></div><?php endif; ?>
  <p class="muted" style="margin-top:-8px">
    Игрок платит на странице <a href="/buy/">/buy/</a>, платёжная система присылает нам уведомление,
    и ключ сам появляется в его аккаунте. Способ можно выбрать любой — настройте один из двух.
  </p>

  <div class="card">
    <div class="stats" style="margin:0">
      <div class="stat"><b><?= $how === '' ? 'ВЫКЛ' : 'ВКЛ' ?></b><span>продажа ключей</span></div>
      <div class="stat"><b><?= h(gd_pay_name($how)) ?></b><span>принимает оплату</span></div>
      <div class="stat"><b><?= (int)$conf['price'] ?> ₽</b><span>цена ключа</span></div>
      <div class="stat"><b><?= $apiOk ? 'есть' : 'нет' ?></b><span>связь с API игры</span></div>
    </div>
    <?php if ($how === ''): ?>
      <p class="muted" style="margin-top:14px">Пока продажа выключена, страница покупки честно пишет об этом,
        а ключи выдаются ссылкой и из админки — игра работает как обычно.</p>
    <?php elseif ($how === 'yookassa' && $testKey): ?>
      <p style="margin-top:14px;color:#ffd24a">Ключ API тестовый (<code>test_…</code>) — настоящие деньги
        не списываются, но ключи выдаются. Для продажи поставьте рабочий ключ.</p>
    <?php endif; ?>
    <?php if ($hasFile): ?>
      <p class="muted" style="margin-top:8px">На сервере есть файл <code><?= h(GD_PAY_FILE) ?></code>.
        Он тоже читается, но то, что сохранено здесь кнопкой «Применить», важнее.</p>
    <?php endif; ?>
    <?php if ($hasShop && $hasKey && $hasRecv && $hasSecr): ?>
      <p class="muted" style="margin-top:8px">Настроены оба способа — работает ЮKassa.
        Чтобы вернуться на кошелёк, уберите <code>shopId</code> и <code>secretKey</code> из файла.</p>
    <?php endif; ?>
  </div>

  <div class="flex" style="gap:10px;margin:20px 0 4px">
    <a class="btn small <?= $tab === 'kassa' ? 'gold' : 'ghost' ?>" href="/admin/pay.php?p=kassa">
      ЮKASSA — МАГАЗИН<?= $how === 'yookassa' ? ' ✓' : '' ?></a>
    <a class="btn small <?= $tab === 'money' ? 'gold' : 'ghost' ?>" href="/admin/pay.php?p=money">
      ЮMONEY — КОШЕЛЁК<?= $how === 'yoomoney' ? ' ✓' : '' ?></a>
  </div>

<?php if ($tab === 'kassa'): ?>

  <div class="card">
    <h3>Чем ЮKassa отличается от кошелька</h3>
    <p>ЮKassa — это приём платежей для магазина: карты, СБП, кошелёк, рассрочка. Деньги приходят
      на расчётный счёт, есть возвраты, чеки и личный кабинет с историей. Нужен ИП или ООО
      (самозанятым — через отдельный тариф), договор и анкета.</p>
    <p class="muted">Кошелёк ЮMoney проще: заводится за минуту и ничего не требует, но это переводы
      физлицу, без чеков и возвратов. Если продажа не разовая — лучше ЮKassa.</p>
  </div>

  <!-- 1 -->
  <div class="step <?= ($hasShop && $hasKey) ? 'done' : 'wait' ?>">
    <h3>Шаг 1. Магазин и ключ API <?= $dot($hasShop && $hasKey) ?></h3>
    <p>Зарегистрируйте магазин на <a href="https://yookassa.ru" target="_blank" rel="noopener">yookassa.ru</a>
      и дождитесь, пока его подключат. Потом в личном кабинете, в разделе
      <b>Интеграция → Ключи API</b>, возьмите две вещи:</p>
    <ul>
      <li><b>shopId</b> — номер магазина, просто цифры;</li>
      <li><b>секретный ключ</b> — длинная строка, начинается на <code>live_</code> (рабочий)
        или <code>test_</code> (тестовый).</li>
    </ul>
    <p class="muted">Ключ показывают один раз — сразу скопируйте. Потерялся — выпустите новый,
      старый при этом перестанет работать.</p>
    <p class="muted">Сейчас: shopId <?= $hasShop ? '<b>' . h($mask($conf['shopId'])) . '</b>' : '<b>не указан</b>' ?>,
      ключ API <?= $hasKey ? ($testKey ? '<b>тестовый</b>' : '<b>рабочий</b>') : '<b>не указан</b>' ?>.</p>
  </div>

  <!-- 2 -->
  <div class="step">
    <h3>Шаг 2. Уведомление об оплате</h3>
    <p>В личном кабинете ЮKassa откройте <b>Интеграция → HTTP-уведомления</b> и вставьте этот адрес:</p>
    <div class="ym-row">
      <div class="ym-code" id="kUrl"><?= h($nKassa) ?></div>
      <button class="btn small ghost ym-copy" data-copy="kUrl" type="button">Скопировать</button>
    </div>
    <p style="margin-top:12px">Отметьте событие <code>payment.succeeded</code> — этого достаточно,
      ключ выдаётся только по нему. Остальные события можно включить, они просто попадут в журнал.</p>
    <p class="muted">Подписи у уведомлений ЮKassa нет, поэтому мы проверяем их дважды: что запрос
      пришёл с её адресов и что платёж действительно оплачен — перечитываем его у самой ЮKassa.
      Подделать уведомление не получится.</p>
  </div>

  <!-- 3 -->
  <div class="step <?= $how === 'yookassa' ? 'done' : 'wait' ?>">
    <h3>Шаг 3. Ввести данные магазина <?= $dot($how === 'yookassa') ?></h3>
    <p>Заполните поля и нажмите «Применить» — оплата включится сразу, заходить на сервер не нужно.
      Данные хранит игровой сервер, файлом только для себя; сайт их не выкладывает и не показывает
      обратно.</p>

    <form class="form" method="post" style="margin-top:16px">
      <input type="hidden" name="act" value="kassa">
      <div class="grid c2" style="gap:16px">
        <div>
          <label>shopId — номер магазина</label>
          <input type="text" name="shopId" value="<?= h($conf['shopId']) ?>" placeholder="123456"
                 inputmode="numeric" autocomplete="off">
        </div>
        <div>
          <label>Секретный ключ API</label>
          <input type="password" name="secretKey" autocomplete="new-password"
                 placeholder="<?= $hasKey ? 'сохранён — оставьте пустым' : 'live_… или test_…' ?>">
        </div>
        <div>
          <label>Цена ключа, ₽</label>
          <input type="number" name="price" min="1" max="100000" value="<?= (int)$conf['price'] ?>">
        </div>
        <div>
          <label>Ставка НДС для чека (vatCode)</label>
          <select name="vatCode">
            <?php foreach ([0 => 'чек не формируем', 1 => '1 — без НДС', 2 => '2 — 0%',
                            3 => '3 — 10%', 4 => '4 — 20%', 5 => '5 — 10/110', 6 => '6 — 20/120'] as $v => $t): ?>
              <option value="<?= $v ?>" <?= (int)$conf['vatCode'] === $v ? 'selected' : '' ?>><?= h($t) ?></option>
            <?php endforeach; ?>
          </select>
        </div>
      </div>
      <p class="muted" style="margin:12px 0 0">Ставка нужна, если у магазина включена фискализация
        (54-ФЗ): тогда страница покупки попросит у игрока e-mail, и чек уйдёт на него.</p>
      <p style="margin-top:18px">
        <button class="btn green" type="submit">ПРИМЕНИТЬ</button>
        <?php if ($hasShop || $hasKey): ?>
          <button class="btn small ghost" type="submit" name="act" value="offkassa"
                  formnovalidate>Выключить ЮKassa</button>
        <?php endif; ?>
      </p>
    </form>

    <details style="margin-top:16px">
      <summary class="muted" style="cursor:pointer">Можно и файлом на сервере — если так привычнее</summary>
      <div class="ym-code" id="kConf" style="margin-top:10px">mkdir -p /etc/geodash
cat &gt; /etc/geodash/pay.json &lt;&lt;'EOF'
{
  "shopId":    "123456",
  "secretKey": "live_XXXXXXXXXXXXXXXXXXXX",
  "price":     <?= (int)$conf['price'] ?>,
  "vatCode":   0
}
EOF
chown root:www-data /etc/geodash/pay.json
chmod 640 /etc/geodash/pay.json</div>
      <p class="ym-row" style="margin-top:10px">
        <button class="btn small ghost ym-copy" data-copy="kConf" type="button">Скопировать команды</button>
        <span class="muted">что сохранено через «Применить», важнее файла</span>
      </p>
    </details>
  </div>

  <!-- 4 -->
  <div class="step">
    <h3>Шаг 4. Проверить</h3>
    <p>С тестовым ключом (<code>test_…</code>) купите ключ сами на странице <a href="/buy/">/buy/</a> —
      ЮKassa покажет тестовые карты, деньги не спишутся. В журнале ниже должно появиться
      «ключ выдан», а сам ключ — в <a href="/account/">аккаунте</a>.</p>
    <p>Состояние заказа всегда можно посмотреть так:</p>
    <div class="ym-code">curl -s "https://<?= h($host) ?>/api.php?action=order&amp;order=НОМЕР_ЗАКАЗА"</div>
    <p class="muted" style="margin-top:10px">Потом поменяйте ключ на рабочий и купите один ключ
      по-настоящему — это самая надёжная проверка.</p>
  </div>

<?php else: ?>

  <div class="card">
    <h3>Кошелёк ЮMoney — для начала</h3>
    <p>Самый быстрый способ: заводится за минуту, ничего не требует. Это переводы физлицу —
      без чеков и возвратов, с ограничениями на суммы у неидентифицированного кошелька.
      Если продажа пойдёт всерьёз, переходите на <a href="/admin/pay.php?p=kassa">ЮKassa</a>.</p>
  </div>

  <!-- 1 -->
  <div class="step <?= $hasRecv ? 'done' : 'wait' ?>">
    <h3>Шаг 1. Номер кошелька <?= $dot($hasRecv) ?></h3>
    <p>Нужен номер счёта ЮMoney — 16 цифр, начинается на <code>4100</code>. Он виден в кошельке
      на <a href="https://yoomoney.ru" target="_blank" rel="noopener">yoomoney.ru</a> и в приложении.
      Это тот же номер, на который вам переводят деньги.</p>
    <p class="muted">Кошелёк должен быть хотя бы «именной»: у анонимного есть ограничения на приём переводов.</p>
    <p class="muted">Сейчас: <?= $hasRecv ? '<b>' . h($mask($conf['receiver'])) . '</b>' : '<b>не указан</b>' ?>.</p>
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
      <div class="ym-code" id="ymUrl"><?= h($nMoney) ?></div>
      <button class="btn small ghost ym-copy" data-copy="ymUrl" type="button">Скопировать</button>
    </div>
    <p style="margin-top:12px">Включите уведомления, поставьте галочки на <b>оба</b> вида:
      переводы с кошелька и с банковской карты — иначе половина оплат не дойдёт.
      Рядом будет <b>секрет</b> (секретное слово) — скопируйте его, он нужен на шаге 3.</p>
    <p class="muted">Секрет — это пароль уведомлений. По нему мы проверяем, что оплату
      подтвердила именно ЮMoney, а не кто-то посторонний. Никому его не показывайте.</p>
    <p class="muted"><b>Приложение на <code>yoomoney.ru/myservices/new</code> регистрировать не нужно.</b>
      Та страница выдаёт <code>client_id</code> для OAuth — доступа к кошельку по API (история операций,
      переводы от вашего имени). Нашей оплате это не требуется: форма оплаты и уведомления работают
      от номера кошелька и секрета, ничего больше.</p>
  </div>

  <!-- 3 -->
  <div class="step <?= $how === 'yoomoney' ? 'done' : 'wait' ?>">
    <h3>Шаг 3. Ввести кошелёк и секрет <?= $dot($how === 'yoomoney') ?></h3>
    <p>Заполните поля и нажмите «Применить» — приём оплаты включится сразу.</p>

    <form class="form" method="post" style="margin-top:16px">
      <input type="hidden" name="act" value="money">
      <div class="grid c2" style="gap:16px">
        <div>
          <label>Номер кошелька</label>
          <input type="text" name="receiver" value="<?= h($conf['receiver']) ?>"
                 placeholder="4100XXXXXXXXXXX" inputmode="numeric" autocomplete="off">
        </div>
        <div>
          <label>Секрет HTTP-уведомлений</label>
          <input type="password" name="secret" autocomplete="new-password"
                 placeholder="<?= $hasSecr ? 'сохранён — оставьте пустым' : 'из настроек кошелька' ?>">
        </div>
        <div>
          <label>Цена ключа, ₽</label>
          <input type="number" name="price" min="1" max="100000" value="<?= (int)$conf['price'] ?>">
        </div>
        <div>
          <label style="margin-top:26px">
            <input type="checkbox" name="askEmail" value="1" <?= !empty($conf['askEmail']) ? 'checked' : '' ?>>
            спрашивать e-mail у покупателя
          </label>
        </div>
      </div>
      <p style="margin-top:18px">
        <button class="btn green" type="submit">ПРИМЕНИТЬ</button>
        <?php if ($hasRecv || $hasSecr): ?>
          <button class="btn small ghost" type="submit" name="act" value="offmoney"
                  formnovalidate>Выключить кошелёк</button>
        <?php endif; ?>
      </p>
    </form>

    <details style="margin-top:16px">
      <summary class="muted" style="cursor:pointer">Можно и файлом на сервере — если так привычнее</summary>
      <div class="ym-code" id="ymConf" style="margin-top:10px">mkdir -p /etc/geodash
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
        <span class="muted">что сохранено через «Применить», важнее файла</span>
      </p>
    </details>
  </div>

  <!-- 4 -->
  <div class="step">
    <h3>Шаг 4. Проверить</h3>
    <p>На странице уведомлений ЮMoney есть кнопка проверки — она пришлёт тестовое уведомление.
      Оно должно появиться в журнале ниже со словами «подпись сошлась».</p>
    <p>Потом купите ключ сами за <?= (int)$conf['price'] ?> ₽ на странице <a href="/buy/">/buy/</a>.</p>
    <p class="muted">Подпись принимаем и новую (<code>sign</code>, HMAC-SHA256), и старую
      (<code>sha1_hash</code>) — какую пришлёт ЮMoney, такую и проверим.</p>
  </div>

<?php endif; ?>

  <div class="card">
    <h3>Журнал уведомлений</h3>
    <?php if (!$apiOk): ?>
      <p class="muted">Нет связи с API игры — журнал недоступен.</p>
    <?php elseif (!$events): ?>
      <p class="muted">Уведомлений ещё не было. Как только придёт первое — оно появится здесь.</p>
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
    <p style="margin-top:12px"><a class="btn small ghost" href="/admin/pay.php?p=<?= $tab ?>">Обновить</a></p>
  </div>

  <div class="card">
    <h3>Если что-то не так</h3>
    <table class="lv">
      <tr><th>Видно в журнале</th><th>Что делать</th></tr>
      <tr><td>«чужой адрес»</td>
          <td>уведомление пришло не с адресов ЮKassa — скорее всего кто-то постучался сам. Ничего делать не нужно</td></tr>
      <tr><td>«платёж не перечитан»</td>
          <td>ЮKassa не приняла наш запрос: проверьте shopId и секретный ключ в <code>pay.json</code></td></tr>
      <tr><td>«платёж не создан»</td>
          <td>в подробностях будет ответ ЮKassa. Чаще всего: магазин ещё не подключён или нужен чек —
              тогда задайте <code>vatCode</code></td></tr>
      <tr><td>«подпись не сошлась»</td>
          <td>для кошелька: секрет в <code>pay.json</code> не тот, что в настройках ЮMoney</td></tr>
      <tr><td>журнал пустой, а оплата прошла</td>
          <td>не включены уведомления или указан другой адрес — сверьте адрес из шага 2 знак в знак</td></tr>
      <tr><td>«платёж без заказа»</td>
          <td>деньги пришли мимо страницы покупки. Ключ выдайте вручную из админки</td></tr>
      <tr><td>«недоплата»</td>
          <td>пришло меньше цены. Ключ не выдан — выдайте вручную или попросите доплатить</td></tr>
      <tr><td>«повтор уведомления»</td>
          <td>так и должно быть: уведомление иногда приходит дважды, второй ключ не выдаётся</td></tr>
      <tr><td>продажа «ВЫКЛ» после правки файла</td>
          <td>сайт не смог прочитать <code>pay.json</code> — проверьте <code>chown root:www-data</code>
              и <code>chmod 640</code>, а также что json без лишней запятой</td></tr>
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
