<?php
/**
 * GEO DASH — маленький служебный вход для сайта.
 *
 *   /api.php?action=updateserver&value=КЛЮЧ   — попросить сервер обновиться
 *   /api.php?action=status                     — версия и состояние сборок
 *
 * Обновление не запускается прямо отсюда: php работает от www-data и прав
 * на обновление у него нет. Вместо этого рядом кладётся файл-заявка,
 * а служба geodash-update (она ходит каждые 5 минут) видит её и обновляется
 * сразу же, после чего заявку удаляет.
 */

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

// ключ можно задать в окружении GEODASH_UPDATE_KEY, иначе берётся этот
define('GD_UPDATE_KEY', getenv('GEODASH_UPDATE_KEY') ?: 'fj66yhKEY_tfde5ADMIN');

$root = __DIR__;
$flag = $root . '/.update-request';
$log  = $root . '/.update-log';

function out(array $data, int $code = 200): void {
    http_response_code($code);
    echo json_encode($data, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
    exit;
}

$action = $_GET['action'] ?? '';

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
            'note' => 'Заявка принята: сервер обновится в ближайшую минуту',
            'at' => date('c')
        ]);

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
            ],
            'updatePending' => is_file($flag),
            'lastUpdate' => is_file($log) ? trim((string)file_get_contents($log)) : null,
        ]);

    default:
        out([
            'ok' => false,
            'error' => 'Неизвестное действие',
            'actions' => ['updateserver', 'status']
        ], 400);
}
