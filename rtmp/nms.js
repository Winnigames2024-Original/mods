// NareNet RTMP (как у VK/OBS): принимает RTMP-поток от OBS и раздаёт его как HLS.
// OBS -> rtmp://ХОСТ/live/<ключ>  ->  зрителям /live-hls/<ключ>/index.m3u8
// Авторизация: при начале публикации спрашиваем у NareNet, чей это ключ; чужой/пустой — отклоняем.
const NodeMediaServer = require('node-media-server');
const http = require('http');

const HOOK   = process.env.NARE_HOOK || 'http://127.0.0.1:2445/stream/rtmp/event';
const SECRET = process.env.NARE_RTMP_SECRET || 'nare-rtmp-9f3b';
const FFMPEG = process.env.NARE_FFMPEG || '/usr/bin/ffmpeg';
const HLS_ROOT = process.env.NARE_HLS_ROOT || '/var/lib/narenet/hls';

function notify(action, key, cb) {
  let u; try { u = new URL(HOOK); } catch (e) { return cb && cb(0); }
  const data = Buffer.from(JSON.stringify({ secret: SECRET, action, key }));
  const r = http.request({ hostname: u.hostname, port: u.port || 80, path: u.pathname, method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Content-Length': data.length } },
    res => { let b = ''; res.on('data', c => b += c); res.on('end', () => cb && cb(res.statusCode, b)); });
  r.on('error', () => cb && cb(0));
  r.write(data); r.end();
}

const config = {
  rtmp: { port: 1935, chunk_size: 60000, gop_cache: true, ping: 30, ping_timeout: 60 },
  http: { port: 8000, mediaroot: HLS_ROOT, allow_origin: '*' },
  trans: { ffmpeg: FFMPEG, tasks: [
    { app: 'live', hls: true, hlsFlags: '[hls_time=2:hls_list_size=4:hls_flags=delete_segments]' }
  ] }
};

const nms = new NodeMediaServer(config);

nms.on('prePublish', (id, StreamPath, args) => {
  const key = String(StreamPath).split('/').pop();
  notify('publish', key, (code) => {
    if (code !== 200) {   // ключ не признан NareNet — выгоняем
      try { const s = nms.getSession(id); s.reject(); } catch (e) {}
    }
  });
});
nms.on('donePublish', (id, StreamPath) => {
  notify('done', String(StreamPath).split('/').pop());
});

nms.run();
console.log('NareNet RTMP слушает: rtmp://0.0.0.0:1935/live , HLS на :8000 ->', HLS_ROOT);
