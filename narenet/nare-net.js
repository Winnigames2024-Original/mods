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

const VERSION = '1.49';

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
    srvFsReq: {},                               // имя -> [ {id,action,path,content} ] запросы от веба
    srvFsRes: {},                               // имя -> { id: result } ответы лаунчера
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
if (!policy.srvFsReq) policy.srvFsReq = {};
if (!policy.srvFsRes) policy.srvFsRes = {};
if (!policy.videos) policy.videos = [];
if (!policy.hostServers) policy.hostServers = {};
if (!policy.remote) policy.remote = {};          // nick -> { salt, hash, enabled }
const SEED_EVENTS = [{"key": "ev67", "name": "ЭВЕНТ 67", "desc": "Лови монеты за вход!", "accent": "5b8cff", "coins": 67, "notifyTitle": "Эвент 67!", "notifyText": "Держи 67 монет 🎉", "img": "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", "mime": "image/jpeg"}, {"key": "evwin", "name": "ЗИМНИЙ ЭВЕНТ", "desc": "Снег и награды", "accent": "4fbaff", "coins": 50, "notifyTitle": "Зима пришла! ❄", "notifyText": "Бонус за вход", "img": "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", "mime": "image/jpeg"}, {"key": "evx2", "name": "ДВОЙНЫЕ МОНЕТЫ", "desc": "Удвоенная награда", "accent": "f0b24a", "coins": 100, "notifyTitle": "x2 монеты!", "notifyText": "Удвоенная награда за заход", "img": "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", "mime": "image/jpeg"}, {"key": "evwknd", "name": "ВЫХОДНЫЕ", "desc": "Отдыхай с бонусом", "accent": "56d364", "coins": 30, "notifyTitle": "Хороших выходных!", "notifyText": "Бонус за вход в выходные", "img": "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", "mime": "image/jpeg"}, {"key": "evsum", "name": "ЛЕТНИЙ ЭВЕНТ", "desc": "Солнечная раздача", "accent": "ff8a3d", "coins": 60, "notifyTitle": "Лето! ☀", "notifyText": "Солнечный бонус монет", "img": "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", "mime": "image/jpeg"}, {"key": "evmega", "name": "МЕГА РАЗДАЧА", "desc": "Огромные награды", "accent": "ff5f8f", "coins": 200, "notifyTitle": "МЕГА! 💰", "notifyText": "Огромная раздача монет", "img": "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", "mime": "image/jpeg"}];
function seedEvents() {
    const SEED_VER = 2;
    if ((policy.eventSeedV | 0) >= SEED_VER) return;
    const oldNames = {
        'Эвент 67': 'ev67', 'Зимний эвент': 'evwin', 'Двойные монеты': 'evx2',
        'Выходные': 'evwknd', 'Летний эвент': 'evsum', 'Мега раздача': 'evmega'
    };
    for (const pz of SEED_EVENTS) {
        // новая картинка
        const imgId = crypto.randomBytes(6).toString('hex');
        try {
            fs.writeFileSync(path.join(EVTIMG_DIR, imgId + '.bin'), Buffer.from(pz.img, 'base64'));
            policy.eventImgs[imgId] = pz.mime || 'image/jpeg';
        } catch (e) {}
        const imgUrl = '/events/img/' + imgId;
        // найти уже созданный пресет (по ключу или по старому имени)
        let e = policy.events.find(x => x.seedKey === pz.key) ||
                policy.events.find(x => oldNames[x.name] === pz.key && !x.seedKey);
        if (e) {
            // обновляем только визуал, сохраняем монеты/уведомление/активность/заходы
            e.seedKey = pz.key;
            e.images = [imgUrl];
            e.accent = pz.accent ? ('#' + String(pz.accent).replace(/^#/, '')) : '';
            if (!e.name) e.name = pz.name;
        } else {
            policy.events.push({
                id: crypto.randomBytes(6).toString('hex'), seedKey: pz.key,
                name: pz.name, desc: pz.desc || '',
                accent: pz.accent ? ('#' + String(pz.accent).replace(/^#/, '')) : '',
                coins: pz.coins || 0, notifyTitle: pz.notifyTitle || '', notifyText: pz.notifyText || '',
                images: [imgUrl], active: false, joined: {}, at: now()
            });
        }
    }
    policy.eventSeedV = SEED_VER;
    savePolicy();
    log('seed эвентов v' + SEED_VER + ' применён');
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

// --- удалённое управление лаунчером из веба ---
// policy.remote[nick] = { salt, hash, enabled } — пароль (хэш), который задаёт сам пользователь.
const remoteState  = new Map();                 // nick -> { jpg, w, h, at, enabled } последний кадр окна лаунчера
const remoteInput  = new Map();                 // nick -> [ событий мыши/клавиш, ожидающих применения в лаунчере ]
const remoteTokens = new Map();                 // rtoken -> { nick, exp } сессия веб-пульта (после пароля)
const REMOTE_TOKEN_LIFE = 30 * 60 * 1000;
const REMOTE_ONLINE = 9 * 1000;                 // кадр свежее этого => лаунчер на связи

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
        restartLaunchers: policy.restartLaunchers || 0,
        restartServers: policy.restartServers || 0,
        serverSlots: (a && a.serverSlots) || 0,
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
    <div class="row" style="margin-top:10px">
      <input id="bcText" placeholder="Текст объявления вверху приложения" style="flex:1;min-width:220px">
      <input id="bcSec" type="number" min="0" max="3600" value="10" title="сколько секунд показывать" style="width:90px">
      <button class="pri" onclick="sendBroadcast()">Показать всем</button>
      <button onclick="clearBroadcast()">Убрать</button>
    </div>
    <div class="muted" style="margin-top:4px">Ваш ник подставится сам. 0 секунд — висит, пока не уберёте.</div>
    <div class="row" style="margin-top:10px">
      <button class="red" onclick="restartAll('launchers')">⟳ Перезапустить лаунчеры у всех</button>
      <button class="red" onclick="restartAll('servers')">⟳ Перезапустить все серверы</button>
    </div>
    <div class="row" style="margin-top:10px">
      <input id="promoCode" placeholder="ПРОМОКОД" style="width:160px;text-transform:uppercase">
      <input id="promoVal" type="number" min="1" placeholder="монет" style="width:110px">
      <input id="promoMax" type="number" min="1" value="1" placeholder="активаций" style="width:120px">
      <button class="pri" onclick="createPromo()">Создать промокод</button>
    </div>
    <div class="muted" style="margin-top:4px">Игроки вводят код в профиле и получают монеты. «Активаций» — на сколько игроков хватит.</div>
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
function sendBroadcast(){var t=(document.getElementById("bcText").value||"").trim();if(!t){msg("впишите текст",false);return;}
  var s=parseInt(document.getElementById("bcSec").value,10)||0;
  api("/admin/broadcast",{text:t,seconds:s}).then(function(d){done(d,"объявление показано всем");});}
function clearBroadcast(){api("/admin/broadcast",{text:"",seconds:0}).then(function(d){done(d,"объявление убрано");});}
function restartAll(what){var n=what==="servers"?"все серверы":"лаунчеры у всех онлайн";
  if(!confirm("Точно перезапустить "+n+"?"))return;
  api("/admin/restart",{what:what}).then(function(d){done(d,"перезапуск разослан: "+n);});}
function createPromo(){var c=(document.getElementById("promoCode").value||"").trim();var v=parseInt(document.getElementById("promoVal").value,10)||0;var m=parseInt(document.getElementById("promoMax").value,10)||1;
  if(!c||!v){msg("код и сумма",false);return;}
  api("/admin/promo/create",{code:c,value:v,max:m}).then(function(d){done(d,d.ok?("промокод "+d.code+" на "+d.value+" монет × "+d.max):null,d);});}
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
      <button class="pri" onclick="daily()">Ежедневный бонус</button>
      <button class="pri" onclick="reward()">🎁 Награда дня</button></div>
    <div id="dailymsg" class="muted" style="margin-top:8px"></div>
    <div class="row" style="margin-top:12px"><input id="promoIn" placeholder="промокод" style="text-transform:uppercase"><button class="pri" onclick="redeemPromo()">Активировать</button></div>
    <div id="promomsg" class="muted" style="margin-top:6px"></div>
    <div style="margin-top:14px;border-top:1px solid #1b2232;padding-top:12px">
      <b>🎰 Рулетка</b> <span class="muted">— ставь монеты, умножай удачу (×0, ×1.5, ×2, ×5)</span>
      <div class="row" style="margin-top:8px"><input id="betIn" type="number" min="1" placeholder="ставка" style="width:140px"><button class="pri" onclick="spin()">Крутить</button></div>
      <div id="spinmsg" class="muted" style="margin-top:6px"></div>
    </div>
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
function reward(){api("/reward/daily",{}).then(function(d){
  if(!d.ok){document.getElementById("dailymsg").textContent=d.error||"ошибка";return;}
  document.getElementById("dailymsg").textContent=d.awarded?("Награда дня: +1000 монет и +1 сервер! Слотов: "+d.slots):("уже брали, через ~"+d.nextInH+" ч");
  if(d.coins!=null)document.getElementById("pcoins").textContent=d.coins;});}
function redeemPromo(){var c=(document.getElementById("promoIn").value||"").trim();if(!c)return;
  api("/promo/redeem",{code:c}).then(function(d){
    document.getElementById("promomsg").textContent=d.ok?("+"+d.value+" монет!"):(d.error||"ошибка");
    if(d.ok&&d.coins!=null)document.getElementById("pcoins").textContent=d.coins;});}
function spin(){var b=parseInt(document.getElementById("betIn").value,10)||0;if(b<1){document.getElementById("spinmsg").textContent="впишите ставку";return;}
  api("/roulette/spin",{bet:b}).then(function(d){
    if(!d.ok){document.getElementById("spinmsg").textContent=d.error||"ошибка";return;}
    document.getElementById("spinmsg").textContent=d.mult===0?("×0 — ставка "+d.bet+" сгорела"):("×"+d.mult+" — выигрыш "+d.win+" монет!");
    if(d.coins!=null)document.getElementById("pcoins").textContent=d.coins;});}
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
<meta name="viewport" content="width=device-width,initial-scale=1"><title>Панель сервера — NareLauncher</title>
<style>
*{box-sizing:border-box}body{margin:0;font-family:Segoe UI,Roboto,system-ui,sans-serif;background:#0a0d14;color:#e7eaf2}
.layout{display:flex;min-height:100vh}
.side{width:230px;flex-shrink:0;background:#0c0f18;border-right:1px solid #171d2b;padding:18px 14px}
.logo{font-size:19px;font-weight:800;padding:6px 10px 16px}.logo span{color:#5b8cff}
.seclbl{color:#525a70;font-size:11px;font-weight:700;letter-spacing:.09em;padding:0 10px 8px}
.nav a{display:flex;align-items:center;gap:11px;padding:10px 12px;border-radius:9px;color:#aeb6c8;font-size:14px;cursor:pointer;margin-bottom:3px}
.nav a:hover{background:#111726;color:#e7eaf2}.nav a.on{background:#16233f;color:#fff}
.content{flex:1;padding:20px 24px;max-width:1500px}
.panel{background:#10141f;border:1px solid #1b2232;border-radius:15px;padding:18px 20px;margin-bottom:14px}
.srvtitle{font-size:24px;font-weight:800;word-break:break-all}
.statusline{display:flex;align-items:center;gap:18px;flex-wrap:wrap;color:#9aa4ba;font-size:13px;margin-top:8px}
.statusline .run{color:#56d364;font-weight:700}.statusline .off{color:#ff6b74;font-weight:700}
.dot{width:9px;height:9px;border-radius:50%;background:#636b80}.dot.on{background:#56d364;box-shadow:0 0 8px #56d364}.dot.offc{background:#ff6b74}
.tabs{display:flex;gap:3px;margin-top:14px;overflow-x:auto;border-bottom:1px solid #1b2232}
.tabs a{white-space:nowrap;color:#8b93a7;font-size:13.5px;padding:9px 14px;cursor:pointer;border-bottom:2px solid transparent;margin-bottom:-1px}
.tabs a:hover{color:#dfe4ee}.tabs a.on{color:#fff;border-bottom-color:#5b8cff;font-weight:600}
.stats{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin-bottom:14px}
.stat{background:#10141f;border:1px solid #1b2232;border-radius:12px;padding:13px 15px}
.stat .lbl{color:#7b8498;font-size:11px;font-weight:700;text-transform:uppercase}.stat .val{font-size:19px;font-weight:800;margin-top:6px}
.pwr{display:flex;gap:9px;margin-bottom:14px}
.pwr button{border:0;border-radius:10px;padding:11px 20px;font-size:13px;font-weight:700;cursor:pointer;color:#fff}
.pwr .start{background:#17602f}.pwr .restart{background:#6a5415}.pwr .stop{background:#6a1f26}
.pwr button:disabled{opacity:.4;cursor:not-allowed}.pwr button:hover:not(:disabled){filter:brightness(1.2)}
.card{background:#10141f;border:1px solid #1b2232;border-radius:14px;overflow:hidden}
.cardhd{display:flex;align-items:center;gap:10px;padding:11px 16px;border-bottom:1px solid #1b2232;font-weight:700;font-size:14px}
.cardhd .sp{flex:1}
.btn{background:#1b2c52;border:0;color:#9db4ff;border-radius:8px;padding:8px 13px;font-size:12.5px;font-weight:700;cursor:pointer}
.btn:hover{background:#223a6b;color:#fff}.btn.red{background:#3a1518;color:#ff8a93}.btn.red:hover{background:#4e1b20}
.btn.green{background:#143a22;color:#73e39a}.btn.gray{background:#1a2130;color:#aeb6c8}
.console{height:560px;overflow:auto;padding:13px 16px;font-family:Consolas,Menlo,monospace;font-size:12.5px;line-height:1.55;color:#c2ccdd;white-space:pre-wrap;word-break:break-word;background:#070a11}
.console .in{color:#6fe39a}.console .warn{color:#f0c674}.console .err{color:#ff7b84}
.prompt{display:flex;align-items:center;border-top:1px solid #141b28;background:#0a0f18}
.prompt .c{color:#56d364;font-weight:800;padding:0 4px 0 15px;font-size:16px}
.prompt input{flex:1;background:transparent;border:0;outline:0;color:#e7eaf2;font-family:Consolas,monospace;font-size:13px;padding:13px 10px}
.prompt button{background:#1b2c52;border:0;color:#9db4ff;font-weight:700;padding:0 18px;cursor:pointer;align-self:stretch;border-radius:8px;margin:7px}
.flist{max-height:520px;overflow:auto}
.frow{display:flex;align-items:center;gap:10px;padding:9px 16px;border-bottom:1px solid #141b26;font-size:13.5px}
.frow:hover{background:#141b2b}.frow .ic{width:18px;text-align:center}.frow .nm{flex:1;cursor:pointer}.frow .sz{color:#7b8498;font-size:12px}
.frow .x{color:#ff8a93;padding:2px 8px;border-radius:6px;cursor:pointer}.frow .x:hover{background:#3a1518}
.crumb{padding:10px 16px;color:#8b93a7;font-size:12.5px;border-bottom:1px solid #1b2232}.crumb b{color:#dfe4ee}
.editor textarea{width:100%;height:480px;background:#070a11;border:0;color:#e7eaf2;font-family:Consolas,monospace;font-size:12.5px;padding:14px 16px;outline:none;resize:none;white-space:pre}
.empty{color:#7b8498;text-align:center;padding:40px}
.frm label{display:block;color:#8b93a7;font-size:12px;margin:12px 0 4px}.frm input,.frm select{width:100%;max-width:440px;background:#0a0d14;border:1px solid #242c3e;color:#e7eaf2;border-radius:8px;padding:9px 11px;font-size:13px}.frm .hint{color:#636b80;font-size:11px;margin-top:3px;max-width:440px}
.login{max-width:380px;margin:9vh auto;background:#10141f;border:1px solid #1b2232;border-radius:16px;padding:26px}
.login h2{margin:0 0 4px}.login .m{color:#8b93a7;font-size:13px;margin-bottom:16px}
.login input{width:100%;background:#0a0d14;border:1px solid #242c3e;color:#e7eaf2;border-radius:10px;padding:12px;font-size:14px;margin-bottom:10px}
.login button{width:100%;background:#2f52d8;border:0;color:#fff;border-radius:10px;padding:12px;font-size:15px;font-weight:700;cursor:pointer}
input:-webkit-autofill{-webkit-box-shadow:0 0 0 1000px #0a0d14 inset;-webkit-text-fill-color:#e7eaf2}
#lerr,#err{color:#ff8a98;font-size:13px}
.ava{width:30px;height:30px;border-radius:50%;background:#2f52d8;color:#fff;display:inline-flex;align-items:center;justify-content:center;font-weight:800}
.toast{position:fixed;right:18px;bottom:18px;background:#17233f;border:1px solid #2f52d8;color:#dfe6ff;padding:11px 16px;border-radius:10px;font-size:13px;opacity:0;transform:translateY(10px);transition:.25s;pointer-events:none;z-index:9}
.toast.show{opacity:1;transform:none}
@media(max-width:860px){.side{display:none}.stats{grid-template-columns:repeat(2,1fr)}}
</style></head><body>
<div id="login" class="login">
  <h2>Панель управления</h2><div class="m">Войдите под аккаунтом владельца сервера</div>
  <input id="u" placeholder="ник"><input id="p" type="password" placeholder="пароль">
  <button id="loginBtn">Войти</button><div id="lerr" style="margin-top:10px"></div>
</div>
<div id="app" class="layout" style="display:none">
  <div class="side">
    <div class="logo">Nare<span>Панель</span></div>
    <div class="seclbl">ОБЩЕЕ</div>
    <div class="nav" id="snav"></div>
  </div>
  <div class="content">
    <div class="panel">
      <div class="srvtitle" id="srv"></div>
      <div class="statusline">
        <div><span>🌐</span> <span id="addr">—</span></div>
        <div><span class="dot" id="sdot"></span> <span id="sstat" class="off">проверка…</span></div>
        <div id="who" style="margin-left:auto"></div>
      </div>
      <div class="tabs" id="tabs"></div>
    </div>
    <div class="stats">
      <div class="stat"><div class="lbl">CPU</div><div class="val" id="sCpu">—</div></div>
      <div class="stat"><div class="lbl">Память</div><div class="val" id="sMem">—</div></div>
      <div class="stat"><div class="lbl">Аптайм</div><div class="val" id="sUp">—</div></div>
      <div class="stat"><div class="lbl">Игроки</div><div class="val" id="sPl">—</div></div>
    </div>
    <div class="pwr">
      <button class="start" id="bStart" data-act="power" data-arg="start">▶ Запустить</button>
      <button class="restart" id="bRestart" data-act="power" data-arg="restart">↻ Рестарт</button>
      <button class="stop" id="bStop" data-act="power" data-arg="stop">■ Остановить</button>
    </div>
    <div id="err" style="margin-bottom:10px"></div>
    <div id="view"></div>
  </div>
</div>
<div class="toast" id="toast"></div>
<script>
function qs(k){var m=new RegExp("[?&]"+k+"=([^&]*)").exec(location.search);return m?decodeURIComponent(m[1]):"";}
function setCk(k,v){document.cookie=k+"="+encodeURIComponent(v)+";path=/;max-age=2592000;samesite=lax"}
function getCk(k){var a=document.cookie.split("; ");for(var i=0;i<a.length;i++){var e=a[i].indexOf("=");if(e>0&&a[i].slice(0,e)===k)return decodeURIComponent(a[i].slice(e+1))}return ""}
function delCk(k){document.cookie=k+"=;path=/;max-age=0"}
var name=qs("name"), token=getCk("srv_"+name)||"", MEMLIM=2048;
var tab="console", curPath="", editPath="", lastLines=[];
var histCpu=[], histMem=[], histPl=[], HIST=60;
var V=function(id){return document.getElementById(id)};
function api(path,body){body=body||{};body.token=token;body.name=name;
  return fetch(path,{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(body)}).then(function(r){return r.json()});}
function esc(s){return String(s==null?"":s).replace(/[&<>"]/g,function(c){return c==="&"?"&amp;":c==="<"?"&lt;":c===">"?"&gt;":"&quot;";});}
function at(s){return esc(s).replace(/"/g,"&quot;");}
function toast(t){var el=V("toast");el.textContent=t;el.classList.add("show");clearTimeout(el._t);el._t=setTimeout(function(){el.classList.remove("show")},2600);}
function ini(s){s=s||"?";return s.charAt(0).toUpperCase()}
function gib(mb){return (mb/1024).toFixed(2)}
function fmtUp(sec){sec=Math.max(0,sec|0);var d=Math.floor(sec/86400),h=Math.floor(sec%86400/3600),m=Math.floor(sec%3600/60);if(d)return d+"д "+h+"ч";if(h)return h+"ч "+m+"м";return m+"м";}
function fmtSz(n){if(n>=1048576)return (n/1048576).toFixed(1)+" МБ";if(n>=1024)return (n/1024).toFixed(0)+" КБ";return n+" Б";}

function login(){var u=V("u").value.trim(),p=V("p").value;V("lerr").textContent="";
  fetch("/account/login",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify({nick:u,password:p})})
   .then(function(r){return r.json()}).then(function(d){if(!d.ok){V("lerr").textContent=d.error||"не вышло";return;}
     token=d.token;setCk("srv_"+name,token);show();}).catch(function(){V("lerr").textContent="сервер недоступен";});}
function logout(){token="";delCk("srv_"+name);show();}

var TABS=[["console","Консоль"],["graph","Нагрузка"],["files","Файлы"],["players","Игроки"],["access","Доступ"],["plugins","Плагины"],["backups","Бэкапы"],["launch","Настройки"],["settings","server.properties"],["logs","Логи"]];
var NAV=TABS;
function tabsHtml(arr){var h="";for(var i=0;i<arr.length;i++){h+='<a data-act="go" data-arg="'+arr[i][0]+'">'+arr[i][1]+'</a>';}return h;}
function buildNav(){ V("tabs").innerHTML=tabsHtml(TABS); V("snav").innerHTML=tabsHtml(NAV)+'<a data-act="logout">Выход</a>'; }
function go(t){ tab=t; markTabs(); render(); }
function markTabs(){ var all=document.querySelectorAll("[data-act=go]"); for(var i=0;i<all.length;i++){ all[i].classList.toggle("on", all[i].getAttribute("data-arg")===tab); } }

document.addEventListener("click", function(e){
  var el=e.target; while(el && el!==document && !el.getAttribute("data-act")) el=el.parentNode;
  if(!el || el===document) return;
  var a=el.getAttribute("data-act"), g=el.getAttribute("data-arg")||"";
  if(a==="go")go(g); else if(a==="logout")logout(); else if(a==="cd")renderFiles(g);
  else if(a==="up")upDir(); else if(a==="open")openEditor(g); else if(a==="del")delFile(g);
  else if(a==="save")saveEditor(); else if(a==="cmd")pcmd(g); else if(a==="send")sendCmd();
  else if(a==="power")power(g); else if(a==="refresh")render(); else if(a==="poll")poll();
  else if(a==="launchsave")saveLaunch(); else if(a==="psearch")doPSearch(); else if(a==="pinstall")doPInstall(g); else if(a==="ban")doBan(g);
  else if(a==="backup")doBackup(); else if(a==="brestore")doRestore(g);
  else if(a==="wladd")listAdd("wl_add","wlin","вайтлист"); else if(a==="wldel")listAct("wl_remove",g,"вайтлист");
  else if(a==="opadd")listAdd("op_add","opin","операторы"); else if(a==="opdel")listAct("op_remove",g,"операторы");
  else if(a==="antisave")saveAnti();
});
document.addEventListener("keydown", function(e){
  if(e.key!=="Enter")return;
  if(e.target && e.target.id==="cmd")sendCmd();
  else if(e.target && (e.target.id==="u"||e.target.id==="p"))login();
});

var timer=null;
function show(){var on=token.length>0;
  V("login").style.display=on?"none":"block"; V("app").style.display=on?"flex":"none";
  if(on){ V("srv").textContent=name; V("addr").textContent=name+".narelauncher.winnigames2024.ru";
    V("who").innerHTML='<span class="ava" title="'+at(name)+'">'+ini(name)+'</span>';
    buildNav(); markTabs(); render(); poll(); if(timer)clearInterval(timer); timer=setInterval(poll,2000);
  } else if(timer){clearInterval(timer);timer=null;}}

function setOnline(on){ V("sdot").className="dot"+(on?" on":" offc"); V("sstat").className=on?"run":"off"; V("sstat").textContent=on?"Онлайн":"Оффлайн";
  V("bStart").disabled=on; V("bStop").disabled=!on; V("bRestart").disabled=!on; }
function poll(){ api("/server/weblog",{}).then(function(d){
  if(!d.ok){ if(d.error)logout(); return; }
  var on=!!d.online; setOnline(on); var st=d.stats;
  if(st&&on){ V("sCpu").textContent=((st.cpu||0)/10).toFixed(1)+" %"; V("sMem").textContent=gib(st.ram||0)+" / "+(MEMLIM/1024)+" GiB"; V("sUp").textContent=fmtUp(st.up); V("sPl").textContent=(st.players!=null?st.players:0);
    pushHist((st.cpu||0)/10, (st.ram||0), (st.players!=null?st.players:0)); }
  else { V("sCpu").textContent="—";V("sMem").textContent="—";V("sUp").textContent="—";V("sPl").textContent="—"; pushHist(0,0,0); }
  lastLines=d.lines||[];
  if(tab==="console"){ var c=V("console"); if(c){ var atB=c.scrollHeight-c.scrollTop-c.clientHeight<60; c.innerHTML=renderLog(lastLines); if(atB)c.scrollTop=c.scrollHeight; } }
  if(tab==="logs"){ var lg=V("logbox"); if(lg) lg.innerHTML=renderLog(lastLines); }
  if(tab==="graph"){ drawGraph(); }
}).catch(function(){}); }
function renderLog(lines){ return (lines||[]).map(function(l){var e=esc(l); if(/^&gt;/.test(e))return '<span class="in">'+e+'</span>'; if(/WARN/i.test(l))return '<span class="warn">'+e+'</span>'; if(/ERROR|severe|exception/i.test(l))return '<span class="err">'+e+'</span>'; return e;}).join("<br>"); }

function power(a){ var nm={start:"запуск",restart:"перезапуск",stop:"остановку"}; api("/server/control",{action:a}).then(function(d){ toast(d.ok?("Команда на "+nm[a]+" отправлена"):(d.error||"не вышло")); }); }
function sendCmd(){ var el=V("cmd"); if(!el)return; var t=(el.value||"").trim(); if(!t)return; api("/server/webcmd",{text:t}).then(function(){}); el.value=""; }

function fsCall(action,path,content){
  return api("/server/fs",{action:action,path:path||"",content:content}).then(function(d){
    if(!d.ok) throw new Error(d.error||"ошибка"); var id=d.id;
    return new Promise(function(resolve,reject){ var n=0;
      var iv=setInterval(function(){ n++; api("/server/fsres",{id:id}).then(function(r){
        if(r.ready){ clearInterval(iv); resolve(r.result||{}); } else if(n>50){ clearInterval(iv); reject(new Error("сервер не ответил — запущен ли он?")); }
      }).catch(function(){}); },350);
    });
  });
}

function render(){ var v=V("view");
  if(tab==="console"){
    v.innerHTML='<div class="card"><div class="cardhd">Консоль сервера<div class="sp"></div></div><div class="console" id="console"></div><div class="prompt"><span class="c">&raquo;</span><input id="cmd" placeholder="Введите команду (без /)…"><button data-act="send">Отправить</button></div></div>';
    V("console").innerHTML=renderLog(lastLines); V("console").scrollTop=V("console").scrollHeight; return;
  }
  if(tab==="logs"){ v.innerHTML='<div class="card"><div class="cardhd">Логи сервера<div class="sp"></div><button class="btn gray" data-act="poll">Обновить</button></div><div class="console" id="logbox" style="height:600px"></div></div>'; V("logbox").innerHTML=renderLog(lastLines); return; }
  if(tab==="players"){ renderPlayers(); return; }
  if(tab==="plugins"){ renderPlugins(); return; }
  if(tab==="settings"){ openEditor("server.properties"); return; }
  if(tab==="files"){ renderFiles(curPath); return; }
  if(tab==="launch"){ renderLaunch(); return; }
  if(tab==="graph"){ renderGraph(); return; }
  if(tab==="backups"){ renderBackups(); return; }
  if(tab==="access"){ renderAccess(); return; }
}

// ---- Нагрузка: живой график ----
function pushHist(cpu,memMb,pl){ histCpu.push(cpu); histMem.push(memMb); histPl.push(pl); if(histCpu.length>HIST){histCpu.shift();histMem.shift();histPl.shift();} }
function renderGraph(){ tab="graph"; markTabs(); var v=V("view");
  v.innerHTML='<div class="card"><div class="cardhd">Нагрузка сервера <span style="color:#8b93a7;font-weight:400;margin-left:8px">последние ~2 минуты</span><div class="sp"></div>'
    +'<span style="color:#56d364;font-size:12px">● CPU</span> <span style="color:#9db4ff;font-size:12px;margin-left:10px">● Память</span> <span style="color:#f0c85a;font-size:12px;margin-left:10px">● Игроки</span></div>'
    +'<div style="padding:14px 16px"><canvas id="gcanvas" width="900" height="300" style="width:100%;height:300px;background:#070a11;border:1px solid #1b2232;border-radius:10px"></canvas></div></div>';
  drawGraph();
}
function drawGraph(){ var c=V("gcanvas"); if(!c)return; var g=c.getContext("2d"); var W=c.width,H=c.height; g.clearRect(0,0,W,H);
  g.strokeStyle="#141a28"; g.lineWidth=1; for(var i=0;i<=4;i++){var y=H*i/4; g.beginPath();g.moveTo(0,y);g.lineTo(W,y);g.stroke();}
  function line(arr,max,col){ if(arr.length<2)return; g.strokeStyle=col;g.lineWidth=2;g.beginPath();
    for(var i=0;i<arr.length;i++){ var x=W*i/(HIST-1); var y=H-(Math.min(arr[i],max)/max)*(H-6)-3; if(i===0)g.moveTo(x,y);else g.lineTo(x,y);} g.stroke(); }
  var maxMem=Math.max(MEMLIM,512); var maxPl=Math.max.apply(null,histPl.concat([4]));
  line(histCpu,100,"#56d364"); line(histMem,maxMem,"#9db4ff"); line(histPl,maxPl,"#f0c85a");
}

// ---- Бэкапы ----
function renderBackups(){ tab="backups"; markTabs(); var v=V("view");
  v.innerHTML='<div class="card"><div class="cardhd">Бэкапы мира<div class="sp"></div><button class="btn green" data-act="backup">💾 Сделать бэкап</button></div>'
    +'<div style="padding:10px 16px;color:#8b93a7;font-size:13px">Сохраняет миры и ключевые файлы в <b>backups/</b>. Восстановление — только при остановленном сервере.</div>'
    +'<div class="flist" id="blist"><div class="empty">загрузка…</div></div></div>';
  loadBackups();
}
function loadBackups(){ fsCall("backuplist","").then(function(r){ var items=r.items||[];
  if(!items.length){ V("blist").innerHTML='<div class="empty">бэкапов пока нет</div>'; return; }
  var h=""; for(var i=0;i<items.length;i++){ var n=items[i]; h+='<div class="frow"><span class="ic">🗄️</span><span class="nm">'+esc(n)+'</span><button class="btn" data-act="brestore" data-arg="'+at(n)+'">Восстановить</button></div>'; }
  V("blist").innerHTML=h;
}).catch(function(e){ V("blist").innerHTML='<div class="empty">'+esc(e.message)+'</div>'; }); }
function doBackup(){ toast("Делаю бэкап…"); fsCall("backup","").then(function(r){ toast("Бэкап готов: "+(r.file||"")); loadBackups(); }).catch(function(e){ toast(e.message); }); }
function doRestore(n){ if(!confirm("Восстановить мир из "+n+"? Текущий мир будет заменён. Сервер должен быть остановлен."))return; fsCall("backuprestore",n).then(function(){ toast("Мир восстановлен из "+n); }).catch(function(e){ toast(e.message); }); }

// ---- Доступ: вайтлист, операторы, анти-лаг ----
function renderAccess(){ tab="access"; markTabs(); var v=V("view");
  v.innerHTML='<div class="card"><div class="cardhd">Белый список (whitelist)<div class="sp"></div></div>'
    +'<div style="display:flex;gap:8px;padding:12px 16px;border-bottom:1px solid #1b2232"><input id="wlin" placeholder="ник игрока" style="flex:1;background:#0a0d14;border:1px solid #242c3e;color:#e7eaf2;border-radius:8px;padding:9px 11px"><button class="btn green" data-act="wladd">Добавить</button></div>'
    +'<div class="flist" id="wllist"><div class="empty">загрузка…</div></div></div>'
    +'<div class="card" style="margin-top:14px"><div class="cardhd">Операторы (OP)<div class="sp"></div></div>'
    +'<div style="display:flex;gap:8px;padding:12px 16px;border-bottom:1px solid #1b2232"><input id="opin" placeholder="ник игрока" style="flex:1;background:#0a0d14;border:1px solid #242c3e;color:#e7eaf2;border-radius:8px;padding:9px 11px"><button class="btn green" data-act="opadd">Выдать OP</button></div>'
    +'<div class="flist" id="oplist"><div class="empty">загрузка…</div></div></div>'
    +'<div class="card" style="margin-top:14px"><div class="cardhd">Анти-лаг<div class="sp"></div></div>'
    +'<div style="padding:14px 16px"><label style="display:flex;align-items:center;gap:10px;color:#e7eaf2"><input type="checkbox" id="antiOn"> Убивать TNT, если на сервере больше <input id="antiMax" type="number" min="10" max="2000" step="10" value="100" style="width:80px;background:#0a0d14;border:1px solid #242c3e;color:#e7eaf2;border-radius:6px;padding:6px 8px"> шт.</label>'
    +'<div style="margin-top:10px"><button class="btn green" data-act="antisave">Сохранить</button></div></div></div>';
  loadList("wl_list","wllist","wldel"); loadList("op_list","oplist","opdel"); loadAnti();
}
function loadList(action,box,delAct){ fsCall(action,"").then(function(r){ var items=r.items||[];
  if(!items.length){ V(box).innerHTML='<div class="empty">пусто</div>'; return; }
  var h=""; for(var i=0;i<items.length;i++){ var n=items[i]; h+='<div class="frow"><span class="ava" style="width:26px;height:26px;font-size:13px">'+ini(n)+'</span><span class="nm">'+esc(n)+'</span><button class="btn red" data-act="'+delAct+'" data-arg="'+at(n)+'">Убрать</button></div>'; }
  V(box).innerHTML=h;
}).catch(function(e){ V(box).innerHTML='<div class="empty">'+esc(e.message)+'</div>'; }); }
function listAdd(action,inputId,label){ var el=V(inputId); var n=(el.value||"").trim(); if(!n)return; fsCall(action,n).then(function(){ toast(label+": добавлен "+n); el.value=""; renderAccess(); }).catch(function(e){ toast(e.message); }); }
function listAct(action,n,label){ fsCall(action,n).then(function(){ toast(label+": убран "+n); renderAccess(); }).catch(function(e){ toast(e.message); }); }
function loadAnti(){ fsCall("antilagget","").then(function(r){ if(V("antiOn")){V("antiOn").checked=!!r.on; V("antiMax").value=r.max||100;} }).catch(function(){}); }
function saveAnti(){ fsCall("antilagset","",JSON.stringify({on:V("antiOn").checked,max:parseInt(V("antiMax").value,10)||100})).then(function(){ toast("Анти-лаг сохранён"); }).catch(function(e){ toast(e.message); }); }

function renderFiles(path){ curPath=path||""; tab="files"; markTabs(); var v=V("view");
  v.innerHTML='<div class="card"><div class="crumb">📁 <b>/'+esc(curPath)+'</b></div><div class="cardhd"><button class="btn gray" data-act="up">⬆ Вверх</button><div class="sp"></div><button class="btn gray" data-act="cd" data-arg="'+at(curPath)+'">Обновить</button></div><div class="flist" id="flist"><div class="empty">загрузка…</div></div></div>';
  fsCall("list",curPath).then(function(r){ if(!r.ok){ V("flist").innerHTML='<div class="empty">'+esc(r.error||"ошибка")+'</div>'; return; }
    var es=r.entries||[]; if(!es.length){ V("flist").innerHTML='<div class="empty">пусто</div>'; return; }
    var h=""; for(var i=0;i<es.length;i++){ var e=es[i]; var pp=(curPath?curPath+"/":"")+e.name;
      h+='<div class="frow"><span class="ic">'+(e.dir?"📁":"📄")+'</span><span class="nm" data-act="'+(e.dir?"cd":"open")+'" data-arg="'+at(pp)+'">'+esc(e.name)+'</span><span class="sz">'+(e.dir?"":fmtSz(e.size))+'</span><span class="x" data-act="del" data-arg="'+at(pp)+'">✕</span></div>';
    } V("flist").innerHTML=h;
  }).catch(function(ex){ V("flist").innerHTML='<div class="empty">'+esc(ex.message)+'</div>'; });
}
function upDir(){ var i=curPath.lastIndexOf("/"); renderFiles(i>0?curPath.slice(0,i):""); }
function delFile(path){ if(!confirm("Удалить «"+path+"»?"))return; fsCall("delete",path).then(function(){ toast("Удалено"); if(tab==="plugins")renderPlugins(); else renderFiles(curPath); }); }

function openEditor(path){ editPath=path; tab=(path==="server.properties"?"settings":tab); markTabs(); var label=path.split("/").pop(); var v=V("view");
  v.innerHTML='<div class="card editor"><div class="cardhd">✎ '+esc(label)+'<div class="sp"></div><button class="btn gray" data-act="go" data-arg="files">← Файлы</button><button class="btn green" data-act="save">💾 Сохранить</button></div><textarea id="edit" spellcheck="false">загрузка…</textarea></div>';
  fsCall("read",path).then(function(r){ V("edit").value=r.ok?(r.content||""):(r.error||"не прочитать"); }).catch(function(ex){ V("edit").value=ex.message; });
}
function saveEditor(){ var c=V("edit").value; fsCall("write",editPath,c).then(function(r){ toast(r.ok?"Сохранено":(r.error||"ошибка")); }).catch(function(ex){ toast(ex.message); }); }

function renderPlugins(){ tab="plugins"; markTabs(); var v=V("view");
  v.innerHTML='<div class="card"><div class="cardhd">Каталог плагинов / модов<div class="sp"></div></div><div style="display:flex;gap:8px;padding:12px 16px;border-bottom:1px solid #1b2232"><input id="psq" placeholder="Поиск в каталоге (Modrinth)…" style="flex:1;background:#0a0d14;border:1px solid #242c3e;color:#e7eaf2;border-radius:8px;padding:9px 11px"><button class="btn" data-act="psearch">Искать</button></div><div class="flist" id="pcat" style="max-height:300px"></div></div>'
   +'<div class="card" style="margin-top:14px"><div class="cardhd">Установленные<div class="sp"></div><button class="btn gray" data-act="go" data-arg="plugins">Обновить</button></div><div class="flist" id="plist"><div class="empty">загрузка…</div></div></div>';
  doPSearch(); loadInstalled();
}
function loadInstalled(){ fsCall("plugins").then(function(r){ if(!r.ok){ V("plist").innerHTML='<div class="empty">'+esc(r.error||"ошибка")+'</div>'; return; }
    var items=r.items||[]; if(!items.length){ V("plist").innerHTML='<div class="empty">папка '+esc(r.sub)+' пуста</div>'; return; }
    var h=""; for(var i=0;i<items.length;i++){ h+='<div class="frow"><span class="ic">🧩</span><span class="nm">'+esc(items[i])+'</span><span class="x" data-act="del" data-arg="'+at(r.sub+"/"+items[i])+'">✕</span></div>'; } V("plist").innerHTML=h;
  }).catch(function(ex){ V("plist").innerHTML='<div class="empty">'+esc(ex.message)+'</div>'; });
}
function renderPlayers(){ tab="players"; markTabs(); var v=V("view"); v.innerHTML='<div class="card"><div class="cardhd">Игроки онлайн<div class="sp"></div><button class="btn gray" data-act="go" data-arg="players">Обновить</button></div><div class="flist" id="plrs"><div class="empty">загрузка…</div></div></div>';
  fsCall("players").then(function(r){ if(!r.ok){ V("plrs").innerHTML='<div class="empty">'+esc(r.error||"ошибка")+'</div>'; return; }
    var ps=r.players||[]; if(!ps.length){ V("plrs").innerHTML='<div class="empty">никого нет онлайн</div>'; return; }
    var h=""; for(var i=0;i<ps.length;i++){ var n=ps[i]; h+='<div class="frow"><span class="ava" style="width:26px;height:26px;font-size:13px">'+ini(n)+'</span><span class="nm">'+esc(n)+'</span><button class="btn" data-act="cmd" data-arg="'+at("op "+n)+'">OP</button><button class="btn gray" data-act="cmd" data-arg="'+at("deop "+n)+'">deOP</button><button class="btn red" data-act="cmd" data-arg="'+at("kick "+n)+'">Кик</button></div>'; } V("plrs").innerHTML=h;
  }).catch(function(ex){ V("plrs").innerHTML='<div class="empty">'+esc(ex.message)+'</div>'; });
}
function pcmd(c){ api("/server/webcmd",{text:c}).then(function(){ toast("Команда: "+c); setTimeout(renderPlayers,800); }); }

function renderLaunch(){ tab="launch"; markTabs(); var v=V("view");
  v.innerHTML='<div class="card"><div class="cardhd">Настройки запуска<div class="sp"></div><button class="btn green" data-act="launchsave">Сохранить</button></div><div style="padding:16px" id="lform"><div class="empty">загрузка…</div></div></div>';
  fsCall("launchget").then(function(c){ if(!c.ok){ V("lform").innerHTML='<div class="empty">'+esc(c.error||"ошибка")+'</div>'; return; } V("lform").innerHTML=launchForm(c); }).catch(function(ex){ V("lform").innerHTML='<div class="empty">'+esc(ex.message)+'</div>'; });
}
function opt(val,cur,t){ return '<option value="'+val+'"'+(String(cur)===String(val)?" selected":"")+'>'+t+'</option>'; }
function launchForm(c){ return '<div class="frm">'
  +'<label>Исполняемый jar</label><input id="f_jar" value="'+at(c.jar||"server.jar")+'">'
  +'<label>Макс. память, МБ</label><input id="f_rmax" type="number" value="'+(c.ramMax||2048)+'"><div class="hint">Сколько ОЗУ максимум отдать серверу (-Xmx)</div>'
  +'<label>Мин. память, МБ</label><input id="f_rmin" type="number" value="'+(c.ramMin||1024)+'">'
  +'<label>Версия Java</label><select id="f_java">'+opt(0,c.java,"Авто (по версии)")+opt(8,c.java,"Java 8")+opt(17,c.java,"Java 17")+opt(21,c.java,"Java 21")+'</select>'
  +'<label>Сборщик мусора (GC)</label><select id="f_gc">'+opt("g1",c.gc,"G1GC — рекомендуется, от 4 ГБ")+opt("parallel",c.gc,"ParallelGC — производительность")+opt("serial",c.gc,"SerialGC — слабые ПК")+opt("none",c.gc,"По умолчанию JVM")+'</select><div class="hint">G1GC: частые короткие паузы, лучшие тайминги. Parallel: чаще, короче паузы. Serial: однопоточный, редкие но длинные паузы.</div>'
  +'<label>Доп. аргументы JVM</label><input id="f_args" value="'+at(c.args||"")+'">'
  +'</div>'; }
function saveLaunch(){ var c={jar:V("f_jar").value.trim()||"server.jar",ramMax:parseInt(V("f_rmax").value)||2048,ramMin:parseInt(V("f_rmin").value)||1024,java:parseInt(V("f_java").value)||0,gc:V("f_gc").value,args:V("f_args").value.trim()};
  fsCall("launchset","",JSON.stringify(c)).then(function(r){ toast(r.ok?"Сохранено — применится при следующем запуске":(r.error||"ошибка")); }).catch(function(ex){ toast(ex.message); }); }

function doPSearch(){ var q=V("psq")?V("psq").value.trim():""; V("pcat").innerHTML='<div class="empty">поиск в каталоге…</div>';
  fsCall("psearch",q).then(function(r){ if(!r.ok){ V("pcat").innerHTML='<div class="empty">'+esc(r.error||"ошибка")+'</div>'; return; }
    var items=r.items||[]; if(!items.length){ V("pcat").innerHTML='<div class="empty">ничего не найдено</div>'; return; }
    var h=''; for(var i=0;i<items.length;i++){ var it=items[i]; h+='<div class="frow"><span class="ic">🧩</span><span class="nm">'+esc(it.title)+'</span><span class="sz">'+(it.downloads>1000?(Math.round(it.downloads/1000)+" тыс"):it.downloads)+'</span><button class="btn green" data-act="pinstall" data-arg="'+at(it.slug)+'">Установить</button></div>'; } V("pcat").innerHTML=h;
  }).catch(function(ex){ V("pcat").innerHTML='<div class="empty">'+esc(ex.message)+'</div>'; }); }
function doPInstall(slug){ toast("Устанавливаю…"); fsCall("pinstall",slug).then(function(r){ toast(r.ok?("Установлено: "+r.file):(r.error||"не вышло")); if(tab==="plugins")loadInstalled(); }).catch(function(ex){ toast(ex.message); }); }

function doBan(n){ var reason=prompt("Причина бана игрока "+n+":",""); if(reason===null)return; var dur=prompt("На сколько? (1d, 2h, 30m — пусто = навсегда):",""); var cmd=(dur&&dur.trim())?("tempban "+n+" "+dur.trim()+" "+reason):("ban "+n+" "+reason); api("/server/webcmd",{text:cmd}).then(function(){ toast("Бан отправлен: "+n); setTimeout(renderPlayers,900); }); }

var lb=V("loginBtn"); if(lb) lb.addEventListener("click", login);
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
        const nick = sessionNick(body.token) || ADMIN;              // кто написал — ставится сам
        const secs = Math.max(0, Math.min(3600, Math.trunc(Number(body.seconds) || 0)));
        policy.broadcast = text
            ? { text, nick, at: now(), until: secs > 0 ? now() + secs * 1000 : 0, secs }
            : null;
        savePolicy();
        send(res, 200, { ok: true });
    });
}

// Перезапуск у всех: ставим отметку времени, клиенты видят её в статусе.
function adminRestart(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        const what = body && body.what;
        if (what === 'launchers' || what === 'all') policy.restartLaunchers = now();
        if (what === 'servers' || what === 'all') policy.restartServers = now();
        savePolicy();
        log('перезапуск запрошен:', what);
        send(res, 200, { ok: true });
    });
}

// Ежеминутная награда: немного монет за то, что игрок онлайн. Раз в минуту.
const MINUTE_REWARD = 5;
const MINUTE_COOLDOWN = 60 * 1000;
function rewardMinute(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const a = acc(nick);
        if (now() - (a.lastMinute || 0) < MINUTE_COOLDOWN)
            return send(res, 200, { ok: true, awarded: 0, coins: a.coins || 0 });
        a.lastMinute = now();
        a.coins = (a.coins || 0) + MINUTE_REWARD;
        saveAccounts();
        send(res, 200, { ok: true, awarded: MINUTE_REWARD, coins: a.coins });
    });
}

// ---------------------------------------------------------------- промокоды
function adminPromoCreate(req, res) {
    readJsonBody(req, body => {
        if (!needAdmin(body, res)) return;
        const code = (body.code || '').trim().toUpperCase();
        const value = Math.max(1, Math.trunc(Number(body.value) || 0));
        const max = Math.max(1, Math.trunc(Number(body.max) || 1));
        if (!code || !value) return send(res, 400, { error: 'нужны код и сумма' });
        if (!policy.promos) policy.promos = {};
        policy.promos[code] = { value, max, used: [] };
        savePolicy();
        send(res, 200, { ok: true, code, value, max });
    });
}
function promoRedeem(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const code = (body.code || '').trim().toUpperCase();
        const p = policy.promos && policy.promos[code];
        if (!p) return send(res, 404, { error: 'нет такого кода' });
        if (p.used.indexOf(nick) >= 0) return send(res, 400, { error: 'вы уже активировали этот код' });
        if (p.used.length >= p.max) return send(res, 400, { error: 'код исчерпан' });
        p.used.push(nick); savePolicy();
        const a = acc(nick); a.coins = (a.coins || 0) + p.value; saveAccounts();
        send(res, 200, { ok: true, value: p.value, coins: a.coins });
    });
}

// ---------------------------------------------------------------- рулетка
// Ставка монет: шанс потерять или умножить. Небольшой «дом» (EV ~0.96).
function rouletteSpin(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const a = acc(nick);
        const bet = Math.max(1, Math.trunc(Number(body && body.bet) || 0));
        if ((a.coins || 0) < bet) return send(res, 402, { error: 'не хватает монет' });
        a.coins -= bet;
        const r = Math.random();
        let mult;
        if (r < 0.52) mult = 0; else if (r < 0.82) mult = 1.5; else if (r < 0.95) mult = 2; else mult = 5;
        const win = Math.floor(bet * mult);
        a.coins += win; saveAccounts();
        send(res, 200, { ok: true, mult, win, bet, coins: a.coins });
    });
}

// Ежедневная награда: +1000 монет и +1 слот сервера, раз в 20 часов.
const REWARD_COOLDOWN = 20 * 3600 * 1000;
function rewardDaily(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const a = acc(nick);
        const left = REWARD_COOLDOWN - (now() - (a.lastReward || 0));
        if (left > 0)
            return send(res, 200, { ok: true, awarded: false, coins: a.coins || 0,
                                    slots: a.serverSlots || 0, nextInH: Math.ceil(left / 3600000) });
        a.coins = (a.coins || 0) + 1000;
        a.serverSlots = (a.serverSlots || 0) + 1;
        a.lastReward = now();
        saveAccounts();
        log('ежедневная награда', nick, '+1000 монет, +1 сервер');
        send(res, 200, { ok: true, awarded: true, coins: a.coins, slots: a.serverSlots });
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
// веб кладёт файловый запрос (список/чтение/запись/плагины/игроки)
function serverFs(req, res) {
    readJsonBody(req, body => {
        const name = clean(body.name, 48);
        if (!srvOwner(body.token, name)) return send(res, 403, { error: 'не ваш сервер' });
        if (!rooms2byNick(name)) return send(res, 409, { error: 'сервер не запущен' });
        const id = crypto.randomBytes(5).toString('hex');
        if (!policy.srvFsReq[name]) policy.srvFsReq[name] = [];
        policy.srvFsReq[name].push({
            id, action: clean(body.action, 16),
            path: String(body.path || '').slice(0, 400),
            content: typeof body.content === 'string' ? body.content.slice(0, 2 * 1024 * 1024) : null
        });
        if (policy.srvFsReq[name].length > 60) policy.srvFsReq[name].shift();
        send(res, 200, { ok: true, id });
    });
}
// веб забирает результат по id
function serverFsRes(req, res) {
    readJsonBody(req, body => {
        const name = clean(body.name, 48);
        if (!srvOwner(body.token, name)) return send(res, 403, { error: 'не ваш сервер' });
        const id = clean(body.id, 16);
        const box = policy.srvFsRes[name] || {};
        if (box[id] !== undefined) {
            const r = box[id];
            delete box[id];
            return send(res, 200, { ok: true, ready: true, result: r });
        }
        send(res, 200, { ok: true, ready: false });
    });
}
// лаунчер забирает файловые запросы своего сервера
function serverFsPull(req, res) {
    readJsonBody(req, body => {
        const room = body && body.token ? rooms.get(body.token) : null;
        if (!room) return send(res, 403, { error: 'нет комнаты' });
        const name = room.nick;
        const out = policy.srvFsReq[name] || [];
        if (out.length) policy.srvFsReq[name] = [];
        send(res, 200, { requests: out });
    });
}
// лаунчер кладёт результат
function serverFsPush(req, res) {
    readJsonBody(req, body => {
        const room = body && body.token ? rooms.get(body.token) : null;
        if (!room) return send(res, 403, { error: 'нет комнаты' });
        const name = room.nick;
        const id = clean(body.id, 16);
        if (!id) return send(res, 400, { error: 'нет id' });
        if (!policy.srvFsRes[name]) policy.srvFsRes[name] = {};
        policy.srvFsRes[name][id] = (body.result !== undefined ? body.result : null);
        // чистим старые
        const keys = Object.keys(policy.srvFsRes[name]);
        if (keys.length > 80) delete policy.srvFsRes[name][keys[0]];
        send(res, 200, { ok: true });
    });
}

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

// ------------------------------------------------------------ удалённое управление лаунчером
//
// Схема: лаунчер пользователя (он залогинен) раз в ~0.6с снимает своё окно и шлёт
// кадр на /remote/host, заодно забирая накопленные события мыши/клавиатуры и применяя
// их у себя. Веб-пульт на сайте сначала проходит /remote/auth по паролю, который
// пользователь сам задал (хранится только как scrypt-хэш), получает короткоживущий
// rtoken и дальше тянет кадр (/remote/frame) и шлёт клики/движения (/remote/input).
// Пароль — второй фактор: даже со входом в аккаунт без него управлять нельзя.

function remoteNickFromRToken(rtoken) {
    const t = remoteTokens.get(rtoken);
    if (!t) return null;
    if (now() > t.exp) { remoteTokens.delete(rtoken); return null; }
    t.exp = now() + REMOTE_TOKEN_LIFE;           // продлеваем, пока пультом пользуются
    return t.nick;
}

// лаунчер (или сайт) задаёт/меняет пароль и флаг включения
function remoteConfig(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });

        if (body.clear === true) {
            delete policy.remote[nick];
            savePolicy();
            return send(res, 200, { ok: true, hasPassword: false, enabled: false });
        }

        const rec = policy.remote[nick] || {};
        const pass = typeof body.password === 'string' ? body.password : '';
        if (pass) {
            if (pass.length < 4) return send(res, 400, { error: 'пароль слишком короткий (минимум 4 символа)' });
            const h = makeHash(pass);
            rec.salt = h.salt; rec.hash = h.hash;
        }
        if (typeof body.enabled === 'boolean') rec.enabled = body.enabled;
        if (rec.salt && rec.hash) {               // сохраняем только если пароль задан
            policy.remote[nick] = rec;
            savePolicy();
        }
        send(res, 200, { ok: true, hasPassword: !!(rec.salt && rec.hash), enabled: !!rec.enabled });
    });
}

// лаунчер шлёт кадр окна и забирает очередь событий
function remoteHost(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });

        const jpg = typeof body.jpg === 'string' ? body.jpg : '';
        if (jpg) {
            if (jpg.length > 4_000_000) return send(res, 413, { error: 'кадр слишком большой' });
            remoteState.set(nick, {
                jpg,
                w: Math.max(1, Math.min(4000, Number(body.w) || 0)),
                h: Math.max(1, Math.min(4000, Number(body.h) || 0)),
                at: now(),
                enabled: body.enabled !== false
            });
        }
        const ev = remoteInput.get(nick) || [];
        remoteInput.set(nick, []);
        send(res, 200, { ok: true, input: ev });
    });
}

// веб: вход в пульт по паролю (управляем своим же аккаунтом)
function remoteAuth(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });

        const rec = policy.remote[nick];
        if (!rec || !rec.salt) return send(res, 400, { error: 'удалённое управление не настроено в лаунчере' });
        if (!checkPassword(typeof body.password === 'string' ? body.password : '', rec))
            return send(res, 403, { error: 'неверный пароль удалённого управления' });

        const rtoken = token();
        remoteTokens.set(rtoken, { nick, exp: now() + REMOTE_TOKEN_LIFE });
        send(res, 200, { ok: true, rtoken });
    });
}

// веб: последний кадр окна лаунчера
function remoteFrame(req, res) {
    readJsonBody(req, body => {
        const nick = remoteNickFromRToken(body && body.rtoken);
        if (!nick) return send(res, 403, { error: 'сессия пульта истекла' });
        const st = remoteState.get(nick);
        const online = !!(st && now() - st.at < REMOTE_ONLINE);
        send(res, 200, {
            ok: true, online,
            jpg: st ? st.jpg : '', w: st ? st.w : 0, h: st ? st.h : 0, at: st ? st.at : 0
        });
    });
}

// веб: поставить события мыши/клавиатуры в очередь лаунчеру
function remoteInputPush(req, res) {
    readJsonBody(req, body => {
        const nick = remoteNickFromRToken(body && body.rtoken);
        if (!nick) return send(res, 403, { error: 'сессия пульта истекла' });

        const list = Array.isArray(body.events) ? body.events : [];
        const arr = remoteInput.get(nick) || [];
        for (const e of list.slice(0, 50)) {
            const t = clean(e && e.t, 10);
            if (!['move', 'down', 'up', 'click', 'dbl', 'rclick', 'scroll', 'key', 'text'].includes(t)) continue;
            arr.push({
                t,
                x: Math.max(0, Math.min(1, Number(e.x) || 0)),   // доли окна 0..1 — лаунчер сам переведёт в пиксели
                y: Math.max(0, Math.min(1, Number(e.y) || 0)),
                dy: Math.max(-20, Math.min(20, Math.trunc(Number(e.dy) || 0))),
                key: clean(e.key, 24),
                text: clean(e.text, 200)
            });
        }
        while (arr.length > 300) arr.shift();
        remoteInput.set(nick, arr);
        send(res, 200, { ok: true });
    });
}

// веб/лаунчер: настроен ли пароль и на связи ли лаунчер
function remoteStateGet(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const rec = policy.remote[nick];
        const st = remoteState.get(nick);
        send(res, 200, {
            ok: true,
            hasPassword: !!(rec && rec.salt),
            enabled: !!(rec && rec.enabled),
            online: !!(st && now() - st.at < REMOTE_ONLINE)
        });
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

// ------------------------------------------------------------------- кликер 67
const CLICK_MAX_BATCH = 200;        // максимум кликов за один запрос
const CLICK_OFFLINE_CAP = 12 * 3600; // не больше 12 часов пассивного дохода за раз

// Старт как «кликер 67»: +67 за клик, полная выносливость под это.
function clickerState(a) {
    if (!a.clk) a.clk = { click: 67, auto: 0, energyMax: 6700, energy: 6700, regen: 10, last: now() };
    const c = a.clk;
    if (c.energyMax == null) c.energyMax = 6700;
    if (c.energy == null) c.energy = c.energyMax;
    if (c.regen == null) c.regen = 10;
    if (c.last == null) c.last = now();
    return c;
}
// Цены пропорциональны текущему значению: прокачка удваивает значение —
// значит и цена сама удваивается с каждым уровнем.
function clickerPriceClick(c)  { return 10 * c.click; }
function clickerPriceAuto(c)   { return c.auto <= 0 ? 50 : 100 * c.auto; }
function clickerPriceEnergy(c) { return Math.round(c.energyMax / 2); }
function clickerPriceRegen(c)  { return 30 * c.regen; }

function clickerAccrue(a) {
    const c = clickerState(a);
    const dt = Math.max(0, now() - c.last);
    c.last = now();
    const sec = Math.floor(dt / 1000);
    if (sec > 0) {
        // пассивный доход (c ограничением на оффлайн)
        if (c.auto > 0) a.coins = (a.coins || 0) + Math.min(sec, CLICK_OFFLINE_CAP) * c.auto;
        // восстановление выносливости (само упирается в потолок)
        c.energy = Math.min(c.energyMax, c.energy + sec * c.regen);
    }
    return c;
}
function clickerPayload(a, c) {
    return { ok: true, coins: a.coins || 0, click: c.click, auto: c.auto,
             energy: Math.floor(c.energy), energyMax: c.energyMax, regen: c.regen,
             priceClick: clickerPriceClick(c), priceAuto: clickerPriceAuto(c),
             priceEnergy: clickerPriceEnergy(c), priceRegen: clickerPriceRegen(c) };
}
function clickerGet(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const a = acc(nick); const c = clickerAccrue(a); saveAccounts();
        send(res, 200, clickerPayload(a, c));
    });
}
function clickerClick(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const a = acc(nick); const c = clickerAccrue(a);
        const want = Math.max(1, Math.min(CLICK_MAX_BATCH, Math.trunc(Number(body && body.count) || 1)));
        // каждый тап стоит выносливости, равной силе клика (как в Hamster Kombat)
        const afford = c.click > 0 ? Math.floor(c.energy / c.click) : 0;
        const did = Math.min(want, afford);
        if (did > 0) { a.coins = (a.coins || 0) + did * c.click; c.energy -= did * c.click; }
        saveAccounts();
        send(res, 200, { ok: true, coins: a.coins, did: did,
                         energy: Math.floor(c.energy), energyMax: c.energyMax });
    });
}
function clickerUpgrade(req, res) {
    readJsonBody(req, body => {
        const nick = sessionNick(body && body.token);
        if (!nick) return send(res, 403, { error: 'нужен вход' });
        const a = acc(nick); const c = clickerAccrue(a);
        const what = body && body.what;
        const price = what === 'click' ? clickerPriceClick(c)
                    : what === 'auto' ? clickerPriceAuto(c)
                    : what === 'energy' ? clickerPriceEnergy(c)
                    : what === 'regen' ? clickerPriceRegen(c) : 0;
        if (!price) return send(res, 400, { error: 'что качаем?' });
        if ((a.coins || 0) < price) return send(res, 402, { error: 'нужно ' + price + ' монет' });
        a.coins -= price;
        // всё удваивается: и значение, и (через цену-пропорцию) следующая цена
        if (what === 'click') c.click *= 2;
        else if (what === 'auto') c.auto = c.auto <= 0 ? 1 : c.auto * 2;
        else if (what === 'energy') { const add = c.energyMax; c.energyMax *= 2; c.energy += add; }
        else if (what === 'regen') c.regen *= 2;
        saveAccounts();
        send(res, 200, clickerPayload(a, c));
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
    if (route === '/server/fs' && req.method === 'POST') return serverFs(req, res);
    if (route === '/server/fsres' && req.method === 'POST') return serverFsRes(req, res);
    if (route === '/server/fspull' && req.method === 'POST') return serverFsPull(req, res);
    if (route === '/server/fspush' && req.method === 'POST') return serverFsPush(req, res);
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
    if (route === '/clicker/get' && req.method === 'POST') return clickerGet(req, res);
    if (route === '/clicker/click' && req.method === 'POST') return clickerClick(req, res);
    if (route === '/clicker/upgrade' && req.method === 'POST') return clickerUpgrade(req, res);
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
    if (route === '/admin/restart' && req.method === 'POST') return adminRestart(req, res);
    if (route === '/restartflag') return send(res, 200, { ok: true, servers: policy.restartServers || 0, launchers: policy.restartLaunchers || 0 });
    if (route === '/reward/daily' && req.method === 'POST') return rewardDaily(req, res);
    if (route === '/reward/minute' && req.method === 'POST') return rewardMinute(req, res);
    if (route === '/admin/promo/create' && req.method === 'POST') return adminPromoCreate(req, res);
    if (route === '/promo/redeem' && req.method === 'POST') return promoRedeem(req, res);
    if (route === '/roulette/spin' && req.method === 'POST') return rouletteSpin(req, res);
    if (route === '/admin/command' && req.method === 'POST') return adminCommand(req, res);
    if (route === '/admin/together' && req.method === 'POST') return adminTogether(req, res);
    if (route === '/admin/blockmod' && req.method === 'POST') return adminBlockMod(req, res);
    if (route === '/admin/update' && req.method === 'POST') return adminUpdate(req, res);

    // --- удалённое управление лаунчером из веба ---
    if (route === '/remote/config' && req.method === 'POST') return remoteConfig(req, res);  // лаунчер/сайт задаёт пароль и вкл/выкл
    if (route === '/remote/host' && req.method === 'POST') return remoteHost(req, res);       // лаунчер шлёт кадр и забирает события
    if (route === '/remote/auth' && req.method === 'POST') return remoteAuth(req, res);       // веб вводит пароль -> rtoken
    if (route === '/remote/frame' && req.method === 'POST') return remoteFrame(req, res);     // веб тянет последний кадр
    if (route === '/remote/input' && req.method === 'POST') return remoteInputPush(req, res); // веб шлёт мышь/клавиши
    if (route === '/remote/state' && req.method === 'POST') return remoteStateGet(req, res);  // настроен ли пароль / онлайн

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
