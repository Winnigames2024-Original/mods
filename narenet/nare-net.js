#!/usr/bin/env node
'use strict';

/*
 * NareNet — сборный пункт для игры вместе.
 *
 * Зачем он: Minecraft умеет открывать одиночный мир «в локальную сеть», но
 * видят его только те, кто в той же сети. Этот сервер держит список
 * открытых миров и, когда у хозяина закрыт порт, проводит трафик гостей
 * через себя. Виртуальной сети, как в Radmin VPN, тут нет и быть не может
 * без драйвера в системе — вместо неё обычный обратный туннель.
 *
 * Что умеет:
 *   - список комнат: кто открыл мир, какая версия, загрузчик, моды;
 *   - сам проверяет, достаётся ли до порта хозяина снаружи;
 *   - хранит jar-ы модов по sha1, чтобы гость скачал ровно те же;
 *   - обратный туннель: хозяин за NAT получает публичный адрес.
 *
 * Весь обмен — на одном порту. Обычное соединение идёт как HTTP, а
 * соединение, начинающееся со слова NARE1, уходит в туннель.
 *
 * Зависимостей нет: только то, что уже есть в Node.js.
 */

const http = require('http');
const net = require('net');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const PORT = Number(process.env.NARENET_PORT || 2445);
const HOST = process.env.NARENET_BIND || '0.0.0.0';

const DATA_DIR = process.env.NARENET_DIR || '/var/lib/narenet';
const FILES_DIR = path.join(DATA_DIR, 'files');
const SHOTS_DIR = path.join(DATA_DIR, 'shots');

const TUNNEL_FROM = Number(process.env.NARENET_TUNNEL_FROM || 30000);
const TUNNEL_TO = Number(process.env.NARENET_TUNNEL_TO || 30060);

const ROOM_LIFE = 60 * 1000;          // без обновления комната пропадает
const ANNOUNCE_MIN = 3 * 1000;        // чаще чем раз в 3 секунды не слушаем
const FILE_MAX = 32 * 1024 * 1024;    // один файл
const FILES_MAX = 3 * 1024 * 1024 * 1024;
const BODY_MAX = 512 * 1024;          // json объявления
const SHOT_MAX = 1536 * 1024;         // json со скриншотом (base64)
const ROOMS_MAX = 200;
const OPEN_WAIT = 8 * 1000;           // сколько ждём встречное соединение хозяина

const VERSION = '1.19';

// ---------------------------------------------------------------- хозяйство

fs.mkdirSync(FILES_DIR, { recursive: true });
fs.mkdirSync(SHOTS_DIR, { recursive: true });

function now() { return Date.now(); }

function log() {
    const stamp = new Date().toISOString().replace('T', ' ').slice(0, 19);
    console.log('[' + stamp + ']', Array.from(arguments).join(' '));
}

function token() { return crypto.randomBytes(16).toString('hex'); }

function query(req) {
    const i = req.url.indexOf('?');
    if (i < 0) return {};
    const out = {};
    for (const pair of req.url.slice(i + 1).split('&')) {
        const eq = pair.indexOf('=');
        if (eq < 0) continue;
        try { out[decodeURIComponent(pair.slice(0, eq))] = decodeURIComponent(pair.slice(eq + 1)); }
        catch (e) { }
    }
    return out;
}

function plainIp(socket) {
    let ip = socket.remoteAddress || '';
    if (ip.startsWith('::ffff:')) ip = ip.slice(7);
    return ip;
}

function clean(text, limit) {
    if (typeof text !== 'string') return '';
    return text.replace(/[\u0000-\u001f\u007f]/g, '').trim().slice(0, limit || 64);
}

function isSha1(text) { return typeof text === 'string' && /^[0-9a-f]{40}$/i.test(text); }

// ------------------------------------------------------------------ комнаты

/** token -> комната */
const rooms = new Map();

function roomList() {
    const out = [];
    for (const room of rooms.values()) {
        if (now() - room.seen > ROOM_LIFE) continue;

        out.push({
            id: room.id,
            nick: room.nick,
            ip: room.mode === 'tunnel' ? null : room.ip,
            port: room.mode === 'tunnel' ? room.publicPort : room.port,
            host: room.mode === 'tunnel' ? null : room.ip,
            mode: room.mode,
            reachable: room.reachable,
            version: room.version,
            loader: room.loader,
            world: room.world,
            players: room.players,
            max: room.max,
            mods: room.mods,
            pack: room.pack,
            note: room.note,
            age: Math.round((now() - room.seen) / 1000)
        });
    }

    out.sort((a, b) => a.nick.localeCompare(b.nick));
    return out;
}

function forget(key, why) {
    const room = rooms.get(key);
    if (!room) return;

    rooms.delete(key);
    if (room.tunnel) room.tunnel.close(why || 'комната снята');
    log('комната снята:', room.nick, why || '');
}

setInterval(() => {
    for (const [key, room] of rooms)
        if (now() - room.seen > ROOM_LIFE) forget(key, 'хозяин перестал отвечать');
}, 10 * 1000);

/** Достаётся ли до порта хозяина снаружи: пробуем открыть соединение сами. */
function probe(ip, port, done) {
    if (!ip || !port) return done(false);

    const socket = new net.Socket();
    let answered = false;

    const finish = ok => {
        if (answered) return;
        answered = true;
        socket.destroy();
        done(ok);
    };

    socket.setTimeout(3000);
    socket.once('connect', () => finish(true));
    socket.once('timeout', () => finish(false));
    socket.once('error', () => finish(false));

    try { socket.connect(port, ip); } catch (e) { finish(false); }
}

// ------------------------------------------------------------------- файлы

function filePath(sha1) { return path.join(FILES_DIR, sha1.toLowerCase() + '.bin'); }

function filesSize() {
    let total = 0;
    for (const name of fs.readdirSync(FILES_DIR)) {
        try { total += fs.statSync(path.join(FILES_DIR, name)).size; } catch (e) { }
    }
    return total;
}

/** Самые старые по времени обращения уходят, когда место кончается. */
function makeRoom(need) {
    let total = filesSize();
    if (total + need <= FILES_MAX) return true;

    const list = fs.readdirSync(FILES_DIR).map(name => {
        const full = path.join(FILES_DIR, name);
        try {
            const info = fs.statSync(full);
            return { full, size: info.size, used: info.atimeMs || info.mtimeMs };
        } catch (e) { return null; }
    }).filter(Boolean);

    list.sort((a, b) => a.used - b.used);

    for (const one of list) {
        if (total + need <= FILES_MAX) break;
        try { fs.unlinkSync(one.full); total -= one.size; log('файл вытеснен:', one.full); }
        catch (e) { }
    }

    return total + need <= FILES_MAX;
}

// --------------------------------------------------------------------- HTTP

function send(res, code, body) {
    const text = JSON.stringify(body);
    res.writeHead(code, {
        'Content-Type': 'application/json; charset=utf-8',
        'Content-Length': Buffer.byteLength(text),
        'Cache-Control': 'no-store'
    });
    res.end(text);
}

function readBody(req, limit, done) {
    let size = 0;
    const parts = [];

    req.on('data', chunk => {
        size += chunk.length;
        if (size > limit) {
            req.destroy();
            return;
        }
        parts.push(chunk);
    });

    req.on('end', () => done(Buffer.concat(parts)));
    req.on('error', () => done(null));
}

function modsOf(raw) {
    if (!Array.isArray(raw)) return [];

    const out = [];
    for (const one of raw.slice(0, 300)) {
        if (!one || typeof one !== 'object') continue;

        out.push({
            id: clean(one.id, 80),
            name: clean(one.name, 120),
            version: clean(one.version, 60),
            file: clean(one.file, 160),
            sha1: isSha1(one.sha1) ? one.sha1.toLowerCase() : '',
            size: Number(one.size) > 0 ? Math.min(Number(one.size), FILE_MAX) : 0,
            need: one.need !== false          // по умолчанию мод обязателен гостю
        });
    }
    return out;
}

function announce(req, res) {
    readBody(req, BODY_MAX, buffer => {
        if (!buffer) return send(res, 400, { error: 'тело запроса не прочиталось' });

        let body;
        try { body = JSON.parse(buffer.toString('utf8')); }
        catch (e) { return send(res, 400, { error: 'это не JSON' }); }

        // забаненный или лишённый «Вместе» хозяином быть не может
        const who = clean(body.nick, 32);
        const ban = banOf(who);
        if (ban) return send(res, 403, { error: 'together-banned', reason: ban.reason || '', until: ban.until || 0 });
        if (policy.together[who]) return send(res, 403, { error: 'together-blocked' });

        const ip = plainIp(req.socket);
        const key = isSha1(body.token) || (typeof body.token === 'string' && body.token.length === 32)
            ? body.token : null;

        let room = key ? rooms.get(key) : null;

        if (room && room.ip !== ip) {
            // чужим ключом объявляться нельзя
            room = null;
        }

        if (!room) {
            if (rooms.size >= ROOMS_MAX) return send(res, 503, { error: 'слишком много комнат' });

            room = {
                key: token(),
                id: crypto.randomBytes(4).toString('hex'),
                ip: ip,
                made: now(),
                reachable: null,
                probed: 0,
                publicPort: 0,
                tunnel: null
            };
            rooms.set(room.key, room);
            log('новая комната:', ip);
        } else if (now() - room.seen < ANNOUNCE_MIN) {
            return send(res, 200, {
                token: room.key, id: room.id, ip: ip,
                port: room.publicPort || room.port,
                mode: room.mode, reachable: room.reachable, wait: true
            });
        }

        room.seen = now();
        room.nick = clean(body.nick, 32) || 'Игрок';
        room.port = Math.min(65535, Math.max(1, Number(body.port) || 25565));
        room.version = clean(body.version, 32);
        room.loader = clean(body.loader, 16);
        room.world = clean(body.world, 64);
        room.players = Number(body.players) || 1;
        room.max = Number(body.max) || 8;
        room.note = clean(body.note, 120);
        room.mods = modsOf(body.mods);
        room.pack = body.pack && typeof body.pack === 'object' ? {
            name: clean(body.pack.name, 120),
            sha1: isSha1(body.pack.sha1) ? body.pack.sha1.toLowerCase() : '',
            size: Number(body.pack.size) || 0,
            url: clean(body.pack.url, 300)
        } : null;

        const wants = clean(body.mode, 10) === 'tunnel' ? 'tunnel' : 'direct';

        // прямое подключение проверяем, но не чаще раза в полминуты
        if (now() - room.probed > 30 * 1000) {
            room.probed = now();
            probe(room.ip, room.port, ok => {
                room.reachable = ok;
                if (!ok && room.mode === 'direct' && !room.tunnel)
                    log('порт закрыт у', room.nick, room.ip + ':' + room.port);
            });
        }

        room.mode = room.tunnel ? 'tunnel' : wants;

        send(res, 200, {
            token: room.key,
            id: room.id,
            ip: ip,
            port: room.publicPort || room.port,
            mode: room.mode,
            reachable: room.reachable,
            tunnelReady: !!room.tunnel,
            players: rooms.size
        });
    });
}

function bye(req, res) {
    readBody(req, 4096, buffer => {
        let body = {};
        try { body = JSON.parse((buffer || Buffer.alloc(0)).toString('utf8') || '{}'); } catch (e) { }

        const room = body.token ? rooms.get(body.token) : null;
        if (room && room.ip === plainIp(req.socket)) forget(body.token, 'хозяин закрыл мир');

        send(res, 200, { ok: true });
    });
}

function fileGet(req, res, sha1, headOnly) {
    if (!isSha1(sha1)) return send(res, 400, { error: 'это не sha1' });

    const full = filePath(sha1);
    let info;
    try { info = fs.statSync(full); } catch (e) { return send(res, 404, { error: 'такого файла тут нет' }); }

    res.writeHead(200, {
        'Content-Type': 'application/java-archive',
        'Content-Length': info.size,
        'Cache-Control': 'public, max-age=604800'
    });

    if (headOnly) return res.end();

    try { fs.utimesSync(full, new Date(), info.mtime); } catch (e) { }
    fs.createReadStream(full).pipe(res);
}

function filePut(req, res, sha1) {
    if (!isSha1(sha1)) return send(res, 400, { error: 'это не sha1' });

    const full = filePath(sha1);
    if (fs.existsSync(full)) return send(res, 200, { ok: true, already: true });

    const declared = Number(req.headers['content-length']) || 0;
    if (declared > FILE_MAX) return send(res, 413, { error: 'файл больше ' + (FILE_MAX / 1048576) + ' МБ' });
    if (!makeRoom(declared || 1024 * 1024)) return send(res, 507, { error: 'на сервере нет места' });

    const temp = full + '.' + process.pid + '.part';
    const out = fs.createWriteStream(temp);
    const digest = crypto.createHash('sha1');
    let size = 0;
    let bad = false;

    const stop = (code, body) => {
        if (bad) return;
        bad = true;
        out.destroy();
        try { fs.unlinkSync(temp); } catch (e) { }
        req.destroy();
        send(res, code, body);
    };

    req.on('data', chunk => {
        size += chunk.length;
        if (size > FILE_MAX) return stop(413, { error: 'файл слишком большой' });

        digest.update(chunk);
        out.write(chunk);
    });

    req.on('error', () => stop(400, { error: 'передача прервалась' }));

    req.on('end', () => {
        if (bad) return;

        out.end(() => {
            const real = digest.digest('hex');

            if (real !== sha1.toLowerCase()) {
                try { fs.unlinkSync(temp); } catch (e) { }
                return send(res, 400, { error: 'содержимое не сходится с sha1', got: real });
            }

            try { fs.renameSync(temp, full); }
            catch (e) { return send(res, 500, { error: 'не сохранилось: ' + e.message }); }

            log('принят файл', sha1, size + ' байт');
            send(res, 200, { ok: true, size: size });
        });
    });
}


// =====================================================================
//  Аккаунты, баны и админ-панель.
//
//  Всё честно и на виду: пароли хранятся только как соль+хэш (scrypt),
//  бан просто запрещает лаунчеру работать и говорит об этом игроку,
//  ничего не прячется и в фоне не крутится. Админ — один ник, он
//  задаётся заранее и защищён своим паролем.
// =====================================================================

const ADMIN = (process.env.NARENET_ADMIN || 'Winnigames2024');
const SERVER_KEY = (process.env.NARENET_SERVER_KEY || '');
const COIN_REWARD = Number(process.env.NARENET_COIN_REWARD || 100);
const COIN_COOLDOWN = Number(process.env.NARENET_COIN_COOLDOWN_MIN || 60) * 60000;
const NAME_PRICE = Number(process.env.NARENET_NAME_PRICE || 1000);
const DAILY_BONUS = Number(process.env.NARENET_DAILY_BONUS || 50);
const DAILY_COOLDOWN = 20 * 60 * 60 * 1000;   // 20 часов

const ACCOUNTS_FILE = path.join(DATA_DIR, 'accounts.json');
const BANS_FILE = path.join(DATA_DIR, 'bans.json');
const WARNS_FILE = path.join(DATA_DIR, 'warns.json');
const POLICY_FILE = path.join(DATA_DIR, 'policy.json');

function loadJson(file, fallback) {
    try { return JSON.parse(fs.readFileSync(file, 'utf8')); }
    catch (e) { return fallback; }
}

function saveJson(file, data) {
    const tmp = file + '.' + process.pid + '.tmp';
    fs.writeFileSync(tmp, JSON.stringify(data, null, 2));
    fs.renameSync(tmp, file);
}

const accounts = loadJson(ACCOUNTS_FILE, {});   // nick -> { salt, hash, created }
const bans = loadJson(BANS_FILE, {});           // nick -> { until, reason, by, at }
const warns = loadJson(WARNS_FILE, {});         // nick -> [ { text, at, by } ]
const policy = loadJson(POLICY_FILE, {
    together: {},                               // nick -> true: нельзя «Вместе»
    mods: [],                                   // [ { id, version } ], version "*" = все
    update: null,                               // { version, changelog, critical, at } — всем
    updateTargets: {},                          // nick -> { version, changelog, critical, at } — точечно
    broadcast: null,                            // { text, at }
    commands: [],                               // очередь команд серверу
    appeals: [],                                // [ { nick, text, at } ]
    servers: [],                                // рекламные серверы на модерации/одобренные
    chats: {},                                  // nick -> [ { from, text, at } ]: переписка
    chatAssign: {},                             // nick -> админ, которому передали чат
    admins: [],                                 // доп. администраторы (кроме главного ADMIN)
    moderators: [],                             // модераторы
    maintenance: { on: false, message: '' },    // техработы
    deletedNames: {}                            // ник -> true: удалённые аккаунты (для чатов)
});
if (!policy.chats) policy.chats = {};
if (!policy.updateTargets) policy.updateTargets = {};
if (!policy.chatAssign) policy.chatAssign = {};
if (!Array.isArray(policy.admins)) policy.admins = [];
if (!Array.isArray(policy.moderators)) policy.moderators = [];
if (!policy.maintenance || typeof policy.maintenance !== 'object') policy.maintenance = { on: false, message: '' };
if (!policy.deletedNames || typeof policy.deletedNames !== 'object') policy.deletedNames = {};
if (!policy.servers) policy.servers = [];

function saveAccounts() { saveJson(ACCOUNTS_FILE, accounts); }
function saveBans() { saveJson(BANS_FILE, bans); }
function saveWarns() { saveJson(WARNS_FILE, warns); }
function savePolicy() { saveJson(POLICY_FILE, policy); }

// ---- пароли: scrypt, без сторонних библиотек

function makeHash(password) {
    const salt = crypto.randomBytes(16).toString('hex');
    const hash = crypto.scryptSync(String(password), salt, 32).toString('hex');
    return { salt, hash };
}

function checkPassword(password, record) {
    if (!record || !record.salt || !record.hash) return false;
    const hash = crypto.scryptSync(String(password), record.salt, 32).toString('hex');
    // сравнение постоянного времени, чтобы по скорости ответа не угадывали пароль
    const a = Buffer.from(hash, 'hex');
    const b = Buffer.from(record.hash, 'hex');
    return a.length === b.length && crypto.timingSafeEqual(a, b);
}

/** Подходит ли пароль как основной ИЛИ как временный (выданный админом). */
function checkAnyPassword(password, record) {
    if (checkPassword(password, record)) return true;
    if (record && record.temp && checkPassword(password, record.temp)) return true;
    return false;
}

/** Короткий человекочитаемый временный пароль. */
function makeTempPass() {
    const abc = 'abcdefghjkmnpqrstuvwxyz23456789';
    let s = '';
    for (let i = 0; i < 8; i++) s += abc[crypto.randomBytes(1)[0] % abc.length];
    return s;
}

function goodNick(nick) {
    return typeof nick === 'string' && /^[A-Za-z0-9_]{3,16}$/.test(nick);
}

function goodPassword(password) {
    return typeof password === 'string' && password.length >= 4 && password.length <= 100;
}

// ---- сессии: токен живёт в памяти, переживать перезапуск ему незачем

const sessions = new Map();                     // token -> { nick, exp }
const SESSION_LIFE = 12 * 60 * 60 * 1000;

function openSession(nick, device) {
    const tok = token();
    sessions.set(tok, { nick, exp: now() + SESSION_LIFE, device: device && device.id ? String(device.id) : null });
    // привязываем токен к устройству аккаунта, чтобы его можно было отвязать
    if (device && device.id) {
        const a = accounts[nick];
        if (a) {
            if (!a.devices || typeof a.devices !== 'object') a.devices = {};
            const id = clean(String(device.id), 64);
            // прежний токен этого устройства больше не нужен
            const old = a.devices[id];
            if (old && old.token) sessions.delete(old.token);
            a.devices[id] = { name: clean(device.name || 'Устройство', 64), at: now(), token: tok };
            saveAccounts();
        }
    }
    return tok;
}

/** Переносим токены устройств из файла аккаунтов в память — чтобы вход
 *  переживал перезапуск сервера, а отвязка устройства реально выкидывала. */
function rehydrateSessions() {
    for (const nick of Object.keys(accounts)) {
        const a = accounts[nick];
        if (!a || !a.devices) continue;
        for (const id of Object.keys(a.devices)) {
            const d = a.devices[id];
            if (d && d.token) sessions.set(d.token, { nick, exp: now() + SESSION_LIFE, device: id });
        }
    }
}
rehydrateSessions();

function sessionNick(tok) {
    const s = sessions.get(tok);
    if (!s) return null;
    if (now() > s.exp) { sessions.delete(tok); return null; }
    return s.nick;
}

setInterval(() => {
    for (const [tok, s] of sessions) if (now() > s.exp) sessions.delete(tok);
}, 60 * 1000);

// на старте можно завести админа переменной окружения, чтобы ник не занял чужой
if (process.env.NARENET_ADMIN_PASSWORD && !accounts[ADMIN]) {
    const made = makeHash(process.env.NARENET_ADMIN_PASSWORD);
    accounts[ADMIN] = { salt: made.salt, hash: made.hash, created: now() };
    saveAccounts();
    log('админ', ADMIN, 'заведён из NARENET_ADMIN_PASSWORD');
}

// ---- баны

/** Активен ли бан сейчас. Просроченный снимаем на месте. */
function banOf(nick) {
    const ban = bans[nick];
    if (!ban) return null;
    if (ban.until && ban.until > 0 && now() > ban.until) {
        delete bans[nick];
        saveBans();
        return null;
    }
    return ban;
}

function banText(ban) {
    if (!ban) return '';
    if (!ban.until || ban.until <= 0) return 'навсегда';
    const left = Math.max(0, ban.until - now());
    const days = Math.floor(left / 86400000);
    const hours = Math.floor((left % 86400000) / 3600000);
    const mins = Math.floor((left % 3600000) / 60000);
    if (days > 0) return 'ещё ' + days + ' д ' + hours + ' ч';
    if (hours > 0) return 'ещё ' + hours + ' ч ' + mins + ' мин';
    return 'ещё ' + Math.max(1, mins) + ' мин';
}

// ---- маленькие ответы-помощники

function readJsonBody(req, done) {
    readBody(req, BODY_MAX, buffer => {
        if (!buffer) return done(null);
        try { done(JSON.parse(buffer.toString('utf8'))); }
        catch (e) { done(null); }
    });
}

function readJsonBodyLimit(req, limit, done) {
    readBody(req, limit, buffer => {
        if (!buffer) return done(null);
        try { done(JSON.parse(buffer.toString('utf8'))); }
        catch (e) { done(null); }
    });
}

function isAdmin(nick) {
    return nick === ADMIN || (policy.admins && policy.admins.indexOf(nick) >= 0);
}

function isModerator(nick) {
    return policy.moderators && policy.moderators.indexOf(nick) >= 0;
}

function isStaff(nick) {
    return isAdmin(nick) || isModerator(nick);
}

/** Доступ для персонала (админ или модератор) — модерация чатов/серверов/игроков. */
function needStaff(body, res) {
    const nick = sessionNick(body && body.token);
    if (!isStaff(nick)) {
        send(res, 403, { error: 'нужен вход администратором или модератором' });
        return false;
    }
    return true;
}

function needAdmin(body, res) {
    const nick = sessionNick(body && body.token);
    if (!isAdmin(nick)) {
        send(res, 403, { error: 'нужен вход администратором' });
        return false;
    }
    return true;
}

/** Только главный администратор (владелец) — для назначения других админов. */
function needOwner(body, res) {
    const nick = sessionNick(body && body.token);
    if (nick !== ADMIN) {
        send(res, 403, { error: 'только главный администратор' });
        return false;
    }
    return true;
}

// --------------------------------------------------------- аккаунты

function accountRegister(req, res) {
    readJsonBody(req, body => {
        if (!body) return send(res, 400, { error: 'это не JSON' });

        const nick = clean(body.nick, 16);
        if (!goodNick(nick)) return send(res, 400, { error: 'ник: 3–16 латинских букв, цифр и _' });
        if (!goodPassword(body.password)) return send(res, 400, { error: 'пароль: от 4 до 100 знаков' });
        if (accounts[nick]) return send(res, 409, { error: 'такой ник уже занят' });

        const made = makeHash(body.password);
        accounts[nick] = { salt: made.salt, hash: made.hash, created: now(),
                           coins: 0, username: nick, earned: {} };
        saveAccounts();
        log('аккаунт заведён:', nick);

        send(res, 200, { ok: true, nick, token: openSession(nick, body.device), admin: nick === ADMIN });
    });
}

function accountLogin(req, res) {
    readJsonBody(req, body => {
        if (!body) return send(res, 400, { error: 'это не JSON' });

        const nick = clean(body.nick, 16);
        const record = accounts[nick];
        if (!record || !checkAnyPassword(body.password, record))
            return send(res, 403, { error: 'ник или пароль не подходят' });

        send(res, 200, { ok: true, nick, token: openSession(nick, body.device), admin: nick === ADMIN });
    });
}

function accountPassword(req, res) {
    readJsonBody(req, body => {
        if (!body) return send(res, 400, { error: 'это не JSON' });

        const nick = clean(body.nick, 16);
        const record = accounts[nick];
        if (!record || !checkAnyPassword(body.password, record))
            return send(res, 403, { error: 'старый пароль не подходит' });
        if (!goodPassword(body.newPassword))
            return send(res, 400, { error: 'новый пароль: от 4 до 100 знаков' });

        const made = makeHash(body.newPassword);
        record.salt = made.salt; record.hash = made.hash;   // остальное (монеты, устройства, ники) сохраняем
        delete record.temp;   // сменил пароль — временный больше не нужен
        saveAccounts();
        log('пароль сменён:', nick);

        send(res, 200, { ok: true, token: openSession(nick) });
    });
}

function accountDelete(req, res) {
    readJsonBody(req, body => {
        if (!body) return send(res, 400, { error: 'это не JSON' });

        const nick = clean(body.nick, 16);
        const record = accounts[nick];
        if (!record || !checkPassword(body.password, record))
            return send(res, 403, { error: 'ник или пароль не подходят' });
        if (nick === ADMIN) return send(res, 403, { error: 'администратора удалять нельзя' });

        delete accounts[nick];
        saveAccounts();
        log('аккаунт удалён:', nick);

        send(res, 200, { ok: true });
    });
}

// --------------------------------------------------------- статус (лаунчер)

/** Аккаунт с заполненными по умолчанию новыми полями. */
function acc(nick) {
    const a = accounts[nick];
    if (!a) return null;
    if (typeof a.coins !== 'number') a.coins = 0;
    if (!a.username) a.username = nick;
    if (!a.earned) a.earned = {};
    if (!Array.isArray(a.subs) || a.subs.length === 0) a.subs = [nick];
    if (!a.devices || typeof a.devices !== 'object') a.devices = {};
    if (typeof a.status !== 'string') a.status = '';
    if (!Array.isArray(a.shots)) a.shots = [];
    return a;
}

function updateFor(nick) {
    const t = policy.updateTargets ? policy.updateTargets[nick] : null;
    const g = policy.update;
    if (t && (!g || (t.at || 0) >= (g.at || 0))) return t;
    return g;
}

function statusFor(nick, tok) {
    const ban = banOf(nick);
    const list = warns[nick] || [];
    const a = acc(nick);

    // токен есть, но он не из этого аккаунта — устройство отвязали, пора выйти
    let loggedOut = false;
    if (tok) {
        const who = sessionNick(tok);
        if (who !== nick) loggedOut = true;
    }

    return {
        nick,
        loggedOut,
        banned: !!ban,
        until: ban ? (ban.until || 0) : 0,
        banReason: ban ? (ban.reason || '') : '',
        banLeft: ban ? banText(ban) : '',
        appealOnly: !!ban,                       // в бане работает только апелляция
        blockedTogether: !!policy.together[nick],
        warnings: list,
        broadcast: policy.broadcast,
        update: updateFor(nick),
        blockedMods: policy.mods,
        coins: a ? a.coins : 0,
        username: a ? a.username : nick,
        admin: isAdmin(nick),
        moderator: isModerator(nick),
        maintenance: (policy.maintenance.on && !isStaff(nick)) ? (policy.maintenance.message || 'Идут технические работы. Зайдите позже.') : ''
    };
}

function status(req, res) {
    const q = query(req);
    const nick = clean(q.nick, 16);
    if (!nick) return send(res, 400, { error: 'не указан ник' });
    const tok = typeof q.token === 'string' ? q.token : null;
    send(res, 200, statusFor(nick, tok));
}

function appeal(req, res) {
    readJsonBody(req, body => {
        if (!body) return send(res, 400, { error: 'это не JSON' });

        const nick = clean(body.nick, 16);
        const text = clean(body.text, 500);
        if (!nick || !text) return send(res, 400, { error: 'нужен ник и текст' });

        policy.appeals.push({ nick, text, at: now() });
        if (policy.appeals.length > 500) policy.appeals.shift();
        savePolicy();
        log('апелляция от', nick);

        send(res, 200, { ok: true });
    });
}

// --------------------------------------------------------- админ-панель

function adminBan(req, res) {
    readJsonBody(req, body => {
        if (!needStaff(body, res)) return;

        const nick = clean(body.nick, 16);
        if (!nick) return send(res, 400, { error: 'не указан ник' });
        if (nick === ADMIN) return send(res, 400, { error: 'себя банить нельзя' });

        const minutes = Number(body.minutes) || 0;      // 0 = навсегда
        const until = minutes > 0 ? now() + minutes * 60000 : 0;

        bans[nick] = { until, reason: clean(body.reason, 300), by: ADMIN, at: now() };
        saveBans();
        log('бан:', nick, minutes > 0 ? ('на ' + minutes + ' мин') : 'навсегда');

        send(res, 200, { ok: true });
    });
}

function adminUnban(req, res) {
    readJsonBody(req, body => {
        if (!needStaff(body, res)) return;
        const nick = clean(body.nick, 16);
        if (bans[nick]) { delete bans[nick]; saveBans(); log('разбан:', nick); }
        send(res, 200, { ok: true });
    });
}

function adminWarn(req, res) {
    readJsonBody(req, body => {
        if (!needStaff(body, res)) return;
        const nick = clean(body.nick, 16);
        const text = clean(body.text, 300);
        if (!nick || !text) return send(res, 400, { error: 'нужен ник и текст' });

        if (!warns[nick]) warns[nick] = [];
        warns[nick].push({ text, at: now(), by: ADMIN });
        if (warns[nick].length > 50) warns[nick].shift();
        saveWarns();
        log('предупреждение', nick);

        send(res, 200, { ok: true });
    });
}

function adminBroadcast(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        const text = clean(body.text, 300);
        policy.broadcast = text ? { text, at: now() } : null;
        savePolicy();
        send(res, 200, { ok: true });
    });
}

function adminCommand(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        const text = clean(body.text, 300);
        if (!text) return send(res, 400, { error: 'пустая команда' });

        policy.commands.push({ text, at: now(), by: ADMIN });
        if (policy.commands.length > 200) policy.commands.shift();
        savePolicy();
        log('команда серверу поставлена в очередь:', text);

        send(res, 200, { ok: true });
    });
}

function adminTogether(req, res) {
    readJsonBody(req, body => {
        if (!needStaff(body, res)) return;
        const nick = clean(body.nick, 16);
        if (!nick) return send(res, 400, { error: 'не указан ник' });

        if (body.on === false) delete policy.together[nick];
        else policy.together[nick] = true;
        savePolicy();

        send(res, 200, { ok: true });
    });
}

function adminBlockMod(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        const id = clean(body.id, 80);
        const version = clean(body.version, 32) || '*';
        if (!id) return send(res, 400, { error: 'не указан мод' });

        const has = policy.mods.some(m => m.id === id && m.version === version);

        if (body.on === false) policy.mods = policy.mods.filter(m => !(m.id === id && m.version === version));
        else if (!has) policy.mods.push({ id, version });

        savePolicy();
        send(res, 200, { ok: true, mods: policy.mods });
    });
}

function adminUpdate(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        const version = clean(body.version, 32);
        if (!version) return send(res, 400, { error: 'не указана версия' });

        const rec = {
            version,
            changelog: clean(body.changelog, 4000),
            critical: body.critical === true,
            at: now()
        };

        const target = clean(body.nick, 16);
        if (target) {
            if (!policy.updateTargets) policy.updateTargets = {};
            policy.updateTargets[target] = rec;
            log('обновление для', target, rec.critical ? '(экстренное)' : '');
        } else {
            policy.update = rec;
            log('объявлено обновление', version, rec.critical ? '(экстренное)' : '');
        }
        savePolicy();
        send(res, 200, { ok: true });
    });
}

// --------------------------------------------------------- устройства

function deviceLabel(d) {
    return d && d.name ? d.name : 'Устройство';
}

/** Список устройств аккаунта (как в Telegram «Устройства»). */
function devicesList(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const a = acc(nick);
        const here = sessions.get(body.token);
        const hereId = here ? here.device : null;
        const list = Object.keys(a.devices).map(id => ({
            id,
            name: deviceLabel(a.devices[id]),
            at: a.devices[id].at || 0,
            current: id === hereId
        })).sort((x, y) => y.at - x.at);
        send(res, 200, { ok: true, devices: list });
    });
}

/** Выйти из аккаунта на этом устройстве (сбросить текущую сессию). */
function accountLogout(req, res) {
    readJsonBody(req, body => {
        const tok = body && body.token;
        const nick = sessionNick(tok);
        if (!nick) return send(res, 200, { ok: true });   // уже вышли
        const a = acc(nick);
        const sess = sessions.get(tok);
        const id = sess ? sess.device : null;
        if (id && a.devices[id]) delete a.devices[id];
        sessions.delete(tok);
        saveAccounts();
        log('выход из аккаунта', nick);
        send(res, 200, { ok: true });
    });
}

/** Выйти на всех ДРУГИХ устройствах — текущее остаётся. Так выкидывают
 *  взломщика, не теряя доступ у себя. */
function accountLogoutOthers(req, res) {
    readJsonBody(req, body => {
        const tok = body && body.token;
        const nick = sessionNick(tok);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const a = acc(nick);
        const sess = sessions.get(tok);
        const keep = sess ? sess.device : null;
        let kicked = 0;
        for (const id of Object.keys(a.devices)) {
            if (id === keep) continue;
            const d = a.devices[id];
            if (d && d.token) sessions.delete(d.token);
            delete a.devices[id];
            kicked++;
        }
        saveAccounts();
        log('выход на других устройствах', nick, '— отключено', kicked);
        send(res, 200, { ok: true, kicked });
    });
}

/** Админ сбрасывает вход игрока на ВСЕХ устройствах — лечит залипший вход. */
function adminLogout(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        const nick = clean(body.nick, 16);
        if (!nick) return send(res, 400, { error: 'не указан игрок' });
        if (!accounts[nick]) return send(res, 404, { error: 'нет такого аккаунта' });
        const a = acc(nick);
        let kicked = 0;
        for (const id of Object.keys(a.devices)) {
            const d = a.devices[id];
            if (d && d.token) sessions.delete(d.token);
            kicked++;
        }
        a.devices = {};
        // на всякий случай подчистим и повисшие сессии этого ника
        for (const [tok, se] of sessions) if (se.nick === nick) sessions.delete(tok);
        saveAccounts();
        log('админ сбросил вход у', nick, '— устройств', kicked);
        send(res, 200, { ok: true, kicked });
    });
}

/** Список всех администраторов (главный + назначенные). */
function adminNicks() {
    const list = [ADMIN];
    if (policy.admins) for (const n of policy.admins) if (list.indexOf(n) < 0) list.push(n);
    return list;
}

/** Игра вылетела — лог уходит случайному админу как сообщение от бота
 *  «Bugs | Players» в чат игрока; чат закрепляется за этим админом. */
function crashReport(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 200, { ok: true });   // не залогинен — молча пропускаем

        let text = String(body.log || '').slice(-1800);     // хвост лога — там обычно ошибка
        if (!text.trim()) text = 'Игра закрылась с ошибкой, лог пустой.';

        const list = chatList('Bugs');
        list.push({ from: 'Bugs | Players', admin: false, bot: true,
                    text: '⚠ Ошибка старта игры у ' + nick + ':\n' + text, at: now() });
        if (list.length > 200) list.shift();
        savePolicy();
        log('краш-репорт от', nick, '-> в канал Bugs');
        send(res, 200, { ok: true });
    });
}

/** Отвязать устройство: его токен перестаёт работать, и на том устройстве
 *  лаунчер при следующей проверке увидит loggedOut и выйдет из аккаунта. */
function deviceUnlink(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const a = acc(nick);
        const id = clean(body.id, 64);
        const d = a.devices[id];
        if (!d) return send(res, 404, { error: 'нет такого устройства' });
        if (d.token) sessions.delete(d.token);
        delete a.devices[id];
        saveAccounts();
        log('устройство отвязано у', nick, ':', deviceLabel(d));
        send(res, 200, { ok: true });
    });
}

// Minecraft-сервер (плагин NareServer) забирает очередь команд этим ключом
function serverCommands(req, res) {
    const q = query(req);
    if (!SERVER_KEY || q.key !== SERVER_KEY)
        return send(res, 403, { error: 'нужен верный ключ сервера' });

    const out = policy.commands.map(c => c.text);
    if (policy.commands.length > 0) { policy.commands = []; savePolicy(); }

    send(res, 200, { commands: out });
}

// ------------------------------------------------- подаккаунты (игровые ники)

/** Занят ли игровой ник кем-то, кроме указанного владельца. */
function nickOwned(playNick, exceptOwner) {
    const low = playNick.toLowerCase();
    for (const name of Object.keys(accounts)) {
        if (name.toLowerCase() === low && name !== exceptOwner) return true;
        const a = acc(name);
        for (const sub of a.subs)
            if (sub.toLowerCase() === low && name !== exceptOwner) return true;
    }
    return false;
}

function subList(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const a = acc(nick);
        send(res, 200, { ok: true, subs: a.subs, active: a.username, coins: a.coins });
    });
}

function subAdd(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });

        const sub = clean(body.nick, 16);
        if (!goodNick(sub)) return send(res, 400, { error: 'ник: 3–16 латинских букв, цифр и _' });

        const a = acc(nick);
        if (a.subs.some(x => x.toLowerCase() === sub.toLowerCase()))
            return send(res, 400, { error: 'этот ник уже ваш' });
        if (nickOwned(sub, nick))
            return send(res, 409, { error: 'этот ник занят другим игроком' });
        if (a.coins < NAME_PRICE)
            return send(res, 402, { error: 'нужно ' + NAME_PRICE + ' монет, у вас ' + a.coins });

        a.coins -= NAME_PRICE;
        a.subs.push(sub);
        a.username = sub;
        saveAccounts();
        log('подаккаунт:', nick, '+', sub, '(-' + NAME_PRICE + ')');

        send(res, 200, { ok: true, subs: a.subs, active: a.username, coins: a.coins });
    });
}

function subUse(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });

        const sub = clean(body.nick, 16);
        const a = acc(nick);
        if (!a.subs.some(x => x.toLowerCase() === sub.toLowerCase()))
            return send(res, 403, { error: 'это не ваш подаккаунт' });

        a.username = a.subs.find(x => x.toLowerCase() === sub.toLowerCase());
        saveAccounts();
        send(res, 200, { ok: true, active: a.username });
    });
}

function subRemove(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });

        const sub = clean(body.nick, 16);
        const a = acc(nick);
        if (sub.toLowerCase() === nick.toLowerCase())
            return send(res, 400, { error: 'основной ник убрать нельзя' });

        a.subs = a.subs.filter(x => x.toLowerCase() !== sub.toLowerCase());
        if (a.username.toLowerCase() === sub.toLowerCase()) a.username = nick;
        saveAccounts();
        send(res, 200, { ok: true, subs: a.subs, active: a.username });
    });
}

// --------------------------------------------------- чат по бану

function chatList(nick) {
    if (!policy.chats[nick]) policy.chats[nick] = [];
    return policy.chats[nick];
}

function banChatGet(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        send(res, 200, { ok: true, messages: chatList(nick) });
    });
}

function banChatSend(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });

        const text = clean(body.text, 500);
        if (!text) return send(res, 400, { error: 'пустое сообщение' });

        const list = chatList(nick);
        list.push({ from: nick, admin: false, text, at: now() });
        if (list.length > 200) list.shift();
        savePolicy();

        send(res, 200, { ok: true, messages: list });
    });
}

function adminChatGet(req, res) {
    readJsonBody(req, body => {
        if (!needStaff(body, res)) return;
        const nick = clean(body.nick, 16);
        send(res, 200, { ok: true, messages: chatList(nick) });
    });
}

function adminChatSend(req, res) {
    readJsonBody(req, body => {
        if (!needStaff(body, res)) return;
        const me = sessionNick(body.token);
        const nick = clean(body.nick, 16);
        const text = clean(body.text, 500);
        if (!nick || !text) return send(res, 400, { error: 'нужен игрок и текст' });

        const list = chatList(nick);
        list.push({ from: me, admin: true, text, at: now() });
        if (list.length > 200) list.shift();
        savePolicy();

        send(res, 200, { ok: true, messages: list });
    });
}

/** Список всех переписок — для вкладки «Чаты» у администратора. */
function adminChatsList(req, res) {
    readJsonBody(req, body => {
        if (!needStaff(body, res)) return;
        const out = [];
        for (const nick of Object.keys(policy.chats)) {
            const list = policy.chats[nick] || [];
            const last = list.length ? list[list.length - 1] : null;
            out.push({
                nick,
                banned: !!banOf(nick),
                count: list.length,
                last: last ? last.text : '',
                lastFrom: last ? last.from : '',
                lastAdmin: last ? !!last.admin : false,
                at: last ? last.at : 0,
                assigned: policy.chatAssign[nick] || '',
                deleted: !!policy.deletedNames[nick]
            });
        }
        out.sort((a, b) => b.at - a.at);
        send(res, 200, { ok: true, chats: out });
    });
}

/** Передать чат другому администратору. */
function adminChatAssign(req, res) {
    readJsonBody(req, body => {
        if (!needStaff(body, res)) return;
        const nick = clean(body.nick, 16);
        const to = clean(body.to, 16);
        if (!nick || !to) return send(res, 400, { error: 'нужен игрок и кому передать' });
        if (!isStaff(to)) return send(res, 400, { error: 'это не администратор и не модератор' });

        policy.chatAssign[nick] = to;
        const list = chatList(nick);
        list.push({ from: sessionNick(body.token), admin: true,
                    text: 'Чат передан администратору ' + to, at: now() });
        if (list.length > 200) list.shift();
        savePolicy();
        send(res, 200, { ok: true });
    });
}

/** Выдать или забрать монеты игроку. */
function adminCoins(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        const nick = clean(body.nick, 16);
        const amount = Math.trunc(Number(body.amount));
        if (!nick) return send(res, 400, { error: 'не указан игрок' });
        if (!accounts[nick]) return send(res, 404, { error: 'нет такого аккаунта' });
        if (!Number.isFinite(amount) || amount === 0) return send(res, 400, { error: 'сколько монет?' });

        const a = acc(nick);
        a.coins = Math.max(0, (a.coins || 0) + amount);
        saveAccounts();
        log('монеты', nick, (amount > 0 ? '+' : '') + amount, '=', a.coins);
        send(res, 200, { ok: true, coins: a.coins });
    });
}

/** Поставить игроку игровой ник (username) — с проверкой занятости. */
function adminDelete(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        const nick = clean(body.nick, 16);
        if (!nick) return send(res, 400, { error: 'не указан игрок' });
        if (nick === ADMIN) return send(res, 403, { error: 'главного администратора удалять нельзя' });
        if (!accounts[nick]) return send(res, 404, { error: 'нет такого аккаунта' });

        const a = accounts[nick];
        if (a.devices) for (const id of Object.keys(a.devices)) {
            const d = a.devices[id];
            if (d && d.token) sessions.delete(d.token);
        }
        for (const [tok, se] of sessions) if (se.nick === nick) sessions.delete(tok);

        delete accounts[nick];
        delete bans[nick];
        delete warns[nick];
        delete policy.together[nick];
        delete policy.chatAssign[nick];
        policy.deletedNames[nick] = true;   // чат оставляем, но помечаем ник как удалённый
        let k;
        while ((k = policy.admins.indexOf(nick)) >= 0) policy.admins.splice(k, 1);
        while ((k = policy.moderators.indexOf(nick)) >= 0) policy.moderators.splice(k, 1);
        policy.servers = policy.servers.filter(x => x.owner !== nick);

        saveAccounts();
        savePolicy();
        log('аккаунт удалён админом:', nick);
        send(res, 200, { ok: true });
    });
}

function adminTempPass(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        const nick = clean(body.nick, 16);
        if (!nick) return send(res, 400, { error: 'не указан игрок' });
        const record = accounts[nick];
        if (!record) return send(res, 404, { error: 'нет такого аккаунта' });

        // убрать временный пароль
        if (body.clear === true) {
            delete record.temp;
            saveAccounts();
            log('временный пароль убран у', nick);
            return send(res, 200, { ok: true, cleared: true });
        }

        // задать: свой или сгенерировать
        let pass = typeof body.password === 'string' && body.password.length >= 4 ? body.password : makeTempPass();
        const made = makeHash(pass);
        record.temp = { salt: made.salt, hash: made.hash };
        saveAccounts();
        log('выдан временный пароль игроку', nick);
        // отдаём админу открытый временный пароль, чтобы передать игроку (оригинал при этом не раскрывается)
        send(res, 200, { ok: true, password: pass });
    });
}

function adminSetPass(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        const nick = clean(body.nick, 16);
        if (!nick) return send(res, 400, { error: 'не указан игрок' });
        const record = accounts[nick];
        if (!record) return send(res, 404, { error: 'нет такого аккаунта' });
        if (!goodPassword(body.password)) return send(res, 400, { error: 'пароль: от 4 до 100 знаков' });

        const made = makeHash(body.password);
        record.salt = made.salt; record.hash = made.hash;
        delete record.temp;
        const a = acc(nick);
        for (const id of Object.keys(a.devices)) {
            const d = a.devices[id];
            if (d && d.token) sessions.delete(d.token);
        }
        a.devices = {};
        for (const [tok, se] of sessions) if (se.nick === nick) sessions.delete(tok);
        saveAccounts();
        log('админ сменил пароль у', nick);
        send(res, 200, { ok: true });
    });
}

function adminSetNick(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        const nick = clean(body.nick, 16);
        const username = clean(body.username, 16);
        if (!nick || !username) return send(res, 400, { error: 'нужен игрок и ник' });
        if (!accounts[nick]) return send(res, 404, { error: 'нет такого аккаунта' });
        if (!goodNick(username)) return send(res, 400, { error: 'ник: 3–16 латинских букв, цифр и _' });
        if (nickOwned(username, nick)) return send(res, 409, { error: 'этот ник занят другим игроком' });

        const a = acc(nick);
        a.username = username;
        if (!Array.isArray(a.subs)) a.subs = [];
        if (a.subs.indexOf(username) < 0) a.subs.push(username);
        saveAccounts();
        log('ник игрока', nick, '->', username);
        send(res, 200, { ok: true, username });
    });
}

/** Назначить/снять администратора (только владелец). */
function adminGrant(req, res, grant) {
    readJsonBody(req, body => {
        if (!needOwner(body, res)) return;
        const nick = clean(body.nick, 16);
        if (!nick) return send(res, 400, { error: 'не указан игрок' });
        if (nick === ADMIN) return send(res, 400, { error: 'это и так главный администратор' });
        if (!accounts[nick]) return send(res, 404, { error: 'нет такого аккаунта' });

        const role = clean(body.role, 16) || 'admin';   // admin | moderator
        const drop = n => { let k; while ((k = policy.admins.indexOf(n)) >= 0) policy.admins.splice(k, 1);
                            while ((k = policy.moderators.indexOf(n)) >= 0) policy.moderators.splice(k, 1); };

        drop(nick);   // роль всегда одна — сначала снимаем все
        if (grant) {
            if (role === 'moderator') policy.moderators.push(nick);
            else policy.admins.push(nick);
        }
        savePolicy();
        log(grant ? ('назначен ' + role + ':') : 'сняты права:', nick);
        send(res, 200, { ok: true, admins: policy.admins, moderators: policy.moderators });
    });
}

/** Список персонала — для выпадашки «передать админу». */
function adminStaff(req, res) {
    readJsonBody(req, body => {
        if (!needStaff(body, res)) return;
        const out = [{ nick: ADMIN, role: 'главный админ' }];
        for (const n of policy.admins) out.push({ nick: n, role: 'админ' });
        for (const n of policy.moderators) out.push({ nick: n, role: 'модератор' });
        send(res, 200, { ok: true, staff: out });
    });
}

/** Заявки на публикацию серверов — для бота Publish. */
function adminPending(req, res) {
    readJsonBody(req, body => {
        if (!needStaff(body, res)) return;
        const out = policy.servers.filter(x => x.status === 'pending')
            .map(x => ({ id: x.id, owner: x.owner, name: x.name, ip: x.ip,
                         hideIp: !!x.hideIp, version: x.version, desc: x.desc, at: x.at }));
        send(res, 200, { ok: true, pending: out });
    });
}

// ------------------------------------------------- рекламные серверы и монеты

function serversPublic(req, res) {
    const out = policy.servers
        .filter(x => x.status === 'approved')
        .map(x => ({ id: x.id, owner: x.owner, name: x.name,
                     ip: x.hideIp ? '' : x.ip, hideIp: !!x.hideIp,
                     version: x.version, loader: x.loader, desc: x.desc, reward: x.reward || COIN_REWARD }));
    send(res, 200, { servers: out });
}

function serverSubmit(req, res) {
    readJsonBody(req, body => {
        if (!body) return send(res, 400, { error: 'это не JSON' });
        const nick = sessionNick(body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });

        const name = clean(body.name, 60);
        const ip = clean(body.ip, 80);
        if (!name || !ip) return send(res, 400, { error: 'нужно название и адрес сервера' });

        // один человек не заваливает очередь десятками заявок
        const mine = policy.servers.filter(x => x.owner === nick && x.status === 'pending').length;
        if (mine >= 5) return send(res, 429, { error: 'у вас уже 5 заявок на модерации' });

        const entry = {
            id: crypto.randomBytes(5).toString('hex'),
            owner: nick,
            name, ip,
            version: clean(body.version, 32),
            loader: clean(body.loader, 16),
            desc: clean(body.desc, 300),
            hideIp: body.hideIp === true,
            reward: COIN_REWARD,
            status: 'pending',
            at: now()
        };
        policy.servers.push(entry);
        if (policy.servers.length > 500) policy.servers.shift();

        // бот Publish: кладём запрос в общий чат модерации
        const pub = chatList('Publish');
        pub.push({ from: 'Publish', admin: false, bot: true, serverId: entry.id,
                   text: '📢 Запрос на публикацию сервера\n' + name + '  (' + (entry.hideIp ? 'IP скрыт' : ip) + ')\n'
                         + (entry.version ? ('версия ' + entry.version + '\n') : '')
                         + (entry.desc ? (entry.desc + '\n') : '')
                         + 'от ' + nick, at: now() });
        if (pub.length > 200) pub.shift();

        savePolicy();
        log('заявка на сервер-рекламу от', nick, ':', name);

        send(res, 200, { ok: true, id: entry.id, status: 'pending' });
    });
}

function serversMine(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });

        const out = policy.servers.filter(x => x.owner === nick)
            .map(x => ({ id: x.id, name: x.name, ip: x.ip, hideIp: !!x.hideIp, status: x.status, reason: x.reason || '' }));
        send(res, 200, { servers: out });
    });
}

// ------------------------------------------------------------ профиль

function shotPath(id) { return path.join(SHOTS_DIR, id + '.png'); }

/** Профиль игрока: статус, статистика, список скриншотов. */
function profileGet(req, res) {
    const q = query(req);
    const nick = clean(q.nick, 16);
    const a = accounts[nick] ? acc(nick) : null;
    if (!a) return send(res, 404, { error: 'нет такого игрока' });
    send(res, 200, {
        ok: true, nick,
        status: a.status || '',
        coins: a.coins || 0,
        username: a.username || nick,
        subs: a.subs || [nick],
        since: a.created || 0,
        shots: (a.shots || []).map(x => ({ id: x.id, at: x.at })),
        admin: isAdmin(nick), moderator: isModerator(nick),
        deleted: !!policy.deletedNames[nick]
    });
}

/** Задать свой статус. */
function profileStatus(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const a = acc(nick);
        a.status = clean(body.status, 200);
        saveAccounts();
        send(res, 200, { ok: true, status: a.status });
    });
}

/** Выложить скриншот (base64 PNG). */
function profileShotAdd(req, res) {
    readJsonBodyLimit(req, SHOT_MAX, body => {
        if (!body) return send(res, 413, { error: 'слишком большой скрин (до ~1 МБ)' });
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        let data = String(body.data || '');
        const comma = data.indexOf(',');
        if (data.slice(0, 5) === 'data:' && comma > 0) data = data.slice(comma + 1);
        let buf;
        try { buf = Buffer.from(data, 'base64'); } catch (e) { return send(res, 400, { error: 'битая картинка' }); }
        if (!buf.length) return send(res, 400, { error: 'пустая картинка' });
        if (buf.length > 900 * 1024) return send(res, 413, { error: 'скрин больше 900 КБ — уменьшите' });

        const a = acc(nick);
        const id = nick.toLowerCase() + '_' + crypto.randomBytes(6).toString('hex');
        try { fs.writeFileSync(shotPath(id), buf); } catch (e) { return send(res, 500, { error: 'не сохранилось' }); }
        a.shots.push({ id, at: now() });
        // не больше 12 скринов — удаляем самые старые
        while (a.shots.length > 12) {
            const old = a.shots.shift();
            try { fs.unlinkSync(shotPath(old.id)); } catch (e) { }
        }
        saveAccounts();
        send(res, 200, { ok: true, id });
    });
}

/** Удалить свой скриншот. */
function profileShotDel(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const id = clean(body.id, 64);
        const a = acc(nick);
        const i = a.shots.findIndex(x => x.id === id);
        if (i < 0) return send(res, 404, { error: 'нет такого скрина' });
        a.shots.splice(i, 1);
        try { fs.unlinkSync(shotPath(id)); } catch (e) { }
        saveAccounts();
        send(res, 200, { ok: true });
    });
}

/** Отдать картинку скриншота (публично). */
function profileImage(req, res) {
    const q = query(req);
    const id = clean(q.id, 72);
    if (!/^[a-z0-9_]+$/i.test(id)) return send(res, 400, { error: 'плохой id' });
    const full = shotPath(id);
    let info;
    try { info = fs.statSync(full); } catch (e) { return send(res, 404, { error: 'нет картинки' }); }
    res.writeHead(200, { 'Content-Type': 'image/png', 'Content-Length': info.size, 'Cache-Control': 'public, max-age=86400' });
    fs.createReadStream(full).pipe(res);
}

/** Поиск игроков по нику/игровому нику. */
function searchPlayers(req, res) {
    const q = clean(query(req).q, 32).toLowerCase();
    if (!q) return send(res, 200, { ok: true, players: [] });
    const out = [];
    for (const n of Object.keys(accounts)) {
        const a = acc(n);
        const u = (a.username || n).toLowerCase();
        if (n.toLowerCase().indexOf(q) >= 0 || u.indexOf(q) >= 0)
            out.push({ nick: n, username: a.username || n, coins: a.coins || 0 });
        if (out.length >= 20) break;
    }
    send(res, 200, { ok: true, players: out });
}

/** Ежедневный бонус монет. */
function coinsDaily(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const a = acc(nick);
        const last = a.lastDaily || 0;
        const left = DAILY_COOLDOWN - (now() - last);
        if (left > 0)
            return send(res, 200, { ok: true, awarded: 0, coins: a.coins, nextIn: Math.ceil(left / 3600000) });
        a.coins += DAILY_BONUS;
        a.lastDaily = now();
        saveAccounts();
        log('ежедневный бонус', nick, '+' + DAILY_BONUS);
        send(res, 200, { ok: true, awarded: DAILY_BONUS, coins: a.coins, nextIn: 20 });
    });
}

/** Перевод монет другому игроку. */
function coinsSend(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const to = clean(body.to, 16);
        const amount = Math.trunc(Number(body.amount));
        if (!to) return send(res, 400, { error: 'кому перевести?' });
        if (to === nick) return send(res, 400, { error: 'себе переводить незачем' });
        if (!accounts[to]) return send(res, 404, { error: 'нет такого игрока' });
        if (!Number.isFinite(amount) || amount <= 0) return send(res, 400, { error: 'сколько монет?' });
        const a = acc(nick);
        if (a.coins < amount) return send(res, 402, { error: 'не хватает монет (у вас ' + a.coins + ')' });
        a.coins -= amount;
        acc(to).coins += amount;
        saveAccounts();
        log('перевод', nick, '->', to, amount);
        send(res, 200, { ok: true, coins: a.coins });
    });
}

/** Топ игроков по монетам. */
function coinsTop(req, res) {
    const out = Object.keys(accounts)
        .map(n => ({ nick: n, coins: acc(n).coins || 0, username: acc(n).username || n }))
        .sort((x, y) => y.coins - x.coins)
        .slice(0, 10);
    send(res, 200, { ok: true, top: out });
}

/** Режим технических работ (вход закрыт всем, кроме персонала). */
function adminMaintenance(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        policy.maintenance = { on: body.on === true, message: clean(body.message, 300) };
        savePolicy();
        log('техработы:', policy.maintenance.on ? 'ВКЛ' : 'выкл');
        send(res, 200, { ok: true, maintenance: policy.maintenance });
    });
}

/** Снять все предупреждения игрока. */
function adminClearWarns(req, res) {
    readJsonBody(req, body => {
        if (!needStaff(body, res)) return;
        const nick = clean(body.nick, 16);
        if (!nick) return send(res, 400, { error: 'не указан игрок' });
        delete warns[nick];
        saveWarns();
        log('сняты предупреждения у', nick);
        send(res, 200, { ok: true });
    });
}

function coinsEarn(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });

        const server = policy.servers.find(x => x.id === body.serverId && x.status === 'approved');
        if (!server) return send(res, 404, { error: 'это не одобренный рекламный сервер' });

        const a = acc(nick);
        const last = a.earned[server.id] || 0;
        const left = COIN_COOLDOWN - (now() - last);

        if (left > 0)
            return send(res, 200, { ok: true, awarded: 0, coins: a.coins,
                                    cooldownLeft: Math.ceil(left / 60000), ip: server.ip });

        const reward = server.reward || COIN_REWARD;
        a.coins += reward;
        a.earned[server.id] = now();
        saveAccounts();
        log('монеты:', nick, '+' + reward, 'за', server.name);

        send(res, 200, { ok: true, awarded: reward, coins: a.coins, ip: server.ip });
    });
}

function usernameChange(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });

        const username = clean(body.username, 16);
        if (!goodNick(username)) return send(res, 400, { error: 'ник: 3–16 латинских букв, цифр и _' });

        const a = acc(nick);
        if (a.username === username) return send(res, 400, { error: 'это и так ваш ник' });
        if (a.coins < NAME_PRICE)
            return send(res, 402, { error: 'нужно ' + NAME_PRICE + ' монет, у вас ' + a.coins });

        a.coins -= NAME_PRICE;
        a.username = username;
        saveAccounts();
        log('смена ника:', nick, '-> показывается как', username, '(-' + NAME_PRICE + ')');

        send(res, 200, { ok: true, username, coins: a.coins });
    });
}

// --- модерация серверов (админ)

function adminServers(req, res) {
    readJsonBody(req, body => {
        if (!needStaff(body, res)) return;
        send(res, 200, { ok: true, servers: policy.servers.slice(-200) });
    });
}

function adminServerDecide(req, res, approve) {
    readJsonBody(req, body => {
        if (!needStaff(body, res)) return;
        const server = policy.servers.find(x => x.id === body.id);
        if (!server) return send(res, 404, { error: 'нет такой заявки' });

        server.status = approve ? 'approved' : 'rejected';
        if (!approve) server.reason = clean(body.reason, 200);
        if (approve && Number(body.reward) > 0) server.reward = Number(body.reward);

        const who = sessionNick(body.token);
        const pub = chatList('Publish');
        pub.push({ from: 'Publish', admin: true, bot: true,
                   text: (approve ? '✅ Одобрено: ' : '❌ Отклонено: ') + server.name
                         + (approve ? '' : (server.reason ? (' — ' + server.reason) : '')) + '  (' + who + ')', at: now() });
        if (pub.length > 200) pub.shift();

        savePolicy();
        log('сервер', server.name, approve ? 'одобрен' : 'отклонён');

        send(res, 200, { ok: true });
    });
}

function adminState(req, res) {
    readJsonBody(req, body => {
        if (!needStaff(body, res)) return;

        // просроченные баны заодно подчистим
        for (const nick of Object.keys(bans)) banOf(nick);

        send(res, 200, {
            ok: true,
            accounts: Object.keys(accounts).map(n => {
                const a = acc(n);
                const ban = banOf(n);
                return { nick: n, admin: isAdmin(n),
                         coins: a ? a.coins : 0, username: a ? a.username : n,
                         banned: !!ban, blockedTogether: !!policy.together[n] };
            }),
            bans: Object.keys(bans).map(n => ({
                nick: n, until: bans[n].until || 0, reason: bans[n].reason || '', left: banText(bans[n])
            })),
            together: Object.keys(policy.together),
            mods: policy.mods,
            update: policy.update,
            broadcast: policy.broadcast,
            maintenance: policy.maintenance,
            appeals: policy.appeals.slice(-100),
            commands: policy.commands.slice(-50),
            stats: {
                accounts: Object.keys(accounts).length,
                bans: Object.keys(bans).length,
                admins: 1 + (policy.admins ? policy.admins.length : 0),
                online: roomList().length,
                pendingServers: policy.servers.filter(x => x.status === 'pending').length,
                approvedServers: policy.servers.filter(x => x.status === 'approved').length,
                chats: Object.keys(policy.chats).length
            }
        });
    });
}


const server = http.createServer((req, res) => {
    const at = req.url.indexOf('?');
    const route = at >= 0 ? req.url.slice(0, at) : req.url;

    res.setHeader('Access-Control-Allow-Origin', '*');

    // --- аккаунты
    if (route === '/account/register' && req.method === 'POST') return accountRegister(req, res);
    if (route === '/account/login' && req.method === 'POST') return accountLogin(req, res);
    if (route === '/account/password' && req.method === 'POST') return accountPassword(req, res);
    if (route === '/account/delete' && req.method === 'POST') return accountDelete(req, res);
    if (route === '/account/devices' && req.method === 'POST') return devicesList(req, res);
    if (route === '/account/device/unlink' && req.method === 'POST') return deviceUnlink(req, res);
    if (route === '/account/logout' && req.method === 'POST') return accountLogout(req, res);
    if (route === '/account/logout/others' && req.method === 'POST') return accountLogoutOthers(req, res);

    // --- лаунчер спрашивает про себя
    if (route === '/status') return status(req, res);
    if (route === '/appeal' && req.method === 'POST') return appeal(req, res);

    // чат по бану
    if (route === '/ban/chat' && req.method === 'POST') return banChatGet(req, res);
    if (route === '/ban/send' && req.method === 'POST') return banChatSend(req, res);
    if (route === '/admin/chat' && req.method === 'POST') return adminChatGet(req, res);
    if (route === '/admin/chat/send' && req.method === 'POST') return adminChatSend(req, res);
    if (route === '/admin/chats' && req.method === 'POST') return adminChatsList(req, res);
    if (route === '/admin/chat/assign' && req.method === 'POST') return adminChatAssign(req, res);
    if (route === '/admin/grant' && req.method === 'POST') return adminGrant(req, res, true);
    if (route === '/admin/revoke' && req.method === 'POST') return adminGrant(req, res, false);
    if (route === '/admin/staff' && req.method === 'POST') return adminStaff(req, res);
    if (route === '/admin/pending' && req.method === 'POST') return adminPending(req, res);
    if (route === '/admin/coins' && req.method === 'POST') return adminCoins(req, res);
    if (route === '/admin/setnick' && req.method === 'POST') return adminSetNick(req, res);
    if (route === '/admin/setpass' && req.method === 'POST') return adminSetPass(req, res);
    if (route === '/admin/temppass' && req.method === 'POST') return adminTempPass(req, res);
    if (route === '/admin/delete' && req.method === 'POST') return adminDelete(req, res);
    if (route === '/admin/logout' && req.method === 'POST') return adminLogout(req, res);
    if (route === '/crash' && req.method === 'POST') return crashReport(req, res);

    // подаккаунты
    if (route === '/subaccount/list' && req.method === 'POST') return subList(req, res);
    if (route === '/subaccount/add' && req.method === 'POST') return subAdd(req, res);
    if (route === '/subaccount/use' && req.method === 'POST') return subUse(req, res);
    if (route === '/subaccount/remove' && req.method === 'POST') return subRemove(req, res);

    // --- админ-панель
    if (route === '/commands') return serverCommands(req, res);

    // реклама серверов и монеты
    if (route === '/servers') return serversPublic(req, res);
    if (route === '/servers/submit' && req.method === 'POST') return serverSubmit(req, res);
    if (route === '/servers/mine' && req.method === 'POST') return serversMine(req, res);
    if (route === '/coins/earn' && req.method === 'POST') return coinsEarn(req, res);
    if (route === '/coins/daily' && req.method === 'POST') return coinsDaily(req, res);
    if (route === '/coins/send' && req.method === 'POST') return coinsSend(req, res);
    if (route === '/top') return coinsTop(req, res);
    if (route === '/profile/get') return profileGet(req, res);
    if (route === '/profile/status' && req.method === 'POST') return profileStatus(req, res);
    if (route === '/profile/shot/add' && req.method === 'POST') return profileShotAdd(req, res);
    if (route === '/profile/shot/del' && req.method === 'POST') return profileShotDel(req, res);
    if (route === '/profile/image') return profileImage(req, res);
    if (route === '/search') return searchPlayers(req, res);
    if (route === '/admin/maintenance' && req.method === 'POST') return adminMaintenance(req, res);
    if (route === '/admin/warns/clear' && req.method === 'POST') return adminClearWarns(req, res);
    if (route === '/username/change' && req.method === 'POST') return usernameChange(req, res);
    if (route === '/admin/servers' && req.method === 'POST') return adminServers(req, res);
    if (route === '/admin/server/approve' && req.method === 'POST') return adminServerDecide(req, res, true);
    if (route === '/admin/server/reject' && req.method === 'POST') return adminServerDecide(req, res, false);
    if (route === '/admin/state' && req.method === 'POST') return adminState(req, res);
    if (route === '/admin/ban' && req.method === 'POST') return adminBan(req, res);
    if (route === '/admin/unban' && req.method === 'POST') return adminUnban(req, res);
    if (route === '/admin/warn' && req.method === 'POST') return adminWarn(req, res);
    if (route === '/admin/broadcast' && req.method === 'POST') return adminBroadcast(req, res);
    if (route === '/admin/command' && req.method === 'POST') return adminCommand(req, res);
    if (route === '/admin/together' && req.method === 'POST') return adminTogether(req, res);
    if (route === '/admin/blockmod' && req.method === 'POST') return adminBlockMod(req, res);
    if (route === '/admin/update' && req.method === 'POST') return adminUpdate(req, res);

    if (route === '/ping') {
        return send(res, 200, {
            ok: true, name: 'NareNet', version: VERSION,
            rooms: roomList().length, time: now()
        });
    }

    if (route === '/self') return send(res, 200, { ip: plainIp(req.socket) });
    if (route === '/rooms') return send(res, 200, { rooms: roomList() });

    if (route === '/announce' && req.method === 'POST') return announce(req, res);
    if (route === '/bye' && req.method === 'POST') return bye(req, res);

    if (route.startsWith('/file/')) {
        const sha1 = route.slice(6);
        if (req.method === 'GET') return fileGet(req, res, sha1, false);
        if (req.method === 'HEAD') return fileGet(req, res, sha1, true);
        if (req.method === 'PUT' || req.method === 'POST') return filePut(req, res, sha1);
        return send(res, 405, { error: 'не тот метод' });
    }

    send(res, 404, { error: 'нет такого адреса', see: ['/ping', '/rooms', '/announce', '/bye', '/file/<sha1>'] });
});

// ------------------------------------------------------------------ туннель

/** Занятые публичные порты: порт -> туннель */
const ports = new Map();

/** Соединения гостей, которые ждут встречного от хозяина: id -> {socket, timer} */
const waiting = new Map();

function freePort() {
    for (let port = TUNNEL_FROM; port <= TUNNEL_TO; port++)
        if (!ports.has(port)) return port;
    return 0;
}

function makeTunnel(control, room) {
    const port = freePort();
    if (!port) {
        control.write(JSON.stringify({ error: 'свободных портов нет' }) + '\n');
        control.destroy();
        return null;
    }

    const tunnel = { port: port, control: control, room: room, listener: null, alive: true };

    tunnel.listener = net.createServer(guest => {
        const id = crypto.randomBytes(8).toString('hex');
        guest.pause();

        const timer = setTimeout(() => {
            if (!waiting.has(id)) return;
            waiting.delete(id);
            log('хозяин не открыл канал для гостя', room.nick);
            guest.destroy();
        }, OPEN_WAIT);

        waiting.set(id, { socket: guest, timer: timer });

        try { control.write(JSON.stringify({ open: id }) + '\n'); }
        catch (e) {
            clearTimeout(timer);
            waiting.delete(id);
            guest.destroy();
        }
    });

    tunnel.listener.on('error', e => log('порт', port, 'не слушается:', e.message));

    tunnel.close = why => {
        if (!tunnel.alive) return;
        tunnel.alive = false;

        ports.delete(port);
        try { tunnel.listener.close(); } catch (e) { }
        try { control.destroy(); } catch (e) { }

        if (room) { room.tunnel = null; room.publicPort = 0; room.mode = 'direct'; }
        log('туннель закрыт:', port, why || '');
    };

    tunnel.listener.listen(port, HOST, () => {
        ports.set(port, tunnel);
        log('туннель открыт:', room ? room.nick : '?', '-> порт', port);
        control.write(JSON.stringify({ port: port, ok: true }) + '\n');
    });

    return tunnel;
}

/** Первая строка соединения говорит, кто пришёл. */
function tunnelConnection(socket, first) {
    const text = first.toString('utf8');
    const line = text.slice(0, text.indexOf('\n')).trim();
    const rest = first.slice(Buffer.byteLength(line, 'utf8') + 1);

    const parts = line.split(/\s+/);          // NARE1 HOST <token> | NARE1 DATA <id>
    const kind = (parts[1] || '').toUpperCase();
    const key = parts[2] || '';

    if (kind === 'HOST') {
        const room = rooms.get(key);
        if (!room) {
            socket.write(JSON.stringify({ error: 'комната не найдена — сначала объявитесь' }) + '\n');
            return socket.destroy();
        }

        // Токен — секрет на 128 бит, его и достаточно. Адрес сверяем, но не
        // отказываем: объявление идёт по HTTP, туннель — отдельным
        // соединением, и они могут выйти разными путями (IPv4 и IPv6).
        const from = plainIp(socket);
        if (room.ip !== from) log('адрес хозяина не совпал:', room.ip, '!=', from, '— пускаю по токену');

        if (room.tunnel) room.tunnel.close('хозяин подключился заново');

        const tunnel = makeTunnel(socket, room);
        if (!tunnel) return;

        room.tunnel = tunnel;
        room.publicPort = tunnel.port;
        room.mode = 'tunnel';

        let buffer = rest.toString('utf8');
        socket.setEncoding('utf8');
        socket.on('data', chunk => {
            buffer += chunk;
            if (buffer.length > 64 * 1024) buffer = '';

            let at;
            while ((at = buffer.indexOf('\n')) >= 0) {
                const one = buffer.slice(0, at).trim();
                buffer = buffer.slice(at + 1);
                if (!one) continue;

                try {
                    const message = JSON.parse(one);
                    if (message.ping) socket.write(JSON.stringify({ pong: 1 }) + '\n');
                } catch (e) { }
            }
        });

        socket.on('close', () => tunnel.close('связь с хозяином потеряна'));
        socket.on('error', () => tunnel.close('ошибка связи с хозяином'));
        return;
    }

    if (kind === 'DATA') {
        const guest = waiting.get(key);
        if (!guest) {
            socket.write(JSON.stringify({ error: 'этого гостя уже нет' }) + '\n');
            return socket.destroy();
        }

        waiting.delete(key);
        clearTimeout(guest.timer);

        if (rest.length > 0) guest.socket.write(rest);

        socket.pipe(guest.socket);
        guest.socket.pipe(socket);
        guest.socket.resume();

        const bye1 = () => { try { socket.destroy(); } catch (e) { } };
        const bye2 = () => { try { guest.socket.destroy(); } catch (e) { } };

        guest.socket.on('close', bye1);
        guest.socket.on('error', bye1);
        socket.on('close', bye2);
        socket.on('error', bye2);
        return;
    }

    socket.write(JSON.stringify({ error: 'непонятное приветствие' }) + '\n');
    socket.destroy();
}

// Один порт на всё: смотрим начало соединения и решаем, куда его отдать.
const front = net.createServer(socket => {
    socket.setNoDelay(true);
    socket.once('data', first => {
        if (first.length >= 5 && first.slice(0, 5).toString('utf8') === 'NARE1') {
            const text = first.toString('utf8');
            if (text.indexOf('\n') < 0) {
                // приветствие пришло по частям — дожидаемся строки
                let all = first;
                const more = chunk => {
                    all = Buffer.concat([all, chunk]);
                    if (all.length > 4096) return socket.destroy();
                    if (all.toString('utf8').indexOf('\n') < 0) return;

                    socket.removeListener('data', more);
                    tunnelConnection(socket, all);
                };
                socket.on('data', more);
                return;
            }
            return tunnelConnection(socket, first);
        }

        socket.unshift(first);
        server.emit('connection', socket);
    });

    socket.on('error', () => { });
});

front.listen(PORT, HOST, () => {
    log('NareNet', VERSION, 'слушает', HOST + ':' + PORT);
    log('туннельные порты:', TUNNEL_FROM + '–' + TUNNEL_TO);
    log('файлы модов:', FILES_DIR);
});

process.on('uncaughtException', e => log('ошибка:', e && e.stack ? e.stack : e));
process.on('SIGTERM', () => { log('останавливаюсь'); process.exit(0); });
