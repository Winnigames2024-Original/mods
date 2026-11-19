<?php
require_once __DIR__ . '/_inc/api.php';
session_destroy();
header('Location: /');
