<?php
// Общая конфигурация и помощники сайта NareLauncher.
// Сайт разговаривает с NareNet (Node) по локальному адресу на том же VPS.

if (session_status() === PHP_SESSION_NONE) session_start();

const NARENET = 'http://127.0.0.1:2445';                 // NareNet на этом же сервере
const SITE    = 'https://narelauncher.winnigames2024.ru';
const GH_UPDATE = 'https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/narelauncher/update.json';
const GH_INSTALLER = 'https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/nareinstaller/NareInstaller.exe';

function api_post($path, $data) {
    $c = curl_init(NARENET . $path);
    curl_setopt_array($c, [
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_POST => true,
        CURLOPT_HTTPHEADER => ['Content-Type: application/json'],
        CURLOPT_POSTFIELDS => json_encode($data, JSON_UNESCAPED_UNICODE),
        CURLOPT_TIMEOUT => 8,
    ]);
    $r = curl_exec($c); curl_close($c);
    return json_decode($r, true) ?: [];
}

function api_get($path) {
    $c = curl_init(NARENET . $path);
    curl_setopt_array($c, [CURLOPT_RETURNTRANSFER => true, CURLOPT_TIMEOUT => 8]);
    $r = curl_exec($c); curl_close($c);
    return json_decode($r, true) ?: [];
}

function me()    { return $_SESSION['nick'] ?? null; }
function token() { return $_SESSION['token'] ?? null; }
function require_login() { if (!me()) { header('Location: /login/'); exit; } }
function h($s)   { return htmlspecialchars((string)$s, ENT_QUOTES, 'UTF-8'); }

/** Последняя версия лаунчера с GitHub (для страниц «Скачать» и «Главная»). */
function launcher_version() {
    static $v = null;
    if ($v !== null) return $v;
    $j = json_decode(@file_get_contents(GH_UPDATE), true);
    $v = $j['version'] ?? '—';
    return $v;
}
