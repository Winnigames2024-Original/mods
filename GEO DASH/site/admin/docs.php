<?php
/**
 * Документация админа. Файл лежит рядом с API (/opt/geodash/ADMIN.md), а не в
 * сайте: внутри ключи выдачи, и наружу он не отдаётся — страницу видит только админ.
 */
require_once __DIR__ . '/../_inc/api.php';
gd_login_required();
if (!gd_is_admin()) {
    http_response_code(403);
    gd_head('Документация');
    echo '<section class="section"><div class="container"><h2>Только для ' . h(GD_ADMIN) . '</h2></div></section>';
    gd_foot();
    exit;
}

$paths = [
    getenv('GEODASH_DOCS') ?: '/opt/geodash/ADMIN.md',
    __DIR__ . '/../../docs/ADMIN.md',
    __DIR__ . '/ADMIN.md',
];
$md = null;
foreach ($paths as $p) if ($p && is_readable($p)) { $md = (string)file_get_contents($p); break; }

/** Маленький разборщик markdown: заголовки, списки, таблицы, код, ссылки. */
function gd_md(string $md): string {
    $out = '';
    $lines = preg_split('/\R/', $md);
    $n = count($lines);
    $inCode = false; $inList = false; $inTable = false;

    $inline = function (string $s): string {
        $s = h($s);
        $s = preg_replace('/`([^`]+)`/u', '<code>$1</code>', $s);
        $s = preg_replace('/\*\*([^*]+)\*\*/u', '<b>$1</b>', $s);
        $s = preg_replace('/\[([^\]]+)\]\(([^)\s]+)\)/u', '<a href="$2">$1</a>', $s);
        return $s;
    };

    for ($i = 0; $i < $n; $i++) {
        $l = rtrim($lines[$i]);

        if (preg_match('/^```/', $l)) {
            if ($inList) { $out .= "</ul>\n"; $inList = false; }
            $out .= $inCode ? "</code></pre>\n" : "<pre class=\"docs-code\"><code>";
            $inCode = !$inCode;
            continue;
        }
        if ($inCode) { $out .= h($l) . "\n"; continue; }

        // таблица
        if (strpos($l, '|') === 0 || (substr_count($l, '|') >= 2 && preg_match('/^\s*\|/', $l))) {
            $cells = array_map('trim', explode('|', trim($l, "| \t")));
            if (preg_match('/^[\s|:\-]+$/', $l)) continue;          // разделитель шапки
            if (!$inTable) { $out .= "<table class=\"lv docs-table\">\n"; $inTable = true; $head = true; }
            $out .= '<tr>';
            foreach ($cells as $c) $out .= ($head ? '<th>' : '<td>') . $inline($c) . ($head ? '</th>' : '</td>');
            $out .= "</tr>\n";
            $head = false;
            continue;
        }
        if ($inTable) { $out .= "</table>\n"; $inTable = false; }

        if (preg_match('/^(#{1,4})\s+(.*)$/u', $l, $m)) {
            if ($inList) { $out .= "</ul>\n"; $inList = false; }
            $lvl = min(4, strlen($m[1]) + 1);
            $out .= "<h$lvl>" . $inline($m[2]) . "</h$lvl>\n";
            continue;
        }
        if (preg_match('/^---+$/', $l)) {
            if ($inList) { $out .= "</ul>\n"; $inList = false; }
            $out .= "<hr>\n";
            continue;
        }
        if (preg_match('/^\s*[\*\-]\s+(.*)$/u', $l, $m)) {
            if (!$inList) { $out .= "<ul>\n"; $inList = true; }
            $out .= '<li>' . $inline($m[1]) . "</li>\n";
            continue;
        }
        if (preg_match('/^\s*(\d+)\.\s+(.*)$/u', $l, $m)) {
            if (!$inList) { $out .= "<ul>\n"; $inList = true; }
            $out .= '<li>' . $inline($m[2]) . "</li>\n";
            continue;
        }
        if (trim($l) === '') {
            if ($inList) { $out .= "</ul>\n"; $inList = false; }
            continue;
        }
        $out .= '<p>' . $inline($l) . "</p>\n";
    }
    if ($inCode) $out .= "</code></pre>\n";
    if ($inList) $out .= "</ul>\n";
    if ($inTable) $out .= "</table>\n";
    return $out;
}

gd_head('Документация админа', 'admin');
?>
<style>
  .docs-code { background:#0d0b22; border:2px solid #000; border-radius:10px; padding:14px 16px;
               overflow-x:auto; font-size:14px; line-height:1.5; color:#cfe3ff; }
  .docs h2 { margin-top:28px } .docs h3 { margin-top:22px }
  .docs-table th { text-align:left }
  .docs code { background:#ffffff14; padding:1px 5px; border-radius:5px }
</style>
<section class="section"><div class="container" style="max-width:900px">
  <p><a class="btn small ghost" href="/admin/">← В админку</a>
     <a class="btn small ghost" href="/admin/pay.php">Настройка оплаты</a></p>
  <div class="card docs">
    <?php if ($md === null): ?>
      <h2>Документация не найдена</h2>
      <p>Файл <code>/opt/geodash/ADMIN.md</code> появится при следующем обновлении сервера:</p>
      <pre class="docs-code"><code>curl -s "https://geodash.winnigames2024.ru/api.php?action=updateserver&amp;value=КЛЮЧ"</code></pre>
      <p class="muted">Он же всегда лежит в репозитории: <code>GEO DASH/docs/ADMIN.md</code>.</p>
    <?php else: ?>
      <?= gd_md($md) ?>
    <?php endif; ?>
  </div>
</div></section>
<?php gd_foot();
