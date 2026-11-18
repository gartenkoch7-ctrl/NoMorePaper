// Factory values of the viewer: the result of the shader spike, approved by the user on
// 2026-11-18 (docs/kb/darstellung.md, "Ab Werk"). The thresholds are bound to the analysis
// resolution and the cell size: do not change one without recalibrating the others.

export const ANALYSIS_SCALE = 2.0; // analysis pixels per PDF point (144 dpi)
// Longest side of the analysis render in pixels. Pages up to 2048 points (72 cm) keep 144 dpi;
// larger ones (photos packed into a PDF without a resolution, plans) are analysed smaller
// (analysis.js, analysisScale).
export const ANALYSIS_MAX_SIDE = 4096;
export const CELL = 12;            // analysis pixels per mask cell (about 2.1 mm on paper)
export const SPACING = 4.5;        // 8 samples x 4.5 px: each cell looks at 36 px (6.4 mm)
export const GROW = [0.15, 0.45];  // mean range of the grown photo mask
export const CORE = [0.55, 0.8];   // mean range of the photo core

export const PARAMS = Object.freeze({
  gamma: 0.65,
  lift: 0.75,
  chroma: 1,
  chromaLo: 0.025,
  chromaHi: 0.06,
  shareLo: 0.35,
  shareHi: 0.6,
  detailLo: 0.35,
  detailHi: 0.6,
  blendLo: 0.25,
  blendHi: 0.75,
  neutralLo: 0.02,
  neutralHi: 0.05,
});

// Shader modes: 0 = original colours (the per-device switch), 2 = intelligent invert.
export const MODE_ORIGINAL = 0;
export const MODE_INTELLIGENT = 2;

export const ZOOM_MIN = 0.25;
export const ZOOM_MAX = 16;
export const ZOOM_STEPS = [0.25, 0.5, 0.75, 1, 1.5, 2, 3, 4, 6, 8, 12, 16];

// CSS pixels per PDF point at 100 %.
export const CSS_PER_POINT = 96 / 72;
// Black border around the page in CSS pixels.
export const PAGE_MARGIN = 16;
