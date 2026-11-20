<?php
/** Старый адрес страницы настройки оплаты — ведёт на общую /admin/pay.php. */
header('Location: /admin/pay.php?p=money', true, 301);
exit;
