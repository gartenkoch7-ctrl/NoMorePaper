// Thumbnails for the document list and search hits (Viewer/Thumbnail.razor). They run through
// the same shader and the same WebGL context as the viewer (ADR-0011): the first page is
// analysed at 144 dpi and the display pass draws it small. Each costs that analysis, so a
// thumbnail is rendered only once it becomes visible, one at a time, and kept in memory.
// Nothing filtered is ever stored on the server.
import { analyzePage } from './analysis.js';
import { MODE_INTELLIGENT, MODE_ORIGINAL } from './defaults.js';
import { FallbackRenderer } from './fallback.js';
import { errorName, openDocument } from './pdf-document.js';
import { sharedRenderer } from './shader-renderer.js';

const MAX_CACHED = 200;
const cache = new Map();
let queue = Promise.resolve();
let fallback;

function renderer() {
  const shared = sharedRenderer();
  return shared.renderer ?? (fallback ??= new FallbackRenderer(shared.reason));
}

async function render(url, width, mode) {
  const key = `${mode}|${width}|${url}`;
  if (cache.has(key)) return cache.get(key);
  const target = renderer();
  const loading = openDocument(url);
  try {
    const pdf = await loading.promise;
    const page = await pdf.getPage(1);
    const analysis = await analyzePage(page, target, true);
    try {
      const image = target.thumbnail(analysis, width, mode);
      if (cache.size >= MAX_CACHED) cache.delete(cache.keys().next().value);
      cache.set(key, image);
      return image;
    } finally {
      target.release(analysis);
    }
  } finally {
    await loading.destroy();
  }
}

// Draws the first page of url into canvas once box is (nearly) visible. The handle's result()
// resolves to 'ok', to null when disposed first, or to the name of the error.
export function observeThumbnail(box, canvas, url, cssWidth, originalColors) {
  let disposed = false;
  let settle;
  const result = new Promise((resolve) => { settle = resolve; });
  const observer = new IntersectionObserver((entries) => {
    if (!entries.some((entry) => entry.isIntersecting)) return;
    observer.disconnect();
    const width = Math.max(1, Math.round(cssWidth * (window.devicePixelRatio || 1)));
    const mode = originalColors ? MODE_ORIGINAL : MODE_INTELLIGENT;
    queue = queue
      .then(() => (disposed ? null : render(url, width, mode)))
      .then((image) => {
        if (!image || disposed) {
          settle(null);
          return;
        }
        canvas.width = image.width;
        canvas.height = image.height;
        canvas.getContext('2d').putImageData(image, 0, 0);
        settle('ok');
      }, (error) => settle(errorName(error)));
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
