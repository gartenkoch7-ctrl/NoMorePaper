// Analysis once per page, independent of the zoom (docs/kb/darstellung.md "Ablauf je Seite"):
// PDF.js renders the whole page at 144 dpi and records where it draws images. From that render
// come paper white and ink black, the photo mask and, if asked for, the thumbnail.
import { ANALYSIS_SCALE } from './defaults.js';
import { estimatePaper } from './paper.js';

export async function analyzePage(page, renderer, keepPage = false) {
  if (!renderer.needsAnalysis && !keepPage) return null;
  const viewport = page.getViewport({ scale: ANALYSIS_SCALE });
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
