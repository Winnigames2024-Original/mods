'use strict';
const net = require('net'), http = require('http'), crypto = require('crypto');

const PORT = 24451;
let fails = 0;
function check(ok, what) { console.log((ok ? 'ok   ' : 'FAIL ') + what); if (!ok) fails++; }

function call(method, route, body, raw) {
    return new Promise(resolve => {
        const data = body === undefined ? null : (raw ? body : Buffer.from(JSON.stringify(body)));
        const req = http.request({ host: '127.0.0.1', port: PORT, path: route, method,
            headers: data ? { 'Content-Length': data.length } : {} }, res => {
            const parts = [];
            res.on('data', c => parts.push(c));
            res.on('end', () => {
                const buf = Buffer.concat(parts);
                let json = null;
                try { json = JSON.parse(buf.toString()); } catch (e) {}
                resolve({ code: res.statusCode, json, buf });
            });
        });
        req.on('error', e => resolve({ code: 0, error: e.message }));
        if (data) req.write(data);
        req.end();
    });
}

const sleep = ms => new Promise(r => setTimeout(r, ms));

(async () => {
    // 1. жив ли
    let r = await call('GET', '/ping');
    check(r.code === 200 && r.json.name === 'NareNet', 'ping отвечает: ' + JSON.stringify(r.json));

    // 2. объявление комнаты
    const mods = [
        { id: 'forge_narevisuals', name: 'NareVisuals', version: '2.9.0', file: 'forge_narevisuals-2.9.0.jar',
          sha1: 'fa623726e669ddaa7f881bf994715c26cc8c46df', size: 810459 },
        { id: 'jei', name: 'JEI', version: '19.0', file: 'jei.jar', sha1: 'x', size: 10, need: false }
    ];
    r = await call('POST', '/announce', { nick: 'Winnigames2024', port: 25565, version: '1.21.4',
        loader: 'forge', world: 'Мой мир', players: 1, max: 8, mods, mode: 'direct' });

    check(r.code === 200 && r.json.token, 'объявление принято');
    const token = r.json.token;
    check(r.json.ip === '127.0.0.1', 'сервер видит адрес хозяина: ' + r.json.ip);

    // 3. список комнат
    r = await call('GET', '/rooms');
    const room = r.json.rooms[0];
    check(r.json.rooms.length === 1, 'в списке одна комната');
    check(room.nick === 'Winnigames2024' && room.version === '1.21.4', 'данные комнаты на месте');
    check(room.mods.length === 2, 'моды переданы: ' + room.mods.length);
    check(room.mods[0].sha1.length === 40, 'sha1 мода сохранён');
    check(room.mods[1].sha1 === '', 'мусорный sha1 отброшен');
    check(room.mods[1].need === false, 'необязательный мод помечен');

    // 4. частое объявление не ломает комнату
    r = await call('POST', '/announce', { token, nick: 'Winnigames2024', port: 25565, version: '1.21.4' });
    check(r.code === 200 && r.json.wait === true, 'слишком частое объявление вежливо отклонено');

    // 5. файлы
    const payload = crypto.randomBytes(5000);
    const sha1 = crypto.createHash('sha1').update(payload).digest('hex');

    r = await call('HEAD', '/file/' + sha1);
    check(r.code === 404, 'файла ещё нет');

    r = await call('PUT', '/file/' + sha1, payload, true);
    check(r.code === 200 && r.json.ok, 'файл принят');

    r = await call('GET', '/file/' + sha1);
    check(r.code === 200 && r.buf.length === payload.length, 'файл отдан целиком');
    check(crypto.createHash('sha1').update(r.buf).digest('hex') === sha1, 'содержимое не испортилось');

    r = await call('PUT', '/file/' + crypto.randomBytes(20).toString('hex'), payload, true);
    check(r.code === 400, 'файл с чужим sha1 не принят: ' + JSON.stringify(r.json));

    // 6. туннель целиком: гость -> VPS -> хозяин -> его minecraft
    const fake = net.createServer(s => {
        s.on('data', d => s.write(Buffer.concat([Buffer.from('мир ответил: '), d])));
    });
    await new Promise(r2 => fake.listen(0, '127.0.0.1', r2));
    const fakePort = fake.address().port;

    const control = net.connect(PORT, '127.0.0.1');
    let publicPort = 0;
    let line = '';

    await new Promise(resolve => {
        control.on('connect', () => control.write('NARE1 HOST ' + token + '\n'));
        control.setEncoding('utf8');
        control.on('data', chunk => {
            line += chunk;
            let at;
            while ((at = line.indexOf('\n')) >= 0) {
                const one = line.slice(0, at); line = line.slice(at + 1);
                if (!one.trim()) continue;
                const msg = JSON.parse(one);

                if (msg.port) { publicPort = msg.port; resolve(); }

                if (msg.open) {
                    // хозяин открывает встречный канал и сводит его со своим миром
                    const data = net.connect(PORT, '127.0.0.1', () => {
                        data.write('NARE1 DATA ' + msg.open + '\n');
                        const local = net.connect(fakePort, '127.0.0.1', () => {
                            data.pipe(local); local.pipe(data);
                        });
                    });
                }
            }
        });
    });

    check(publicPort >= 30000 && publicPort <= 30060, 'выдан публичный порт ' + publicPort);

    r = await call('GET', '/rooms');
    check(r.json.rooms[0].mode === 'tunnel', 'комната переключилась на туннель');
    check(r.json.rooms[0].port === publicPort, 'в списке стоит публичный порт');
    check(r.json.rooms[0].ip === null, 'настоящий адрес хозяина не раздаётся при туннеле');

    const answer = await new Promise(resolve => {
        const guest = net.connect(publicPort, '127.0.0.1', () => guest.write('привет'));
        guest.on('data', d => { resolve(d.toString('utf8')); guest.destroy(); });
        guest.on('error', e => resolve('ошибка: ' + e.message));
        setTimeout(() => resolve('молчание'), 5000);
    });
    check(answer === 'мир ответил: привет', 'через туннель прошло насквозь: ' + answer);

    // второй гость тоже проходит
    const answer2 = await new Promise(resolve => {
        const guest = net.connect(publicPort, '127.0.0.1', () => guest.write('ещё один'));
        guest.on('data', d => { resolve(d.toString('utf8')); guest.destroy(); });
        setTimeout(() => resolve('молчание'), 5000);
    });
    check(answer2 === 'мир ответил: ещё один', 'второй гость тоже прошёл: ' + answer2);

    // 7. чужой токен не пускают в туннель
    const thief = net.connect(PORT, '127.0.0.1', () => thief.write('NARE1 HOST ' + 'f'.repeat(32) + '\n'));
    const denied = await new Promise(resolve => {
        thief.on('data', d => resolve(d.toString()));
        thief.on('close', () => resolve('закрыто'));
        setTimeout(() => resolve('молчание'), 3000);
    });
    check(denied.indexOf('error') >= 0, 'чужой токен отклонён: ' + denied.trim());

    // 8. снятие комнаты
    control.destroy();
    await sleep(200);
    r = await call('POST', '/bye', { token });
    check(r.code === 200, 'комната снята');

    r = await call('GET', '/rooms');
    check(r.json.rooms.length === 0, 'список опустел');

    fake.close();
    console.log(fails === 0 ? '\nВСЁ СОШЛОСЬ' : '\nОШИБОК: ' + fails);
    process.exit(fails === 0 ? 0 : 1);
})();
