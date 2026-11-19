#!/usr/bin/env node
/*
 * GEO DASH — API сервер (аккаунты, уровни, друзья, daily/weekly, музыка по ID, админка).
 * Без зависимостей: только стандартная библиотека Node.
 *
 *   Порт:    GEODASH_PORT   (по умолчанию 37534)
 *   Данные:  GEODASH_DIR    (по умолчанию /var/lib/geodash)
 *   Админ:   GEODASH_ADMIN  (по умолчанию Winnigames2024)
 *
 * Музыка: любой трек Newgrounds по ID — качается на сервер и раздаётся клиентам
 * (без модерации: GEODASH_MODERATION=off).
 */

'use strict';

const http = require('http');
const https = require('https');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { URL } = require('url');

const PORT = parseInt(process.env.GEODASH_PORT || '37534', 10);
const DIR = process.env.GEODASH_DIR || '/var/lib/geodash';
const ADMIN = process.env.GEODASH_ADMIN || 'Winnigames2024';
const MODERATION = (process.env.GEODASH_MODERATION || 'off').toLowerCase() === 'on';
const VERSION_FILE = process.env.GEODASH_VERSION_FILE || '/opt/geodash/update.json';

const DATA_DIR = path.join(DIR, 'leveldata');
const MUSIC_DIR = path.join(DIR, 'music');

for (const d of [DIR, DATA_DIR, MUSIC_DIR]) fs.mkdirSync(d, { recursive: true });

// ----------------------------------------------------------------- хранилище

function loadJson(name, def) {
  try {
    const p = path.join(DIR, name);
    if (!fs.existsSync(p)) return def;
    return JSON.parse(fs.readFileSync(p, 'utf8'));
  } catch (e) {
    console.error('load ' + name, e.message);
    return def;
  }
}

const saveTimers = {};
function saveJson(name, obj) {
  clearTimeout(saveTimers[name]);
  saveTimers[name] = setTimeout(() => {
    try {
      const p = path.join(DIR, name);
      fs.writeFileSync(p + '.tmp', JSON.stringify(obj));
      fs.renameSync(p + '.tmp', p);
    } catch (e) { console.error('save ' + name, e.message); }
  }, 120);
}

const db = {
  users: loadJson('users.json', {}),
  levels: loadJson('levels.json', {}),
  scores: loadJson('scores.json', {}),
  tokens: loadJson('tokens.json', {}),
  state: loadJson('state.json', { nextId: 1, daily: null, weekly: null, news: [], songs: {} })
};

const save = {
  users: () => saveJson('users.json', db.users),
  levels: () => saveJson('levels.json', db.levels),
  scores: () => saveJson('scores.json', db.scores),
  tokens: () => saveJson('tokens.json', db.tokens),
  state: () => saveJson('state.json', db.state)
};

// ------------------------------------------------------------------ утилиты

const DIFF_STARS = [0, 1, 2, 3, 5, 7, 9, 10, 10, 10, 10, 10];
const isDemon = d => d >= 7;
const now = () => new Date().toISOString();
const key = n => String(n || '').trim().toLowerCase();

function hash(pass, salt) {
  return crypto.scryptSync(String(pass), salt, 32).toString('hex');
}

function newToken(name) {
  const t = crypto.randomBytes(24).toString('hex');
  db.tokens[t] = { name, created: now() };
  save.tokens();
  return t;
}

function userOf(req) {
  const h = req.headers['authorization'] || '';
  const m = h.match(/^Bearer\s+(.+)$/i);
  if (!m) return null;
  const rec = db.tokens[m[1]];
  if (!rec) return null;
  return db.users[key(rec.name)] || null;
}

function pub(u) {
  if (!u) return null;
  return {
    name: u.name, stars: u.stars | 0, coins: u.coins | 0, demons: u.demons | 0,
    levels: Object.values(db.levels).filter(l => key(l.author) === key(u.name)).length,
    attempts: u.attempts | 0, jumps: u.jumps | 0, about: u.about || '',
    admin: !!u.admin, banned: !!u.banned, created: u.created,
    friends: (u.friends || []).length
  };
}

function levelPub(l) {
  return {
    id: l.id, name: l.name, author: l.author, desc: l.desc || '',
    songId: l.songId | 0, songName: l.songName || '',
    difficulty: l.difficulty | 0, stars: l.stars | 0,
    likes: l.likes | 0, downloads: l.downloads | 0, plays: l.plays | 0,
    featured: !!l.featured, epic: !!l.epic, offline: !!l.offline,
    daily: db.state.daily && db.state.daily.id === l.id,
    weekly: db.state.weekly && db.state.weekly.id === l.id,
    version: l.version | 0, length: l.length || 0,
    created: l.created, updated: l.updated
  };
}

function send(res, code, obj, headers) {
  const body = Buffer.from(JSON.stringify(obj === undefined ? {} : obj));
  res.writeHead(code, Object.assign({
    'Content-Type': 'application/json; charset=utf-8',
    'Content-Length': body.length,
    'Access-Control-Allow-Origin': '*',
    'Access-Control-Allow-Headers': 'Authorization,Content-Type',
    'Access-Control-Allow-Methods': 'GET,POST,OPTIONS,DELETE',
    'Cache-Control': 'no-store'
  }, headers || {}));
  res.end(body);
}

const ok = (res, obj) => send(res, 200, obj === undefined ? { ok: true } : obj);
const bad = (res, msg, code) => send(res, code || 400, { ok: false, error: msg });

function readBody(req) {
  return new Promise((resolve) => {
    let data = '';
    let tooBig = false;
    req.on('data', c => {
      data += c;
      if (data.length > 12 * 1024 * 1024) { tooBig = true; req.destroy(); }
    });
    req.on('end', () => {
      if (tooBig) return resolve({});
      try { resolve(data ? JSON.parse(data) : {}); } catch { resolve({}); }
    });
    req.on('error', () => resolve({}));
  });
}

// ------------------------------------------------------- daily / weekly

function dayStamp(d) { return new Date(d || Date.now()).toISOString().slice(0, 10); }
function weekStamp(d) {
  const dt = new Date(d || Date.now());
  const onejan = new Date(dt.getFullYear(), 0, 1);
  const week = Math.ceil((((dt - onejan) / 86400000) + onejan.getDay() + 1) / 7);
  return dt.getFullYear() + '-W' + week;
}

function rotateIfNeeded() {
  const st = db.state;
  const rated = Object.values(db.levels).filter(l => l.difficulty > 0);
  if (!rated.length) return;

  if (!st.daily || st.daily.date !== dayStamp()) {
    const pick = rated[Math.floor(Math.random() * rated.length)];
    st.daily = { id: pick.id, date: dayStamp() };
    save.state();
  }
  if (!st.weekly || st.weekly.week !== weekStamp()) {
    const hard = rated.filter(l => l.difficulty >= 4);
    const pool = hard.length ? hard : rated;
    const pick = pool[Math.floor(Math.random() * pool.length)];
    st.weekly = { id: pick.id, week: weekStamp(), date: dayStamp() };
    save.state();
  }
}

function secondsLeftDay() {
  const d = new Date();
  const end = new Date(d.getFullYear(), d.getMonth(), d.getDate() + 1);
  return Math.floor((end - d) / 1000);
}
function secondsLeftWeek() {
  const d = new Date();
  const dow = (d.getDay() + 6) % 7;
  const end = new Date(d.getFullYear(), d.getMonth(), d.getDate() + (7 - dow));
  return Math.floor((end - d) / 1000);
}

// ---------------------------------------------------------------- музыка

function songMetaPath(id) { return path.join(MUSIC_DIR, id + '.json'); }
function songFilePath(id) { return path.join(MUSIC_DIR, id + '.mp3'); }

function httpsGet(url, headers) {
  return new Promise((resolve, reject) => {
    const req = https.get(url, {
      headers: Object.assign({
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) GeoDash/1.0',
        'Accept': '*/*'
      }, headers || {})
    }, res => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
        res.resume();
        return resolve(httpsGet(res.headers.location, headers));
      }
      const chunks = [];
      res.on('data', c => chunks.push(c));
      res.on('end', () => resolve({ status: res.statusCode, body: Buffer.concat(chunks), headers: res.headers }));
    });
    req.on('error', reject);
    req.setTimeout(25000, () => { req.destroy(new Error('timeout')); });
  });
}

/** Достаёт ссылку на mp3 и название трека со страницы Newgrounds. */
async function resolveSong(id) {
  const cachedMeta = fs.existsSync(songMetaPath(id)) ? JSON.parse(fs.readFileSync(songMetaPath(id), 'utf8')) : null;
  if (cachedMeta && cachedMeta.url) return cachedMeta;

  const page = await httpsGet('https://www.newgrounds.com/audio/listen/' + id);
  if (page.status !== 200) throw new Error('Newgrounds вернул ' + page.status);
  const html = page.body.toString('utf8');

  let url = null;
  const m1 = html.match(/https:\\?\/\\?\/audio\.ngfiles\.com\\?\/[^"'\\]+\.mp3/);
  if (m1) url = m1[0].replace(/\\\//g, '/');
  if (!url) {
    const m2 = html.match(/"url"\s*:\s*"([^"]+\.mp3[^"]*)"/);
    if (m2) url = m2[1].replace(/\\\//g, '/').replace(/\\u0026/g, '&');
  }
  if (!url) throw new Error('не нашёл mp3 на странице трека');

  let name = '';
  const t = html.match(/<title>([^<]+)<\/title>/i);
  if (t) name = t[1].replace(/\s*\|\s*Newgrounds.*$/i, '').trim();
  let artist = '';
  const a = html.match(/"artist"\s*:\s*"([^"]+)"/) || html.match(/by\s+<a[^>]*>([^<]+)<\/a>/i);
  if (a) artist = a[1].trim();

  const meta = { id, name, artist, url, resolved: now() };
  fs.writeFileSync(songMetaPath(id), JSON.stringify(meta));
  return meta;
}

async function ensureSongFile(id) {
  const f = songFilePath(id);
  if (fs.existsSync(f) && fs.statSync(f).size > 8192) return f;
  const meta = await resolveSong(id);
  const r = await httpsGet(meta.url, { Referer: 'https://www.newgrounds.com/audio/listen/' + id });
  if (r.status !== 200 || r.body.length < 8192) throw new Error('не скачалось (' + r.status + ')');
  fs.writeFileSync(f, r.body);
  meta.size = r.body.length;
  fs.writeFileSync(songMetaPath(id), JSON.stringify(meta));
  return f;
}

// ------------------------------------------------------------------ роутер

const routes = [];
const route = (method, pattern, handler) => routes.push({ method, pattern, handler });

function match(pattern, pathname) {
  const a = pattern.split('/').filter(Boolean);
  const b = pathname.split('/').filter(Boolean);
  if (a.length !== b.length) return null;
  const params = {};
  for (let i = 0; i < a.length; i++) {
    if (a[i][0] === ':') params[a[i].slice(1)] = decodeURIComponent(b[i]);
    else if (a[i] !== b[i]) return null;
  }
  return params;
}

// ---------- служебное ----------

route('GET', '/api/ping', (req, res) => ok(res, { ok: true, time: now(), users: Object.keys(db.users).length, levels: Object.keys(db.levels).length }));

route('GET', '/api/version', (req, res) => {
  let v = { version: '1.0.0', exe: '', apk: '', notes: '', date: '' };
  try { if (fs.existsSync(VERSION_FILE)) v = JSON.parse(fs.readFileSync(VERSION_FILE, 'utf8')); } catch {}
  ok(res, v);
});

route('GET', '/api/news', (req, res) => ok(res, (db.state.news || []).slice(-30).reverse()));

// ---------- аккаунты ----------

route('POST', '/api/register', async (req, res) => {
  const b = await readBody(req);
  const name = String(b.name || '').trim();
  const pass = String(b.pass || '');
  if (!/^[A-Za-z0-9_\-а-яА-ЯёЁ ]{3,20}$/.test(name)) return bad(res, 'Имя: 3–20 символов, без спецсимволов');
  if (pass.length < 3) return bad(res, 'Пароль слишком короткий');
  if (db.users[key(name)]) return bad(res, 'Такое имя уже занято');

  const salt = crypto.randomBytes(12).toString('hex');
  const u = {
    name, salt, pass: hash(pass, salt), created: now(),
    stars: 0, coins: 0, demons: 0, attempts: 0, jumps: 0,
    admin: key(name) === key(ADMIN), banned: false, about: '',
    friends: [], reqIn: [], reqOut: [], completed: {}
  };
  db.users[key(name)] = u;
  save.users();
  ok(res, { ok: true, token: newToken(name), account: pub(u) });
});

route('POST', '/api/login', async (req, res) => {
  const b = await readBody(req);
  const u = db.users[key(b.name)];
  if (!u) return bad(res, 'Игрок не найден');
  if (u.pass !== hash(String(b.pass || ''), u.salt)) return bad(res, 'Неверный пароль');
  if (u.banned) return bad(res, 'Аккаунт заблокирован', 403);
  if (key(u.name) === key(ADMIN)) { u.admin = true; save.users(); }
  ok(res, { ok: true, token: newToken(u.name), account: pub(u) });
});

route('GET', '/api/me', (req, res) => {
  const u = userOf(req);
  if (!u) return bad(res, 'Нужен вход', 401);
  ok(res, pub(u));
});

route('GET', '/api/users/:name', (req, res, p) => {
  const u = db.users[key(p.name)];
  if (!u) return bad(res, 'Игрок не найден', 404);
  ok(res, pub(u));
});

route('GET', '/api/users', (req, res, p, q) => {
  const term = key(q.get('q') || '');
  const list = Object.values(db.users)
    .filter(u => !term || key(u.name).includes(term))
    .slice(0, 50).map(pub);
  ok(res, list);
});

route('GET', '/api/top', (req, res) => {
  const list = Object.values(db.users)
    .sort((a, b) => (b.stars | 0) - (a.stars | 0) || (b.coins | 0) - (a.coins | 0))
    .slice(0, 100).map(pub);
  ok(res, list);
});

// ---------- уровни ----------

route('GET', '/api/levels', (req, res, p, q) => {
  rotateIfNeeded();
  const term = key(q.get('q') || '');
  const author = key(q.get('author') || '');
  const diff = parseInt(q.get('diff') || '-1', 10);
  const sort = q.get('sort') || 'recent';
  const page = Math.max(0, parseInt(q.get('page') || '0', 10));

  let list = Object.values(db.levels).filter(l => !l.hidden);
  if (term) list = list.filter(l => key(l.name).includes(term) || String(l.id) === term.replace('#', ''));
  if (author) list = list.filter(l => key(l.author) === author);
  if (diff >= 0) list = list.filter(l => (l.difficulty | 0) === diff);

  switch (sort) {
    case 'popular': list.sort((a, b) => (b.downloads | 0) - (a.downloads | 0)); break;
    case 'top': list.sort((a, b) => (b.likes | 0) - (a.likes | 0)); break;
    case 'featured': list = list.filter(l => l.featured); break;
    case 'epic': list = list.filter(l => l.epic); break;
    default: list.sort((a, b) => String(b.created).localeCompare(String(a.created)));
  }
  ok(res, list.slice(page * 50, page * 50 + 50).map(levelPub));
});

route('GET', '/api/levels/:id', (req, res, p) => {
  const l = db.levels[p.id];
  if (!l) return bad(res, 'Уровень не найден', 404);
  let data = '';
  try { data = fs.readFileSync(path.join(DATA_DIR, l.id + '.gdl'), 'utf8'); } catch {}
  l.downloads = (l.downloads | 0) + 1;
  save.levels();
  ok(res, Object.assign(levelPub(l), { data }));
});

route('POST', '/api/levels', async (req, res) => {
  const u = userOf(req);
  if (!u) return bad(res, 'Нужен вход', 401);
  if (u.banned) return bad(res, 'Аккаунт заблокирован', 403);

  const b = await readBody(req);
  const data = String(b.data || '');
  if (data.length < 20) return bad(res, 'Пустой уровень');
  if (data.length > 10 * 1024 * 1024) return bad(res, 'Уровень слишком большой');

  let id = parseInt(b.id || '0', 10);
  let l = id > 0 ? db.levels[id] : null;

  if (l) {
    const owner = key(l.author) === key(u.name);
    if (!owner && !u.admin) return bad(res, 'Это не твой уровень', 403);
    if (!owner && l.password && l.password !== String(b.password || '')) return bad(res, 'Неверный пароль уровня', 403);
  } else {
    // тот же локальный уровень не плодим: ищем по localId автора
    const existing = Object.values(db.levels).find(
      x => x.localId && b.localId && x.localId === b.localId && key(x.author) === key(u.name));
    if (existing) l = existing;
  }

  if (!l) {
    id = db.state.nextId++;
    l = {
      id, author: u.name, created: now(), likes: 0, downloads: 0, plays: 0,
      difficulty: 0, stars: 0, featured: false, epic: false, offline: false, version: 0
    };
    db.levels[id] = l;
  }

  l.name = String(b.name || 'Без названия').slice(0, 60);
  l.desc = String(b.desc || '').slice(0, 500);
  l.songId = parseInt(b.songId || '0', 10);
  l.songName = String(b.songName || '').slice(0, 120);
  if (!l.difficulty) l.difficulty = parseInt(b.difficulty || '0', 10);
  l.length = Number(b.length || 0);
  l.localId = b.localId || l.localId;
  l.password = String(b.password || l.password || '');
  l.version = (l.version | 0) + 1;
  l.updated = now();

  fs.writeFileSync(path.join(DATA_DIR, l.id + '.gdl'), data);
  save.levels();
  save.state();
  ok(res, levelPub(l));
});

route('POST', '/api/levels/:id/like', async (req, res, p) => {
  const u = userOf(req);
  const l = db.levels[p.id];
  if (!l) return bad(res, 'Уровень не найден', 404);
  const b = await readBody(req);
  l.likes = Math.max(0, (l.likes | 0) + (b.like === false ? -1 : 1));
  save.levels();
  ok(res, levelPub(l));
});

route('POST', '/api/levels/:id/complete', async (req, res, p) => {
  const u = userOf(req);
  const l = db.levels[p.id];
  if (!l) return bad(res, 'Уровень не найден', 404);
  const b = await readBody(req);
  const percent = Math.max(0, Math.min(100, Number(b.percent || 0)));
  const attempts = Math.max(0, parseInt(b.attempts || '0', 10));
  const coins = Math.max(0, Math.min(3, parseInt(b.coins || '0', 10)));

  l.plays = (l.plays | 0) + 1;
  save.levels();

  if (u) {
    u.attempts = (u.attempts | 0) + attempts;
    const sc = db.scores[l.id] || (db.scores[l.id] = {});
    const prev = sc[key(u.name)] || { percent: 0, coins: 0, attempts: 0 };
    sc[key(u.name)] = {
      user: u.name,
      percent: Math.max(prev.percent, percent),
      coins: Math.max(prev.coins, coins),
      attempts: (prev.attempts | 0) + attempts,
      date: now()
    };
    u.completed = u.completed || {};
    if (percent >= 100 && !u.completed[l.id]) {
      u.completed[l.id] = now();
      u.stars = (u.stars | 0) + (DIFF_STARS[l.difficulty | 0] || 0);
      if (isDemon(l.difficulty | 0)) u.demons = (u.demons | 0) + 1;
    }
    const gainCoins = Math.max(0, coins - (prev.coins | 0));
    if (gainCoins) u.coins = (u.coins | 0) + gainCoins;
    save.users(); save.scores();
  }
  ok(res, levelPub(l));
});

route('GET', '/api/levels/:id/scores', (req, res, p) => {
  const sc = db.scores[p.id] || {};
  const list = Object.values(sc).sort((a, b) => b.percent - a.percent || a.attempts - b.attempts).slice(0, 100);
  ok(res, list);
});

route('POST', '/api/levels/:id/delete', (req, res, p) => {
  const u = userOf(req);
  const l = db.levels[p.id];
  if (!l) return bad(res, 'Уровень не найден', 404);
  if (!u || (key(l.author) !== key(u.name) && !u.admin)) return bad(res, 'Нельзя', 403);
  delete db.levels[p.id];
  delete db.scores[p.id];
  try { fs.unlinkSync(path.join(DATA_DIR, p.id + '.gdl')); } catch {}
  save.levels(); save.scores();
  ok(res);
});

// ---------- daily / weekly / offline ----------

route('GET', '/api/daily', (req, res) => {
  rotateIfNeeded();
  const st = db.state.daily;
  const l = st && db.levels[st.id];
  ok(res, { date: st ? st.date : '', level: l ? levelPub(l) : null, secondsLeft: secondsLeftDay() });
});

route('GET', '/api/weekly', (req, res) => {
  rotateIfNeeded();
  const st = db.state.weekly;
  const l = st && db.levels[st.id];
  ok(res, { date: st ? st.date : '', level: l ? levelPub(l) : null, secondsLeft: secondsLeftWeek() });
});

route('GET', '/api/offline', (req, res) => {
  const list = Object.values(db.levels).filter(l => l.offline).map(levelPub);
  ok(res, list);
});

// ---------- друзья ----------

function requireUser(req, res) {
  const u = userOf(req);
  if (!u) { bad(res, 'Нужен вход', 401); return null; }
  return u;
}

route('GET', '/api/friends', (req, res) => {
  const u = requireUser(req, res); if (!u) return;
  const list = (u.friends || []).map(n => {
    const f = db.users[key(n)];
    return f ? { name: f.name, stars: f.stars | 0, since: '' } : null;
  }).filter(Boolean);
  ok(res, list);
});

route('GET', '/api/friends/requests', (req, res) => {
  const u = requireUser(req, res); if (!u) return;
  const inc = (u.reqIn || []).map(n => ({ from: n, to: u.name, created: '' }));
  const out = (u.reqOut || []).map(n => ({ from: u.name, to: n, created: '' }));
  ok(res, inc.concat(out));
});

route('POST', '/api/friends/request', async (req, res) => {
  const u = requireUser(req, res); if (!u) return;
  const b = await readBody(req);
  const t = db.users[key(b.name)];
  if (!t) return bad(res, 'Игрок не найден', 404);
  if (key(t.name) === key(u.name)) return bad(res, 'Сам себе друг и так');
  u.friends = u.friends || []; t.friends = t.friends || [];
  if (u.friends.some(n => key(n) === key(t.name))) return bad(res, 'Вы уже друзья');
  u.reqOut = u.reqOut || []; t.reqIn = t.reqIn || [];
  if (!u.reqOut.some(n => key(n) === key(t.name))) u.reqOut.push(t.name);
  if (!t.reqIn.some(n => key(n) === key(u.name))) t.reqIn.push(u.name);
  save.users();
  ok(res);
});

route('POST', '/api/friends/accept', async (req, res) => {
  const u = requireUser(req, res); if (!u) return;
  const b = await readBody(req);
  const t = db.users[key(b.name)];
  if (!t) return bad(res, 'Игрок не найден', 404);
  u.reqIn = (u.reqIn || []).filter(n => key(n) !== key(t.name));
  t.reqOut = (t.reqOut || []).filter(n => key(n) !== key(u.name));
  u.friends = u.friends || []; t.friends = t.friends || [];
  if (!u.friends.some(n => key(n) === key(t.name))) u.friends.push(t.name);
  if (!t.friends.some(n => key(n) === key(u.name))) t.friends.push(u.name);
  save.users();
  ok(res);
});

route('POST', '/api/friends/decline', async (req, res) => {
  const u = requireUser(req, res); if (!u) return;
  const b = await readBody(req);
  const t = db.users[key(b.name)];
  if (!t) return bad(res, 'Игрок не найден', 404);
  u.reqIn = (u.reqIn || []).filter(n => key(n) !== key(t.name));
  t.reqOut = (t.reqOut || []).filter(n => key(n) !== key(u.name));
  save.users();
  ok(res);
});

route('POST', '/api/friends/remove', async (req, res) => {
  const u = requireUser(req, res); if (!u) return;
  const b = await readBody(req);
  const t = db.users[key(b.name)];
  if (!t) return bad(res, 'Игрок не найден', 404);
  u.friends = (u.friends || []).filter(n => key(n) !== key(t.name));
  t.friends = (t.friends || []).filter(n => key(n) !== key(u.name));
  save.users();
  ok(res);
});

// ---------- музыка ----------

route('GET', '/api/music/:id', async (req, res, p) => {
  const id = parseInt(p.id, 10);
  if (!id || id < 0) return bad(res, 'Нужен положительный ID трека');
  try {
    const meta = await resolveSong(id);
    const cached = fs.existsSync(songFilePath(id));
    ok(res, {
      id, name: meta.name || ('Track ' + id), artist: meta.artist || '',
      size: cached ? fs.statSync(songFilePath(id)).size : 0,
      url: '/api/music/' + id + '/file', cached
    });
  } catch (e) { bad(res, 'Не вышло получить трек: ' + e.message, 502); }
});

route('GET', '/api/music/:id/file', async (req, res, p) => {
  const id = parseInt(p.id, 10);
  if (!id || id < 0) return bad(res, 'Нужен положительный ID трека');
  try {
    const f = await ensureSongFile(id);
    const stat = fs.statSync(f);
    res.writeHead(200, {
      'Content-Type': 'audio/mpeg',
      'Content-Length': stat.size,
      'Access-Control-Allow-Origin': '*',
      'Cache-Control': 'public, max-age=604800'
    });
    fs.createReadStream(f).pipe(res);
  } catch (e) { bad(res, 'Трек недоступен: ' + e.message, 502); }
});

// ---------- админка ----------

function requireAdmin(req, res) {
  const u = userOf(req);
  if (!u || (!u.admin && key(u.name) !== key(ADMIN))) { bad(res, 'Только для админа', 403); return null; }
  return u;
}

route('GET', '/api/admin/users', (req, res) => {
  if (!requireAdmin(req, res)) return;
  ok(res, Object.values(db.users).map(pub));
});

route('GET', '/api/admin/stats', (req, res) => {
  if (!requireAdmin(req, res)) return;
  ok(res, {
    users: Object.keys(db.users).length,
    levels: Object.keys(db.levels).length,
    songs: fs.readdirSync(MUSIC_DIR).filter(f => f.endsWith('.mp3')).length,
    daily: db.state.daily, weekly: db.state.weekly,
    moderation: MODERATION, uptime: Math.floor(process.uptime())
  });
});

route('POST', '/api/admin/ban', async (req, res) => {
  if (!requireAdmin(req, res)) return;
  const b = await readBody(req);
  const t = db.users[key(b.name)];
  if (!t) return bad(res, 'Игрок не найден', 404);
  t.banned = !!b.ban;
  save.users();
  ok(res);
});

route('POST', '/api/admin/delete-user', async (req, res) => {
  if (!requireAdmin(req, res)) return;
  const b = await readBody(req);
  const t = db.users[key(b.name)];
  if (!t) return bad(res, 'Игрок не найден', 404);
  for (const l of Object.values(db.levels)) {
    if (key(l.author) === key(t.name)) {
      delete db.levels[l.id];
      try { fs.unlinkSync(path.join(DATA_DIR, l.id + '.gdl')); } catch {}
    }
  }
  delete db.users[key(b.name)];
  save.users(); save.levels();
  ok(res);
});

route('POST', '/api/admin/rate', async (req, res) => {
  if (!requireAdmin(req, res)) return;
  const b = await readBody(req);
  const l = db.levels[parseInt(b.id, 10)];
  if (!l) return bad(res, 'Уровень не найден', 404);
  l.difficulty = Math.max(0, Math.min(11, parseInt(b.difficulty || '0', 10)));
  l.stars = DIFF_STARS[l.difficulty] || 0;
  l.featured = !!b.featured;
  l.epic = !!b.epic;
  save.levels();
  ok(res, levelPub(l));
});

route('POST', '/api/admin/daily', async (req, res) => {
  if (!requireAdmin(req, res)) return;
  const b = await readBody(req);
  const l = db.levels[parseInt(b.id, 10)];
  if (!l) return bad(res, 'Уровень не найден', 404);
  db.state.daily = { id: l.id, date: dayStamp() };
  save.state();
  ok(res, levelPub(l));
});

route('POST', '/api/admin/weekly', async (req, res) => {
  if (!requireAdmin(req, res)) return;
  const b = await readBody(req);
  const l = db.levels[parseInt(b.id, 10)];
  if (!l) return bad(res, 'Уровень не найден', 404);
  db.state.weekly = { id: l.id, week: weekStamp(), date: dayStamp() };
  save.state();
  ok(res, levelPub(l));
});

/** Продвинуть текущий daily в weekly. */
route('POST', '/api/admin/daily-to-weekly', (req, res) => {
  if (!requireAdmin(req, res)) return;
  const d = db.state.daily;
  if (!d || !db.levels[d.id]) return bad(res, 'Сейчас нет уровня дня');
  db.state.weekly = { id: d.id, week: weekStamp(), date: dayStamp() };
  const l = db.levels[d.id];
  l.offline = true;               // weekly всегда доступен офлайн
  save.state(); save.levels();
  ok(res, levelPub(l));
});

route('POST', '/api/admin/offline', async (req, res) => {
  if (!requireAdmin(req, res)) return;
  const b = await readBody(req);
  const l = db.levels[parseInt(b.id, 10)];
  if (!l) return bad(res, 'Уровень не найден', 404);
  l.offline = !!b.offline;
  save.levels();
  ok(res, levelPub(l));
});

route('POST', '/api/admin/broadcast', async (req, res) => {
  if (!requireAdmin(req, res)) return;
  const b = await readBody(req);
  const text = String(b.text || '').slice(0, 500);
  if (!text) return bad(res, 'Пустой текст');
  db.state.news = db.state.news || [];
  db.state.news.push('[' + now().slice(0, 16).replace('T', ' ') + '] ' + text);
  save.state();
  ok(res);
});

// ------------------------------------------------------------------ сервер

const server = http.createServer(async (req, res) => {
  if (req.method === 'OPTIONS') return send(res, 204, {});
  let u;
  try { u = new URL(req.url, 'http://x'); } catch { return bad(res, 'bad url'); }

  for (const r of routes) {
    if (r.method !== req.method) continue;
    const params = match(r.pattern, u.pathname);
    if (!params) continue;
    try {
      await r.handler(req, res, params, u.searchParams);
    } catch (e) {
      console.error('handler', u.pathname, e);
      if (!res.headersSent) bad(res, 'Ошибка сервера: ' + e.message, 500);
    }
    return;
  }

  if (u.pathname === '/' || u.pathname === '/api') {
    return ok(res, {
      name: 'GEO DASH API', brand: 'Winnigames2024', port: PORT,
      site: 'http://geodash.winnigames2024.ru/',
      endpoints: routes.map(r => r.method + ' ' + r.pattern)
    });
  }
  bad(res, 'Нет такого метода', 404);
});

server.listen(PORT, '0.0.0.0', () => {
  rotateIfNeeded();
  console.log(`GEO DASH API слушает 0.0.0.0:${PORT}`);
  console.log(`данные: ${DIR}, админ: ${ADMIN}, модерация музыки: ${MODERATION ? 'вкл' : 'выкл'}`);
});

process.on('uncaughtException', e => console.error('uncaught', e));
process.on('unhandledRejection', e => console.error('unhandled', e));
