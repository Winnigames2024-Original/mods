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

const VERSION = '1.0';

// ---------------------------------------------------------------- хозяйство

fs.mkdirSync(FILES_DIR, { recursive: true });

function now() { return Date.now(); }

function log() {
    const stamp = new Date().toISOString().replace('T', ' ').slice(0, 19);
    console.log('[' + stamp + ']', Array.from(arguments).join(' '));
}

function token() { return crypto.randomBytes(16).toString('hex'); }

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

const server = http.createServer((req, res) => {
    const at = req.url.indexOf('?');
    const route = at >= 0 ? req.url.slice(0, at) : req.url;

    res.setHeader('Access-Control-Allow-Origin', '*');

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
        if (!room || room.ip !== plainIp(socket)) {
            socket.write(JSON.stringify({ error: 'комната не найдена — сначала объявитесь' }) + '\n');
            return socket.destroy();
        }

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
