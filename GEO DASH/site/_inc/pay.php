<?php
/**
 * GEO DASH — лицензионные ключи и оплата через ЮMoney.
 *
 * Поддерживаются два способа, настраивается любой из них:
 *
 *   ЮKassa (магазин, API v3) — предпочтительный, если он настроен:
 *     "shopId": "123456", "secretKey": "live_XXXX"
 *
 *   ЮMoney (кошелёк, платёжная форма + http-уведомления):
 *     "receiver": "4100XXXXXXXXXX", "secret": "секрет уведомлений"
 *
 * Настройки оплаты НЕ лежат в репозитории. Файл на сервере:
 *
 *   /etc/geodash/pay.json
 *   {
 *     "shopId":    "",
 *     "secretKey": "",
 *     "receiver":  "",
 *     "secret":    "",
 *     "price":     199,
 *     "currency":  "RUB",
 *     "vatCode":   0,
 *     "issueKey":  "GEODASH_AADDMMIINN22002266JSD"
 *   }
 *
 *   chown root:www-data /etc/geodash/pay.json && chmod 640 /etc/geodash/pay.json
 *
 * Пока файла нет — продажа просто выключена, всё остальное работает:
 * ключи выдаются по ссылке и из админки.
 */

define('GD_PAY_FILE', getenv('GEODASH_PAY_FILE') ?: '/etc/geodash/pay.json');
define('GD_PAY_API', getenv('GEODASH_API') ?: 'http://127.0.0.1:37534');

/**
 * Настройки «снизу»: значения по умолчанию, переменные окружения и файл
 * /etc/geodash/pay.json, если он есть. Отсюда берётся ключ выдачи, поэтому
 * этот слой не обращается к API — иначе получилось бы кольцо.
 */
function gd_pay_base(): array {
    static $c = null;
    if ($c !== null) return $c;
    $c = [
        'receiver'  => (string)(getenv('GEODASH_YOOMONEY') ?: ''),
        'secret'    => (string)(getenv('GEODASH_YOOMONEY_SECRET') ?: ''),
        'shopId'    => (string)(getenv('GEODASH_KASSA_SHOP') ?: ''),
        'secretKey' => (string)(getenv('GEODASH_KASSA_KEY') ?: ''),
        'vatCode'   => (int)(getenv('GEODASH_VAT') ?: 0),
        'askEmail'  => false,
        'price'     => (int)(getenv('GEODASH_PRICE') ?: 199),
        'currency'  => 'RUB',
        'issueKey'  => (string)(getenv('GEODASH_ISSUE_SECRET') ?: 'GEODASH_AADDMMIINN22002266JSD'),
    ];
    if (is_readable(GD_PAY_FILE)) {
        $j = json_decode((string)file_get_contents(GD_PAY_FILE), true);
        if (is_array($j)) $c = array_merge($c, array_filter($j, fn($v) => $v !== '' && $v !== null));
    }
    return $c;
}

/** Есть ли файл настроек на сервере (его правят руками). */
function gd_pay_file_used(): bool { return is_readable(GD_PAY_FILE); }

/**
 * Настройки, сохранённые из админки. Лежат у API: он пишет их файлом только для
 * себя, поэтому сайту не нужен доступ к /etc и правка файла руками.
 */
function gd_pay_store(bool $fresh = false): array {
    static $s = null;
    if ($s !== null && !$fresh) return $s;
    [$ok, $d] = gd_pay_api('GET', '/api/licence/payconf');
    $s = $ok && is_array($d['conf'] ?? null) ? $d['conf'] : [];
    return $s;
}

/** Сохранить настройки из админки. $patch: поле => значение, null — убрать. */
function gd_pay_store_set(array $patch): array {
    [$ok, $d, $code] = gd_pay_api('POST', '/api/licence/payconf', ['conf' => $patch]);
    gd_pay_store(true);
    gd_pay_conf(true);
    return [$ok, $d, $code];
}

/** Итоговые настройки: сохранённое из админки важнее файла и окружения. */
function gd_pay_conf(bool $fresh = false): array {
    static $c = null;
    if ($c !== null && !$fresh) return $c;
    $c = array_merge(gd_pay_base(),
        array_filter(gd_pay_store($fresh), fn($v) => $v !== '' && $v !== null));
    $c['price'] = max(1, (int)$c['price']);
    $c['vatCode'] = max(0, (int)$c['vatCode']);
    return $c;
}

/**
 * Чем принимаем оплату: 'yookassa' (магазин), 'yoomoney' (кошелёк) или ''.
 * Если настроены оба — работает ЮKassa, у неё и чеки, и возвраты.
 */
function gd_pay_provider(): string {
    $c = gd_pay_conf();
    if ($c['shopId'] !== '' && $c['secretKey'] !== '') return 'yookassa';
    if ($c['receiver'] !== '' && $c['secret'] !== '') return 'yoomoney';
    return '';
}

/** Настроена ли продажа ключей. */
function gd_pay_ready(): bool { return gd_pay_provider() !== ''; }

function gd_pay_price(): int { return gd_pay_conf()['price']; }
function gd_pay_receiver(): string { return gd_pay_conf()['receiver']; }

/** Нужен ли чек по 54-ФЗ: тогда просим e-mail у покупателя. */
function gd_pay_needs_email(): bool {
    return gd_pay_provider() === 'yookassa' && gd_pay_conf()['vatCode'] > 0;
}

function gd_pay_name(string $p = ''): string {
    $p = $p ?: gd_pay_provider();
    return $p === 'yookassa' ? 'ЮKassa' : ($p === 'yoomoney' ? 'ЮMoney' : '—');
}

/** Запрос к игровому серверу с секретом выдачи (только по 127.0.0.1). */
function gd_pay_api(string $method, string $path, $body = null): array {
    $ch = curl_init(GD_PAY_API . $path);
    // ключ выдачи берём из нижнего слоя: он не зависит от сохранённых настроек
    $headers = ['Accept: application/json', 'X-GD-Issue: ' . gd_pay_base()['issueKey']];
    if ($body !== null) {
        $headers[] = 'Content-Type: application/json';
        curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($body, JSON_UNESCAPED_UNICODE));
    }
    curl_setopt_array($ch, [
        CURLOPT_CUSTOMREQUEST => $method,
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_HTTPHEADER => $headers,
        CURLOPT_TIMEOUT => 15,
    ]);
    $raw = curl_exec($ch);
    $code = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
    curl_close($ch);
    if ($raw === false) return [false, ['error' => 'Сервер игры не отвечает'], 0];
    $json = json_decode((string)$raw, true);
    return [$code >= 200 && $code < 300, is_array($json) ? $json : [], $code];
}

/** Выдать ключи. $o: count, note, price, user, order, buyer, by. */
function gd_pay_issue(array $o): array {
    return gd_pay_api('POST', '/api/licence/issue', [
        'count' => max(1, min(50, (int)($o['count'] ?? 1))),
        'note'  => (string)($o['note'] ?? ''),
        'price' => (int)($o['price'] ?? 0),
        'user'  => (string)($o['user'] ?? ''),
        'order' => (string)($o['order'] ?? ''),
        'buyer' => (string)($o['buyer'] ?? ''),
        'by'    => (string)($o['by'] ?? 'site'),
    ]);
}

function gd_pay_stats(): array {
    [$ok, $d, $c] = gd_pay_api('GET', '/api/licence/stats');
    return [$ok, $d, $c];
}

/** Записать событие оплаты в журнал — он виден на странице настройки ЮMoney. */
function gd_pay_event(string $kind, array $o = []): void {
    gd_pay_api('POST', '/api/licence/payevent', [
        'kind'  => $kind,
        'order' => (string)($o['order'] ?? ''),
        'sum'   => (float)($o['sum'] ?? 0),
        'sign'  => (string)($o['sign'] ?? ''),
        'note'  => (string)($o['note'] ?? ''),
    ]);
}

/** Закрепить оплаченный заказ за аккаунтом и сразу активировать ключ. */
function gd_pay_claim(string $order, string $name): array {
    [$ok, $d, $code] = gd_pay_api('POST', '/api/licence/claim', ['order' => $order, 'name' => $name]);
    return [$ok, $d, $code];
}

/** Последние уведомления об оплате. */
function gd_pay_events(): array {
    [$ok, $d] = gd_pay_api('GET', '/api/licence/payevents');
    return $ok && is_array($d['events'] ?? null) ? $d['events'] : [];
}

function gd_pay_order(string $order): array {
    [$ok, $d] = gd_pay_api('GET', '/api/licence/order/' . rawurlencode($order));
    return [$ok, $d];
}

/**
 * Новый заказ: номер и одноразовый код получения лицензии.
 * Код уходит в ссылку возврата после оплаты и работает ровно один раз.
 */
function gd_pay_start(string $buyer, int $count = 1): ?array {
    $order = 'gd' . bin2hex(random_bytes(8));
    [$ok, $r] = gd_pay_api('POST', '/api/licence/order', [
        'order' => $order, 'buyer' => $buyer,
        'count' => max(1, min(20, $count)), 'sum' => gd_pay_price() * max(1, $count),
    ]);
    if (!$ok) return null;
    return ['order' => $order, 'token' => (string)($r['order']['token'] ?? '')];
}

/** Заказ по одноразовому коду получения. */
function gd_pay_by_token(string $token): array {
    [$ok, $d] = gd_pay_api('GET', '/api/licence/token/' . rawurlencode($token));
    return [$ok, $d];
}

/** Закрепить заказ за аккаунтом по одноразовому коду. */
function gd_pay_claim_token(string $token, string $name): array {
    [$ok, $d, $code] = gd_pay_api('POST', '/api/licence/claim', ['token' => $token, 'name' => $name]);
    return [$ok, $d, $code];
}

/** Ссылка возврата после оплаты — выдача лицензии по одноразовому коду. */
function gd_pay_return(string $token, string $siteUrl): string {
    return rtrim($siteUrl, '/') . '/licensekey/index.php?action=givelicense&value=' . urlencode($token);
}

/**
 * Проверка подписи уведомления. ЮMoney сейчас подписывает уведомления полем
 * «sign» (HMAC-SHA256 по всем параметрам, отсортированным по имени), а старые
 * кошельки присылают «sha1_hash» с жёстким порядком полей. Принимаем и то, и
 * другое: что пришло — то и проверяем.
 */
function gd_pay_sign_ok(array $post, string $secret): bool {
    // новый вид: sign = HMAC-SHA256
    $given = (string)($post['sign'] ?? '');
    if ($given !== '') {
        $p = $post;
        unset($p['sign']);
        ksort($p);
        $parts = [];
        foreach ($p as $k => $v) $parts[] = $k . '=' . rawurlencode((string)$v);
        $mine = hash_hmac('sha256', implode('&', $parts), $secret);
        return hash_equals($mine, strtolower($given));
    }

    // старый вид: sha1_hash, порядок полей задан документацией и менять его нельзя
    $given = strtolower((string)($post['sha1_hash'] ?? ''));
    if ($given === '') return false;
    $mine = sha1(implode('&', [
        $post['notification_type'] ?? '',
        $post['operation_id'] ?? '',
        $post['amount'] ?? '',
        $post['currency'] ?? '',
        $post['datetime'] ?? '',
        $post['sender'] ?? '',
        $post['codepro'] ?? '',
        $secret,
        $post['label'] ?? '',
    ]));
    return hash_equals($mine, $given);
}

/** Уведомление от ЮMoney. Проверяем подпись и выдаём ключи один раз. */
function gd_pay_notify(array $post): array {
    $c = gd_pay_conf();
    if ($c['secret'] === '') return ['ok' => false, 'code' => 503, 'error' => 'Оплата не настроена'];

    $how = isset($post['sign']) && $post['sign'] !== '' ? 'sign' : 'sha1_hash';
    $label = (string)($post['label'] ?? '');
    $sum = (float)($post['amount'] ?? 0);

    if (!gd_pay_sign_ok($post, $c['secret'])) {
        gd_pay_event('подпись не сошлась', ['order' => $label, 'sum' => $sum, 'sign' => $how,
            'note' => 'проверьте секрет в pay.json']);
        return ['ok' => false, 'code' => 403, 'error' => 'Подпись не сошлась'];
    }

    if (!empty($post['test_notification'])) {
        gd_pay_event('тестовое уведомление', ['sign' => $how, 'note' => 'подпись сошлась']);
        return ['ok' => true, 'note' => 'Тестовое уведомление принято'];
    }

    if ($label === '' || !preg_match('/^gd[0-9a-f]{16}$/', $label)) {
        gd_pay_event('платёж без заказа', ['sum' => $sum, 'sign' => $how,
            'note' => 'метка: ' . ($label === '' ? 'пусто' : $label)]);
        return ['ok' => true, 'note' => 'Платёж без номера заказа — ключ не выдаём'];
    }

    [$ok, $info] = gd_pay_order($label);
    $order = $info['order'] ?? null;
    if ($ok && $order && !empty($order['keys'])) {
        gd_pay_event('повтор уведомления', ['order' => $label, 'sum' => $sum, 'sign' => $how,
            'note' => 'ключ уже выдан, второй раз не выдаём']);
        return ['ok' => true, 'note' => 'Уже выдано'];
    }

    $count = max(1, (int)($order['count'] ?? 1));
    $need  = gd_pay_price() * $count * 0.9;      // карта забирает процент — допуск 10%
    $paid  = (float)($post['amount'] ?? 0);
    if ($paid + 0.01 < $need) {
        gd_pay_api('POST', '/api/licence/order', [
            'order' => $label, 'buyer' => (string)($order['buyer'] ?? ''),
            'count' => $count, 'sum' => $paid,
        ]);
        gd_pay_event('недоплата', ['order' => $label, 'sum' => $paid, 'sign' => $how,
            'note' => 'нужно было ' . (int)($need) . ' ₽ за ' . $count . ' шт.']);
        return ['ok' => true, 'note' => 'Недоплата: ключ не выдан'];
    }

    gd_pay_issue([
        'count' => $count,
        'note'  => 'ЮMoney ' . (string)($post['operation_id'] ?? ''),
        'price' => gd_pay_price(),
        'order' => $label,
        'buyer' => (string)($order['buyer'] ?? ''),
        'by'    => 'yoomoney',
    ]);
    gd_pay_event('ключ выдан', ['order' => $label, 'sum' => $paid, 'sign' => $how,
        'note' => 'покупатель ' . (string)($order['buyer'] ?? '?') . ', ключей: ' . $count]);
    return ['ok' => true, 'note' => 'Ключ выдан'];
}

// --------------------------------------------------------------- ЮKassa
//
// Платёж создаём сами: POST /v3/payments с басовой авторизацией shopId:secretKey
// и ключом идемпотентности, в ответ приходит адрес, куда отправить покупателя.
// Уведомление проверяем дважды — по адресу отправителя и перечитывая платёж у
// ЮKassa: подписи у уведомлений нет, и доверять телу запроса нельзя.

define('GD_KASSA_API', getenv('GEODASH_KASSA_API') ?: 'https://api.yookassa.ru/v3');

/** Адреса, с которых ЮKassa присылает уведомления (из её документации). */
function gd_kassa_nets(): array {
    return ['185.71.76.0/27', '185.71.77.0/27', '77.75.153.0/25',
            '77.75.156.11/32', '77.75.156.35/32', '77.75.154.128/25', '2a02:5180::/32'];
}

/** Попадает ли адрес в одну из подсетей ЮKassa. */
function gd_kassa_ip_ok(string $ip): bool {
    if (getenv('GEODASH_KASSA_ANYIP')) return true;      // для проверки на тестовом сервере
    $bin = @inet_pton($ip);
    if ($bin === false) return false;
    foreach (gd_kassa_nets() as $net) {
        [$addr, $bits] = array_pad(explode('/', $net, 2), 2, null);
        $nb = @inet_pton($addr);
        if ($nb === false || strlen($nb) !== strlen($bin)) continue;
        $bits = $bits === null ? strlen($nb) * 8 : (int)$bits;
        $full = intdiv($bits, 8);
        $rest = $bits % 8;
        if ($full && strncmp($bin, $nb, $full) !== 0) continue;
        if ($rest) {
            $mask = chr((0xff << (8 - $rest)) & 0xff);
            if ((($bin[$full] ?? "\0") & $mask) !== (($nb[$full] ?? "\0") & $mask)) continue;
        }
        return true;
    }
    return false;
}

/** Запрос к API ЮKassa. */
function gd_kassa_api(string $method, string $path, ?array $body = null, string $idem = ''): array {
    $c = gd_pay_conf();
    $ch = curl_init(GD_KASSA_API . $path);
    $h = ['Accept: application/json'];
    if ($body !== null) {
        $h[] = 'Content-Type: application/json';
        $h[] = 'Idempotence-Key: ' . ($idem !== '' ? $idem : bin2hex(random_bytes(12)));
        curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($body, JSON_UNESCAPED_UNICODE));
    }
    curl_setopt_array($ch, [
        CURLOPT_CUSTOMREQUEST => $method,
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_HTTPHEADER => $h,
        CURLOPT_USERPWD => $c['shopId'] . ':' . $c['secretKey'],
        CURLOPT_HTTPAUTH => CURLAUTH_BASIC,
        CURLOPT_TIMEOUT => 25,
    ]);
    $raw = curl_exec($ch);
    $code = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
    $err = curl_error($ch);
    curl_close($ch);
    if ($raw === false) return [false, ['description' => 'ЮKassa не отвечает: ' . $err], 0];
    $j = json_decode((string)$raw, true);
    return [$code >= 200 && $code < 300, is_array($j) ? $j : [], $code];
}

/**
 * Создать платёж и вернуть адрес оплаты.
 * Ключ идемпотентности — номер заказа: повторное нажатие кнопки не создаёт второй платёж.
 */
function gd_kassa_start(string $order, int $count, string $siteUrl, string $email = '', string $token = ''): array {
    $c = gd_pay_conf();
    $sum = number_format($c['price'] * max(1, $count), 2, '.', '');
    $what = 'Лицензионный ключ GEO DASH' . ($count > 1 ? " ($count шт.)" : '');

    $body = [
        'amount' => ['value' => $sum, 'currency' => $c['currency']],
        'capture' => true,
        'confirmation' => [
            'type' => 'redirect',
            'return_url' => $token !== ''
                ? gd_pay_return($token, $siteUrl)
                : rtrim($siteUrl, '/') . '/buy/done.php?order=' . urlencode($order),
        ],
        'description' => $what,
        'metadata' => ['order' => $order],
    ];

    // чек нужен, когда у магазина включена фискализация
    if ($c['vatCode'] > 0 && $email !== '') {
        $body['receipt'] = [
            'customer' => ['email' => $email],
            'items' => [[
                'description' => $what,
                'quantity' => '1.00',
                'amount' => ['value' => $sum, 'currency' => $c['currency']],
                'vat_code' => $c['vatCode'],
            ]],
        ];
    }

    [$ok, $r] = gd_kassa_api('POST', '/payments', $body, $order);
    if (!$ok) {
        gd_pay_event('платёж не создан', ['order' => $order, 'sum' => (float)$sum, 'sign' => 'kassa',
            'note' => (string)($r['description'] ?? 'ЮKassa отказала')]);
        return [false, (string)($r['description'] ?? 'ЮKassa отказала в создании платежа')];
    }
    $url = $r['confirmation']['confirmation_url'] ?? '';
    if ($url === '') return [false, 'ЮKassa не вернула адрес оплаты'];
    return [true, $url];
}

/**
 * Уведомление от ЮKassa: {"type":"notification","event":"payment.succeeded","object":{…}}.
 * Подписи нет, поэтому проверяем адрес отправителя и перечитываем платёж.
 */
function gd_kassa_notify(string $raw, string $ip): array {
    if (gd_pay_provider() !== 'yookassa')
        return ['ok' => false, 'code' => 503, 'error' => 'ЮKassa не настроена'];

    $j = json_decode($raw, true);
    if (!is_array($j) || ($j['type'] ?? '') !== 'notification')
        return ['ok' => false, 'code' => 400, 'error' => 'Не похоже на уведомление'];

    $event = (string)($j['event'] ?? '');
    $obj = is_array($j['object'] ?? null) ? $j['object'] : [];
    $id = (string)($obj['id'] ?? '');

    if (!gd_kassa_ip_ok($ip)) {
        gd_pay_event('чужой адрес', ['sign' => 'kassa', 'note' => 'уведомление пришло с ' . $ip]);
        return ['ok' => false, 'code' => 403, 'error' => 'Адрес не принадлежит ЮKassa'];
    }

    if ($event !== 'payment.succeeded') {
        gd_pay_event('событие ' . ($event ?: '?'), ['sign' => 'kassa',
            'note' => 'ключ выдаём только по payment.succeeded']);
        return ['ok' => true, 'note' => 'Событие принято'];
    }
    if ($id === '') return ['ok' => false, 'code' => 400, 'error' => 'В уведомлении нет платежа'];

    // перечитываем платёж у ЮKassa — телу запроса не верим
    [$ok, $p] = gd_kassa_api('GET', '/payments/' . rawurlencode($id));
    if (!$ok) {
        gd_pay_event('платёж не перечитан', ['sign' => 'kassa', 'note' => 'id ' . $id]);
        return ['ok' => false, 'code' => 502, 'error' => 'Не удалось перечитать платёж'];
    }
    if (($p['status'] ?? '') !== 'succeeded' || empty($p['paid'])) {
        gd_pay_event('платёж не оплачен', ['sign' => 'kassa',
            'note' => 'статус ' . (string)($p['status'] ?? '?')]);
        return ['ok' => true, 'note' => 'Платёж не оплачен — ключ не выдаём'];
    }

    $label = (string)($p['metadata']['order'] ?? '');
    $paid = (float)($p['amount']['value'] ?? 0);

    if (!preg_match('/^gd[0-9a-f]{16}$/', $label)) {
        gd_pay_event('платёж без заказа', ['sum' => $paid, 'sign' => 'kassa',
            'note' => 'метка: ' . ($label === '' ? 'пусто' : $label)]);
        return ['ok' => true, 'note' => 'Платёж без номера заказа'];
    }

    [$found, $info] = gd_pay_order($label);
    $order = $info['order'] ?? null;
    if ($found && $order && !empty($order['keys'])) {
        gd_pay_event('повтор уведомления', ['order' => $label, 'sum' => $paid, 'sign' => 'kassa',
            'note' => 'ключ уже выдан, второй раз не выдаём']);
        return ['ok' => true, 'note' => 'Уже выдано'];
    }

    $count = max(1, (int)($order['count'] ?? 1));
    $need = gd_pay_price() * $count;
    if ($paid + 0.01 < $need) {
        gd_pay_api('POST', '/api/licence/order', [
            'order' => $label, 'buyer' => (string)($order['buyer'] ?? ''),
            'count' => $count, 'sum' => $paid,
        ]);
        gd_pay_event('недоплата', ['order' => $label, 'sum' => $paid, 'sign' => 'kassa',
            'note' => 'нужно было ' . (int)$need . ' ₽ за ' . $count . ' шт.']);
        return ['ok' => true, 'note' => 'Недоплата: ключ не выдан'];
    }

    gd_pay_issue([
        'count' => $count,
        'note' => 'ЮKassa ' . $id,
        'price' => gd_pay_price(),
        'order' => $label,
        'buyer' => (string)($order['buyer'] ?? ''),
        'by' => 'yookassa',
    ]);
    gd_pay_event('ключ выдан', ['order' => $label, 'sum' => $paid, 'sign' => 'kassa',
        'note' => 'покупатель ' . (string)($order['buyer'] ?? '?') . ', ключей: ' . $count]);
    return ['ok' => true, 'note' => 'Ключ выдан'];
}

// ------------------------------------- приложение ЮMoney (проверка оплаты)
//
// Приложение, зарегистрированное на yoomoney.ru/myservices/new, даёт доступ к
// API кошелька. Нам от него нужно одно право — operation-history: по нему сайт
// сам спрашивает у ЮMoney, пришёл ли платёж с меткой нашего заказа. Тогда ключ
// выдаётся, даже если уведомление не дошло.

define('GD_YM_SCOPE', 'account-info operation-history');
define('GD_YM_HOST', getenv('GEODASH_YM_HOST') ?: 'https://yoomoney.ru');

function gd_ym_ready(): bool {
    $c = gd_pay_conf();
    return !empty($c['clientId']) && !empty($c['ymToken']);
}

/** Адрес, который нужно вписать в «Redirect URI» при регистрации приложения. */
function gd_ym_redirect(string $host = ''): string {
    $host = $host ?: ($_SERVER['HTTP_HOST'] ?? 'geodash.winnigames2024.ru');
    return 'https://' . $host . '/licensekey/index.php';
}

/** Адрес для «Notification URI» — без параметров, их ЮMoney не принимает. */
function gd_ym_notify_url(string $host = ''): string {
    $host = $host ?: ($_SERVER['HTTP_HOST'] ?? 'geodash.winnigames2024.ru');
    return 'https://' . $host . '/pay/yoomoney.php';
}

/** Обменять временный код на постоянный токен. */
function gd_ym_token_from_code(string $code, string $host = ''): array {
    return gd_ym_token_from_code_at($code, gd_ym_redirect($host));
}

/** То же, но с заданным redirect_uri: ЮMoney сверяет его побайтно. */
function gd_ym_token_from_code_at(string $code, string $redirect): array {
    $c = gd_pay_conf();
    $post = [
        'code' => $code,
        'client_id' => (string)$c['clientId'],
        'grant_type' => 'authorization_code',
        'redirect_uri' => $redirect,
    ];
    if (!empty($c['clientSecret'])) $post['client_secret'] = (string)$c['clientSecret'];

    $ch = curl_init(GD_YM_HOST . '/oauth/token');
    curl_setopt_array($ch, [
        CURLOPT_POST => true,
        CURLOPT_POSTFIELDS => http_build_query($post),
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_HTTPHEADER => ['Content-Type: application/x-www-form-urlencoded'],
        CURLOPT_TIMEOUT => 25,
    ]);
    $raw = curl_exec($ch);
    curl_close($ch);
    $j = json_decode((string)$raw, true);
    if (!is_array($j)) return [false, 'ЮMoney не ответила'];
    if (!empty($j['access_token'])) return [true, (string)$j['access_token']];
    return [false, 'ЮMoney отказала: ' . (string)($j['error'] ?? 'неизвестно')];
}

/** Запрос к API кошелька. Токен уходит только заголовком, как требует ЮMoney. */
function gd_ym_api(string $path, array $params = []): array {
    $c = gd_pay_conf();
    if (empty($c['ymToken'])) return [false, ['error' => 'Приложение не подключено']];
    $ch = curl_init(GD_YM_HOST . '/api/' . ltrim($path, '/'));
    curl_setopt_array($ch, [
        CURLOPT_POST => true,
        CURLOPT_POSTFIELDS => http_build_query($params),
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_HTTPHEADER => [
            'Authorization: Bearer ' . $c['ymToken'],
            'Content-Type: application/x-www-form-urlencoded',
        ],
        CURLOPT_TIMEOUT => 25,
    ]);
    $raw = curl_exec($ch);
    $code = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
    curl_close($ch);
    $j = json_decode((string)$raw, true);
    if ($code === 401) return [false, ['error' => 'Токен больше не действует — подключите приложение заново']];
    if (!is_array($j)) return [false, ['error' => 'ЮMoney не ответила']];
    return [$code >= 200 && $code < 300, $j];
}

/**
 * Нашёлся ли успешный приход с меткой заказа.
 * Возвращает [платёж|null, текст ошибки]: ошибку ЮMoney важно показать,
 * иначе протухший токен выглядит как «оплаты нет».
 */
function gd_ym_find_payment(string $label): array {
    [$ok, $r] = gd_ym_api('operation-history', [
        'type' => 'deposition', 'label' => $label, 'records' => 10,
    ]);
    if (!$ok) return [null, (string)($r['error'] ?? 'ЮMoney не ответила')];
    if (empty($r['operations'])) return [null, ''];
    foreach ((array)$r['operations'] as $op) {
        if (($op['direction'] ?? '') !== 'in') continue;
        if (($op['status'] ?? '') !== 'success') continue;
        if ((string)($op['label'] ?? '') !== $label) continue;
        return [['amount' => (float)($op['amount'] ?? 0), 'id' => (string)($op['operation_id'] ?? '')], ''];
    }
    return [null, ''];
}

/**
 * Спросить у ЮMoney напрямую, оплачен ли заказ, и выдать ключи, если да.
 * Нужна, когда уведомление не дошло — например кошелёк его не отправил.
 */
function gd_pay_poll(string $order): array {
    if (!preg_match('/^gd[0-9a-f]{16}$/', $order)) return [false, 'Неверный номер заказа'];
    if (!gd_ym_ready()) return [false, 'Приложение ЮMoney не подключено'];

    [$found, $info] = gd_pay_order($order);
    $o = $found ? ($info['order'] ?? null) : null;
    if (!$o) return [false, 'Заказ не найден'];
    if (!empty($o['keys'])) return [true, 'Ключи по этому заказу уже выданы'];

    [$pay, $why] = gd_ym_find_payment($order);
    if (!$pay) return [false, $why !== '' ? $why : 'ЮMoney пока не видит оплату по этому заказу'];

    $count = max(1, (int)($o['count'] ?? 1));
    if ($pay['amount'] + 0.01 < gd_pay_price() * $count * 0.9) {
        gd_pay_event('недоплата', ['order' => $order, 'sum' => $pay['amount'], 'sign' => 'проверка',
            'note' => 'найдено в истории операций']);
        return [false, 'Платёж найден, но сумма меньше цены'];
    }

    gd_pay_issue([
        'count' => $count,
        'note'  => 'ЮMoney ' . $pay['id'] . ' (проверка по истории)',
        'price' => gd_pay_price(),
        'order' => $order,
        'buyer' => (string)($o['buyer'] ?? ''),
        'by'    => 'yoomoney-api',
    ]);
    gd_pay_event('ключ выдан', ['order' => $order, 'sum' => $pay['amount'], 'sign' => 'проверка',
        'note' => 'уведомление не дошло, нашли сами по истории операций']);
    return [true, 'Оплата найдена — ключи выданы'];
}

/** Куда отправляется форма кошелька ЮMoney. */
function gd_pay_action(): string { return 'https://yoomoney.ru/quickpay/confirm.xml'; }

/**
 * Поля формы ЮMoney для кнопки «Купить». Способ оплаты (кошелёк или карта)
 * не задаём жёстко — его выбирает сам покупатель прямо в форме.
 *
 * need-fio / need-phone / need-address выключены сознательно: для ключа нужен
 * только номер заказа, лишние данные о покупателе нам собирать незачем.
 * E-mail спрашиваем лишь если в настройках включено askEmail.
 */
function gd_pay_form(string $order, int $count, string $siteUrl, string $token = ''): array {
    $c = gd_pay_conf();
    $what = 'Лицензионный ключ GEO DASH' . ($count > 1 ? " ($count шт.)" : '');
    return [
        'receiver' => $c['receiver'],
        'quickpay-form' => 'shop',
        'targets' => $what,
        'formcomment' => $what,
        'short-dest' => $what,
        'comment' => 'Заказ ' . $order,
        'sum' => $c['price'] * max(1, $count),
        'label' => $order,
        'successURL' => $token !== ''
            ? gd_pay_return($token, $siteUrl)
            : rtrim($siteUrl, '/') . '/buy/done.php?order=' . urlencode($order),
        'need-fio' => 'false',
        'need-email' => !empty($c['askEmail']) ? 'true' : 'false',
        'need-phone' => 'false',
        'need-address' => 'false',
    ];
}
