<?php
/**
 * GEO DASH — лицензионные ключи и оплата через ЮMoney.
 *
 * Настройки оплаты НЕ лежат в репозитории. Файл на сервере:
 *
 *   /etc/geodash/pay.json
 *   {
 *     "receiver": "4100XXXXXXXXXX",            номер кошелька или shopId
 *     "secret":   "секрет http-уведомлений",   из настроек ЮMoney
 *     "price":    199,
 *     "currency": "RUB",
 *     "issueKey": "GEODASH_AADDMMIINN22002266JSD"
 *   }
 *
 *   chown root:www-data /etc/geodash/pay.json && chmod 640 /etc/geodash/pay.json
 *
 * Пока файла нет — продажа просто выключена, всё остальное работает:
 * ключи выдаются по ссылке и из админки.
 */

define('GD_PAY_FILE', getenv('GEODASH_PAY_FILE') ?: '/etc/geodash/pay.json');
define('GD_PAY_API', getenv('GEODASH_API') ?: 'http://127.0.0.1:37534');

function gd_pay_conf(): array {
    static $c = null;
    if ($c !== null) return $c;
    $c = [
        'receiver' => (string)(getenv('GEODASH_YOOMONEY') ?: ''),
        'secret'   => (string)(getenv('GEODASH_YOOMONEY_SECRET') ?: ''),
        'price'    => (int)(getenv('GEODASH_PRICE') ?: 199),
        'currency' => 'RUB',
        'issueKey' => (string)(getenv('GEODASH_ISSUE_SECRET') ?: 'GEODASH_AADDMMIINN22002266JSD'),
    ];
    if (is_readable(GD_PAY_FILE)) {
        $j = json_decode((string)file_get_contents(GD_PAY_FILE), true);
        if (is_array($j)) $c = array_merge($c, array_filter($j, fn($v) => $v !== '' && $v !== null));
    }
    $c['price'] = max(1, (int)$c['price']);
    return $c;
}

/** Настроена ли продажа ключей. */
function gd_pay_ready(): bool {
    $c = gd_pay_conf();
    return $c['receiver'] !== '' && $c['secret'] !== '';
}

function gd_pay_price(): int { return gd_pay_conf()['price']; }
function gd_pay_receiver(): string { return gd_pay_conf()['receiver']; }

/** Запрос к игровому серверу с секретом выдачи (только по 127.0.0.1). */
function gd_pay_api(string $method, string $path, $body = null): array {
    $ch = curl_init(GD_PAY_API . $path);
    $headers = ['Accept: application/json', 'X-GD-Issue: ' . gd_pay_conf()['issueKey']];
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

function gd_pay_order(string $order): array {
    [$ok, $d] = gd_pay_api('GET', '/api/licence/order/' . rawurlencode($order));
    return [$ok, $d];
}

/** Новый номер заказа и запись «кто покупает» до оплаты. */
function gd_pay_start(string $buyer, int $count = 1): ?string {
    $order = 'gd' . bin2hex(random_bytes(8));
    [$ok] = gd_pay_api('POST', '/api/licence/order', [
        'order' => $order, 'buyer' => $buyer,
        'count' => max(1, min(20, $count)), 'sum' => gd_pay_price() * max(1, $count),
    ]);
    return $ok ? $order : null;
}

/**
 * Уведомление от ЮMoney. Проверяем подпись и выдаём ключи один раз.
 * Порядок полей в подписи задан документацией ЮMoney и менять его нельзя.
 */
function gd_pay_notify(array $post): array {
    $c = gd_pay_conf();
    if ($c['secret'] === '') return ['ok' => false, 'code' => 503, 'error' => 'Оплата не настроена'];

    $fields = [
        $post['notification_type'] ?? '',
        $post['operation_id'] ?? '',
        $post['amount'] ?? '',
        $post['currency'] ?? '',
        $post['datetime'] ?? '',
        $post['sender'] ?? '',
        $post['codepro'] ?? '',
        $c['secret'],
        $post['label'] ?? '',
    ];
    $mine = sha1(implode('&', $fields));
    $given = strtolower((string)($post['sha1_hash'] ?? ''));
    if (!hash_equals($mine, $given)) return ['ok' => false, 'code' => 403, 'error' => 'Подпись не сошлась'];

    $label = (string)($post['label'] ?? '');
    if ($label === '' || !preg_match('/^gd[0-9a-f]{16}$/', $label))
        return ['ok' => true, 'note' => 'Платёж без номера заказа — ключ не выдаём'];

    [$ok, $info] = gd_pay_order($label);
    $order = $info['order'] ?? null;
    if ($ok && $order && !empty($order['keys']))
        return ['ok' => true, 'note' => 'Уже выдано'];

    $count = max(1, (int)($order['count'] ?? 1));
    $need  = gd_pay_price() * $count * 0.9;      // карта забирает процент — допуск 10%
    $paid  = (float)($post['amount'] ?? 0);
    if ($paid + 0.01 < $need) {
        gd_pay_api('POST', '/api/licence/order', [
            'order' => $label, 'buyer' => (string)($order['buyer'] ?? ''),
            'count' => $count, 'sum' => $paid,
        ]);
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
    return ['ok' => true, 'note' => 'Ключ выдан'];
}

/** Поля формы ЮMoney для кнопки «Купить». */
function gd_pay_form(string $order, int $count, string $siteUrl): array {
    $c = gd_pay_conf();
    return [
        'receiver' => $c['receiver'],
        'quickpay-form' => 'shop',
        'paymentType' => 'AC',
        'targets' => 'Лицензионный ключ GEO DASH' . ($count > 1 ? " ($count шт.)" : ''),
        'sum' => $c['price'] * max(1, $count),
        'label' => $order,
        'successURL' => rtrim($siteUrl, '/') . '/buy/done.php?order=' . urlencode($order),
    ];
}
