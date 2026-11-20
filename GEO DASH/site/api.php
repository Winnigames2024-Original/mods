<?php
/**
 * GEO DASH — маленький служебный вход для сайта.
 *
 *   /api.php?action=updateserver&value=КЛЮЧ    — попросить сервер обновиться
 *   /api.php?action=status                      — версия и состояние сборок
 *   /api.php?action=getlicencekey&value=...     — выдать лицензионный ключ
 *   /api.php?action=licenceinfo&value=...       — сводка по выданным ключам
 *   /api.php?action=yoomoney                    — уведомление об оплате (ЮMoney)
 *   /api.php?action=order&order=...             — что выдано по заказу
 *
 * Обновление не запускается прямо отсюда: php работает от www-data и прав
 * на обновление у него нет. Вместо этого рядом кладётся файл-заявка.
 * За файлом следит systemd (geodash-request.path) и запускает обновление
 * в тот же миг, как заявка появилась; минутный таймер остаётся на случай сбоя.
 */

require_once __DIR__ . '/_inc/pay.php';

header('Cache-Control: no-store');

// ключи можно задать в окружении, иначе берутся эти
define('GD_UPDATE_KEY',  getenv('GEODASH_UPDATE_KEY')  ?: 'fj66yhKEY_tfde5ADMIN');
define('GD_LICENCE_KEY', getenv('GEODASH_LICENCE_KEY') ?: 'GEODASH_AADDMMIINN22002266JSD');

$root = __DIR__;
$flag = $root . '/.update-request';
$log  = $root . '/.update-log';

function out(array $data, int $code = 200): void {
    header('Content-Type: application/json; charset=utf-8');
    http_response_code($code);
    echo json_encode($data, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
    exit;
}

function out_text(string $text, int $code = 200): void {
    header('Content-Type: text/plain; charset=utf-8');
    http_response_code($code);
    echo $text;
    exit;
}

/**
 * Разбор value вида «key:АБВ;format:json;count:3;note:для друзей;»
 * Пары идут через «;», имя и значение — через первое «:».
 * Если «:» нет вообще, считаем, что передали только ключ.
 */
function gd_parse_value(string $value): array {
    $value = trim($value);
    if ($value === '') return [];
    if (strpos($value, ':') === false) return ['key' => $value];
    $out = [];
    foreach (explode(';', $value) as $part) {
        $part = trim($part);
        if ($part === '') continue;
        $p = explode(':', $part, 2);
        $name = strtolower(trim($p[0]));
        if ($name === '') continue;
        $out[$name] = isset($p[1]) ? trim($p[1]) : '';
    }
    return $out;
}

/** Неверный ключ: отвечаем не сразу, чтобы перебор был бессмысленным. */
function gd_deny(string $format): void {
    usleep(400000);
    if ($format === 'text') out_text("Неверный ключ админа\n", 403);
    out(['ok' => false, 'error' => 'Неверный ключ админа'], 403);
}

$action = strtolower((string)($_GET['action'] ?? ''));

switch ($action) {

    case 'updateserver':
        $value = $_GET['value'] ?? '';
        // сравнение без утечки времени
        if (!is_string($value) || !hash_equals(GD_UPDATE_KEY, $value)) {
            usleep(300000);
            out(['ok' => false, 'error' => 'Неверный ключ'], 403);
        }

        $ok = @file_put_contents($flag, date('c') . ' ' . ($_SERVER['REMOTE_ADDR'] ?? '?') . "\n") !== false;
        if (!$ok) out(['ok' => false, 'error' => 'Не удалось записать заявку на обновление'], 500);

        out([
            'ok' => true,
            'queued' => true,
            'note' => 'Заявка принята: обновление началось',
            'at' => date('c')
        ]);

    // ------------------------------------------------- лицензионные ключи
    //
    // /api.php?action=getlicencekey&value=key:АДМИНКЛЮЧ;format:json;
    //
    // Необязательные поля в value:
    //   count:3           — сколько ключей выдать за раз (1..50)
    //   note:подарок      — пометка, видна только в админке
    //   user:ИМЯ          — сразу активировать ключ на этом аккаунте
    //   price:199         — записать цену в карточку ключа
    //   format:json|text  — ответ как json или простым текстом
    //
    case 'getlicencekey':
    case 'licencekey':
    case 'getkey':
        $v = gd_parse_value((string)($_GET['value'] ?? ''));
        $format = strtolower($v['format'] ?? ($_GET['format'] ?? 'json')) === 'text' ? 'text' : 'json';
        $given = (string)($v['key'] ?? '');

        if ($given === '' || !hash_equals(GD_LICENCE_KEY, $given)) gd_deny($format);

        [$ok, $data, $code] = gd_pay_issue([
            'count' => max(1, min(50, (int)($v['count'] ?? 1))),
            'note'  => (string)($v['note'] ?? 'выдано по ссылке'),
            'price' => (int)($v['price'] ?? 0),
            'user'  => (string)($v['user'] ?? ''),
            'by'    => 'link',
        ]);

        if (!$ok) {
            $err = $data['error'] ?? 'Сервер игры не отвечает';
            if ($format === 'text') out_text("Ошибка: $err\n", $code ?: 500);
            out(['ok' => false, 'error' => $err], $code ?: 500);
        }

        $keys = $data['keys'] ?? [];
        if ($format === 'text') out_text(implode("\n", $keys) . "\n");

        out([
            'ok' => true,
            'count' => count($keys),
            'keys' => $keys,
            'activated' => $data['activated'] ?? '',
            'note' => 'Ключ вводится в игре при первом запуске. Один ключ — один аккаунт, навсегда.',
            'at' => date('c'),
        ]);

    /** Сводка по ключам: сколько выдано, занято, свободно. */
    case 'licenceinfo':
        $v = gd_parse_value((string)($_GET['value'] ?? ''));
        $given = (string)($v['key'] ?? '');
        if ($given === '' || !hash_equals(GD_LICENCE_KEY, $given)) gd_deny('json');
        [$ok, $data] = gd_pay_stats();
        if (!$ok) out(['ok' => false, 'error' => 'Сервер игры не отвечает'], 502);
        out(['ok' => true] + $data);

    // ------------------------------------------------------------- оплата
    //
    // Уведомление от ЮMoney (http-notification). Подпись проверяется
    // секретом из настроек оплаты; ключ выдаётся один раз на заказ.
    //
    case 'yoomoney':
        $res = gd_pay_notify($_POST);
        if (!$res['ok']) { http_response_code($res['code'] ?? 400); echo $res['error']; exit; }
        // ЮMoney ждёт только 200 OK
        out_text('OK');

    /**
     * Уведомление от ЮKassa (API v3). Тело приходит как json, подписи нет:
     * проверяем адрес отправителя и перечитываем платёж у самой ЮKassa.
     * На всё, кроме успеха, отвечаем ошибкой — она повторит доставку.
     */
    case 'yookassa':
        $raw = (string)file_get_contents('php://input');
        $ip  = (string)($_SERVER['REMOTE_ADDR'] ?? '');
        $res = gd_kassa_notify($raw, $ip);
        if (!$res['ok']) { http_response_code($res['code'] ?? 400); echo $res['error']; exit; }
        out_text('OK');

    /** Что выдано по заказу — этим пользуется страница «оплата прошла». */
    case 'order':
        $order = (string)($_GET['order'] ?? '');
        if ($order === '') out(['ok' => false, 'error' => 'Не указан заказ'], 400);
        [$ok, $data] = gd_pay_order($order);
        if (!$ok) out(['ok' => false, 'error' => 'Сервер игры не отвечает'], 502);
        out(['ok' => true] + (array)$data);

    case 'status':
        $dl = $root . '/download';
        $file = function (string $n) use ($dl) {
            $p = "$dl/$n";
            return is_file($p)
                ? ['size' => filesize($p), 'updated' => date('c', filemtime($p))]
                : null;
        };
        $version = null;
        $vf = $root . '/update.json';
        if (is_file($vf)) {
            $j = json_decode((string)file_get_contents($vf), true);
            $version = $j['version'] ?? null;
        }
        out([
            'ok' => true,
            'version' => $version,
            'builds' => [
                'exe' => $file('GEO_DASH.exe'),
                'apk' => $file('GEO_DASH.apk'),
                'setup' => $file('GEO_DASH_Setup.exe'),
                'setupApk' => $file('GEO_DASH_Setup.apk'),
                'autoupdate' => $file('autoupdate.exe'),
            ],
            'licence' => [
                'sale' => gd_pay_ready(),
                'price' => gd_pay_price(),
                'provider' => gd_pay_provider(),
            ],
            'updatePending' => is_file($flag),
            'lastUpdate' => is_file($log) ? trim((string)file_get_contents($log)) : null,
        ]);

    default:
        out([
            'ok' => false,
            'error' => 'Неизвестное действие',
            'actions' => ['updateserver', 'status', 'getlicencekey', 'licenceinfo', 'order',
                          'yoomoney', 'yookassa']
        ], 400);
}
