// Takes the page: the full video frame, straightened to the sheet's corners (perspective warp), as
// JPEG. Only the cropped page is kept, never the whole photo (M4 decision).

/** The longer side of a page image; more only costs memory and upload time (≈ 300 dpi on A4 at 3000 px). */
export const MAX_SIDE = 3000;
export const JPEG_QUALITY = 0.85;

/**
 * @param cv the ready OpenCV.js object.
 * @param video the playing <video>.
 * @param quad the sheet's corners normalized 0–1 (top-left, top-right, bottom-right, bottom-left), or
 *        null for the whole frame (manual picture without a sheet found).
 * @returns {Promise<Blob>} the page as baseline JPEG with a JFIF header.
 */
export async function capturePage(cv, video, quad) {
  const frame = document.createElement('canvas');
  frame.width = video.videoWidth;
  frame.height = video.videoHeight;
  let page;
  try {
    frame.getContext('2d').drawImage(video, 0, 0, frame.width, frame.height);
    page = quad ? warp(cv, frame, quad) : scaled(frame);
  } finally {
    frame.width = 0; // frees the frame's pixels at once instead of at the next garbage collection
  }
  try {
    return await toJfif(await toJpeg(page));
  } finally {
    page.width = 0;
  }
}

function warp(cv, frame, quad) {
  const corners = quad.map((p) => ({ x: p.x * frame.width, y: p.y * frame.height }));
  const [tl, tr, br, bl] = corners;
  let width = Math.max(distance(tl, tr), distance(bl, br));
  let height = Math.max(distance(tl, bl), distance(tr, br));
  const factor = Math.min(1, MAX_SIDE / Math.max(width, height));
  width = Math.max(1, Math.round(width * factor));
  height = Math.max(1, Math.round(height * factor));

  const mats = [];
  const track = (mat) => {
    mats.push(mat);
    return mat;
  };
  try {
    const source = track(cv.imread(frame));
    const from = track(cv.matFromArray(4, 1, cv.CV_32FC2, corners.flatMap((p) => [p.x, p.y])));
    const to = track(cv.matFromArray(4, 1, cv.CV_32FC2, [0, 0, width, 0, width, height, 0, height]));
    const transform = track(cv.getPerspectiveTransform(from, to));
    const target = track(new cv.Mat());
    cv.warpPerspective(source, target, transform, new cv.Size(width, height), cv.INTER_LINEAR, cv.BORDER_REPLICATE);
    const out = document.createElement('canvas');
    cv.imshow(out, target);
    return out;
  } finally {
    for (const mat of mats) {
      mat.delete();
    }
  }
}

function scaled(frame) {
  const factor = Math.min(1, MAX_SIDE / Math.max(frame.width, frame.height));
  const out = document.createElement('canvas');
  out.width = Math.round(frame.width * factor);
  out.height = Math.round(frame.height * factor);
  out.getContext('2d').drawImage(frame, 0, 0, out.width, out.height);
  return out;
}

function toJpeg(canvas) {
  return new Promise((resolve, reject) => {
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('jpeg'))), 'image/jpeg', JPEG_QUALITY);
  });
}

const JFIF_APP0 = [0xff, 0xe0, 0x00, 0x10, 0x4a, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00];

/**
 * The server's PDF writer (PDFsharp) accepts a JPEG only when the JFIF header follows the start marker
 * directly; other segments there (Exif, comments) make it refuse the image. Browsers differ in what
 * they write, so the header segments before the image data are rebuilt: start marker, JFIF, then only
 * the segments the decoder needs (tables, frame, scan). Everything from the scan on stays as it is.
 */
export async function toJfif(blob) {
  const bytes = new Uint8Array(await blob.arrayBuffer());
  return new Blob([rebuildHeader(bytes)], { type: 'image/jpeg' });
}

/** Pure part of toJfif, exported for the tests. Returns the input unchanged if it is not a JPEG it understands. */
export function rebuildHeader(bytes) {
  if (bytes.length < 4 || bytes[0] !== 0xff || bytes[1] !== 0xd8) {
    return bytes;
  }
  const kept = [];
  let offset = 2;
  while (offset + 4 <= bytes.length && bytes[offset] === 0xff) {
    const marker = bytes[offset + 1];
    if (marker === 0xda) {
      // Start of scan: the compressed data follows; the rest is copied as it is.
      const head = new Uint8Array([0xff, 0xd8, ...JFIF_APP0]);
      return concat([head, ...kept, bytes.subarray(offset)]);
    }
    const length = (bytes[offset + 2] << 8) | bytes[offset + 3];
    const end = offset + 2 + length;
    if (length < 2 || end > bytes.length) {
      return bytes;
    }
    // APP0–APP15 (0xE0–0xEF) and comments (0xFE) carry nothing the decoder needs.
    const metadata = (marker >= 0xe0 && marker <= 0xef) || marker === 0xfe;
    if (!metadata) {
      kept.push(bytes.subarray(offset, end));
    }
    offset = end;
  }
  return bytes;
}

function concat(parts) {
  const out = new Uint8Array(parts.reduce((sum, part) => sum + part.length, 0));
  let at = 0;
  for (const part of parts) {
    out.set(part, at);
    at += part.length;
  }
  return out;
}

function distance(a, b) {
  return Math.hypot(a.x - b.x, a.y - b.y);
}
