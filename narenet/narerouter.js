'use strict';
/*
 * NareRouter — Minecraft-роутер по доменному имени.
 *
 * Слушает 25565. Читает из handshake адрес, на который зашёл игрок
 * (например prinke_52.narelauncher.winnigames2024.ru), отрезает суффикс,
 * спрашивает у NareNet (/resolve?name=prinke_52) порт тоннеля этого мира
 * «Вместе» и прозрачно проксирует соединение в 127.0.0.1:<порт>.
 *
 * Запуск: node narerouter.js  (порт/суффикс/адрес NareNet — через env).
 */

const net = require('net');
const http = require('http');

const PORT = Number(process.env.NAREROUTER_PORT || 25565);
const NARENET = process.env.NARENET_HOST || '127.0.0.1:2445';
const SUFFIX = ('.' + (process.env.NAREROUTER_SUFFIX || 'narelauncher.winnigames2024.ru'))
    .replace(/^\.\./, '.').toLowerCase();

function log() {
    const t = new Date().toISOString().replace('T', ' ').slice(0, 19);
    console.log('[' + t + '] ' + Array.from(arguments).join(' '));
}

/** Прочитать VarInt из буфера. null — данных пока не хватает. */
function readVarInt(buf, offset) {
    let numRead = 0, result = 0, read;
    do {
        if (offset + numRead >= buf.length) return null;
        read = buf[offset + numRead];
        result |= (read & 0x7f) << (7 * numRead);
        numRead++;
        if (numRead > 5) throw new Error('VarInt слишком длинный');
    } while ((read & 0x80) !== 0);
    return { value: result >>> 0, size: numRead };
}

/**
 * Разобрать handshake и достать адрес сервера.
 * Возвращает { host } когда пакет распознан, { host: null } если это не
 * handshake, или null — если пакет ещё не дочитан (ждём данные).
 */
function parseHandshakeHost(buf) {
    let p = 0;
    const len = readVarInt(buf, p); if (!len) return null;
    p += len.size;
    if (buf.length < p + len.value) return null;       // пакет ещё не целиком
    const pid = readVarInt(buf, p); if (!pid) return null;
    p += pid.size;
    if (pid.value !== 0x00) return { host: null };     // не handshake
    const proto = readVarInt(buf, p); if (!proto) return null;
    p += proto.size;
    const slen = readVarInt(buf, p); if (!slen) return null;
    p += slen.size;
    if (buf.length < p + slen.value) return null;
    let addr = buf.toString('utf8', p, p + slen.value);
    const nul = addr.indexOf('\0');                    // Forge дописывает \0FML\0
    if (nul >= 0) addr = addr.slice(0, nul);
    return { host: addr.toLowerCase().replace(/\.$/, '') };
}

function nickFromHost(host) {
    if (!host) return null;
    if (host.endsWith(SUFFIX)) return host.slice(0, host.length - SUFFIX.length);
    return null;
}

function resolve(nick) {
    return new Promise(res => {
        const url = 'http://' + NARENET + '/resolve?name=' + encodeURIComponent(nick);
        const req = http.get(url, r => {
            let s = '';
            r.on('data', c => s += c);
            r.on('end', () => { try { const j = JSON.parse(s); res(j && j.ok ? j.port : null); } catch (e) { res(null); } });
        });
        req.on('error', () => res(null));
        req.setTimeout(4000, () => { req.destroy(); res(null); });
    });
}

const server = net.createServer(client => {
    client.setNoDelay(true);
    let buf = Buffer.alloc(0);

    const onData = chunk => {
        buf = Buffer.concat([buf, chunk]);
        let parsed;
        try { parsed = parseHandshakeHost(buf); } catch (e) { client.destroy(); return; }
        if (parsed === null) {
            if (buf.length > 4096) client.destroy();   // мусор — рвём
            return;
        }
        client.removeListener('data', onData);
        client.pause();
        route(parsed);
    };

    client.on('data', onData);
    client.on('error', () => {});

    async function route(parsed) {
        const nick = nickFromHost(parsed.host);
        if (!nick) { client.destroy(); return; }
        const port = await resolve(nick);
        if (!port) {
            log('нет мира для', nick, '(', parsed.host, ')');
            client.destroy();
            return;
        }
        const backend = net.connect(port, '127.0.0.1', () => {
            backend.setNoDelay(true);
            backend.write(buf);
            client.pipe(backend);
            backend.pipe(client);
            client.resume();
            log('соединение', nick, '-> 127.0.0.1:' + port);
        });
        backend.on('error', () => client.destroy());
        client.on('error', () => backend.destroy());
        backend.on('close', () => client.destroy());
        client.on('close', () => backend.destroy());
    }
});

server.on('error', e => log('ошибка роутера:', e.message));
server.listen(PORT, '0.0.0.0', () => log('NareRouter слушает 0.0.0.0:' + PORT, '→ NareNet', NARENET, '| суффикс', SUFFIX));
