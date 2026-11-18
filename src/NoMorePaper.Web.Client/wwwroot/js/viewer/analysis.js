// Analysis once per page, independent of the zoom (docs/kb/darstellung.md "Ablauf je Seite"):
// PDF.js renders the whole page at 144 dpi and records where it draws images. From that render
// come paper white and ink black, the photo mask and, if asked for, the thumbnail.
import { ANALYSIS_MAX_SIDE, ANALYSIS_SCALE } from './defaults.js';
import { estimatePaper } from './paper.js';

// Analysis pixels per PDF point for a page of points[0] x points[1]. 144 dpi, unless the longer
// side would exceed ANALYSIS_MAX_SIDE or the GPU's texture limit: canvas and texture would then
// fail (a phone photo packed by img2pdf without a resolution has 3000 points and more per side).
// The photo thresholds are calibrated at 144 dpi; on such a page the photo detection runs on the
// reduced image, so one mask cell covers more paper and photo edges are coarser. The display is
// not affected: it renders its own tiles at the zoom.
export function analysisScale(points, maxTextureSize = Infinity) {
  const limit = Math.min(ANALYSIS_MAX_SIDE, maxTextureSize || Infinity);
  return Math.min(ANALYSIS_SCALE, limit / Math.max(points[0], points[1], 1));
}

export async function analyzePage(page, renderer, keepPage = false) {
  if (!renderer.needsAnalysis && !keepPage) return null;
  const size = page.getViewport({ scale: 1 });
  const viewport = page.getViewport({ scale: analysisScale([size.width, size.height], renderer.maxTextureSize) });
  const canvas = document.createElement('canvas');
  canvas.width = Math.round(viewport.width);
  canvas.height = Math.round(viewport.height);
  const task = page.render({ canvas, viewport, recordImages: renderer.needsAnalysis });
  await task.promise;
  const coordinates = Array.from(task.imageCoordinates ?? page.imageCoordinates ?? []);
  const paper = renderer.needsAnalysis ? estimatePaper(canvas) : null;
  const analysis = renderer.analyzePage(canvas, paper, coordinates, keepPage);
  // The canvas is uploaded as a texture now (or kept by the fallback for its thumbnail).
  if (renderer.needsAnalysis) {
    canvas.width = 0;
    canvas.height = 0;
  }
  return analysis;
}
