// Crop and straighten one page in "Seiten bearbeiten" (Editing/CropInterop.cs, ADR-0025). The page is drawn
// from the PDF with PDF.js at 200 dpi in its original colours, the sheet is looked for with the camera's edge
// detection (camera/edges.js) and the chosen frame is straightened with the camera's warp (camera/capture.js)
// into a JPEG that replaces the page. Nothing leaves the browser except that JPEG, sent with "Speichern".
//
// The drawn page is held only between detect() and apply() or release(); OpenCV's Mats are freed inside
// findQuad and warp. Corner handles (.pe-crop-handle inside .pe-crop-page) are dragged with the mouse here;
// the arrow keys move them in Blazor, this module only keeps those keys from scrolling the page.
import { JPEG_QUALITY, MAX_SIDE, toJfif, warp } from '../camera/capture.js';
import { findQuad, loadOpenCv } from '../camera/edges.js';
import { openDocument } from '../viewer/pdf-document.js';

/** Pixels per inch of the drawn page: enough for OCR, as the plan says. */
const DPI = 200;
/** The sheet is looked for in a copy this large: fast, and the outline needs no more. */
const ANALYSIS_SIDE = 1000;
/** An outline smaller than this share of the page is a box or a table on the sheet, not the sheet. */
const MIN_SHEET_AREA = 0.25;
/** An outline this close to all four page corners is the page itself: nothing to cut off. */
const FULL_PAGE_MARGIN = 0.01;
const FULL = [0, 0, 1, 0, 1, 1, 0, 1];

let held = null;
let dotnet = null;

/**
 * Draws page `number` of the PDF at `url` and looks for the sheet. Never throws:
 * { found, corners: [x, y] × 4 as fractions clockwise from the top left (the whole page when nothing was
 * found), widthPt, heightPt, elapsedMs } or { error: 'opencv' | 'load', elapsedMs }.
 */
export async function detect(url, number, target) {
  const started = performance.now();
  const elapsed = () => Math.round(performance.now() - started);
  release();
  dotnet = target;
  let cv;
  try {
    cv = await loadOpenCv();
  } catch {
    return { error: 'opencv', elapsedMs: elapsed() };
  }
  let drawn;
  try {
    drawn = await drawPage(url, number);
  } catch {
    return { error: 'load', elapsedMs: elapsed() };
  }
  held = drawn.canvas;
  const quad = sheetOf(cv, held);
  const corners = quad ? quad.flatMap((point) => [point.x, point.y]) : FULL;
  return { found: !!quad, corners, widthPt: drawn.widthPt, heightPt: drawn.heightPt, elapsedMs: elapsed() };
}

/** Straightens the frame (8 fractions as detect() reports them) of the drawn page; the JPEG bytes. */
export async function apply(corners) {
  const page = held;
  if (!page) {
    throw new Error('no-page');
  }
  held = null;
  dotnet = null; // the frame is closed: no handle is dragged any more
  let out;
  try {
    const cv = await loadOpenCv();
    out = warp(cv, page, [0, 1, 2, 3].map((i) => ({ x: corners[2 * i], y: corners[2 * i + 1] })));
  } finally {
    page.width = 0; // frees the drawn page at once instead of at the next garbage collection
  }
  try {
    const blob = await toJfif(await toJpeg(out));
    return new Uint8Array(await blob.arrayBuffer());
  } finally {
    out.width = 0;
  }
}

/** Forgets the drawn page and stops reporting dragged corners. */
export function release() {
  if (held) {
    held.width = 0;
    held = null;
  }
  dotnet = null;
}

async function drawPage(url, number) {
  const loading = openDocument(url);
  try {
    const pdf = await loading.promise;
    const page = await pdf.getPage(number);
    try {
      const size = page.getViewport({ scale: 1 });
      // A photo packed into a PDF can have thousands of points per side; more than the JPEG keeps is not drawn.
      const scale = Math.min(DPI / 72, MAX_SIDE / Math.max(size.width, size.height, 1));
      const viewport = page.getViewport({ scale });
      const canvas = document.createElement('canvas');
      canvas.width = Math.max(1, Math.round(viewport.width));
      canvas.height = Math.max(1, Math.round(viewport.height));
      await page.render({ canvas, viewport }).promise;
      return { canvas, widthPt: size.width, heightPt: size.height };
    } finally {
      page.cleanup();
    }
  } finally {
    await loading.destroy().catch(() => {});
  }
}

function sheetOf(cv, page) {
  const factor = Math.min(1, ANALYSIS_SIDE / Math.max(page.width, page.height));
  const small = document.createElement('canvas');
  small.width = Math.max(1, Math.round(page.width * factor));
  small.height = Math.max(1, Math.round(page.height * factor));
  try {
    small.getContext('2d').drawImage(page, 0, 0, small.width, small.height);
    const quad = findQuad(cv, small);
    return quad && area(quad) >= MIN_SHEET_AREA && !isWholePage(quad) ? quad : null;
  } finally {
    small.width = 0;
  }
}

/** Shoelace formula; the corners are fractions, so the result is the share of the page. */
function area(quad) {
  let sum = 0;
  for (let i = 0; i < 4; i++) {
    const a = quad[i];
    const b = quad[(i + 1) % 4];
    sum += a.x * b.y - b.x * a.y;
  }
  return Math.abs(sum) / 2;
}

function isWholePage(quad) {
  return quad.every((point, i) => Math.abs(point.x - FULL[2 * i]) <= FULL_PAGE_MARGIN
    && Math.abs(point.y - FULL[2 * i + 1]) <= FULL_PAGE_MARGIN);
}

function toJpeg(canvas) {
  return new Promise((resolve, reject) => {
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('jpeg'))), 'image/jpeg', JPEG_QUALITY);
  });
}

// Arrow keys on a handle move the corner; without this they would also scroll the page.
document.addEventListener('keydown', (event) => {
  if (event.key.startsWith('Arrow') && event.target instanceof Element && event.target.closest('.pe-crop-handle')) {
    event.preventDefault();
  }
});

// Dragging a handle: its position on the page as seen (the box is turned with the page, so its bounding box is
// what is seen), reported at most once per frame and once more when the mouse is let go.
document.addEventListener('pointerdown', (event) => {
  const handle = event.target instanceof Element ? event.target.closest('.pe-crop-handle') : null;
  const box = handle?.closest('.pe-crop-page');
  if (!handle || !box || !dotnet || event.button !== 0) {
    return;
  }
  event.preventDefault();
  handle.setPointerCapture(event.pointerId);
  const corner = Number(handle.dataset.corner);
  let frame = 0;
  let last = null;
  const report = (done) => {
    frame = 0;
    if (last && dotnet) {
      dotnet.invokeMethodAsync('OnDrag', corner, last.x, last.y, done).catch(() => {});
    }
  };
  const move = (e) => {
    const rect = box.getBoundingClientRect();
    last = { x: (e.clientX - rect.left) / rect.width, y: (e.clientY - rect.top) / rect.height };
    frame ||= requestAnimationFrame(() => report(false));
  };
  const end = () => {
    cancelAnimationFrame(frame);
    handle.removeEventListener('pointermove', move);
    handle.removeEventListener('pointerup', end);
    handle.removeEventListener('pointercancel', end);
    report(true);
  };
  handle.addEventListener('pointermove', move);
  handle.addEventListener('pointerup', end);
  handle.addEventListener('pointercancel', end);
});
