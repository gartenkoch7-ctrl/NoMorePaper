// Registers the PWA service worker. Kept as a file (not inline) so a strict CSP stays possible.
if ('serviceWorker' in navigator) {
  navigator.serviceWorker.register('service-worker.js').catch(function (error) {
    console.warn('Service worker registration failed', error);
  });
}
