<?php
require_once __DIR__ . '/../_inc/api.php';
gd_login_required();
$me = gd_me();
if (!$me) { session_destroy(); header('Location: /login/'); exit; }
[$okL, $levels] = gd_get('/api/levels?author=' . urlencode(gd_user()));
[$okF, $friends] = gd_get('/api/friends', gd_token());
gd_head('Мой аккаунт', 'account');
?>
<section class="section"><div class="container">
  <h2><?= h($me['name']) ?><?php if (!empty($me['admin'])): ?> <span class="badge">админ</span><?php endif; ?></h2>

  <div class="stats" style="margin:18px 0 26px">
    <div class="stat"><b><?= (int)$me['stars'] ?></b><span>звёзды</span></div>
    <div class="stat"><b><?= (int)$me['coins'] ?></b><span>монеты</span></div>
    <div class="stat"><b><?= (int)$me['demons'] ?></b><span>демоны</span></div>
    <div class="stat"><b><?= (int)$me['levels'] ?></b><span>своих уровней</span></div>
    <div class="stat"><b><?= (int)($me['friends'] ?? 0) ?></b><span>друзей</span></div>
    <div class="stat"><b><?= (int)$me['attempts'] ?></b><span>попыток</span></div>
  </div>

  <div class="grid c2">
    <div class="card">
      <h3>Мои уровни на сервере</h3>
      <table class="lv">
        <tr><th>ID</th><th>Название</th><th>Сложность</th><th>♥</th><th>↓</th></tr>
        <?php foreach ($levels ?: [] as $l): ?>
          <tr>
            <td class="muted">#<?= (int)$l['id'] ?></td>
            <td class="lvname"><a href="/levels/view.php?id=<?= (int)$l['id'] ?>"><?= h($l['name']) ?></a></td>
            <td><span class="diff <?= gd_diff_class((int)$l['difficulty']) ?>"><?= h(gd_diff_name((int)$l['difficulty'])) ?></span></td>
            <td><?= (int)$l['likes'] ?></td><td><?= (int)$l['downloads'] ?></td>
          </tr>
        <?php endforeach; ?>
        <?php if (!$levels): ?><tr><td colspan="5" class="muted">Пока нет — загрузи уровень из редактора игры.</td></tr><?php endif; ?>
      </table>
    </div>

    <div class="card">
      <h3>Друзья</h3>
      <?php if ($friends): ?>
        <?php foreach ($friends as $f): ?>
          <div class="flex" style="padding:6px 0;border-bottom:1px solid #ffffff14">
            <a href="/profile/?user=<?= urlencode($f['name']) ?>"><b><?= h($f['name']) ?></b></a>
            <span class="muted right">★ <?= (int)$f['stars'] ?></span>
          </div>
        <?php endforeach; ?>
      <?php else: ?>
        <p class="muted">Друзей пока нет.</p>
      <?php endif; ?>
      <p style="margin-top:14px"><a class="btn small" href="/friends/">Запросы и поиск друзей</a></p>
    </div>
  </div>
</div></section>
<?php gd_foot();
