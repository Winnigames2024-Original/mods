<?php
/**
 * Выдача лицензии по одноразовому коду:
 *   /licensekey/index.php?action=givelicense&value=<код>
 *
 * Сюда же ЮMoney возвращает код авторизации приложения, если этот адрес
 * указан в «Redirect URI». Сама страница — в _inc/licencepage.php, её делят
 * этот адрес и короткий /lklr/.
 */
require __DIR__ . '/../_inc/licencepage.php';
