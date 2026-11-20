<?php
/**
 * Сюда ЮMoney возвращает временный код после того, как админ разрешил
 * приложению доступ к кошельку. Меняем код на постоянный токен и сохраняем.
 *
 * Этот же адрес вписывается в «Redirect URI» при регистрации приложения.
 */
require_once __DIR__ . '/../_inc/api.php';
require_once __DIR__ . '/../_inc/pay.php';
gd_login_required();
if (!gd_is_admin()) {
    http_response_code(403);
    gd_head('Приложение ЮMoney');
    echo '<section class="section"><div class="container"><h2>Только для ' . h(GD_ADMIN) . '</h2></div></section>';
    gd_foot();
    exit;
}

$code = (string)($_GET['code'] ?? '');
$deny = (string)($_GET['error'] ?? '');
$ok = false; $text = '';

if ($deny !== '') {
    $text = $deny === 'access_denied'
        ? 'Доступ не выдан: на странице ЮMoney нажата «Отказать».'
        : 'ЮMoney вернула ошибку: ' . $deny;
} elseif ($code === '') {
    $text = 'ЮMoney не передала код. Начните подключение заново со страницы оплаты.';
} else {
    // ЮMoney сверяет redirect_uri побайтно с тем, что было в запросе, а код
    // мог прийти на любой из наших адресов — пробуем их по очереди
    $host = $_SERVER['HTTP_HOST'] ?? '';
    $got = false; $res = 'ЮMoney не ответила';
    foreach ([gd_ym_redirect(), 'https://' . $host . '/admin/ymauth.php',
              'https://' . $host . '/lklr/'] as $uri) {
        [$g, $r] = gd_ym_token_from_code_at($code, $uri);
        if ($g) { $got = true; $res = $r; break; }
        $res = $r;
    }

    if ($got) {
        [$saved, $r] = gd_pay_store_set(['ymToken' => $res]);
        $ok = $saved;
        $text = $saved
            ? 'Приложение подключено: теперь сайт может сам спрашивать у ЮMoney, пришла ли оплата.'
            : 'Токен получили, но сохранить не вышло: ' . (string)($r['error'] ?? 'сервер игры не отвечает');
    } else {
        $text = (string)$res;
    }
}

gd_head('Приложение ЮMoney', 'admin');
?>
<section class="section"><div class="container" style="max-width:720px">
  <h2><?= $ok ? 'Готово' : 'Не получилось' ?></h2>
  <div class="card">
    <p><?= h($text) ?></p>
    <?php if (!$ok): ?>
      <p class="muted">Код действует меньше минуты — если между нажатиями прошло больше,
        просто попробуйте ещё раз.</p>
    <?php endif; ?>
    <p style="margin-top:14px"><a class="btn small" href="/admin/pay.php?p=money">К настройке оплаты</a></p>
  </div>
</div></section>
<?php gd_foot();
