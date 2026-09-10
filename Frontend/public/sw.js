// Only these public, non-personal files belong in the offline cache. Bump the version when they change.
const CachePrefix = 'askarabbi-offline-';
const CacheName = `${CachePrefix}__BUILD_VERSION__`;
const OfflineUrl = '/offline.html';
const OfflineAssets = __OFFLINE_ASSETS__;

self.addEventListener('install', event => {
  event.waitUntil(caches.open(CacheName).then(cache => cache.addAll(
    OfflineAssets.map(path => new Request(new URL(path, self.location.origin), { cache: 'reload', credentials: 'omit' })),
  )));
  // Let a new worker wait naturally. Never reload or replace an active conversation to install an update.
});

self.addEventListener('activate', event => {
  event.waitUntil((async () => {
    const keys = await caches.keys();
    await Promise.all(keys.filter(key => key.startsWith(CachePrefix) && key !== CacheName).map(key => caches.delete(key)));
    await self.clients.claim();
  })());
});

self.addEventListener('fetch', event => {
  const request = event.request;
  const url = new URL(request.url);
  if (request.method !== 'GET' || url.origin !== self.location.origin || request.headers.has('Authorization')
    || url.pathname === '/api' || url.pathname.startsWith('/api/')) {
    return;
  }

  if (request.mode === 'navigate') {
    // Network-only HTML keeps sign-in/reset links and deployments fresh; never cache URLs or account pages.
    event.respondWith(fetch(request).catch(async () => {
      const cache = await caches.open(CacheName);
      return await cache.match(OfflineUrl) ?? new Response('AskRabbi is offline. Reconnect and try again.', {
        status: 503,
        headers: { 'Content-Type': 'text/plain; charset=utf-8' },
      });
    }));
    return;
  }

  if (url.search === '' && OfflineAssets.includes(url.pathname)) {
    event.respondWith(caches.open(CacheName).then(async cache => await cache.match(url.pathname) ?? fetch(request)));
  }
});
