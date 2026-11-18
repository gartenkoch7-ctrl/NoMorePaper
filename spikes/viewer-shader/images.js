// Sorts the embedded images of a page into photos and everything else (scans, graphics),
// using the image positions PDF.js records while rendering (recordImages) and the raw photo
// score of the analysis pass. A photo frame is exact at any zoom and has no holes, which the
// pixel mask cannot offer; the pixel mask is still needed for photos inside a scanned page.

export const MAX_RECTS = 8;
// An image that covers most of the page is a scan: the page itself, not a picture on it.
const SCAN_AREA = 0.6;
// Share of an image's cells that must look like a photo.
const PHOTO_SCORE = 0.25;
const MIN_SIDE = 0.02;

// coordinates: 6 numbers per image (three corners, 0..1, y down) from RenderTask.imageCoordinates.
// raw: RGBA bytes of the analysis target (bottom row first), r = photo score per cell.
// Returns rectangles [x, y, width, height] in page UV with y pointing up.
export function classifyImages(coordinates, raw, cells, maskScale) {
  const photos = [];
  const scans = [];
  for (let i = 0; i + 5 < coordinates.length; i += 6) {
    const xs = [coordinates[i], coordinates[i + 2], coordinates[i + 4]];
    const ys = [coordinates[i + 1], coordinates[i + 3], coordinates[i + 5]];
    // Fourth corner of the parallelogram; a rotated image falls back to its bounding box.
    xs.push(xs[1] + xs[2] - xs[0]);
    ys.push(ys[1] + ys[2] - ys[0]);
    const x0 = Math.max(0, Math.min(...xs)), x1 = Math.min(1, Math.max(...xs));
    const y0 = Math.max(0, Math.min(...ys)), y1 = Math.min(1, Math.max(...ys));
    const width = x1 - x0, height = y1 - y0;
    if (width < MIN_SIDE || height < MIN_SIDE) continue;
    const rect = [x0, 1 - y1, width, height];
    const isPhoto = width * height <= SCAN_AREA
      && meanScore(raw, cells, maskScale, x0, 1 - y1, x1, 1 - y0) >= PHOTO_SCORE;
    const list = isPhoto ? photos : scans;
    if (list.length < MAX_RECTS) list.push(rect);
  }
  return { photos, scans };
}

function meanScore(raw, cells, maskScale, x0, y0, x1, y1) {
  const cx0 = Math.floor(x0 * maskScale[0] * cells[0]), cx1 = Math.ceil(x1 * maskScale[0] * cells[0]);
  const cy0 = Math.floor(y0 * maskScale[1] * cells[1]), cy1 = Math.ceil(y1 * maskScale[1] * cells[1]);
  let sum = 0, count = 0;
  for (let y = Math.max(0, cy0); y < Math.min(cells[1], cy1); y++) {
    for (let x = Math.max(0, cx0); x < Math.min(cells[0], cx1); x++) {
      sum += raw[(y * cells[0] + x) * 4] / 255;
      count++;
    }
  }
  return count ? sum / count : 0;
}

const PHOTO_CELL = 128;   // raw score (0..255) from which a cell counts as photo
const MIN_CELLS = 24;     // smaller groups are icons or stamps, not photos (about 1 cm²)
const BOX_DENSITY = 0.55; // share of a group's bounding box that must be photo cells

// Photos are rectangles, but large neutral parts (clouds, a white wall) score as "no photo"
// and would be inverted. Each connected group of photo cells that fills most of its bounding
// box therefore claims the whole box. Edits raw in place.
export function fillPhotoBoxes(raw, cells) {
  const [width, height] = cells;
  const seen = new Uint8Array(width * height);
  const stack = [];
  for (let start = 0; start < width * height; start++) {
    if (seen[start] || raw[start * 4] < PHOTO_CELL) continue;
    let count = 0, x0 = width, x1 = 0, y0 = height, y1 = 0;
    seen[start] = 1;
    stack.push(start);
    while (stack.length) {
      const cell = stack.pop();
      const x = cell % width, y = (cell - x) / width;
      count++;
      x0 = Math.min(x0, x); x1 = Math.max(x1, x);
      y0 = Math.min(y0, y); y1 = Math.max(y1, y);
      for (let dy = -1; dy <= 1; dy++) {
        for (let dx = -1; dx <= 1; dx++) {
          const nx = x + dx, ny = y + dy;
          if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
          const next = ny * width + nx;
          if (seen[next] || raw[next * 4] < PHOTO_CELL) continue;
          seen[next] = 1;
          stack.push(next);
        }
      }
    }
    const box = (x1 - x0 + 1) * (y1 - y0 + 1);
    if (count < MIN_CELLS || count < box * BOX_DENSITY) continue;
    for (let y = y0; y <= y1; y++) {
      for (let x = x0; x <= x1; x++) raw[(y * width + x) * 4] = 255;
    }
  }
}
