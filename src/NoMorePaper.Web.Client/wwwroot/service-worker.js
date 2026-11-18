// Minimal service worker: makes the app installable as a PWA.
// Deliberately no caching and no fetch handler yet (offline logic comes later), so every
// request still goes straight to the server and nothing stale is ever shown.
self.addEventListener('install', function () {
  self.skipWaiting();
});

self.addEventListener('activate', function (event) {
  event.waitUntil(self.clients.claim());
});
