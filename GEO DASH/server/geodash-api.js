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
const IMAGE_DIR = path.join(DIR, 'images');

for (const d of [DIR, DATA_DIR, MUSIC_DIR, IMAGE_DIR]) fs.mkdirSync(d, { recursive: true });

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
  state: loadJson('state.json', { nextId: 1, daily: null, weekly: null, news: [], songs: {},
                                  nextSongId: 2000001, nextImageId: 1 }),
  images: loadJson('images.json', {}),
  comments: loadJson('comments.json', {})
};

const save = {
  users: () => saveJson('users.json', db.users),
  levels: () => saveJson('levels.json', db.levels),
  scores: () => saveJson('scores.json', db.scores),
  tokens: () => saveJson('tokens.json', db.tokens),
  state: () => saveJson('state.json', db.state),
  images: () => saveJson('images.json', db.images),
  comments: () => saveJson('comments.json', db.comments)
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
    name: u.name, avatar: u.avatar | 0,
    stars: u.stars | 0, coins: u.coins | 0, demons: u.demons | 0,
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
    daily: !!(db.state.daily && db.state.daily.id === l.id),
    weekly: !!(db.state.weekly && db.state.weekly.id === l.id),
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

  // если трек уже загружен вручную через админку — отдаём его
  if (fs.existsSync(songFilePath(id)) && fs.statSync(songFilePath(id)).size > 8192) {
    const meta = { id, name: 'Track ' + id, artist: '', url: 'local', resolved: now(), local: true };
    fs.writeFileSync(songMetaPath(id), JSON.stringify(meta));
    return meta;
  }

  const pages = [
    'https://www.newgrounds.com/audio/listen/' + id,
    'https://www.newgrounds.com/audio/listen/' + id + '/'
  ];
  let page = null, lastStatus = 0;
  for (const u of pages) {
    try {
      page = await httpsGet(u, {
        'Accept': 'text/html,application/xhtml+xml',
        'Accept-Language': 'en-US,en;q=0.9',
        'Referer': 'https://www.newgrounds.com/audio'
      });
      lastStatus = page.status;
      if (page.status === 200) break;
    } catch (e) { lastStatus = 0; }
  }
  if (!page || page.status !== 200)
    throw new Error('Newgrounds вернул ' + lastStatus +
      ' — загрузи mp3 вручную: админка сайта → «Музыка по ID»');
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
  if (meta.local) return f;
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

/** Скачать .gdl файлом — для кнопки на сайте. */
route('GET', '/api/levels/:id/data', (req, res, p) => {
  const l = db.levels[p.id];
  if (!l) return bad(res, 'Уровень не найден', 404);
  let data = '';
  try { data = fs.readFileSync(path.join(DATA_DIR, l.id + '.gdl'), 'utf8'); } catch {}
  if (!data) return bad(res, 'Файл уровня пропал', 404);
  const name = String(l.name || ('level' + l.id)).replace(/[^\w\-. ]+/g, '_').slice(0, 60);
  const buf = Buffer.from(data, 'utf8');
  res.writeHead(200, {
    'Content-Type': 'application/json; charset=utf-8',
    'Content-Length': buf.length,
    'Content-Disposition': 'attachment; filename="' + name + '.gdl"',
    'Access-Control-Allow-Origin': '*'
  });
  res.end(buf);
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

/** Загрузка mp3 вручную (админ): raw-тело запроса = файл. Нужна, если Newgrounds недоступен с сервера. */
route('POST', '/api/music/:id/upload', async (req, res, p) => {
  const u = userOf(req);
  if (!u || (!u.admin && key(u.name) !== key(ADMIN))) return bad(res, 'Только для админа', 403);
  const id = parseInt(p.id, 10);
  if (!id || id < 0) return bad(res, 'Нужен положительный ID трека');

  const chunks = [];
  let size = 0, tooBig = false;
  req.on('data', c => {
    size += c.length;
    if (size > 40 * 1024 * 1024) { tooBig = true; req.destroy(); return; }
    chunks.push(c);
  });
  req.on('end', () => {
    if (tooBig) return bad(res, 'Файл больше 40 МБ');
    const buf = Buffer.concat(chunks);
    if (buf.length < 8192) return bad(res, 'Файл слишком маленький');
    fs.writeFileSync(songFilePath(id), buf);
    let title = 'Track ' + id;
    try { title = decodeURIComponent(String(req.headers['x-song-name'] || '')) || title; } catch {}
    fs.writeFileSync(songMetaPath(id), JSON.stringify({
      id, name: title, artist: String(req.headers['x-song-artist'] || ''), url: 'local', local: true,
      size: buf.length, resolved: now()
    }));
    ok(res, { ok: true, id, size: buf.length });
  });
  req.on('error', () => bad(res, 'Ошибка загрузки'));
});

// ================================================================ своя музыка с сайта

/** Принять тело запроса как бинарник (для mp3 и картинок). */
function readRaw(req, limitMb) {
  return new Promise((resolve) => {
    const chunks = [];
    let size = 0, tooBig = false;
    req.on('data', c => {
      size += c.length;
      if (size > limitMb * 1024 * 1024) { tooBig = true; req.destroy(); return; }
      chunks.push(c);
    });
    req.on('end', () => resolve(tooBig ? null : Buffer.concat(chunks)));
    req.on('error', () => resolve(null));
  });
}

function headerText(req, name) {
  try { return decodeURIComponent(String(req.headers[name] || '')).slice(0, 120); }
  catch { return ''; }
}

/** Залить свой трек: ID выдаётся сам, трек сразу виден всем в игре. */
route('POST', '/api/music/upload', async (req, res) => {
  const u = userOf(req);
  if (!u) return bad(res, 'Нужен вход', 401);
  if (u.banned) return bad(res, 'Аккаунт заблокирован', 403);

  const buf = await readRaw(req, 40);
  if (!buf) return bad(res, 'Файл больше 40 МБ');
  if (buf.length < 8192) return bad(res, 'Файл слишком маленький');

  const id = db.state.nextSongId++ || 2000001;
  db.state.nextSongId = id + 1;
  save.state();

  fs.writeFileSync(songFilePath(id), buf);
  fs.writeFileSync(songMetaPath(id), JSON.stringify({
    id,
    name: headerText(req, 'x-song-name') || ('Трек ' + id),
    artist: headerText(req, 'x-song-artist') || u.name,
    author: u.name, url: 'local', local: true,
    size: buf.length, resolved: now()
  }));
  ok(res, { ok: true, id, size: buf.length });
});

// ================================================================ картинки (toolbox)

function imageFile(id, ext) { return path.join(IMAGE_DIR, id + (ext || '.png')); }

/** Залить картинку: png или jpg, вернётся ID для объекта «картинка». */
route('POST', '/api/images/upload', async (req, res) => {
  const u = userOf(req);
  if (!u) return bad(res, 'Нужен вход', 401);
  if (u.banned) return bad(res, 'Аккаунт заблокирован', 403);

  const buf = await readRaw(req, 8);
  if (!buf) return bad(res, 'Картинка больше 8 МБ');
  if (buf.length < 64) return bad(res, 'Файл слишком маленький');

  // по сигнатуре понимаем формат
  let ext = null;
  if (buf[0] === 0x89 && buf[1] === 0x50) ext = '.png';
  else if (buf[0] === 0xFF && buf[1] === 0xD8) ext = '.jpg';
  else if (buf.slice(0, 3).toString() === 'GIF') ext = '.gif';
  if (!ext) return bad(res, 'Нужен png, jpg или gif');

  const id = db.state.nextImageId++ || 1;
  db.state.nextImageId = id + 1;
  fs.writeFileSync(imageFile(id, ext), buf);
  db.images[id] = {
    id, ext,
    name: headerText(req, 'x-image-name') || ('Картинка ' + id),
    author: u.name, size: buf.length, created: now()
  };
  save.images(); save.state();
  ok(res, { ok: true, id, ext, size: buf.length });
});

/** Список картинок: id, название, автор, размер. */
route('GET', '/api/images', (req, res) => {
  const list = Object.values(db.images)
    .sort((a, b) => b.id - a.id)
    .slice(0, 500)
    .map(i => ({ id: i.id, name: i.name, author: i.author, size: i.size, ext: i.ext,
                 url: '/api/images/' + i.id + '/file' }));
  ok(res, list);
});

route('GET', '/api/images/:id', (req, res, p) => {
  const i = db.images[parseInt(p.id, 10)];
  if (!i) return bad(res, 'Картинка не найдена', 404);
  ok(res, i);
});

route('GET', '/api/images/:id/file', (req, res, p) => {
  const i = db.images[parseInt(p.id, 10)];
  if (!i) return bad(res, 'Картинка не найдена', 404);
  const f = imageFile(i.id, i.ext);
  if (!fs.existsSync(f)) return bad(res, 'Файл пропал', 404);
  const type = i.ext === '.jpg' ? 'image/jpeg' : i.ext === '.gif' ? 'image/gif' : 'image/png';
  const data = fs.readFileSync(f);
  res.writeHead(200, {
    'Content-Type': type,
    'Content-Length': data.length,
    'Access-Control-Allow-Origin': '*',
    'Cache-Control': 'public, max-age=604800'
  });
  res.end(data);
});

route('POST', '/api/images/:id/delete', (req, res, p) => {
  const u = userOf(req);
  const i = db.images[parseInt(p.id, 10)];
  if (!i) return bad(res, 'Картинка не найдена', 404);
  if (!u || (key(u.name) !== key(i.author) && !u.admin && key(u.name) !== key(ADMIN)))
    return bad(res, 'Это не твоя картинка', 403);
  try { fs.unlinkSync(imageFile(i.id, i.ext)); } catch {}
  delete db.images[i.id];
  save.images();
  ok(res);
});

// ================================================================ комментарии

/** Комментарии к уровню: свежие сверху. */
route('GET', '/api/levels/:id/comments', (req, res, p) => {
  const id = parseInt(p.id, 10);
  const list = (db.comments[id] || []).slice(-200).reverse()
    .map(c => ({ id: c.id, author: c.author, text: c.text, created: c.created,
                 avatar: (db.users[key(c.author)] || {}).avatar | 0 }));
  ok(res, list);
});

/** Написать комментарий. */
route('POST', '/api/levels/:id/comments', async (req, res, p) => {
  const u = userOf(req);
  if (!u) return bad(res, 'Нужен вход', 401);
  if (u.banned) return bad(res, 'Аккаунт заблокирован', 403);

  const id = parseInt(p.id, 10);
  if (!db.levels[id]) return bad(res, 'Уровень не найден', 404);

  const b = await readBody(req);
  const text = String(b.text || '').trim().slice(0, 300);
  if (text.length < 1) return bad(res, 'Пустой комментарий');

  const arr = db.comments[id] || (db.comments[id] = []);
  const last = arr.filter(c => key(c.author) === key(u.name)).pop();
  if (last && Date.now() - new Date(last.created).getTime() < 5000)
    return bad(res, 'Слишком часто, подожди пару секунд');

  const c = { id: Date.now() + '-' + Math.random().toString(36).slice(2, 6),
              author: u.name, text, created: now() };
  arr.push(c);
  if (arr.length > 500) arr.splice(0, arr.length - 500);
  save.comments();
  ok(res, c);
});

/** Удалить комментарий: свой, свой уровень или админ. */
route('POST', '/api/levels/:id/comments/delete', async (req, res, p) => {
  const u = userOf(req);
  if (!u) return bad(res, 'Нужен вход', 401);
  const id = parseInt(p.id, 10);
  const b = await readBody(req);
  const arr = db.comments[id] || [];
  const idx = arr.findIndex(c => c.id === String(b.commentId || ''));
  if (idx < 0) return bad(res, 'Комментарий не найден', 404);

  const lvl = db.levels[id] || {};
  const mine = key(arr[idx].author) === key(u.name);
  const owner = key(lvl.author || '') === key(u.name);
  if (!mine && !owner && !u.admin && key(u.name) !== key(ADMIN))
    return bad(res, 'Нельзя удалить чужой комментарий', 403);

  arr.splice(idx, 1);
  save.comments();
  ok(res);
});

// ================================================================ аватарка

/** Поставить себе аватарку из картинок toolbox. */
route('POST', '/api/me/avatar', async (req, res) => {
  const u = userOf(req);
  if (!u) return bad(res, 'Нужен вход', 401);
  const b = await readBody(req);
  const img = parseInt(b.imageId || '0', 10);
  if (img && !db.images[img]) return bad(res, 'Такой картинки нет', 404);
  u.avatar = img || 0;
  save.users();
  ok(res, pub(u));
});

// ================================================================ мультиплеер

/** Комнаты в памяти: ключ — уровень + номер комнаты. */
const mpRooms = new Map();

function mpCleanup() {
  const t = Date.now();
  for (const [key2, room] of mpRooms) {
    for (const [name, pl] of Object.entries(room.players))
      if (t - pl.t > 12000) delete room.players[name];
    if (!Object.keys(room.players).length && t - room.touched > 60000) mpRooms.delete(key2);
  }
}
setInterval(mpCleanup, 5000).unref?.();

/** Зайти в комнату уровня: сервер сам находит свободную по вместимости. */
route('POST', '/api/mp/join', async (req, res) => {
  const u = userOf(req);
  if (!u) return bad(res, 'Нужен вход', 401);
  const b = await readBody(req);
  const levelId = parseInt(b.levelId || '0', 10);
  const max = Math.max(2, Math.min(50, parseInt(b.maxPlayers || '4', 10)));
  if (!levelId) return bad(res, 'Нужен ID уровня');

  mpCleanup();
  let room = null;
  for (const [k, r] of mpRooms)
    if (r.levelId === levelId && Object.keys(r.players).length < r.max) { room = r; break; }
  if (!room) {
    room = { id: levelId + '-' + Math.random().toString(36).slice(2, 8),
             levelId, max, players: {}, touched: Date.now() };
    mpRooms.set(room.id, room);
  }
  room.players[key(u.name)] = { name: u.name, x: 0, y: 0, mode: 0, dir: 1, dead: false,
                                percent: 0, t: Date.now() };
  room.touched = Date.now();
  ok(res, { ok: true, room: room.id, max: room.max, players: Object.keys(room.players).length });
});

/** Отправить свои координаты и получить всех остальных. */
route('POST', '/api/mp/state', async (req, res) => {
  const u = userOf(req);
  if (!u) return bad(res, 'Нужен вход', 401);
  const b = await readBody(req);
  const room = mpRooms.get(String(b.room || ''));
  if (!room) return bad(res, 'Комната закрыта', 404);

  room.touched = Date.now();
  room.players[key(u.name)] = {
    name: u.name,
    x: +b.x || 0, y: +b.y || 0,
    mode: parseInt(b.mode || '0', 10) | 0,
    dir: parseInt(b.dir || '1', 10) | 0,
    mini: !!b.mini, dead: !!b.dead,
    percent: Math.max(0, Math.min(100, +b.percent || 0)),
    color: parseInt(b.color || '0', 10) | 0,
    t: Date.now()
  };
  const others = Object.values(room.players)
    .filter(p => key(p.name) !== key(u.name) && Date.now() - p.t < 6000)
    .map(p => ({ name: p.name, x: p.x, y: p.y, mode: p.mode, dir: p.dir,
                 mini: !!p.mini, dead: !!p.dead, percent: p.percent, color: p.color }));
  ok(res, { ok: true, players: others, room: room.id, max: room.max });
});

route('POST', '/api/mp/leave', async (req, res) => {
  const u = userOf(req);
  const b = await readBody(req);
  const room = mpRooms.get(String(b.room || ''));
  if (room && u) { delete room.players[key(u.name)]; }
  ok(res);
});

/** Сколько народу сейчас в уровне. */
route('GET', '/api/mp/rooms', (req, res) => {
  mpCleanup();
  const list = [];
  for (const r of mpRooms.values())
    list.push({ room: r.id, levelId: r.levelId, max: r.max, players: Object.keys(r.players).length });
  ok(res, list);
});

/** Список треков, которые уже лежат на сервере. */
route('GET', '/api/music', (req, res) => {
  const list = fs.readdirSync(MUSIC_DIR)
    .filter(f => f.endsWith('.mp3'))
    .map(f => {
      const id = parseInt(f, 10);
      let meta = {};
      try { meta = JSON.parse(fs.readFileSync(songMetaPath(id), 'utf8')); } catch {}
      return {
        id, name: meta.name || ('Track ' + id), artist: meta.artist || '',
        size: fs.statSync(path.join(MUSIC_DIR, f)).size, url: '/songs/' + f, cached: true
      };
    })
    .sort((a, b) => a.id - b.id);
  ok(res, list);
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

// ---- назначить или снять админа
route('POST', '/api/admin/grant', async (req, res) => {
  const me = requireAdmin(req, res);
  if (!me) return;
  const b = await readBody(req);
  const t = db.users[key(b.name)];
  if (!t) return bad(res, 'Игрок не найден', 404);
  if (key(t.name) === key(ADMIN) && !b.admin) return bad(res, 'Главного админа снять нельзя', 400);
  t.admin = !!b.admin;
  save.users();
  ok(res, pub(t));
});

// ---- выдать звёзды, монеты и демонов
route('POST', '/api/admin/give', async (req, res) => {
  if (!requireAdmin(req, res)) return;
  const b = await readBody(req);
  const t = db.users[key(b.name)];
  if (!t) return bad(res, 'Игрок не найден', 404);
  t.stars = Math.max(0, (t.stars | 0) + (parseInt(b.stars || '0', 10) || 0));
  t.coins = Math.max(0, (t.coins | 0) + (parseInt(b.coins || '0', 10) || 0));
  t.demons = Math.max(0, (t.demons | 0) + (parseInt(b.demons || '0', 10) || 0));
  save.users();
  ok(res, pub(t));
});

// ---- сбросить пароль игроку
route('POST', '/api/admin/reset-password', async (req, res) => {
  if (!requireAdmin(req, res)) return;
  const b = await readBody(req);
  const t = db.users[key(b.name)];
  if (!t) return bad(res, 'Игрок не найден', 404);
  const pass = String(b.password || '');
  if (pass.length < 3) return bad(res, 'Пароль — от 3 символов', 400);
  t.salt = crypto.randomBytes(12).toString('hex');
  t.pass = hash(pass, t.salt);
  for (const [tok, v] of Object.entries(db.tokens))
    if (key(v.name) === key(t.name)) delete db.tokens[tok];
  save.users(); save.tokens();
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
