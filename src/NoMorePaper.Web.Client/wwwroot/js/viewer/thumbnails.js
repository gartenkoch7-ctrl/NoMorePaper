// Thumbnails for the document list and search hits (Viewer/Thumbnail.razor). They run through
// the same shader and the same WebGL context as the viewer (ADR-0011): the first page is
// analysed at 144 dpi and the display pass draws it small. Each costs that analysis, so a
// thumbnail is rendered only once it becomes visible, one at a time, and kept in memory.
// Nothing filtered is ever stored on the server. A thumbnail drawn while the WebGL context was
// lost is black: it is drawn again once the context is back, never cached or shown.
// The cache key contains the URL: a file that changes must get another URL (the page editor
// appends the version of the document), or its old thumbnails would be shown.
import { analyzePage } from './analysis.js';
import { MODE_INTELLIGENT, MODE_ORIGINAL } from './defaults.js';
import { FallbackRenderer } from './fallback.js';
import { errorName, openDocument } from './pdf-document.js';
import { onRendererChange, sharedRenderer } from './shared-renderer.js';

// Limited by memory, not by count: a thumbnail is width x height x 4 bytes of pixels.
const MAX_CACHED_BYTES = 64 * 1024 * 1024;
// A context lost again on every try is broken; give up instead of looping.
const MAX_ATTEMPTS = 3;
const cache = new Map();
let cachedBytes = 0;
let queue = Promise.resolve();
// Thumbnails waiting or running; the document stays open only while there are some.
let waiting = 0;
// The document the queue draws from: the page editor draws every page of one PDF in a row.
let open;
let fallback;

onRendererChange((event) => {
  if (event === 'lost') {
    cache.clear();
    cachedBytes = 0;
  }
});

function remember(key, image) {
  cache.set(key, image);
  cachedBytes += image.data.byteLength;
  // The oldest go first; the newest stays even when it alone is larger than the limit.
  for (const [oldest, old] of cache) {
    if (cachedBytes <= MAX_CACHED_BYTES || cache.size === 1) break;
    cache.delete(oldest);
    cachedBytes -= old.data.byteLength;
  }
}

function renderer() {
  const shared = sharedRenderer();
  return shared.renderer ?? (fallback ??= new FallbackRenderer(shared.reason));
}

// The renderer to draw with; while the shared context is lost, waits until it is back or given up.
function usableRenderer() {
  const current = renderer();
  if (!current.lost) return Promise.resolve(current);
  return new Promise((resolve) => {
    const stop = onRendererChange((event) => {
      if (event === 'lost') return;
      stop();
      resolve(renderer());
    });
  });
}

async function closeDocument() {
  const old = open;
  open = undefined;
  if (old) await old.loading.destroy().catch(() => {});
}

async function documentFor(url) {
  if (open?.url !== url) {
    await closeDocument();
    open = { url, loading: openDocument(url) };
  }
  try {
    return await open.loading.promise;
  } catch (error) {
    // A failed load is not kept: the next thumbnail of the same URL tries again.
    await closeDocument();
    throw error;
  }
}

async function draw(target, url, width, mode, pageNumber) {
  const pdf = await documentFor(url);
  const page = await pdf.getPage(pageNumber);
  try {
    const analysis = await analyzePage(page, target, true);
    try {
      return target.thumbnail(analysis, width, mode);
    } finally {
      target.release(analysis);
    }
  } finally {
    // The document stays open for the next page; the decoded data of this page does not.
    page.cleanup();
  }
}

async function render(url, width, mode, pageNumber) {
  const key = `${mode}|${width}|${pageNumber}|${url}`;
  if (cache.has(key)) return cache.get(key);
  for (let attempt = 1; ; attempt++) {
    const target = await usableRenderer();
    const generation = target.generation;
    const image = await draw(target, url, width, mode, pageNumber);
    // Lost during the draw (even if already restored): the pixels are black.
    if (!target.lost && target.generation === generation) {
      remember(key, image);
      return image;
    }
    if (attempt === MAX_ATTEMPTS) {
      const error = new Error('WebGL context lost');
      error.name = 'ContextLost';
      throw error;
    }
  }
}

// Draws page pageNumber (default: the first) of url into canvas once box is (nearly) visible.
// The handle's result() resolves to 'ok', to null when disposed first, or to the name of the error.
export function observeThumbnail(box, canvas, url, cssWidth, originalColors, pageNumber = 1) {
  let disposed = false;
  let settle;
  const result = new Promise((resolve) => { settle = resolve; });
  const observer = new IntersectionObserver((entries) => {
    if (!entries.some((entry) => entry.isIntersecting)) return;
    observer.disconnect();
    const width = Math.max(1, Math.round(cssWidth * (window.devicePixelRatio || 1)));
    const mode = originalColors ? MODE_ORIGINAL : MODE_INTELLIGENT;
    waiting++;
    queue = queue
      .then(() => (disposed ? null : render(url, width, mode, pageNumber)))
      .then((image) => {
        if (!image || disposed) {
          settle(null);
          return;
        }
        canvas.width = image.width;
        canvas.height = image.height;
        canvas.getContext('2d').putImageData(image, 0, 0);
        settle('ok');
      }, (error) => settle(errorName(error)))
      .finally(() => (--waiting === 0 ? closeDocument() : undefined));
  }, { rootMargin: '200px' });
  observer.observe(box);
  return {
    result: () => result,
    dispose: () => {
      disposed = true;
      observer.disconnect();
      settle(null);
    },
  };
}
