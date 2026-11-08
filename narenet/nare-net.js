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

const TUNNEL_FROM = Number(process.env.NARENET_TUNNEL_FROM || 30000);
const TUNNEL_TO = Number(process.env.NARENET_TUNNEL_TO || 30060);

const ROOM_LIFE = 60 * 1000;          // без обновления комната пропадает
const ANNOUNCE_MIN = 3 * 1000;        // чаще чем раз в 3 секунды не слушаем
const FILE_MAX = 32 * 1024 * 1024;    // один файл
const FILES_MAX = 3 * 1024 * 1024 * 1024;
const BODY_MAX = 512 * 1024;          // json объявления
const ROOMS_MAX = 200;
const OPEN_WAIT = 8 * 1000;           // сколько ждём встречное соединение хозяина

const VERSION = '1.3';

// ---------------------------------------------------------------- хозяйство

fs.mkdirSync(FILES_DIR, { recursive: true });

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
    update: null,                               // { version, changelog, critical, at }
    broadcast: null,                            // { text, at }
    commands: [],                               // очередь команд серверу
    appeals: []                                 // [ { nick, text, at } ]
});

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

function goodNick(nick) {
    return typeof nick === 'string' && /^[A-Za-z0-9_]{3,16}$/.test(nick);
}

function goodPassword(password) {
    return typeof password === 'string' && password.length >= 4 && password.length <= 100;
}

// ---- сессии: токен живёт в памяти, переживать перезапуск ему незачем

const sessions = new Map();                     // token -> { nick, exp }
const SESSION_LIFE = 12 * 60 * 60 * 1000;

function openSession(nick) {
    const tok = token();
    sessions.set(tok, { nick, exp: now() + SESSION_LIFE });
    return tok;
}

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

function needAdmin(body, res) {
    const nick = sessionNick(body && body.token);
    if (nick !== ADMIN) {
        send(res, 403, { error: 'нужен вход администратором' });
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
        accounts[nick] = { salt: made.salt, hash: made.hash, created: now() };
        saveAccounts();
        log('аккаунт заведён:', nick);

        send(res, 200, { ok: true, nick, token: openSession(nick), admin: nick === ADMIN });
    });
}

function accountLogin(req, res) {
    readJsonBody(req, body => {
        if (!body) return send(res, 400, { error: 'это не JSON' });

        const nick = clean(body.nick, 16);
        const record = accounts[nick];
        if (!record || !checkPassword(body.password, record))
            return send(res, 403, { error: 'ник или пароль не подходят' });

        send(res, 200, { ok: true, nick, token: openSession(nick), admin: nick === ADMIN });
    });
}

function accountPassword(req, res) {
    readJsonBody(req, body => {
        if (!body) return send(res, 400, { error: 'это не JSON' });

        const nick = clean(body.nick, 16);
        const record = accounts[nick];
        if (!record || !checkPassword(body.password, record))
            return send(res, 403, { error: 'старый пароль не подходит' });
        if (!goodPassword(body.newPassword))
            return send(res, 400, { error: 'новый пароль: от 4 до 100 знаков' });

        const made = makeHash(body.newPassword);
        accounts[nick] = { salt: made.salt, hash: made.hash, created: record.created };
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

function statusFor(nick) {
    const ban = banOf(nick);
    const list = warns[nick] || [];

    return {
        nick,
        banned: !!ban,
        until: ban ? (ban.until || 0) : 0,
        banReason: ban ? (ban.reason || '') : '',
        banLeft: ban ? banText(ban) : '',
        appealOnly: !!ban,                       // в бане работает только апелляция
        blockedTogether: !!policy.together[nick],
        warnings: list,
        broadcast: policy.broadcast,
        update: policy.update,
        blockedMods: policy.mods
    };
}

function status(req, res) {
    const q = query(req);
    const nick = clean(q.nick, 16);
    if (!nick) return send(res, 400, { error: 'не указан ник' });
    send(res, 200, statusFor(nick));
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
        if (!needAdmin(body, res)) return;

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
        if (!needAdmin(body, res)) return;
        const nick = clean(body.nick, 16);
        if (bans[nick]) { delete bans[nick]; saveBans(); log('разбан:', nick); }
        send(res, 200, { ok: true });
    });
}

function adminWarn(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
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
        if (!needAdmin(body, res)) return;
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

        policy.update = {
            version,
            changelog: clean(body.changelog, 4000),
            critical: body.critical === true,
            at: now()
        };
        savePolicy();
        log('объявлено обновление', version, policy.update.critical ? '(экстренное)' : '');

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

function adminState(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;

        // просроченные баны заодно подчистим
        for (const nick of Object.keys(bans)) banOf(nick);

        send(res, 200, {
            ok: true,
            accounts: Object.keys(accounts).map(n => ({ nick: n, admin: n === ADMIN })),
            bans: Object.keys(bans).map(n => ({
                nick: n, until: bans[n].until || 0, reason: bans[n].reason || '', left: banText(bans[n])
            })),
            together: Object.keys(policy.together),
            mods: policy.mods,
            update: policy.update,
            broadcast: policy.broadcast,
            appeals: policy.appeals.slice(-100),
            commands: policy.commands.slice(-50)
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

    // --- лаунчер спрашивает про себя
    if (route === '/status') return status(req, res);
    if (route === '/appeal' && req.method === 'POST') return appeal(req, res);

    // --- админ-панель
    if (route === '/commands') return serverCommands(req, res);
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
