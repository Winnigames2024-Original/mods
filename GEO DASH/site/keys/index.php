<?php
/**
 * «Мои ключи» — все лицензионные ключи аккаунта: активированный, свободные,
 * отключённые. Отсюда ключ можно активировать, временно отключить и включить.
 */
require_once __DIR__ . '/../_inc/api.php';
require_once __DIR__ . '/../_inc/pay.php';
gd_login_required();
$me = gd_me();
if (!$me) { session_destroy(); header('Location: /login/'); exit; }

$msg = ''; $err = '';

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $act = $_POST['act'] ?? '';
    $key = trim((string)($_POST['key'] ?? ''));
    [$ok, $r] = match ($act) {
        'activate' => gd_post('/api/licence/activate', ['key' => $key, 'device' => 'site', 'platform' => 'site'], gd_token()),
        'off'      => gd_post('/api/licence/toggle', ['key' => $key, 'off' => true], gd_token()),
        'on'       => gd_post('/api/licence/toggle', ['key' => $key, 'off' => false], gd_token()),
        default    => [false, ['error' => 'неизвестное действие']],
    };
    if ($ok) {
        $msg = match ($act) {
            'activate' => 'Ключ активирован — игра открыта на этом аккаунте',
            'off'      => 'Ключ отключён. Игра на нём больше не запустится, пока не включите обратно',
            'on'       => 'Ключ включён — можно играть',
            default    => 'Готово',
        };
    } else $err = $r['error'] ?? 'Не получилось';
}

[$okK, $lic] = gd_get('/api/licence/mine', gd_token());
$keys     = $okK && is_array($lic['licences'] ?? null) ? $lic['licences'] : [];
$licensed = $okK && !empty($lic['licensed']);
$reason   = (string)($lic['reason'] ?? 'none');
$myKey    = (string)($lic['key'] ?? '');
$isAdmin  = !empty($me['admin']);

/** Что с ключом: активирован, отключён, отозван, свободен. */
$state = function (array $k) use ($myKey): array {
    if (!empty($k['revoked'])) return ['отозван', '#a33', 'его отключил администратор'];
    if (!empty($k['off']))     return ['отключён', '#a76', 'вы отключили его сами'];
    if (!empty($k['user']))    return ['активирован', '#1d6',
        strcasecmp((string)$k['key'], $myKey) === 0 ? 'этим ключом открыта игра' : 'активирован на ' . $k['user']];
    return ['свободен', '#48c', 'можно активировать или отдать другому игроку'];
};

gd_head('Мои ключи', 'keys');
?>
<style>
  .kcard { border:2px solid #0006; border-radius:14px; padding:14px 16px; margin-bottom:12px;
           background:#ffffff0d; display:flex; gap:14px; align-items:center; flex-wrap:wrap }
  .kcode { font-family:Consolas,monospace; font-size:21px; letter-spacing:.5px }
  .kbadge { font-size:12px; font-weight:bold; padding:3px 9px; border-radius:20px; color:#071 }
  .kgrow { flex:1 1 220px }
</style>
<section class="section"><div class="container" style="max-width:860px">
  <h2>Мои ключи</h2>
  <p class="muted" style="margin-top:-8px">
    Один ключ открывает игру на одном аккаунте — навсегда и на всех ваших устройствах,
    и на компьютере, и на телефоне. Лишние ключи можно отдать друзьям.
  </p>

  <?php if ($msg): ?><div class="alert ok"><?= h($msg) ?></div><?php endif; ?>
  <?php if ($err): ?><div class="alert err"><?= h($err) ?></div><?php endif; ?>

  <div class="card" style="margin-bottom:18px">
    <?php if ($isAdmin): ?>
      <p><b>Аккаунт администратора</b> — игра открыта без ключа.</p>
    <?php elseif ($licensed): ?>
      <p><b>Игра активирована.</b> Ключ <code class="kcode"><?= h($myKey) ?></code></p>
    <?php elseif ($reason === 'off'): ?>
      <p style="color:#ffd24a"><b>Ваш ключ отключён.</b> Игра его не примет и попросит другой —
        включите ключ в списке ниже, и всё заработает.</p>
    <?php elseif ($reason === 'revoked'): ?>
      <p style="color:#f88"><b>Ваш ключ отозван администратором.</b> Напишите
        <?= h(GD_ADMIN) ?> или активируйте другой ключ.</p>
    <?php else: ?>
      <p>Игра ещё не активирована: при первом запуске она попросит ключ.</p>
    <?php endif; ?>

    <form class="flex" method="post" style="gap:8px;margin-top:14px;flex-wrap:wrap">
      <input type="hidden" name="act" value="activate">
      <input type="text" name="key" placeholder="GD-XXXX-XXXX-XXXX-XXXX" required
             style="max-width:280px;font-family:Consolas,monospace;text-transform:uppercase">
      <button class="btn small green" type="submit">АКТИВИРОВАТЬ КЛЮЧ</button>
      <?php if (gd_pay_ready()): ?>
        <a class="btn small gold" href="/buy/">КУПИТЬ ЕЩЁ — <?= (int)gd_pay_price() ?> ₽</a>
      <?php endif; ?>
    </form>
  </div>

  <?php if (!$keys): ?>
    <div class="card">
      <p>Ключей пока нет.
        <?php if (gd_pay_ready()): ?>
          Купить можно <a href="/buy/">на странице покупки</a> — ключ придёт сюда сразу после оплаты.
        <?php else: ?>
          Получить ключ можно у <?= h(GD_ADMIN) ?>.
        <?php endif; ?>
      </p>
    </div>
  <?php else: ?>
    <?php foreach ($keys as $k): ?>
      <?php [$label, $color, $note] = $state($k); ?>
      <div class="kcard">
        <div class="kgrow">
          <div class="kcode"><?= h($k['key']) ?></div>
          <div class="muted" style="font-size:13px;margin-top:4px">
            <?= h($note) ?><?= !empty($k['created']) ? ' · получен ' . h(substr((string)$k['created'], 0, 10)) : '' ?>
          </div>
        </div>
        <span class="kbadge" style="background:<?= $color ?>"><?= h($label) ?></span>
        <?php if (empty($k['revoked'])): ?>
          <form method="post">
            <input type="hidden" name="act" value="<?= !empty($k['off']) ? 'on' : 'off' ?>">
            <input type="hidden" name="key" value="<?= h($k['key']) ?>">
            <button class="btn small <?= !empty($k['off']) ? 'green' : 'ghost' ?>" type="submit">
              <?= !empty($k['off']) ? 'ВКЛЮЧИТЬ' : 'ОТКЛЮЧИТЬ' ?></button>
          </form>
        <?php endif; ?>
      </div>
    <?php endforeach; ?>
    <p class="muted">Отключённый ключ не работает ни на одном устройстве: игра попросит другой ключ,
      пока вы не включите этот обратно. Отключение ничего не тратит — ключ остаётся вашим.</p>
  <?php endif; ?>
</div></section>
<?php gd_foot();
