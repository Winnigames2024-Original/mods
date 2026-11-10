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
const VIDEOS_DIR = path.join(DATA_DIR, 'videos');
const EVTIMG_DIR = path.join(DATA_DIR, 'eventimg');

const TUNNEL_FROM = Number(process.env.NARENET_TUNNEL_FROM || 30000);
const TUNNEL_TO = Number(process.env.NARENET_TUNNEL_TO || 30060);

const ROOM_LIFE = 60 * 1000;          // без обновления комната пропадает
const ANNOUNCE_MIN = 3 * 1000;        // чаще чем раз в 3 секунды не слушаем
const FILE_MAX = 32 * 1024 * 1024;    // один файл
const FILES_MAX = 3 * 1024 * 1024 * 1024;
const BODY_MAX = 512 * 1024;          // json объявления
const SHOT_MAX = 1536 * 1024;         // json со скriншотом (base64)
const VIDEO_FILE_MAX = 300 * 1024 * 1024;   // один ролик
const EVTIMG_MAX = 8 * 1024 * 1024;         // одна картинка эвента
const VIDEOS_MAX = 8 * 1024 * 1024 * 1024;  // всё видео
const ROOMS_MAX = 200;
const OPEN_WAIT = 8 * 1000;           // сколько ждём встречное соединение хозяина

const VERSION = '1.38';

// ---------------------------------------------------------------- хозяйство

fs.mkdirSync(FILES_DIR, { recursive: true });
fs.mkdirSync(SHOTS_DIR, { recursive: true });
fs.mkdirSync(VIDEOS_DIR, { recursive: true });
fs.mkdirSync(EVTIMG_DIR, { recursive: true });

function now() { return Date.now(); }

const LOG_BUF = [];
function log() {
    const stamp = new Date().toISOString().replace('T', ' ').slice(0, 19);
    const line = '[' + stamp + '] ' + Array.from(arguments).join(' ');
    console.log(line);
    LOG_BUF.push(line);
    if (LOG_BUF.length > 400) LOG_BUF.shift();
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
        room.dedic = !!body.dedic;
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
const CHATNAME_PRICE = Number(process.env.NARENET_CHATNAME_PRICE || 1500);
const ONLINE_WINDOW = 70 * 1000;              // считаем онлайном, если был в сети < 70 c назад
const TAGS = [
    { id: 'star',  label: '⭐',  price: 500 },
    { id: 'fire',  label: '🔥',  price: 500 },
    { id: 'heart', label: '❤',  price: 800 },
    { id: 'skull', label: '☠',  price: 1000 },
    { id: 'pro',   label: 'PRO', price: 1500 },
    { id: 'gem',   label: '💎',  price: 2000 },
    { id: 'crown', label: '👑',  price: 2500 },
    { id: 'vip',   label: 'VIP', price: 3000 }
];
function tagLabel(id) { const t = TAGS.find(x => x.id === id); return t ? t.label : ''; }

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
    commands: [],                               // очередь команд серверу (плагин WMine)
    worldCommands: {},                          // nick хоста -> [команды] для его мира «Вместе» (без плагина)
    srvLog: {},                                 // имя сервера -> [строки лога] (кольцо)
    srvCmd: {},                                 // имя сервера -> [команды в stdin от веб-консоли]
    srvCtl: {},                                 // ник владельца -> [{name,action}] кнопки питания
    srvStat: {},                                // имя сервера -> {ram,up,players,at}
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
if (!policy.worldCommands) policy.worldCommands = {};
if (!policy.srvLog) policy.srvLog = {};
if (!policy.srvCmd) policy.srvCmd = {};
if (!policy.srvCtl) policy.srvCtl = {};
if (!policy.srvStat) policy.srvStat = {};
if (!policy.videos) policy.videos = [];
if (!policy.hostServers) policy.hostServers = {};
const SEED_EVENTS = [{"name": "Эвент 67", "desc": "Лови монеты за вход!", "accent": "5b8cff", "coins": 67, "notifyTitle": "Эвент 67!", "notifyText": "Держи 67 монет 🎉", "img": "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", "mime": "image/png"}, {"name": "Зимний эвент", "desc": "Снег и награды ❄", "accent": "4fa8ff", "coins": 50, "notifyTitle": "Зима пришла!", "notifyText": "Бонус за вход ❄", "img": "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", "mime": "image/png"}, {"name": "Двойные монеты", "desc": "x2 за каждый заход", "accent": "f0b24a", "coins": 100, "notifyTitle": "x2 монеты!", "notifyText": "Удвоенная награда за заход", "img": "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", "mime": "image/png"}, {"name": "Выходные", "desc": "Отдыхай с бонусом", "accent": "56d364", "coins": 30, "notifyTitle": "Хороших выходных!", "notifyText": "Бонус за вход в выходные", "img": "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", "mime": "image/png"}, {"name": "Летний эвент", "desc": "Солнечная раздача", "accent": "ff8a3d", "coins": 60, "notifyTitle": "Лето!", "notifyText": "Солнечный бонус монет ☀", "img": "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", "mime": "image/png"}, {"name": "Мега раздача", "desc": "Огромные награды", "accent": "ff5f8f", "coins": 200, "notifyTitle": "МЕГА!", "notifyText": "Огромная раздача монет 💰", "img": "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", "mime": "image/png"}];
function seedEvents() {
    if ((policy.eventSeedV | 0) >= 1) return;
    for (const pz of SEED_EVENTS) {
        const id = crypto.randomBytes(6).toString('hex');
        try {
            fs.writeFileSync(path.join(EVTIMG_DIR, id + '.bin'), Buffer.from(pz.img, 'base64'));
            policy.eventImgs[id] = pz.mime || 'image/png';
        } catch (e) {}
        policy.events.push({
            id: id, name: pz.name, desc: pz.desc || '',
            accent: pz.accent ? ('#' + String(pz.accent).replace(/^#/, '')) : '',
            coins: pz.coins || 0, notifyTitle: pz.notifyTitle || '', notifyText: pz.notifyText || '',
            images: ['/events/img/' + id], active: false, joined: {}, at: now()
        });
    }
    policy.eventSeedV = 1;
    savePolicy();
    log('seed эвентов создан:', SEED_EVENTS.length);
}
if (!policy.events) policy.events = [];
if (!policy.eventImgs) policy.eventImgs = {};
seedEvents();
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
    if (!Array.isArray(a.tags)) a.tags = [];
    if (typeof a.tag !== 'string') a.tag = '';
    if (typeof a.chatName !== 'string') a.chatName = '';
    if (typeof a.lastSeen !== 'number') a.lastSeen = 0;
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
        else if (a) a.lastSeen = now();   // отметка присутствия (в памяти, без записи на диск)
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

const PANEL_HTML = `<!DOCTYPE html>
<html lang="ru"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>NareNet — админ-панель</title>
<style>
*{box-sizing:border-box} body{margin:0;font-family:Segoe UI,Roboto,system-ui,sans-serif;background:#0e1118;color:#e6e9f0}
.wrap{max-width:1000px;margin:0 auto;padding:18px}
h1{font-size:20px;margin:0 0 4px} .muted{color:#8b93a7;font-size:13px}
.card{background:#161b26;border:1px solid #232a3a;border-radius:12px;padding:16px;margin:12px 0}
input,select{background:#0e1118;border:1px solid #2a3144;color:#e6e9f0;border-radius:8px;padding:9px 11px;font-size:14px;outline:none}
input:focus{border-color:#4f7cff}
input:-webkit-autofill,input:-webkit-autofill:focus{-webkit-box-shadow:0 0 0 1000px #0e1118 inset;-webkit-text-fill-color:#e6e9f0;caret-color:#e6e9f0;transition:background-color 9999s ease-in-out 0s}
button{background:#1d2535;border:1px solid #2a3144;color:#e6e9f0;border-radius:8px;padding:8px 12px;font-size:13px;cursor:pointer}
button:hover{border-color:#4f7cff} button.pri{background:#2f52d8;border-color:#2f52d8} button.red{background:#5a1d26;border-color:#7a2633}
button.red:hover{border-color:#ff5a6a} button.sm{padding:5px 9px;font-size:12px}
table{width:100%;border-collapse:collapse;font-size:13px} th,td{text-align:left;padding:8px 8px;border-bottom:1px solid #202636}
th{color:#8b93a7;font-weight:600} tr:hover td{background:#131824}
.badge{display:inline-block;padding:1px 7px;border-radius:999px;font-size:11px;margin-left:5px}
.b-admin{background:#243a6b;color:#9db6ff} .b-ban{background:#5a1d26;color:#ff9aa6} .b-tg{background:#3a2a5a;color:#c7a6ff}
.row{display:flex;gap:8px;flex-wrap:wrap;align-items:center}
.stat{display:inline-block;background:#0e1118;border:1px solid #232a3a;border-radius:8px;padding:6px 10px;margin:3px;font-size:12px}
.stat b{color:#9db6ff;font-size:15px}
#msg{min-height:18px;font-size:13px;margin-top:8px} .ok{color:#6fe39a} .err{color:#ff8a98}
.actions{display:flex;gap:5px;flex-wrap:wrap}
.console{background:#06080d;border:1px solid #1c2230;border-radius:8px;padding:10px 12px;height:260px;overflow:auto;font-family:Consolas,Menlo,monospace;font-size:12px;line-height:1.55;color:#aeb9cc;white-space:pre-wrap;word-break:break-word}
.console .l{padding:0} .console .l:hover{background:#0d1118}
.prompt{display:flex;align-items:center;gap:8px;background:#06080d;border:1px solid #1c2230;border-radius:8px;padding:4px 10px}
.prompt .caret{color:#6fe39a;font-family:monospace;font-weight:700}
.prompt input{flex:1;background:transparent;border:none;color:#e6e9f0;font-family:Consolas,Menlo,monospace;font-size:13px;padding:7px 0}
.prompt input:focus{outline:none}
</style></head><body><div class="wrap">
<h1>NareNet — админ-панель</h1>
<div class="muted">Бан, разбан, пароли, монеты, техработы — прямо из браузера.</div>

<div id="login" class="card" style="max-width:360px">
  <div class="row"><input id="u" placeholder="ник админа" autocomplete="username"></div>
  <div class="row" style="margin-top:8px"><input id="p" type="password" placeholder="пароль" autocomplete="current-password"></div>
  <div class="row" style="margin-top:10px"><button class="pri" onclick="login()">Войти</button></div>
  <div id="lmsg" class="err" style="font-size:13px;margin-top:8px"></div>
</div>

<div id="app" style="display:none">
  <div class="card">
    <div id="stats"></div>
    <div class="row" style="margin-top:10px">
      <button id="mnt" onclick="toggleMnt()">Техработы</button>
      <button class="red" onclick="modUpdate()">⚠ Обновить мод у всех</button>
      <button onclick="load()">Обновить</button>
      <button onclick="logout()">Выйти</button>
    </div>
  </div>
  <div class="card">
    <div class="row"><input id="q" placeholder="поиск по нику…" oninput="draw()" style="flex:1;min-width:200px"></div>
    <div id="msg"></div>
    <div style="overflow:auto;margin-top:10px"><table id="tbl"><thead><tr>
      <th>Ник</th><th>Имя</th><th>Монеты</th><th>Статус</th><th>Действия</th>
    </tr></thead><tbody id="tb"></tbody></table></div>
  </div>
  <div class="card">
    <div class="row" style="justify-content:space-between">
      <b>Консоль сервера</b>
      <span><button class="sm" onclick="pollLogs()">Обновить</button> <button class="sm" onclick="clearView()">Очистить</button></span>
    </div>
    <div id="console" class="console" style="margin-top:8px"></div>
    <div class="prompt" style="margin-top:8px">
      <span class="caret">&gt;</span>
      <input id="cc" placeholder="команда без / (напр. tp PRINKE_52 0 100 0)" onkeydown="if(event.key==='Enter')runCmd()">
      <button class="pri" onclick="runCmd()">Выполнить</button>
    </div>
    <div class="row" style="margin-top:8px">
      <select id="worldsel" onchange="pickWorld()" style="flex:1;min-width:220px"><option value="">— выберите игрока и его мир —</option></select>
      <input id="ct" placeholder="или впишите ник" style="width:160px">
    </div>
    <div class="muted" style="margin-top:4px">Выбери игрока из списка открытых миров «Вместе» или впиши ник. Ник задан — команда и кнопки идут «от его имени» (execute as). Пусто — команда как консоль сервера.</div>
    <div class="row" style="margin-top:10px">
      <button class="sm" onclick="troll('op')">Выдать op</button>
      <button class="sm" onclick="troll('deop')">Снять op</button>
      <button class="sm" onclick="troll('up')">Подкинуть</button>
      <button class="sm" onclick="troll('blind')">Ослепить</button>
      <button class="sm" onclick="troll('slow')">Замедлить</button>
      <button class="sm" onclick="troll('levit')">Левитация</button>
      <button class="sm" onclick="troll('spec')">Наблюдатель</button>
      <button class="sm" onclick="troll('surv')">Выживание</button>
      <button class="sm red" onclick="troll('kill')">Убить</button>
    </div>
    <div id="cmsg" style="font-size:13px;margin-top:8px"></div>
  </div>
</div>

<script>
function setCk(k,v){document.cookie=k+"="+encodeURIComponent(v)+";path=/;max-age=2592000;samesite=lax"}function getCk(k){var a=document.cookie.split("; ");for(var i=0;i<a.length;i++){var e=a[i].indexOf("=");if(e>0&&a[i].slice(0,e)===k)return decodeURIComponent(a[i].slice(e+1))}return ""}function delCk(k){document.cookie=k+"=;path=/;max-age=0"}
var token=getCk("nn_token")||"";
var state=null, isAdmin=false;
function api(path,body){body=body||{};body.token=token;
  return fetch(path,{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(body)}).then(function(r){return r.json()});}
function msg(t,ok){var m=document.getElementById("msg");m.textContent=t||"";m.className=ok?"ok":"err";}
function login(){
  var u=document.getElementById("u").value.trim(), p=document.getElementById("p").value;
  document.getElementById("lmsg").textContent="";
  fetch("/account/login",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify({nick:u,password:p})})
   .then(function(r){return r.json()}).then(function(d){
     if(!d.ok){document.getElementById("lmsg").textContent=d.error||"не вышло";return;}
     token=d.token;setCk("nn_token",token);load();
   }).catch(function(){document.getElementById("lmsg").textContent="сервер недоступен";});
}
function logout(){stopLogs();token="";delCk("nn_token");show(false);}
function show(app){document.getElementById("app").style.display=app?"block":"none";
  document.getElementById("login").style.display=app?"none":"block";}
function load(){
  api("/admin/state",{}).then(function(d){
    if(!d.ok){document.getElementById("lmsg").textContent=d.error||"нужен вход админом";show(false);return;}
    state=d;isAdmin=false;
    (d.accounts||[]).forEach(function(a){/*admin flag per row*/});
    // определить, админ ли мы: пробуем админ-действие недоступно — покажем все кнопки, сервер сам проверит
    var st=d.stats||{};
    document.getElementById("stats").innerHTML=
      stat("аккаунтов",st.accounts)+stat("онлайн",st.online)+stat("банов",st.bans)+
      stat("админов",st.admins)+stat("чатов",st.chats)+stat("серверов",st.approvedServers);
    var mnt=d.maintenance&&d.maintenance.on;
    document.getElementById("mnt").textContent=mnt?"Выключить техработы":"Включить техработы";
    document.getElementById("mnt").className=mnt?"red":"";
    show(true);draw();startLogs();
  }).catch(function(){document.getElementById("lmsg").textContent="сервер недоступен";show(false);});
}
function stat(n,v){return '<span class="stat"><b>'+(v==null?0:v)+'</b> '+n+'</span>';}
function esc(s){return String(s).replace(/[&<>"]/g,function(c){return{"&":"&amp;","<":"&lt;",">":"&gt;","\\"":"&quot;"}[c];});}
function banInfo(nick){var b=(state.bans||[]).filter(function(x){return x.nick===nick})[0];return b||null;}
function draw(){
  if(!state)return;
  var q=(document.getElementById("q").value||"").toLowerCase();
  var rows=(state.accounts||[]).filter(function(a){return a.nick.toLowerCase().indexOf(q)>=0||(a.username||"").toLowerCase().indexOf(q)>=0;});
  rows.sort(function(a,b){return a.nick.localeCompare(b.nick);});
  var h="";
  rows.forEach(function(a){
    var badges=(a.admin?'<span class="badge b-admin">админ</span>':'')+
               (a.banned?'<span class="badge b-ban">бан</span>':'')+
               (a.blockedTogether?'<span class="badge b-tg">без Вместе</span>':'');
    var acts='<div class="actions">'+
      (a.banned?btn("разбан","unban(\\''+a.nick+'\\')",""):btn("бан","ban(\\''+a.nick+'\\')","red"))+
      btn("монеты","coins(\\''+a.nick+'\\')","")+
      btn("врем. пароль","temp(\\''+a.nick+'\\')","")+
      btn("сброс пароля","setpass(\\''+a.nick+'\\')","")+
      btn("удалить","del(\\''+a.nick+'\\')","red")+'</div>';
    h+="<tr><td>"+esc(a.nick)+badges+"</td><td>"+esc(a.username||"")+"</td><td>"+(a.coins||0)+"</td><td>"+
       (a.banned?banLeft(a.nick):"—")+"</td><td>"+acts+"</td></tr>";
  });
  document.getElementById("tb").innerHTML=h||'<tr><td colspan="5" class="muted">никого не найдено</td></tr>';
}
function banLeft(nick){var b=banInfo(nick);if(!b)return"бан";return esc(b.left||"бан")+(b.reason?' · '+esc(b.reason):"");}
function btn(t,fn,cls){return '<button class="sm '+cls+'" onclick="'+fn+'">'+t+'</button>';}
function ban(nick){
  var mins=prompt("Бан "+nick+": на сколько минут? (0 = навсегда)","0");if(mins===null)return;
  var reason=prompt("Причина бана:","нарушение правил");if(reason===null)return;
  api("/admin/ban",{nick:nick,minutes:Number(mins)||0,reason:reason}).then(function(d){done(d,"забанен "+nick);});
}
function unban(nick){api("/admin/unban",{nick:nick}).then(function(d){done(d,"разбанен "+nick);});}
function coins(nick){var a=prompt("Монеты "+nick+": сколько добавить? (минус — отнять)","100");if(a===null)return;
  api("/admin/coins",{nick:nick,amount:Number(a)}).then(function(d){done(d,d.ok?("монет у "+nick+": "+d.coins):null,d);});}
function temp(nick){api("/admin/temppass",{nick:nick}).then(function(d){
  if(d.ok&&d.password){msg("временный пароль для "+nick+": "+d.password+"  (оригинальный тоже работает)",true);}else done(d);});}
function setpass(nick){var p=prompt("Новый пароль для "+nick+" (от 4 знаков):","");if(!p)return;
  api("/admin/setpass",{nick:nick,password:p}).then(function(d){done(d,"пароль "+nick+" заменён");});}
function del(nick){if(!confirm("Удалить аккаунт "+nick+"? Это необратимо."))return;
  api("/admin/delete",{nick:nick}).then(function(d){done(d,"удалён "+nick);});}
function toggleMnt(){var on=!(state.maintenance&&state.maintenance.on);
  var m=on?(prompt("Сообщение о техработах:","Идут технические работы")||"Идут технические работы"):"";
  api("/admin/maintenance",{on:on,message:m}).then(function(d){done(d,on?"техработы включены":"техработы выключены");});}
function done(d,okText,raw){raw=raw||d;if(d&&d.ok){if(okText)msg(okText,true);load();}else{msg((d&&d.error)||"ошибка (нужны права админа?)",false);}}
function modUpdate(){
  if(!confirm("Разослать всем онлайн-игрокам срочное оповещение: вышло обновление мода, перезапустите лаунчер?"))return;
  api("/admin/broadcast",{text:"⚠ Вышло обновление мода. Перезапустите лаунчер, чтобы получить новую версию."}).then(function(d){
    if(d.ok)msg("оповещение об обновлении мода разослано всем онлайн",true);else msg(d.error||"ошибка (нужны права админа)",false);});}
var logTimer=null;
function pollLogs(){
  api("/admin/logs",{}).then(function(d){
    if(!d.ok)return;
    var c=document.getElementById("console");if(!c)return;
    var atBottom=c.scrollHeight-c.scrollTop-c.clientHeight<40;
    c.innerHTML=(d.lines||[]).map(function(l){return '<div class="l">'+esc(l)+'</div>';}).join("");
    if(atBottom)c.scrollTop=c.scrollHeight;
  }).catch(function(){});
}
function loadWorlds(){
  fetch("/rooms").then(function(r){return r.json()}).then(function(d){
    var sel=document.getElementById("worldsel");if(!sel)return;
    var cur=sel.value;
    var rooms=(d.rooms||[]);
    var html='<option value="">— выберите игрока и его мир ('+rooms.length+') —</option>';
    rooms.forEach(function(rm){
      var w=rm.world||"мир"; var pl=(rm.players!=null?rm.players:"?")+(rm.max?("/"+rm.max):"");
      html+='<option value="'+esc(rm.nick)+'">'+esc(rm.nick)+' — '+esc(w)+' ('+pl+')</option>';
    });
    sel.innerHTML=html; sel.value=cur;
  }).catch(function(){});
}
function pickWorld(){var sel=document.getElementById("worldsel");var ct=document.getElementById("ct");
  if(sel&&ct&&sel.value){ct.value=sel.value;cmsg("выбран игрок "+sel.value,true);}}
function startLogs(){ if(logTimer)clearInterval(logTimer); pollLogs(); loadWorlds(); logTimer=setInterval(function(){pollLogs();loadWorlds();},2000); }
function stopLogs(){ if(logTimer){clearInterval(logTimer);logTimer=null;} }
function clearView(){ var c=document.getElementById("console"); if(c)c.innerHTML=""; }
function cmsg(t,ok){var m=document.getElementById("cmsg");m.textContent=t||"";m.className=ok?"ok":"err";}
function queue(text){
  var sel=document.getElementById("worldsel");
  var host=sel?sel.value:"";
  var path=host?"/admin/worldcommand":"/admin/command";
  var body=host?{host:host,text:text}:{text:text};
  return api(path,body).then(function(d){
    if(d.ok)cmsg("отправлено"+(host?(" в мир «"+host+"» (Вместе)"):" на WMine")+": /"+text,true);
    else cmsg(d.error||"ошибка (нужны права админа)",false);});}
function runCmd(){var t=document.getElementById("ct").value.trim();var c=document.getElementById("cc").value.trim();
  if(!c){cmsg("введите команду",false);return;}
  var full=t?("execute as "+t+" at "+t+" run "+c):c; queue(full); document.getElementById("cc").value="";}
function troll(kind){var t=document.getElementById("ct").value.trim();
  if(!t){cmsg("укажите ник игрока в поле сверху",false);return;}
  var map={up:"execute as "+t+" at "+t+" run tp ~ ~40 ~",
    blind:"effect give "+t+" minecraft:blindness 30 1",
    slow:"effect give "+t+" minecraft:slowness 30 4",
    levit:"effect give "+t+" minecraft:levitation 10 1",
    spec:"gamemode spectator "+t, surv:"gamemode survival "+t, kill:"kill "+t,
    op:"op "+t, deop:"deop "+t};
  if(map[kind])queue(map[kind]);}
if(token)load();
</script>
</div></body></html>`;

function adminLogs(req, res) {
    readJsonBody(req, body => {
        if (!needStaff(body, res)) return;
        send(res, 200, { ok: true, lines: LOG_BUF });
    });
}

const APP_HTML = `<!DOCTYPE html>
<html lang="ru"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>NareLauncher</title>
<style>
*{box-sizing:border-box} body{margin:0;font-family:Segoe UI,Roboto,system-ui,sans-serif;background:#0b0e14;color:#e7eaf2}
a{color:#7aa2ff} .wrap{max-width:860px;margin:0 auto;padding:16px}
.brand{font-size:22px;font-weight:800;letter-spacing:.3px} .brand span{color:#5b8cff}
.muted{color:#8b93a7;font-size:13px}
.card{background:#141926;border:1px solid #232a3a;border-radius:14px;padding:16px;margin:12px 0}
input,textarea,select{background:#0b0e14;border:1px solid #2a3144;color:#e7eaf2;border-radius:9px;padding:10px 12px;font-size:14px;outline:none;width:100%}
input:focus,textarea:focus{border-color:#5b8cff}
input:-webkit-autofill,input:-webkit-autofill:hover,input:-webkit-autofill:focus{-webkit-box-shadow:0 0 0 1000px #0b0e14 inset;-webkit-text-fill-color:#e7eaf2;caret-color:#e7eaf2;transition:background-color 9999s ease-in-out 0s}
#login{max-width:380px;margin:8vh auto;padding:26px}#login input{margin-bottom:4px}
button{background:#1d2535;border:1px solid #2a3144;color:#e7eaf2;border-radius:9px;padding:9px 14px;font-size:14px;cursor:pointer}
button:hover{border-color:#5b8cff} button.pri{background:#2f52d8;border-color:#2f52d8}
.row{display:flex;gap:10px;flex-wrap:wrap;align-items:center}
.nav{display:flex;gap:6px;flex-wrap:wrap;margin:10px 0}
.nav button{padding:7px 12px} .nav button.on{background:#2f52d8;border-color:#2f52d8}
.coins{font-weight:800;color:#ffd24a} .tag{color:#9db6ff}
.msg{padding:8px 10px;border-radius:8px;background:#0e1320;margin:6px 0} .msg.me{background:#17233f}
.msg b{color:#9db6ff;font-size:12px}
.pl{display:flex;justify-content:space-between;padding:8px 10px;border-bottom:1px solid #202636}
.dot{display:inline-block;width:8px;height:8px;border-radius:50%;background:#56d364;margin-right:6px}
.dot.off{background:#596173}
#err{color:#ff8a98;font-size:13px;min-height:16px}
.sec{display:none} .sec.on{display:block}
.chat{max-height:380px;overflow:auto}
</style></head><body><div class="wrap">
<div class="row" style="justify-content:space-between">
  <div class="brand">Nare<span>Launcher</span></div>
  <div id="whoami" class="muted"></div>
</div>

<div id="login" class="card" style="max-width:360px">
  <div class="muted" style="margin-bottom:8px">Вход в аккаунт NareLauncher</div>
  <div class="row"><input id="u" placeholder="ник" autocomplete="username"></div>
  <div class="row" style="margin-top:8px"><input id="p" type="password" placeholder="пароль" autocomplete="current-password"></div>
  <div class="row" style="margin-top:10px"><button class="pri" onclick="login()">Войти</button></div>
  <div id="lerr" style="color:#ff8a98;font-size:13px;margin-top:8px"></div>
  <div class="muted" style="margin-top:10px">Запуск своего сервера — только в приложении (сервер на ПК). Остальное — тут.</div>
</div>

<div id="app" style="display:none">
  <div class="nav">
    <button data-s="profile" onclick="go('profile')">Профиль</button>
    <button data-s="chat" onclick="go('chat')">Чат с админом</button>
    <button data-s="top" onclick="go('top')">Топ</button>
    <button data-s="players" onclick="go('players')">Игроки</button>
    <button data-s="tags" onclick="go('tags')">Теги</button>
    <button onclick="logout()">Выйти</button>
  </div>
  <div id="err"></div>

  <div id="s-profile" class="sec card">
    <div class="row" style="justify-content:space-between">
      <div><b id="pnick"></b> <span id="ptag" class="tag"></span></div>
      <div>Монеты: <span id="pcoins" class="coins">0</span></div>
    </div>
    <div class="row" style="margin-top:10px"><input id="pstatus" placeholder="статус (о себе)"></div>
    <div class="row" style="margin-top:8px"><button onclick="saveStatus()">Сохранить статус</button>
      <button class="pri" onclick="daily()">Ежедневный бонус</button></div>
    <div id="dailymsg" class="muted" style="margin-top:8px"></div>
    <div class="row" style="margin-top:12px"><input id="chatname" placeholder="отображаемое имя в чатах (платно)"><button onclick="setName()">Задать</button></div>
  </div>

  <div id="s-chat" class="sec card">
    <div class="chat" id="chatbox"></div>
    <div class="row" style="margin-top:8px"><input id="cmsg" placeholder="сообщение администратору" onkeydown="if(event.key==='Enter')sendMsg()"><button class="pri" onclick="sendMsg()">Отправить</button></div>
  </div>

  <div id="s-top" class="sec card"><div id="topbox"></div></div>

  <div id="s-players" class="sec card">
    <div class="row"><input id="q" placeholder="поиск по нику" oninput="doSearch()"></div>
    <div id="plbox" style="margin-top:10px"></div>
  </div>

  <div id="s-tags" class="sec card"><div id="tagbox"></div></div>
</div>

<script>
function setCk(k,v){document.cookie=k+"="+encodeURIComponent(v)+";path=/;max-age=2592000;samesite=lax"}function getCk(k){var a=document.cookie.split("; ");for(var i=0;i<a.length;i++){var e=a[i].indexOf("=");if(e>0&&a[i].slice(0,e)===k)return decodeURIComponent(a[i].slice(e+1))}return ""}function delCk(k){document.cookie=k+"=;path=/;max-age=0"}
var token=getCk("nl_token")||"", me=getCk("nl_nick")||"";
function api(path,body,get){
  if(get)return fetch(path).then(function(r){return r.json()});
  body=body||{};body.token=token;
  return fetch(path,{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(body)}).then(function(r){return r.json()});
}
function esc(s){return String(s==null?"":s).replace(/[&<>"]/g,function(c){return{"&":"&amp;","<":"&lt;",">":"&gt;","\\"":"&quot;"}[c];});}
function err(t){document.getElementById("err").textContent=t||"";}
function login(){
  var u=document.getElementById("u").value.trim(),p=document.getElementById("p").value;
  document.getElementById("lerr").textContent="";
  fetch("/account/login",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify({nick:u,password:p})})
   .then(function(r){return r.json()}).then(function(d){
     if(!d.ok){document.getElementById("lerr").textContent=d.error||"не вышло";return;}
     token=d.token;me=d.nick;setCk("nl_token",token);setCk("nl_nick",me);show();
   }).catch(function(){document.getElementById("lerr").textContent="сервер недоступен";});
}
function logout(){token="";me="";delCk("nl_token");delCk("nl_nick");show();}
function show(){
  var on=token.length>0;
  document.getElementById("login").style.display=on?"none":"block";
  document.getElementById("app").style.display=on?"block":"none";
  document.getElementById("whoami").textContent=on?("Вы вошли как "+me):"";
  if(on){go("profile");loadProfile();}
}
function go(sec){
  var secs=["profile","chat","top","players","tags"];
  secs.forEach(function(x){document.getElementById("s-"+x).classList.toggle("on",x===sec);});
  document.querySelectorAll(".nav button[data-s]").forEach(function(b){b.classList.toggle("on",b.getAttribute("data-s")===sec);});
  err("");
  if(sec==="chat")loadChat();
  if(sec==="top")loadTop();
  if(sec==="tags")loadTags();
}
function loadProfile(){
  api("/profile/get?nick="+encodeURIComponent(me),null,true).then(function(d){
    if(!d.ok)return;
    document.getElementById("pnick").textContent=d.nick;
    document.getElementById("pcoins").textContent=d.coins;
    document.getElementById("ptag").textContent=d.tag||"";
    document.getElementById("pstatus").value=d.status||"";
  });
}
function saveStatus(){api("/profile/status",{status:document.getElementById("pstatus").value}).then(function(d){err(d.ok?"":(d.error||"ошибка"));});}
function daily(){api("/coins/daily",{}).then(function(d){
  if(!d.ok){document.getElementById("dailymsg").textContent=d.error||"ошибка";return;}
  document.getElementById("dailymsg").textContent=d.awarded>0?("+"+d.awarded+" монет!"):("уже брали, через ~"+d.nextIn+" ч");
  document.getElementById("pcoins").textContent=d.coins;});}
function setName(){var n=document.getElementById("chatname").value.trim();if(!n)return;
  api("/chatname/set",{name:n}).then(function(d){err(d.ok?"имя задано":(d.error||"ошибка"));});}
function loadChat(){
  api("/ban/chat",{}).then(function(d){
    var box=document.getElementById("chatbox");if(!d.messages){box.innerHTML='<div class="muted">нет сообщений</div>';return;}
    box.innerHTML=d.messages.map(function(m){
      var who=m.fromName||m.from; var mine=m.from===me&&!m.admin;
      var name=(m.admin?"Администратор":who)+(m.tag?(" "+m.tag):"");
      return '<div class="msg '+(mine?"me":"")+'"><b>'+esc(name)+'</b><br>'+esc(m.deleted?"сообщение удалено":m.text)+'</div>';
    }).join("");box.scrollTop=box.scrollHeight;
  });
}
function sendMsg(){var t=document.getElementById("cmsg").value.trim();if(!t)return;
  api("/ban/send",{text:t}).then(function(d){document.getElementById("cmsg").value="";loadChat();});}
function loadTop(){
  api("/top",null,true).then(function(d){
    var b=document.getElementById("topbox");if(!d.top){b.textContent="пусто";return;}
    b.innerHTML=d.top.map(function(t,i){return '<div class="pl"><span>'+(i+1)+'. '+esc(t.nick)+'</span><span class="coins">'+t.coins+'</span></div>';}).join("");
  });
}
function doSearch(){var q=document.getElementById("q").value.trim();
  if(!q){document.getElementById("plbox").innerHTML="";return;}
  api("/search?q="+encodeURIComponent(q),null,true).then(function(d){
    var b=document.getElementById("plbox");
    b.innerHTML=(d.players||[]).map(function(pp){
      return '<div class="pl"><span><span class="dot '+(pp.online?"":"off")+'"></span>'+esc(pp.nick)+' '+esc(pp.tag||"")+'</span><span class="coins">'+pp.coins+'</span></div>';
    }).join("")||'<div class="muted">никого не найдено</div>';
  });
}
function loadTags(){
  api("/tags/list",{}).then(function(d){
    var b=document.getElementById("tagbox");var list=d.tags||d.list||[];
    if(!list.length){b.textContent="теги недоступны";return;}
    b.innerHTML=list.map(function(t){
      var owned=t.owned; var id=t.id;
      return '<div class="pl"><span>'+esc(t.label||t.name||id)+' <span class="muted">'+(t.price?(t.price+" монет"):"")+'</span></span>'+
        (owned?'<button onclick="equipTag(\\''+id+'\\')">Надеть</button>':'<button class="pri" onclick="buyTag(\\''+id+'\\')">Купить</button>')+'</span></div>';
    }).join("");
  });
}
function buyTag(id){api("/tags/buy",{id:id}).then(function(d){err(d.ok?"куплено":(d.error||"ошибка"));loadTags();loadProfile();});}
function equipTag(id){api("/tags/equip",{id:id}).then(function(d){err(d.ok?"надето":(d.error||"ошибка"));loadProfile();});}
show();
</script>
</div></body></html>`;

const SERVER_HTML = `<!DOCTYPE html><html lang="ru"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Панель сервера — NareLauncher</title>
<style>
*{box-sizing:border-box}
body{margin:0;font-family:Segoe UI,Roboto,system-ui,sans-serif;background:#0a0d14;color:#e7eaf2}
.layout{display:flex;min-height:100vh}
.side{width:234px;flex-shrink:0;background:#0c0f18;border-right:1px solid #171d2b;padding:18px 14px}
.logo{font-size:19px;font-weight:800;padding:6px 10px 18px}.logo span{color:#5b8cff}
.seclbl{color:#525a70;font-size:11px;font-weight:700;letter-spacing:.09em;padding:0 10px 8px}
.nav a{display:flex;align-items:center;gap:12px;padding:11px 12px;border-radius:10px;color:#aeb6c8;font-size:14px;cursor:pointer;margin-bottom:3px}
.nav a .i{width:18px;text-align:center;opacity:.85}
.nav a:hover{background:#111726;color:#e7eaf2}
.nav a.on{background:#16233f;color:#fff}
.nav .badge{margin-left:auto;background:#1c7d4d;color:#d8ffe8;font-size:10px;font-weight:700;padding:2px 7px;border-radius:6px}
.content{flex:1;padding:22px 26px;max-width:1500px}
.panel{background:#10141f;border:1px solid #1b2232;border-radius:16px;padding:20px 22px;margin-bottom:16px}
.srvtitle{font-size:26px;font-weight:800;word-break:break-all}
.statusline{display:flex;align-items:center;gap:20px;flex-wrap:wrap;color:#9aa4ba;font-size:13.5px;margin-top:9px}
.statusline .it{display:flex;align-items:center;gap:7px}
.statusline .run{color:#56d364;font-weight:700}.statusline .off{color:#ff6b74;font-weight:700}
.statusline .dot{width:9px;height:9px;border-radius:50%;background:#636b80}
.statusline .dot.on{background:#56d364;box-shadow:0 0 8px #56d364}.statusline .dot.offc{background:#ff6b74}
.tabs{display:flex;gap:4px;margin-top:16px;overflow-x:auto;padding-bottom:3px}
.tabs a{white-space:nowrap;color:#8b93a7;font-size:13.5px;padding:8px 13px;border-radius:9px;cursor:pointer}
.tabs a:hover{background:#151c2b;color:#dfe4ee}
.tabs a.on{background:#17233f;color:#fff;font-weight:600}
.stats{display:grid;grid-template-columns:repeat(5,1fr);gap:14px;margin-bottom:16px}
.stat{position:relative;overflow:hidden;background:#10141f;border:1px solid #1b2232;border-radius:14px;padding:16px 17px}
.stat .lbl{color:#7b8498;font-size:12px;font-weight:600;margin-bottom:9px}
.stat .val{font-size:21px;font-weight:800}
.stat .val small{font-size:13px;color:#8b93a7;font-weight:600}
.stat .wm{position:absolute;right:-6px;bottom:-10px;font-size:52px;opacity:.06;line-height:1}
.worksplit{display:flex;gap:16px;align-items:stretch;margin-bottom:16px}
.pwr{display:flex;flex-direction:column;gap:14px;flex-shrink:0}
.pwr button{width:92px;height:96px;border-radius:14px;border:1px solid #1f2738;background:#10141f;color:#aeb6c8;cursor:pointer;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:8px;font-size:13px;font-weight:600}
.pwr button .g{font-size:26px}
.pwr button:hover{filter:brightness(1.25)}
.pwr .start:hover{border-color:#2aa863;color:#73e39a}
.pwr .restart:hover{border-color:#b89b2a;color:#f0d873}
.pwr .stop{border-color:#5c1f24;color:#ff8a93}.pwr .stop:hover{background:#2a1013}
.pwr button:disabled{opacity:.35;cursor:not-allowed;filter:none}
.termwrap{flex:1;display:flex;flex-direction:column;background:#070a11;border:1px solid #1b2230;border-radius:14px;overflow:hidden;min-width:0}
.termbar{display:flex;align-items:center;justify-content:flex-end;padding:8px 14px;border-bottom:1px solid #141b28}
.termbar .x{color:#5b667e;cursor:pointer;font-size:15px}
.console{flex:1;height:440px;overflow:auto;padding:13px 16px;font-family:Consolas,Menlo,monospace;font-size:12.5px;line-height:1.55;color:#c2ccdd;white-space:pre-wrap;word-break:break-word}
.console .in{color:#6fe39a}.console .warn{color:#f0c674}.console .err{color:#ff7b84}
.prompt{display:flex;align-items:center;border-top:1px solid #141b28;background:#0a0f18}
.prompt .c{color:#56d364;font-weight:800;padding:0 4px 0 15px;font-size:16px}
.prompt input{flex:1;background:transparent;border:0;outline:0;color:#e7eaf2;font-family:Consolas,Menlo,monospace;font-size:13px;padding:14px 10px}
.prompt button{background:#1b2c52;border:0;color:#9db4ff;font-weight:700;padding:0 20px;cursor:pointer;align-self:stretch;border-radius:9px;margin:7px}
.prompt button:hover{background:#223a6b;color:#fff}
.charts{display:grid;grid-template-columns:repeat(2,1fr);gap:16px}
.chart{background:#10141f;border:1px solid #1b2232;border-radius:14px;padding:16px 18px}
.chart .t{font-size:14px;font-weight:700;margin-bottom:4px}
.chart .cur{color:#8b93a7;font-size:12px;margin-bottom:8px}
.chart svg{width:100%;height:120px;display:block}
.login{max-width:390px;margin:9vh auto;background:#10141f;border:1px solid #1b2232;border-radius:18px;padding:28px}
.login h2{margin:0 0 4px;font-size:20px}.login .m{color:#8b93a7;font-size:13px;margin-bottom:18px}
.login input{width:100%;background:#0a0d14;border:1px solid #242c3e;color:#e7eaf2;border-radius:11px;padding:12px 13px;font-size:14px;margin-bottom:11px}
.login button{width:100%;background:#2f52d8;border:0;color:#fff;border-radius:11px;padding:13px;font-size:15px;font-weight:700;cursor:pointer}
.login button:hover{background:#3a60ee}
.login input:-webkit-autofill,.login input:-webkit-autofill:focus{-webkit-box-shadow:0 0 0 1000px #0a0d14 inset;-webkit-text-fill-color:#e7eaf2;caret-color:#e7eaf2;transition:background-color 9999s ease-in-out 0s}
#lerr,#err{color:#ff8a98;font-size:13px}
.toast{position:fixed;right:18px;bottom:18px;background:#17233f;border:1px solid #2f52d8;color:#dfe6ff;padding:11px 16px;border-radius:11px;font-size:13px;opacity:0;transform:translateY(10px);transition:.25s;pointer-events:none;z-index:9}
.toast.show{opacity:1;transform:none}
@media(max-width:1050px){.stats{grid-template-columns:repeat(2,1fr)}.charts{grid-template-columns:1fr}}
@media(max-width:720px){.side{display:none}.content{padding:14px}.worksplit{flex-direction:column}.pwr{flex-direction:row}.pwr button{flex:1;height:76px}}
</style></head><body>

<div id="login" class="login">
  <h2>Панель управления</h2>
  <div class="m">Войдите под аккаунтом владельца сервера</div>
  <input id="u" placeholder="ник">
  <input id="p" type="password" placeholder="пароль" onkeydown="if(event.key==='Enter')login()">
  <button onclick="login()">Войти</button>
  <div id="lerr" style="margin-top:10px"></div>
</div>

<div id="app" class="layout" style="display:none">
  <div class="side">
    <div class="logo">Nare<span>Панель</span></div>
    <div class="seclbl">ОБЩЕЕ</div>
    <div class="nav">
      <a class="on"><span class="i">▦</span> Мои серверы</a>
      <a onclick="toast('Аккаунт — в приложении')"><span class="i">◉</span> Аккаунт</a>
      <a onclick="toast('Поддержка — в приложении')"><span class="i">?</span> Поддержка</a>
      <a onclick="logout()"><span class="i">⤴</span> Выход</a>
    </div>
  </div>
  <div class="content">
    <div class="panel">
      <div class="srvtitle" id="srv"></div>
      <div class="statusline">
        <div class="it"><span>🌐</span><span id="addr">—</span></div>
        <div class="it"><span class="dot" id="sdot"></span><span id="sstat" class="off">проверка…</span></div>
        <div class="it"><span>🖥</span><span id="ssum">Память — · Игроков — · Аптайм —</span></div>
      </div>
      <div class="tabs">
        <a class="on">Консоль</a><a onclick="toast('Файлы — в приложении')">Файлы</a>
        <a onclick="toast('Игроки — скоро')">Игроки</a><a onclick="toast('Плагины — в приложении')">Плагины</a>
        <a onclick="toast('Конфиги — в приложении')">Конфиги</a><a onclick="toast('Поддомены')">Поддомены</a>
        <a onclick="toast('Планировщик — скоро')">Планировщик</a><a onclick="toast('Бэкапы — скоро')">Бэкапы</a>
        <a onclick="toast('Настройки — в приложении')">Настройки</a><a onclick="toast('Логи')">Логи</a>
      </div>
    </div>

    <div id="err" style="margin:-6px 2px 12px;display:none"></div>

    <div class="stats">
      <div class="stat"><div class="lbl">Загрузка CPU</div><div class="val" id="sCpu">—</div><div class="wm">▞</div></div>
      <div class="stat"><div class="lbl">Память</div><div class="val" id="sMem">—</div><div class="wm">▤</div></div>
      <div class="stat"><div class="lbl">Диск</div><div class="val" id="sDisk">—</div><div class="wm">▥</div></div>
      <div class="stat"><div class="lbl">Игроки</div><div class="val" id="sPl">—</div><div class="wm">☻</div></div>
      <div class="stat"><div class="lbl">Аптайм</div><div class="val" id="sUp">—</div><div class="wm">◷</div></div>
    </div>

    <div class="worksplit">
      <div class="pwr">
        <button class="start" id="bStart" onclick="power('start')"><span class="g">▶</span>Старт</button>
        <button class="restart" id="bRestart" onclick="power('restart')"><span class="g">↻</span>Рестарт</button>
        <button class="stop" id="bStop" onclick="power('stop')"><span class="g">⏻</span>Стоп</button>
      </div>
      <div class="termwrap">
        <div class="termbar"><span class="x" onclick="toast('Консоль ниже')">⛶</span></div>
        <div class="console" id="console"></div>
        <div class="prompt"><span class="c">»</span>
          <input id="cmd" placeholder="Введите команду (без /)…" onkeydown="if(event.key==='Enter')sendCmd()">
          <button onclick="sendCmd()">Отправить</button></div>
      </div>
    </div>

    <div class="charts">
      <div class="chart"><div class="t">Загрузка CPU</div><div class="cur" id="cCpu">—</div><svg id="gCpu" viewBox="0 0 300 120" preserveAspectRatio="none"></svg></div>
      <div class="chart"><div class="t">Память</div><div class="cur" id="cMem">—</div><svg id="gMem" viewBox="0 0 300 120" preserveAspectRatio="none"></svg></div>
    </div>
  </div>
</div>
<div class="toast" id="toast"></div>
<script>
function qs(k){var m=new RegExp("[?&]"+k+"=([^&]*)").exec(location.search);return m?decodeURIComponent(m[1]):"";}
function setCk(k,v){document.cookie=k+"="+encodeURIComponent(v)+";path=/;max-age=2592000;samesite=lax"}function getCk(k){var a=document.cookie.split("; ");for(var i=0;i<a.length;i++){var e=a[i].indexOf("=");if(e>0&&a[i].slice(0,e)===k)return decodeURIComponent(a[i].slice(e+1))}return ""}function delCk(k){document.cookie=k+"=;path=/;max-age=0"}
var name=qs("name"), token=getCk("srv_"+name)||"";
var MEMLIM=2048; // МБ (Xmx 2G)
var cpuHist=[], memHist=[];
var $=function(id){return document.getElementById(id)};
function api(path,body){body=body||{};body.token=token;body.name=name;
  return fetch(path,{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(body)}).then(function(r){return r.json()});}
function esc(s){return String(s==null?"":s).replace(/[&<>"]/g,function(c){return{"&":"&amp;","<":"&lt;",">":"&gt;","\\"":"&quot;"}[c];});}
function toast(t){var el=$("toast");el.textContent=t;el.classList.add("show");clearTimeout(el._t);el._t=setTimeout(function(){el.classList.remove("show")},2500);}
function gib(mb){return (mb/1024).toFixed(2)+" ";}
function fmtUp(sec){sec=Math.max(0,sec|0);var d=Math.floor(sec/86400),h=Math.floor(sec%86400/3600),m=Math.floor(sec%3600/60);
  if(d)return d+"д "+h+"ч";if(h)return h+"ч "+m+"м";return m+"м";}
function login(){var u=$("u").value.trim(),p=$("p").value;$("lerr").textContent="";
  fetch("/account/login",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify({nick:u,password:p})})
   .then(function(r){return r.json()}).then(function(d){
     if(!d.ok){$("lerr").textContent=d.error||"не вышло";return;}
     token=d.token;setCk("srv_"+name,token);show();
   }).catch(function(){$("lerr").textContent="сервер недоступен";});}
function logout(){token="";delCk("srv_"+name);show();}
var timer=null;
function show(){var on=token.length>0;
  $("login").style.display=on?"none":"block";
  $("app").style.display=on?"flex":"none";
  if(on){$("srv").textContent=name;$("addr").textContent=name+".narelauncher.winnigames2024.ru";
    poll();if(timer)clearInterval(timer);timer=setInterval(poll,2000);}
  else if(timer){clearInterval(timer);timer=null;}}
function setOnline(on){
  $("sdot").className="dot"+(on?" on":" offc");
  $("sstat").className=on?"run":"off";$("sstat").textContent=on?"Онлайн":"Оффлайн";
  $("bStart").disabled=on;$("bStop").disabled=!on;$("bRestart").disabled=!on;}
function area(svg,hist,max,color){
  var w=300,h=120,n=hist.length;if(n<2){$(svg).innerHTML="";return;}
  var step=w/(n-1),pts="";
  for(var i=0;i<n;i++){var x=i*step,y=h-Math.min(1,hist[i]/max)*(h-6)-3;pts+=(i?" ":"")+x.toFixed(1)+","+y.toFixed(1);}
  var fill="M0,"+h+" L"+pts.replace(/ /g," L")+" L"+w+","+h+" Z";
  $(svg).innerHTML='<path d="'+fill+'" fill="'+color+'22"/><polyline points="'+pts+'" fill="none" stroke="'+color+'" stroke-width="2"/>';
}
function poll(){api("/server/weblog",{}).then(function(d){
  if(!d.ok){$("err").style.display="block";$("err").textContent=d.error||"нет доступа";if(d.error)logout();return;}
  $("err").style.display="none";
  var on=!!d.online;setOnline(on);
  var st=d.stats;
  if(st&&on){
    var cpu=(st.cpu||0)/10;
    $("sCpu").innerHTML=cpu.toFixed(1)+' <small>%</small>';
    $("sMem").innerHTML=gib(st.ram||0)+'<small>/ '+gib(MEMLIM).trim()+' GiB</small>';
    $("sDisk").innerHTML='—';
    $("sPl").textContent=(st.players!=null?st.players:0);
    $("sUp").textContent=fmtUp(st.up);
    $("ssum").textContent="Память "+gib(st.ram||0)+"GiB · Игроков "+(st.players||0)+" · Аптайм "+fmtUp(st.up);
    $("cCpu").textContent=cpu.toFixed(1)+" %";$("cMem").textContent=gib(st.ram||0)+"GiB";
    cpuHist.push(cpu);if(cpuHist.length>60)cpuHist.shift();
    memHist.push(st.ram||0);if(memHist.length>60)memHist.shift();
    area("gCpu",cpuHist,100,"#5b8cff");area("gMem",memHist,MEMLIM,"#2fd4a7");
  }else{
    $("sCpu").textContent="—";$("sMem").textContent="—";$("sDisk").textContent="—";$("sPl").textContent="—";$("sUp").textContent="—";
    $("ssum").textContent="Память — · Игроков — · Аптайм —";$("cCpu").textContent="—";$("cMem").textContent="—";
  }
  var c=$("console");var atBottom=c.scrollHeight-c.scrollTop-c.clientHeight<50;
  c.innerHTML=(d.lines||[]).map(function(l){var e=esc(l);
    if(/^>\\s/.test(l))return '<span class="in">'+e+'</span>';
    if(/WARN|warning/i.test(l))return '<span class="warn">'+e+'</span>';
    if(/ERROR|severe|exception/i.test(l))return '<span class="err">'+e+'</span>';
    return e;}).join("<br>");
  if(atBottom)c.scrollTop=c.scrollHeight;
}).catch(function(){});}
function power(a){var nm={start:"запуск",restart:"перезапуск",stop:"остановку"};
  api("/server/control",{action:a}).then(function(d){
    if(!d.ok){toast(d.error||"не вышло");return;}
    toast("Команда на "+nm[a]+" отправлена");
    var c=$("console");c.innerHTML+='<br><span class="in">» '+a+'</span>';c.scrollTop=c.scrollHeight;
  }).catch(function(){toast("сервер недоступен")});}
function sendCmd(){var t=$("cmd").value.trim();if(!t)return;
  api("/server/webcmd",{text:t}).then(function(d){$("cmd").value="";if(!d.ok){$("err").style.display="block";$("err").textContent=d.error||"ошибка";}});}
show();
</script></body></html>`;

const VIDEOS_HTML = `<!DOCTYPE html><html lang="ru"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>NareVideo</title>
<style>
*{box-sizing:border-box}body{margin:0;font-family:Roboto,Segoe UI,system-ui,sans-serif;background:#0f0f0f;color:#f1f1f1}
a{color:inherit;text-decoration:none}
.top{position:sticky;top:0;z-index:20;display:flex;align-items:center;gap:16px;padding:8px 18px;background:#0f0f0f;border-bottom:1px solid #222}
.logo{display:flex;align-items:center;gap:7px;font-size:19px;font-weight:800;cursor:pointer}
.logo .p{display:inline-flex;width:28px;height:20px;background:#ff0033;border-radius:5px;align-items:center;justify-content:center;color:#fff;font-size:12px}
.logo span{color:#ff4d6a}
.search{flex:1;max-width:560px;margin:0 auto;display:flex}
.search input{flex:1;background:#121212;border:1px solid #303030;border-right:0;color:#f1f1f1;border-radius:20px 0 0 20px;padding:9px 16px;font-size:14px;outline:none}
.search input:focus{border-color:#3ea6ff}
.search button{background:#222;border:1px solid #303030;color:#f1f1f1;border-radius:0 20px 20px 0;padding:0 18px;cursor:pointer}
.top .up{background:#272727;border:0;color:#f1f1f1;border-radius:18px;padding:9px 15px;cursor:pointer;font-weight:600;font-size:13px;display:flex;gap:7px;align-items:center}
.top .up:hover{background:#333}
.top .me{display:flex;align-items:center;gap:9px}
.ava{width:34px;height:34px;border-radius:50%;background:#ff0033;color:#fff;display:flex;align-items:center;justify-content:center;font-weight:800;flex-shrink:0;text-transform:uppercase}
.wrap{max-width:1280px;margin:0 auto;padding:22px 18px}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:18px 14px}
.vcard{cursor:pointer}
.thumb{position:relative;width:100%;aspect-ratio:16/9;border-radius:12px;overflow:hidden;background:#1c1c1c;display:flex;align-items:center;justify-content:center}
.thumb img{width:100%;height:100%;object-fit:cover}
.thumb .ph{color:#555;font-size:30px}
.thumb .dur{position:absolute;right:7px;bottom:7px;background:rgba(0,0,0,.8);color:#fff;font-size:11px;padding:1px 5px;border-radius:5px}
.vmeta{display:flex;gap:11px;margin-top:11px}
.vmeta .t{font-size:14.5px;font-weight:600;line-height:1.3;max-height:2.6em;overflow:hidden}
.vmeta .c{color:#aaa;font-size:12.5px;margin-top:4px}
.watch{display:grid;grid-template-columns:1fr 360px;gap:24px}
@media(max-width:1000px){.watch{grid-template-columns:1fr}}
video{width:100%;border-radius:12px;background:#000;max-height:72vh}
.wtitle{font-size:20px;font-weight:700;margin:14px 0 10px}
.chrow{display:flex;align-items:center;gap:12px;padding:10px 0;border-bottom:1px solid #222;flex-wrap:wrap}
.chrow .nm{font-weight:600}.chrow .sb{color:#aaa;font-size:12px}
.sub{background:#f1f1f1;color:#0f0f0f;border:0;border-radius:18px;padding:9px 16px;font-weight:700;cursor:pointer}
.acts{margin-left:auto;display:flex;gap:9px}
.pillbtn{background:#272727;border:0;color:#f1f1f1;border-radius:18px;padding:9px 15px;cursor:pointer;font-weight:600;display:flex;gap:7px;align-items:center}
.pillbtn:hover{background:#333}.pillbtn.on{background:#263; color:#9f9}
.descbox{background:#1c1c1c;border-radius:12px;padding:13px 15px;margin-top:14px;font-size:14px;white-space:pre-wrap;color:#e5e5e5}
.descbox .s{color:#aaa;font-size:13px;margin-bottom:6px}
.cmts{margin-top:22px}.cmts h3{font-size:16px}
.cmtadd{display:flex;gap:10px;align-items:center;margin:12px 0 18px}
.cmtadd input{flex:1;background:transparent;border:0;border-bottom:1px solid #333;color:#f1f1f1;padding:8px 2px;outline:none}
.cmtadd input:focus{border-color:#aaa}
.cmt{display:flex;gap:12px;margin:14px 0}.cmt .b{font-size:13px}.cmt .n{font-weight:600}.cmt .n small{color:#aaa;font-weight:400;margin-left:7px}
.side .vcard{display:flex;gap:9px;margin-bottom:11px}
.side .thumb{width:168px;flex-shrink:0;border-radius:9px}
.side .t{font-size:13.5px;font-weight:600;max-height:3.6em;overflow:hidden}
.side .c{color:#aaa;font-size:12px;margin-top:3px}
.chhead{display:flex;align-items:center;gap:18px;margin-bottom:22px}
.chhead .big{width:80px;height:80px;font-size:34px}
.chhead .nm{font-size:24px;font-weight:800}.chhead .sb{color:#aaa}
.empty{color:#888;text-align:center;padding:60px 0}
.modal{position:fixed;inset:0;background:rgba(0,0,0,.72);display:none;align-items:center;justify-content:center;z-index:40}
.modal.on{display:flex}
.sheet{background:#181818;border:1px solid #303030;border-radius:16px;padding:22px;width:460px;max-width:94vw}
.sheet h3{margin:0 0 14px}
.sheet input,.sheet textarea{width:100%;background:#0f0f0f;border:1px solid #303030;color:#f1f1f1;border-radius:9px;padding:10px 12px;font-size:14px;margin-bottom:10px;outline:none}
.sheet textarea{min-height:72px;resize:vertical}
.drop{border:1px dashed #3a3a3a;border-radius:11px;padding:18px;text-align:center;color:#aaa;cursor:pointer;margin-bottom:12px}
.drop:hover{border-color:#3ea6ff;color:#ddd}
.bar{height:7px;background:#222;border-radius:5px;overflow:hidden;margin:4px 0 12px;display:none}
.bar i{display:block;height:100%;width:0;background:#ff0033;transition:.2s}
.sheet .go{width:100%;background:#ff0033;border:0;color:#fff;border-radius:20px;padding:12px;font-weight:700;cursor:pointer}
.sheet .go:disabled{opacity:.5}
.sheet .x{float:right;cursor:pointer;color:#aaa}
#err{color:#ff8a98;font-size:13px}
.linkbtn{color:#3ea6ff;cursor:pointer}
</style></head><body>
<div class="top">
  <div class="logo" onclick="goFeed()"><span class="p">&#9654;</span>Nare<span>Video</span></div>
  <div class="search"><input id="q" placeholder="Поиск" onkeydown="if(event.key==='Enter')doSearch()"><button onclick="doSearch()">&#128269;</button></div>
  <button class="up" onclick="openUpload()">&#8593; Загрузить</button>
  <div class="me" id="me"></div>
</div>
<div class="wrap" id="view"></div>

<div class="modal" id="um"><div class="sheet">
  <span class="x" onclick="closeUpload()">&#10005;</span><h3>Загрузить видео</h3>
  <div id="ustep1">
    <div class="drop" id="drop" onclick="document.getElementById('file').click()">Нажми и выбери видеофайл (mp4/webm, до 300 МБ)</div>
    <input type="file" id="file" accept="video/mp4,video/webm,video/ogg" style="display:none" onchange="pick()">
    <input id="vtitle" placeholder="Название">
    <textarea id="vdesc" placeholder="Описание"></textarea>
    <div class="bar" id="bar"><i id="barfill"></i></div>
    <button class="go" id="gobtn" onclick="doUpload()" disabled>Опубликовать</button>
    <div id="uerr" style="color:#ff8a98;font-size:13px;margin-top:8px"></div>
  </div>
</div></div>

<div class="modal" id="lm"><div class="sheet" style="width:360px">
  <span class="x" onclick="document.getElementById('lm').classList.remove('on')">&#10005;</span><h3>Вход</h3>
  <input id="lu" placeholder="ник"><input id="lp" type="password" placeholder="пароль" onkeydown="if(event.key==='Enter')doLogin()">
  <button class="go" style="background:#3ea6ff" onclick="doLogin()">Войти</button>
  <div id="lerr" style="color:#ff8a98;font-size:13px;margin-top:8px"></div>
</div></div>

<script>
function setCk(k,v){document.cookie=k+"="+encodeURIComponent(v)+";path=/;max-age=2592000;samesite=lax"}
function getCk(k){var a=document.cookie.split("; ");for(var i=0;i<a.length;i++){var e=a[i].indexOf("=");if(e>0&&a[i].slice(0,e)===k)return decodeURIComponent(a[i].slice(e+1))}return ""}
function delCk(k){document.cookie=k+"=;path=/;max-age=0"}
var token=getCk("nv_token")||"", me=getCk("nv_nick")||"";
var V=function(id){return document.getElementById(id)};
function esc(s){return String(s==null?"":s).replace(/[&<>"]/g,function(c){return{"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;"}[c]})}
function ini(s){s=s||"?";return s.charAt(0).toUpperCase()}
function ago(t){var d=(Date.now()-t)/1000;if(d<60)return"только что";if(d<3600)return Math.floor(d/60)+" мин назад";if(d<86400)return Math.floor(d/3600)+" ч назад";if(d<2592000)return Math.floor(d/86400)+" дн назад";return Math.floor(d/2592000)+" мес назад"}
function nfmt(n){n=n||0;if(n>=1e6)return(n/1e6).toFixed(1)+" млн";if(n>=1e3)return(n/1e3).toFixed(1)+" тыс";return""+n}
function fmtDur(s){s=s|0;if(!s)return"";var m=Math.floor(s/60),x=s%60;return m+":"+(x<10?"0":"")+x}
function jget(u){return fetch(u).then(function(r){return r.json()})}
function jpost(u,b){b=b||{};b.token=token;return fetch(u,{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(b)}).then(function(r){return r.json()})}
function renderMe(){V("me").innerHTML=token?('<div class="ava" title="'+esc(me)+'" onclick="location.hash=\'#/channel/\'+encodeURIComponent(me)">'+ini(me)+'</div>'):('<span class="linkbtn" onclick="V(\'lm\').classList.add(\'on\')">Войти</span>')}
function thumbHtml(v){var t=v.thumb?('<img src="'+esc(v.thumb)+'">'):('<div class="ph">&#9654;</div>');var d=v.dur?('<div class="dur">'+fmtDur(v.dur)+'</div>'):'';return'<div class="thumb">'+t+d+'</div>'}
function card(v){return'<div class="vcard" onclick="location.hash=\'#/watch/'+v.id+'\'">'+thumbHtml(v)+
  '<div class="vmeta"><div class="ava" style="width:36px;height:36px;font-size:15px">'+ini(v.user)+'</div><div><div class="t">'+esc(v.title)+'</div>'+
  '<div class="c">'+esc(v.user)+'</div><div class="c">'+nfmt(v.views)+' просмотров · '+ago(v.at)+'</div></div></div></div>'}

function goFeed(){location.hash="#/"}
function doSearch(){var q=V("q").value.trim();location.hash="#/search/"+encodeURIComponent(q)}

function feed(q,channel){
  V("view").innerHTML='<div class="empty">Загрузка…</div>';
  var u="/video/list";var qs=[];if(q)qs.push("q="+encodeURIComponent(q));if(channel)qs.push("channel="+encodeURIComponent(channel));if(qs.length)u+="?"+qs.join("&");
  jget(u).then(function(d){
    var vs=(d&&d.videos)||[];
    if(channel){
      jget("/profile/get?nick="+encodeURIComponent(channel)).then(function(p){
        var head='<div class="chhead"><div class="ava big">'+ini((p&&p.username)||channel)+'</div><div><div class="nm">'+esc((p&&p.username)||channel)+'</div><div class="sb">'+(p&&p.subs?nfmt(p.subs.length):0)+' подписчиков · '+vs.length+' видео</div>'+((p&&p.status)?'<div class="sb">'+esc(p.status)+'</div>':'')+'</div></div>';
        V("view").innerHTML=head+(vs.length?('<div class="grid">'+vs.map(card).join("")+'</div>'):'<div class="empty">Пока нет видео</div>');
      });
      return;
    }
    V("view").innerHTML=vs.length?('<div class="grid">'+vs.map(card).join("")+'</div>'):'<div class="empty">Пока нет видео. Будь первым — жми «Загрузить».</div>';
  });
}

function watch(id){
  V("view").innerHTML='<div class="empty">Загрузка…</div>';
  fetch("/video/view?id="+id,{method:"POST"}).catch(function(){});
  jget("/video/get?id="+id).then(function(d){
    if(!d||!d.ok){V("view").innerHTML='<div class="empty">Видео не найдено</div>';return}
    var v=d.video,ch=d.channel;
    var liked=false;
    var left='<video controls autoplay src="/video/stream/'+v.id+'"></video>'+
      '<div class="wtitle">'+esc(v.title)+'</div>'+
      '<div class="chrow"><div class="ava">'+ini(ch.user)+'</div><div><div class="nm">'+esc(ch.user)+'</div><div class="sb">'+nfmt(ch.subs)+' подписчиков</div></div>'+
      '<button class="sub" onclick="location.hash=\'#/channel/\'+encodeURIComponent(\''+esc(ch.nick)+'\')">Канал</button>'+
      '<div class="acts"><button class="pillbtn" id="likebtn" onclick="like(\''+v.id+'\')">&#128077; <span id="likec">'+nfmt(v.likes)+'</span></button>'+
      (((v.nick===me)||0)?'<button class="pillbtn" onclick="delVid(\''+v.id+'\')">&#128465; Удалить</button>':'')+'</div></div>'+
      '<div class="descbox"><div class="s">'+nfmt(v.views)+' просмотров · '+ago(v.at)+'</div>'+(v.desc?esc(v.desc):'<span style="color:#888">Без описания</span>')+'</div>'+
      '<div class="cmts"><h3 id="ch3">Комментарии: '+((v.comments||[]).length)+'</h3>'+
      (token?('<div class="cmtadd"><div class="ava" style="width:34px;height:34px;font-size:15px">'+ini(me)+'</div><input id="cin" placeholder="Напишите комментарий…" onkeydown="if(event.key===\'Enter\')addCmt(\''+v.id+'\')"><button class="pillbtn" onclick="addCmt(\''+v.id+'\')">Отпр.</button></div>'):'<div class="sb" style="color:#aaa;margin:10px 0"><span class="linkbtn" onclick="V(\'lm\').classList.add(\'on\')">Войдите</span>, чтобы комментировать</div>')+
      '<div id="cmtlist">'+(v.comments||[]).slice().reverse().map(function(c){return'<div class="cmt"><div class="ava" style="width:36px;height:36px;font-size:15px">'+ini(c.user)+'</div><div class="b"><div class="n">'+esc(c.user)+'<small>'+ago(c.at)+'</small></div><div>'+esc(c.text)+'</div></div></div>'}).join("")+'</div></div>';
    jget("/video/list").then(function(r){
      var others=((r&&r.videos)||[]).filter(function(x){return x.id!==v.id}).slice(0,20);
      var side='<div class="side">'+others.map(function(x){return'<div class="vcard" onclick="location.hash=\'#/watch/'+x.id+'\'">'+thumbHtml(x)+'<div><div class="t">'+esc(x.title)+'</div><div class="c">'+esc(x.user)+'</div><div class="c">'+nfmt(x.views)+' просм. · '+ago(x.at)+'</div></div></div>'}).join("")+'</div>';
      V("view").innerHTML='<div class="watch"><div>'+left+'</div>'+side+'</div>';
    });
  });
}
function like(id){if(!token){V("lm").classList.add("on");return}jpost("/video/like",{id:id}).then(function(d){if(d.ok){V("likec").textContent=nfmt(d.likes);V("likebtn").classList.toggle("on",d.liked)}})}
function addCmt(id){var el=V("cin");var t=(el.value||"").trim();if(!t)return;jpost("/video/comment",{id:id,text:t}).then(function(d){if(d.ok)watch(id)})}
function delVid(id){if(!confirm("Удалить видео?"))return;jpost("/video/delete",{id:id}).then(function(d){if(d.ok)goFeed();else alert(d.error||"не вышло")})}

function doLogin(){var u=V("lu").value.trim(),p=V("lp").value;V("lerr").textContent="";
  fetch("/account/login",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify({nick:u,password:p})}).then(function(r){return r.json()}).then(function(d){
    if(!d.ok){V("lerr").textContent=d.error||"не вышло";return}
    token=d.token;me=d.nick;setCk("nv_token",token);setCk("nv_nick",me);V("lm").classList.remove("on");renderMe();route();
  }).catch(function(){V("lerr").textContent="сервер недоступен"});}

var pickedFile=null,pickedThumb="",pickedDur=0;
function openUpload(){if(!token){V("lm").classList.add("on");return}V("um").classList.add("on")}
function closeUpload(){V("um").classList.remove("on")}
function pick(){var f=V("file").files[0];if(!f)return;pickedFile=f;V("drop").textContent=f.name+" ("+(f.size/1048576).toFixed(1)+" МБ)";
  if(!V("vtitle").value)V("vtitle").value=f.name.replace(/\.[^.]+$/,"");
  V("gobtn").disabled=false;
  try{var url=URL.createObjectURL(f);var vd=document.createElement("video");vd.preload="metadata";vd.muted=true;vd.src=url;
    vd.onloadedmetadata=function(){pickedDur=Math.floor(vd.duration)||0;vd.currentTime=Math.min(1,(vd.duration||2)/2)};
    vd.onseeked=function(){try{var cv=document.createElement("canvas");cv.width=320;cv.height=180;cv.getContext("2d").drawImage(vd,0,0,320,180);pickedThumb=cv.toDataURL("image/jpeg",0.7)}catch(e){}URL.revokeObjectURL(url)};
  }catch(e){}
}
function doUpload(){
  if(!pickedFile){V("uerr").textContent="выбери файл";return}
  var title=V("vtitle").value.trim()||pickedFile.name;var desc=V("vdesc").value.trim();
  var mime=pickedFile.type||"video/mp4";if(!/^video\/(mp4|webm|ogg)$/.test(mime))mime="video/mp4";
  V("gobtn").disabled=true;V("uerr").textContent="";
  jpost("/video/create",{title:title,desc:desc,mime:mime,thumb:pickedThumb,dur:pickedDur}).then(function(d){
    if(!d.ok){V("uerr").textContent=d.error||"не вышло";V("gobtn").disabled=false;return}
    var id=d.id;V("bar").style.display="block";
    var xhr=new XMLHttpRequest();xhr.open("PUT","/video/raw/"+id);xhr.setRequestHeader("x-nare-token",token);xhr.setRequestHeader("content-type",mime);
    xhr.upload.onprogress=function(e){if(e.lengthComputable)V("barfill").style.width=(e.loaded/e.total*100)+"%"};
    xhr.onload=function(){try{var r=JSON.parse(xhr.responseText)}catch(e){r={}}
      if(r.ok){closeUpload();V("bar").style.display="none";V("barfill").style.width="0";pickedFile=null;V("file").value="";V("vtitle").value="";V("vdesc").value="";V("drop").textContent="Нажми и выбери видеофайл";V("gobtn").disabled=true;location.hash="#/watch/"+id}
      else{V("uerr").textContent=r.error||"ошибка загрузки";V("gobtn").disabled=false}};
    xhr.onerror=function(){V("uerr").textContent="сеть отвалилась";V("gobtn").disabled=false};
    xhr.send(pickedFile);
  });
}

function route(){var h=location.hash||"#/";
  if(h.indexOf("#/watch/")===0){watch(h.slice(8));return}
  if(h.indexOf("#/channel/")===0){feed(null,decodeURIComponent(h.slice(10)));return}
  if(h.indexOf("#/search/")===0){var q=decodeURIComponent(h.slice(9));V("q").value=q;feed(q,null);return}
  feed(null,null);
}
window.addEventListener("hashchange",route);
renderMe();route();
</script></body></html>`;

function videosPage(req, res) {
    res.writeHead(200, { 'content-type': 'text/html; charset=utf-8' });
    res.end(VIDEOS_HTML);
}

function serverConsolePage(req, res) {
    res.writeHead(200, { 'content-type': 'text/html; charset=utf-8' });
    res.end(SERVER_HTML);
}

function appPage(req, res) {
    res.writeHead(200, { 'content-type': 'text/html; charset=utf-8' });
    res.end(APP_HTML);
}

function panelPage(req, res) {
    res.writeHead(200, { 'content-type': 'text/html; charset=utf-8' });
    res.end(PANEL_HTML);
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

/** Админ ставит команду в очередь конкретного мира «Вместе» (хоста) — без плагина. */
function adminWorldCommand(req, res) {
    readJsonBody(req, body => {
        if (!needStaff(body, res)) return;
        const host = clean(body.host, 32);
        const text = clean(body.text, 300);
        if (!host) return send(res, 400, { error: 'не указан игрок/мир' });
        if (!text) return send(res, 400, { error: 'пустая команда' });
        if (!policy.worldCommands) policy.worldCommands = {};
        if (!policy.worldCommands[host]) policy.worldCommands[host] = [];
        policy.worldCommands[host].push(text);
        if (policy.worldCommands[host].length > 100) policy.worldCommands[host].shift();
        savePolicy();
        log('команда для мира', host, 'поставлена в очередь:', text);
        send(res, 200, { ok: true });
    });
}

/** Роутер спрашивает: на каком порту тоннель мира игрока <name> (ник хоста). */
function resolveName(req, res) {
    const q = query(req);
    const name = (typeof q.name === 'string' ? q.name : '').toLowerCase();
    if (!name) return send(res, 400, { error: 'нет имени' });
    for (const room of rooms.values()) {
        if (now() - room.seen > ROOM_LIFE) continue;
        if ((room.nick || '').toLowerCase() === name && room.mode === 'tunnel' && room.publicPort) {
            return send(res, 200, { ok: true, port: room.publicPort, nick: room.nick });
        }
    }
    send(res, 404, { error: 'мир не найден или закрыт' });
}

/** Лаунчер шлёт строки консоли своего сервера (авторизация — токеном комнаты). */
function serverPush(req, res) {
    readJsonBody(req, body => {
        const room = body && body.token ? rooms.get(body.token) : null;
        if (!room) return send(res, 403, { error: 'нет комнаты' });
        const name = room.nick;
        if (!policy.srvLog[name]) policy.srvLog[name] = [];
        const lines = Array.isArray(body.lines) ? body.lines : [];
        for (const l of lines) policy.srvLog[name].push(String(l).slice(0, 500));
        if (policy.srvLog[name].length > 400) policy.srvLog[name] = policy.srvLog[name].slice(-400);
        if (body.stats && typeof body.stats === 'object') {
            policy.srvStat[name] = {
                ram: Number(body.stats.ram) || 0,
                up: Number(body.stats.up) || 0,
                players: Number(body.stats.players) || 0,
                at: now()
            };
        }
        send(res, 200, { ok: true });
    });
}

/** Лаунчер забирает команды из веб-консоли для своего сервера. */
function serverPull(req, res) {
    readJsonBody(req, body => {
        const room = body && body.token ? rooms.get(body.token) : null;
        if (!room) return send(res, 403, { error: 'нет комнаты' });
        const name = room.nick;
        const out = policy.srvCmd[name] || [];
        if (out.length) { policy.srvCmd[name] = []; }
        send(res, 200, { commands: out });
    });
}

function srvOwner(token, name) {
    const nick = sessionNick(token);
    if (!nick) return false;
    const n = String(name || '').toLowerCase();
    return n === nick.toLowerCase() || n.startsWith(nick.toLowerCase() + '-') || nick === ADMIN;
}

/** Веб: кнопка питания (start/stop/restart) — в очередь владельцу. */
function serverControl(req, res) {
    readJsonBody(req, body => {
        const name = clean(body.name, 48);
        const action = clean(body.action, 12);
        if (!srvOwner(body.token, name)) return send(res, 403, { error: 'не ваш сервер' });
        if (['start', 'stop', 'restart'].indexOf(action) < 0) return send(res, 400, { error: 'действие?' });
        const owner = sessionNick(body.token);
        if (!policy.srvCtl[owner]) policy.srvCtl[owner] = [];
        policy.srvCtl[owner].push({ name, action });
        if (policy.srvCtl[owner].length > 50) policy.srvCtl[owner].shift();
        send(res, 200, { ok: true });
    });
}

/** Лаунчер-агент забирает кнопки питания для серверов этого аккаунта. */
function serverAgent(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const out = policy.srvCtl[nick] || [];
        if (out.length) policy.srvCtl[nick] = [];
        send(res, 200, { controls: out });
    });
}

/** Веб-консоль: владелец читает лог своего сервера. */
function serverWebLog(req, res) {
    readJsonBody(req, body => {
        const name = clean(body.name, 48);
        if (!srvOwner(body.token, name)) return send(res, 403, { error: 'не ваш сервер' });
        const st = policy.srvStat[name] || null;
        send(res, 200, { ok: true, lines: policy.srvLog[name] || [], online: !!(rooms2byNick(name)), stats: st });
    });
}

/** Веб-консоль: владелец шлёт команду в stdin сервера. */
function serverWebCmd(req, res) {
    readJsonBody(req, body => {
        const name = clean(body.name, 48);
        const text = clean(body.text, 300);
        if (!srvOwner(body.token, name)) return send(res, 403, { error: 'не ваш сервер' });
        if (!text) return send(res, 400, { error: 'пусто' });
        if (!policy.srvCmd[name]) policy.srvCmd[name] = [];
        policy.srvCmd[name].push(text);
        if (policy.srvCmd[name].length > 100) policy.srvCmd[name].shift();
        send(res, 200, { ok: true });
    });
}

function rooms2byNick(name) {
    const low = String(name || '').toLowerCase();
    for (const r of rooms.values()) if ((r.nick || '').toLowerCase() === low && now() - r.seen < ROOM_LIFE) return r;
    return null;
}

/** Мод-хост забирает команды для своего мира «Вместе». Авторизация — токеном комнаты. */
function worldCommandsPull(req, res) {
    readJsonBody(req, body => {
        const tok = body && body.token;
        const room = tok ? rooms.get(tok) : null;
        if (!room) return send(res, 403, { error: 'нет такой комнаты' });
        const nick = room.nick;
        const list = (policy.worldCommands && policy.worldCommands[nick]) || [];
        if (list.length > 0) { policy.worldCommands[nick] = []; savePolicy(); }
        send(res, 200, { commands: list });
    });
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

function displayName(nick) {
    const a = accounts[nick] ? acc(nick) : null;
    return a && a.chatName ? a.chatName : nick;
}
function isOnline(nick) {
    const a = accounts[nick] ? acc(nick) : null;
    return !!(a && a.lastSeen && (now() - a.lastSeen) < ONLINE_WINDOW);
}

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

        const a = acc(nick);
        const list = chatList(nick);
        list.push({ id: crypto.randomBytes(6).toString('hex'), from: nick, fromName: displayName(nick), tag: tagLabel(a.tag), admin: false, text, at: now() });
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

        const ame = acc(me);
        const list = chatList(nick);
        list.push({ id: crypto.randomBytes(6).toString('hex'), from: me, fromName: displayName(me), tag: ame ? tagLabel(ame.tag) : '', admin: true, text, at: now() });
        if (list.length > 200) list.shift();
        savePolicy();

        send(res, 200, { ok: true, messages: list });
    });
}

/** Редактировать сообщение: своё — любой, чужое — только персонал. */
function chatEdit(req, res) {
    readJsonBody(req, body => {
        const me = sessionNick(body && body.token);
        if (!me) return send(res, 403, { error: 'нужен вход' });
        const owner = clean(body.owner, 16);
        const id = clean(body.id, 32);
        const text = clean(body.text, 500);
        if (!text) return send(res, 400, { error: 'пустое сообщение' });
        const list = policy.chats[owner];
        if (!list) return send(res, 404, { error: 'нет чата' });
        const m = list.find(x => x.id === id);
        if (!m) return send(res, 404, { error: 'нет сообщения' });
        if (!(isStaff(me) || m.from === me)) return send(res, 403, { error: 'можно менять только своё' });
        m.text = text;
        m.edited = true;
        savePolicy();
        send(res, 200, { ok: true });
    });
}

/** Удалить сообщение: своё — любой, чужое — только персонал. */
function chatDelete(req, res) {
    readJsonBody(req, body => {
        const me = sessionNick(body && body.token);
        if (!me) return send(res, 403, { error: 'нужен вход' });
        const owner = clean(body.owner, 16);
        const id = clean(body.id, 32);
        const list = policy.chats[owner];
        if (!list) return send(res, 404, { error: 'нет чата' });
        const m = list.find(x => x.id === id);
        if (!m) return send(res, 404, { error: 'нет сообщения' });
        if (!(isStaff(me) || m.from === me)) return send(res, 403, { error: 'можно удалять только своё' });
        m.deleted = true;
        m.text = '';
        savePolicy();
        send(res, 200, { ok: true });
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
                deleted: !!policy.deletedNames[nick],
                online: isOnline(nick), display: displayName(nick), tag: tagLabel((accounts[nick] ? acc(nick).tag : ''))
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

    // живые выделенные серверы «Мои серверы» — показываем, пока запущены
    for (const room of rooms.values()) {
        if (!room.dedic) continue;
        if (now() - (room.seen || 0) > ROOM_LIFE) continue;
        const nk = room.nick || '';
        const dash = nk.indexOf('-');
        const owner = dash > 0 ? nk.slice(0, dash) : nk;
        out.push({
            id: 'dedic-' + nk,
            owner: owner,
            name: nk,
            ip: nk + '.narelauncher.winnigames2024.ru',
            hideIp: false,
            version: room.version || '',
            loader: room.loader || '',
            desc: 'Выделенный сервер NareLauncher (запущен на ПК владельца)',
            reward: COIN_REWARD,
            dedic: true
        });
    }
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

// ------------------------------------------------------------ теги и имя

function tagsList(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const a = acc(nick);
        send(res, 200, { ok: true, catalog: TAGS, owned: a.tags, equipped: a.tag,
                         coins: a.coins, chatName: a.chatName, chatNamePrice: CHATNAME_PRICE });
    });
}

function tagsBuy(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const id = clean(body.id, 16);
        const tag = TAGS.find(x => x.id === id);
        if (!tag) return send(res, 404, { error: 'нет такого тега' });
        const a = acc(nick);
        if (a.tags.indexOf(id) >= 0) return send(res, 400, { error: 'этот тег уже у вас есть' });
        if (a.coins < tag.price) return send(res, 402, { error: 'нужно ' + tag.price + ' монет, у вас ' + a.coins });
        a.coins -= tag.price;
        a.tags.push(id);
        a.tag = id;   // сразу надеваем
        saveAccounts();
        send(res, 200, { ok: true, coins: a.coins, owned: a.tags, equipped: a.tag });
    });
}

function tagsEquip(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const id = clean(body.id, 16);
        const a = acc(nick);
        if (id && a.tags.indexOf(id) < 0) return send(res, 400, { error: 'этого тега у вас нет' });
        a.tag = id;   // '' = снять
        saveAccounts();
        send(res, 200, { ok: true, equipped: a.tag });
    });
}

function chatNameSet(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const name = clean(body.name, 20);
        if (!name) return send(res, 400, { error: 'введите имя' });
        if (name.length < 2) return send(res, 400, { error: 'имя слишком короткое' });
        // нельзя выдавать себя за чужой аккаунт
        if (accounts[name] && name !== nick) return send(res, 409, { error: 'это ник другого аккаунта' });
        const a = acc(nick);
        if (a.chatName === name) return send(res, 400, { error: 'это имя уже стоит' });
        if (a.coins < CHATNAME_PRICE) return send(res, 402, { error: 'нужно ' + CHATNAME_PRICE + ' монет, у вас ' + a.coins });
        a.coins -= CHATNAME_PRICE;
        a.chatName = name;
        saveAccounts();
        send(res, 200, { ok: true, coins: a.coins, chatName: a.chatName });
    });
}

function chatNameClear(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const a = acc(nick);
        a.chatName = '';
        saveAccounts();
        send(res, 200, { ok: true });
    });
}

// ------------------------------------------------------------ профиль

function shotPath(id) { return path.join(SHOTS_DIR, id + '.png'); }

/** Профиль игрока: статус, статистика, список скриншотов. */
// ======================= ВИДЕО (как YouTube) =======================
function vidPath(id) { return path.join(VIDEOS_DIR, String(id).replace(/[^0-9a-f]/gi, '') + '.bin'); }
function vidById(id) { return policy.videos.find(v => v.id === id) || null; }
function vidPublic(v, full) {
    const o = {
        id: v.id, nick: v.nick, user: v.user || v.nick, title: v.title, desc: full ? (v.desc || '') : undefined,
        thumb: v.thumb || '', mime: v.mime || 'video/mp4', at: v.at, dur: v.dur || 0,
        views: v.views || 0, likes: (v.likes || []).length, ready: !!v.ready
    };
    if (full) o.comments = (v.comments || []).map(c => ({ user: c.user, nick: c.nick, text: c.text, at: c.at }));
    return o;
}
function vidsTotal() {
    let t = 0;
    try { for (const n of fs.readdirSync(VIDEOS_DIR)) { try { t += fs.statSync(path.join(VIDEOS_DIR, n)).size; } catch (e) {} } } catch (e) {}
    return t;
}

function videoCreate(req, res) {
    readJsonBodyLimit(req, SHOT_MAX, body => {
        if (!body) return send(res, 413, { error: 'слишком большое превью' });
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const title = clean(body.title, 120) || 'Без названия';
        const desc = clean(body.desc, 2000);
        const mime = /^video\/(mp4|webm|ogg)$/.test(body.mime || '') ? body.mime : 'video/mp4';
        let thumb = String(body.thumb || '');
        if (thumb && !/^data:image\/(png|jpeg|webp);base64,/.test(thumb)) thumb = '';
        if (thumb.length > 400 * 1024) thumb = '';
        const a = acc(nick);
        const v = {
            id: crypto.randomBytes(8).toString('hex'), nick, user: a.username || nick,
            title, desc, mime, thumb, at: now(), dur: Number(body.dur) || 0,
            views: 0, likes: [], comments: [], ready: false
        };
        policy.videos.push(v);
        if (policy.videos.length > 5000) policy.videos = policy.videos.slice(-5000);
        savePolicy();
        send(res, 200, { ok: true, id: v.id });
    });
}

function videoRaw(req, res, id) {
    const v = vidById(id);
    if (!v) return send(res, 404, { error: 'нет такого видео' });
    const nick = sessionNick(req.headers['x-nare-token'] || query(req).token);
    if (!nick || nick !== v.nick) return send(res, 403, { error: 'не ваше видео' });
    const declared = Number(req.headers['content-length']) || 0;
    if (declared > VIDEO_FILE_MAX) return send(res, 413, { error: 'ролик больше ' + (VIDEO_FILE_MAX / 1048576) + ' МБ' });
    if (vidsTotal() + (declared || 1) > VIDEOS_MAX) return send(res, 507, { error: 'нет места для видео' });
    const full = vidPath(id), temp = full + '.part';
    const out = fs.createWriteStream(temp);
    let size = 0, bad = false;
    const stop = (code, b) => { if (bad) return; bad = true; out.destroy(); try { fs.unlinkSync(temp); } catch (e) {} req.destroy(); send(res, code, b); };
    req.on('data', ch => { size += ch.length; if (size > VIDEO_FILE_MAX) return stop(413, { error: 'ролик слишком большой' }); out.write(ch); });
    req.on('error', () => stop(400, { error: 'передача прервалась' }));
    req.on('end', () => {
        if (bad) return;
        out.end(() => {
            try { fs.renameSync(temp, full); } catch (e) { return send(res, 500, { error: 'не записалось' }); }
            v.size = size; v.ready = true; savePolicy();
            send(res, 200, { ok: true, id, size });
        });
    });
}

function videoStream(req, res, id) {
    const v = vidById(id);
    if (!v) return send(res, 404, { error: 'нет такого видео' });
    const full = vidPath(id);
    let info;
    try { info = fs.statSync(full); } catch (e) { return send(res, 404, { error: 'файл видео не готов' }); }
    const mime = v.mime || 'video/mp4';
    const range = req.headers.range;
    if (range) {
        const m = /bytes=(\d*)-(\d*)/.exec(range) || [];
        let start = m[1] ? parseInt(m[1], 10) : 0;
        let end = m[2] ? parseInt(m[2], 10) : info.size - 1;
        if (isNaN(start) || start < 0) start = 0;
        if (isNaN(end) || end >= info.size) end = info.size - 1;
        if (start > end) { res.writeHead(416, { 'Content-Range': 'bytes */' + info.size }); return res.end(); }
        res.writeHead(206, {
            'Content-Type': mime, 'Accept-Ranges': 'bytes',
            'Content-Range': 'bytes ' + start + '-' + end + '/' + info.size,
            'Content-Length': (end - start + 1), 'Cache-Control': 'public, max-age=604800'
        });
        return fs.createReadStream(full, { start, end }).pipe(res);
    }
    res.writeHead(200, { 'Content-Type': mime, 'Accept-Ranges': 'bytes', 'Content-Length': info.size, 'Cache-Control': 'public, max-age=604800' });
    fs.createReadStream(full).pipe(res);
}

function videoList(req, res) {
    const q = query(req);
    const ch = clean(q.channel, 16).toLowerCase();
    const term = clean(q.q, 60).toLowerCase();
    let list = policy.videos.filter(v => v.ready);
    if (ch) list = list.filter(v => v.nick === ch);
    if (term) list = list.filter(v => (v.title || '').toLowerCase().indexOf(term) >= 0 || (v.user || '').toLowerCase().indexOf(term) >= 0);
    list = list.slice().sort((a, b) => b.at - a.at).slice(0, 200).map(v => vidPublic(v, false));
    send(res, 200, { ok: true, videos: list });
}

function videoGet(req, res) {
    const v = vidById(clean(query(req).id, 20));
    if (!v || !v.ready) return send(res, 404, { error: 'нет такого видео' });
    const a = accounts[v.nick] ? acc(v.nick) : null;
    send(res, 200, { ok: true, video: vidPublic(v, true),
        channel: { nick: v.nick, user: v.user || v.nick, subs: a ? (a.subs || [v.nick]).length : 0, status: a ? (a.status || '') : '' } });
}

function videoView(req, res) {
    const v = vidById(clean((query(req).id) || '', 20));
    if (v) { v.views = (v.views || 0) + 1; savePolicy(); }
    send(res, 200, { ok: true });
}

function videoLike(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const v = vidById(clean(body.id, 20));
        if (!v) return send(res, 404, { error: 'нет видео' });
        v.likes = v.likes || [];
        const i = v.likes.indexOf(nick);
        if (i >= 0) v.likes.splice(i, 1); else v.likes.push(nick);
        savePolicy();
        send(res, 200, { ok: true, likes: v.likes.length, liked: i < 0 });
    });
}

function videoComment(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const v = vidById(clean(body.id, 20));
        if (!v) return send(res, 404, { error: 'нет видео' });
        const text = clean(body.text, 500);
        if (!text) return send(res, 400, { error: 'пусто' });
        const a = acc(nick);
        v.comments = v.comments || [];
        v.comments.push({ nick, user: a.username || nick, text, at: now() });
        if (v.comments.length > 1000) v.comments = v.comments.slice(-1000);
        savePolicy();
        send(res, 200, { ok: true });
    });
}

function videoDelete(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const v = vidById(clean(body.id, 20));
        if (!v) return send(res, 404, { error: 'нет видео' });
        if (v.nick !== nick && !isAdmin(nick)) return send(res, 403, { error: 'не ваше видео' });
        try { fs.unlinkSync(vidPath(v.id)); } catch (e) {}
        policy.videos = policy.videos.filter(x => x.id !== v.id);
        savePolicy();
        send(res, 200, { ok: true });
    });
}

// ---- выделенные серверы «Мои серверы»: хранение списка/путей на нашем сервере ----
function hostSave(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const arr = Array.isArray(body.servers) ? body.servers : [];
        const out = arr.slice(0, 50).map(x => ({
            id: clean(x.id, 40),
            folder: clean(x.folder, 300),
            port: Math.min(65535, Math.max(1, Number(x.port) || 25600))
        })).filter(x => x.id);
        policy.hostServers[nick] = out;
        savePolicy();
        send(res, 200, { ok: true, count: out.length });
    });
}
function hostList(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        send(res, 200, { ok: true, servers: policy.hostServers[nick] || [] });
    });
}

// ========================= ЭВЕНТЫ =========================
function evById(id) { return policy.events.find(e => e.id === id) || null; }
function evPublic(e, joined) {
    return {
        id: e.id, name: e.name, desc: e.desc || '', accent: e.accent || '',
        coins: e.coins || 0, active: !!e.active,
        notifyTitle: e.notifyTitle || '', notifyText: e.notifyText || '',
        images: e.images || [],
        joined: !!joined
    };
}

// список активных эвентов для приложения (+ отметка, заходил ли уже)
function eventsActive(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        const out = policy.events.filter(e => e.active).map(e => evPublic(e, nick && e.joined && e.joined[nick]));
        send(res, 200, { ok: true, events: out });
    });
}

// заход на эвент: один раз начисляет монеты и отдаёт уведомление
function eventsJoin(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const e = evById(clean(body.id, 20));
        if (!e || !e.active) return send(res, 404, { error: 'эвент не активен' });
        if (!e.joined) e.joined = {};
        let awarded = 0;
        const a = acc(nick);
        if (!e.joined[nick]) {
            e.joined[nick] = now();
            awarded = Math.max(0, Number(e.coins) || 0);
            if (awarded > 0) { a.coins = (a.coins || 0) + awarded; saveAccounts(); }
            savePolicy();
        }
        send(res, 200, {
            ok: true, awarded, coins: a.coins || 0, first: awarded > 0 || (e.joined[nick] === now()),
            notify: { title: e.notifyTitle || '', text: e.notifyText || '' }, name: e.name
        });
    });
}

// загрузка картинки эвента (админ): PUT сырые байты, заголовок x-nare-token
function eventImgUpload(req, res) {
    const nick = sessionNick(req.headers['x-nare-token'] || query(req).token);
    if (!isAdmin(nick)) return send(res, 403, { error: 'нужен админ' });
    const mime = String(req.headers['content-type'] || '');
    if (!/^image\/(png|jpeg|jpg|webp|gif)$/.test(mime)) return send(res, 400, { error: 'только картинки' });
    const declared = Number(req.headers['content-length']) || 0;
    if (declared > EVTIMG_MAX) return send(res, 413, { error: 'картинка больше 8 МБ' });
    const id = crypto.randomBytes(8).toString('hex');
    const full = path.join(EVTIMG_DIR, id + '.bin'), temp = full + '.part';
    const out = fs.createWriteStream(temp);
    let size = 0, bad = false;
    const stop = (c, b) => { if (bad) return; bad = true; out.destroy(); try { fs.unlinkSync(temp); } catch (e) {} req.destroy(); send(res, c, b); };
    req.on('data', ch => { size += ch.length; if (size > EVTIMG_MAX) return stop(413, { error: 'слишком большая' }); out.write(ch); });
    req.on('error', () => stop(400, { error: 'передача прервалась' }));
    req.on('end', () => {
        if (bad) return;
        out.end(() => {
            try { fs.renameSync(temp, full); } catch (e) { return send(res, 500, { error: 'не записалось' }); }
            policy.eventImgs[id] = mime; savePolicy();
            send(res, 200, { ok: true, url: '/events/img/' + id });
        });
    });
}
function eventImgServe(req, res, id) {
    const mime = policy.eventImgs[id];
    const full = path.join(EVTIMG_DIR, id + '.bin');
    let info;
    try { info = fs.statSync(full); } catch (e) { return send(res, 404, { error: 'нет картинки' }); }
    res.writeHead(200, { 'Content-Type': mime || 'image/png', 'Content-Length': info.size, 'Cache-Control': 'public, max-age=604800' });
    fs.createReadStream(full).pipe(res);
}

// ---- админ ----
function eventsAll(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        send(res, 200, { ok: true, events: policy.events.map(e => ({
            id: e.id, name: e.name, desc: e.desc || '', accent: e.accent || '', coins: e.coins || 0,
            active: !!e.active, notifyTitle: e.notifyTitle || '', notifyText: e.notifyText || '',
            images: e.images || [], joins: e.joined ? Object.keys(e.joined).length : 0
        })) });
    });
}
function eventsSave(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        const name = clean(body.name, 80);
        if (!name) return send(res, 400, { error: 'нужно название' });
        let e = body.id ? evById(clean(body.id, 20)) : null;
        if (!e) {
            e = { id: crypto.randomBytes(6).toString('hex'), active: false, joined: {}, at: now() };
            policy.events.push(e);
        }
        e.name = name;
        e.desc = clean(body.desc, 400);
        e.accent = /^#?[0-9a-fA-F]{6}$/.test(body.accent || '') ? (body.accent[0] === '#' ? body.accent : '#' + body.accent) : '';
        e.coins = Math.max(0, Math.min(100000, Math.trunc(Number(body.coins) || 0)));
        e.notifyTitle = clean(body.notifyTitle, 80);
        e.notifyText = clean(body.notifyText, 400);
        if (Array.isArray(body.images)) e.images = body.images.slice(0, 80).map(u => clean(u, 300)).filter(Boolean);
        savePolicy();
        send(res, 200, { ok: true, id: e.id });
    });
}
function eventsState(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        const e = evById(clean(body.id, 20));
        if (!e) return send(res, 404, { error: 'нет эвента' });
        e.active = !!body.active;
        savePolicy();
        send(res, 200, { ok: true, active: e.active });
    });
}
function eventsDelete(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        policy.events = policy.events.filter(e => e.id !== clean(body.id, 20));
        savePolicy();
        send(res, 200, { ok: true });
    });
}

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
        deleted: !!policy.deletedNames[nick],
        online: isOnline(nick), lastSeen: a.lastSeen || 0,
        tag: tagLabel(a.tag), chatName: a.chatName || ''
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
            out.push({ nick: n, username: a.username || n, coins: a.coins || 0, online: isOnline(n), tag: tagLabel(a.tag) });
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
    if (route === '/app' || route === '/' || route === '') return appPage(req, res);
    if (route === '/panel' || route === '/panel/') return panelPage(req, res);
    if (route === '/admin/logs' && req.method === 'POST') return adminLogs(req, res);
    if (route === '/status') return status(req, res);
    if (route === '/appeal' && req.method === 'POST') return appeal(req, res);

    // чат по бану
    if (route === '/ban/chat' && req.method === 'POST') return banChatGet(req, res);
    if (route === '/ban/send' && req.method === 'POST') return banChatSend(req, res);
    if (route === '/admin/chat' && req.method === 'POST') return adminChatGet(req, res);
    if (route === '/admin/chat/send' && req.method === 'POST') return adminChatSend(req, res);
    if (route === '/admin/chats' && req.method === 'POST') return adminChatsList(req, res);
    if (route === '/chat/edit' && req.method === 'POST') return chatEdit(req, res);
    if (route === '/chat/delete' && req.method === 'POST') return chatDelete(req, res);
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
    if (route === '/admin/worldcommand' && req.method === 'POST') return adminWorldCommand(req, res);
    if (route === '/world/commands' && req.method === 'POST') return worldCommandsPull(req, res);
    if (route === '/server/push' && req.method === 'POST') return serverPush(req, res);
    if (route === '/server/pull' && req.method === 'POST') return serverPull(req, res);
    if (route === '/server/weblog' && req.method === 'POST') return serverWebLog(req, res);
    if (route === '/server/webcmd' && req.method === 'POST') return serverWebCmd(req, res);
    if (route === '/server/control' && req.method === 'POST') return serverControl(req, res);
    if (route === '/server/agent' && req.method === 'POST') return serverAgent(req, res);
    if (route === '/server') return serverConsolePage(req, res);
    if (route === '/resolve') return resolveName(req, res);

    // реклама серверов и монеты
    if (route === '/servers') return serversPublic(req, res);
    if (route === '/servers/submit' && req.method === 'POST') return serverSubmit(req, res);
    if (route === '/servers/mine' && req.method === 'POST') return serversMine(req, res);
    if (route === '/coins/earn' && req.method === 'POST') return coinsEarn(req, res);
    if (route === '/coins/daily' && req.method === 'POST') return coinsDaily(req, res);
    if (route === '/coins/send' && req.method === 'POST') return coinsSend(req, res);
    if (route === '/top') return coinsTop(req, res);
    if (route === '/profile/get') return profileGet(req, res);
    if (route === '/host/save' && req.method === 'POST') return hostSave(req, res);
    if (route === '/events' && req.method === 'POST') return eventsActive(req, res);
    if (route === '/events/join' && req.method === 'POST') return eventsJoin(req, res);
    if (route === '/events/img/upload' && (req.method === 'PUT' || req.method === 'POST')) return eventImgUpload(req, res);
    if (route.startsWith('/events/img/')) return eventImgServe(req, res, clean(route.slice(12), 20));
    if (route === '/events/all' && req.method === 'POST') return eventsAll(req, res);
    if (route === '/events/save' && req.method === 'POST') return eventsSave(req, res);
    if (route === '/events/state' && req.method === 'POST') return eventsState(req, res);
    if (route === '/events/delete' && req.method === 'POST') return eventsDelete(req, res);
    if (route === '/host/list' && req.method === 'POST') return hostList(req, res);
    if (route === '/video/create' && req.method === 'POST') return videoCreate(req, res);
    if (route.startsWith('/video/raw/') && (req.method === 'PUT' || req.method === 'POST')) return videoRaw(req, res, clean(route.slice(11), 20));
    if (route.startsWith('/video/stream/')) return videoStream(req, res, clean(route.slice(14), 20));
    if (route === '/video/list') return videoList(req, res);
    if (route === '/video/get') return videoGet(req, res);
    if (route === '/video/view' && req.method === 'POST') return videoView(req, res);
    if (route === '/video/like' && req.method === 'POST') return videoLike(req, res);
    if (route === '/video/comment' && req.method === 'POST') return videoComment(req, res);
    if (route === '/video/delete' && req.method === 'POST') return videoDelete(req, res);
    if (route === '/videos' || route === '/video') return videosPage(req, res);
    if (route === '/profile/status' && req.method === 'POST') return profileStatus(req, res);
    if (route === '/profile/shot/add' && req.method === 'POST') return profileShotAdd(req, res);
    if (route === '/profile/shot/del' && req.method === 'POST') return profileShotDel(req, res);
    if (route === '/profile/image') return profileImage(req, res);
    if (route === '/search') return searchPlayers(req, res);
    if (route === '/tags/list' && req.method === 'POST') return tagsList(req, res);
    if (route === '/tags/buy' && req.method === 'POST') return tagsBuy(req, res);
    if (route === '/tags/equip' && req.method === 'POST') return tagsEquip(req, res);
    if (route === '/chatname/set' && req.method === 'POST') return chatNameSet(req, res);
    if (route === '/chatname/clear' && req.method === 'POST') return chatNameClear(req, res);
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
