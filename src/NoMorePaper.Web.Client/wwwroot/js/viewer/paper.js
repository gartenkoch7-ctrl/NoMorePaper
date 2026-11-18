// Estimates paper white and ink black of a page from the analysis render (spike: paper.js).
// Digital PDFs give pure white and pure black; scans give grey or tinted paper and grey ink
// that must still end up as black background and white text.

const toLinear = (v) => (v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4);
// OKLab L is steep near black: #111 has L = 0.18 and would only turn light grey. Everything
// up to MIN_BLACK therefore counts as ink and becomes pure white.
const MIN_BLACK = 0.15;
const NEUTRAL = Object.freeze({ linear: [1, 1, 1], lightness: 1, white: 1, black: MIN_BLACK });
// Highest ink level accepted; a page without dark ink must not be stretched into noise.
const MAX_BLACK = 0.4;
const INK_SHARE = 0.015;

function oklabLightness(r, g, b) {
  const l = Math.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
  const m = Math.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
  const s = Math.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
  return 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s;
}

function lightnessOf(data, i) {
  return oklabLightness(toLinear(data[i] / 255), toLinear(data[i + 1] / 255), toLinear(data[i + 2] / 255));
}

// Paper: the most common bright tone of a strongly downscaled copy (averages grain away).
function findPaper(pageCanvas) {
  const width = 96;
  const height = Math.max(1, Math.round(width * pageCanvas.height / pageCanvas.width));
  const small = document.createElement('canvas');
  small.width = width;
  small.height = height;
  const ctx = small.getContext('2d', { willReadFrequently: true });
  ctx.drawImage(pageCanvas, 0, 0, width, height);
  const data = ctx.getImageData(0, 0, width, height).data;

  const bins = 50;
  const histogram = new Uint32Array(bins);
  const binOf = new Uint8Array(data.length / 4);
  for (let i = 0; i < data.length; i += 4) {
    const bin = Math.min(bins - 1, Math.floor(lightnessOf(data, i) * bins));
    histogram[bin]++;
    binOf[i / 4] = bin;
  }
  // Only tones in the bright half can be paper; a dark page keeps the neutral default.
  let peak = bins - 1;
  for (let bin = bins / 2; bin < bins; bin++) {
    if (histogram[bin] > histogram[peak]) peak = bin;
  }
  if (histogram[peak] < binOf.length * 0.15) return null;

  const sum = [0, 0, 0];
  let count = 0;
  for (let p = 0; p < binOf.length; p++) {
    if (Math.abs(binOf[p] - peak) > 1) continue;
    for (let k = 0; k < 3; k++) sum[k] += toLinear(data[p * 4 + k] / 255);
    count++;
  }
  return sum.map((v) => Math.max(0.05, v / count));
}

// Ink: the tone below which the darkest INK_SHARE of the page lies, at full analysis
// resolution (thin strokes disappear in a downscaled copy).
function findInk(pageCanvas) {
  const { width, height } = pageCanvas;
  const data = pageCanvas.getContext('2d').getImageData(0, 0, width, height).data;
  const bins = 100;
  const histogram = new Uint32Array(bins);
  let total = 0;
  for (let y = 0; y < height; y += 3) {
    for (let x = 0; x < width; x += 3) {
      histogram[Math.min(bins - 1, Math.floor(lightnessOf(data, (y * width + x) * 4) * bins))]++;
      total++;
    }
  }
  let seen = 0;
  for (let bin = 0; bin < bins; bin++) {
    seen += histogram[bin];
    if (seen >= total * INK_SHARE) return bin / bins;
  }
  return 0;
}

// Returns { linear: paper colour, lightness: paper L, white, black } with the white and black
// point as OKLab L.
export function estimatePaper(pageCanvas) {
  const linear = findPaper(pageCanvas);
  if (!linear) return NEUTRAL;
  const lightness = oklabLightness(linear[0], linear[1], linear[2]);
  // Pure white stays exact so pastel table rows keep their tint.
  if (lightness > 0.985) return NEUTRAL;
  // Real paper gets a margin below its tone so shading and grain disappear into black.
  return {
    linear,
    lightness,
    white: Math.max(0.5, lightness - 0.05),
    black: Math.min(MAX_BLACK, Math.max(MIN_BLACK, findInk(pageCanvas))),
  };
}
