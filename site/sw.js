// Service worker NareLauncher (PWA). Кэшируем только статику — страницы всегда свежие.
const CACHE = 'nare-v1';

self.addEventListener('install', e => { self.skipWaiting(); });
self.addEventListener('activate', e => {
  e.waitUntil(
    caches.keys().then(ks => Promise.all(ks.filter(k => k !== CACHE).map(k => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', e => {
  if (e.request.method !== 'GET') return;
  let url;
  try { url = new URL(e.request.url); } catch (_) { return; }
  if (url.origin !== location.origin) return;
  // кэш-первым только для статики; HTML/страницы и API — всегда из сети
  if (/\.(css|js|png|jpg|jpeg|webp|svg|gif|ico|woff2?)$/i.test(url.pathname)) {
    e.respondWith(
      caches.match(e.request).then(c => c || fetch(e.request).then(r => {
        if (r && r.ok) { const cl = r.clone(); caches.open(CACHE).then(ca => ca.put(e.request, cl)); }
        return r;
      }).catch(() => c))
    );
  }
});
