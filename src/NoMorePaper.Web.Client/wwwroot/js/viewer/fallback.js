// Fallback without WebGL (ADR-0011, docs/kb/darstellung.md "Rückfall ohne WebGL"): the tile is
// drawn with a 2D context and a CSS filter inverts it while turning the hue back. No photo
// detection, no paper white point. Same interface as ShaderRenderer, so the page view does not
// care which one it gets. Each viewer gets its own canvas (2D contexts are not scarce).

export const FALLBACK_FILTER = 'invert(1) hue-rotate(180deg)';

const filterFor = (mode) => (mode === 0 ? 'none' : FALLBACK_FILTER);

export class FallbackRenderer {
  constructor(reason) {
    this.kind = 'fallback';
    this.reason = reason;
    this.canvas = document.createElement('canvas');
    this.ctx = this.canvas.getContext('2d');
    this.tile = null;
  }

  // Needs no analysis for the viewer; a thumbnail keeps the page render as its source.
  get needsAnalysis() { return false; }

  analyzePage(pageCanvas, _paper, _imageCoordinates, keepPage) {
    return { page: keepPage ? pageCanvas : null, pageSize: [pageCanvas.width, pageCanvas.height] };
  }

  release(analysis) {
    if (analysis) analysis.page = null;
  }

  resize(width, height) {
    if (this.canvas.width !== width || this.canvas.height !== height) {
      this.canvas.width = width;
      this.canvas.height = height;
    }
  }

  uploadTile(canvas) { this.tile = canvas; }

  draw(part, _analysis, mode) {
    this.canvas.style.filter = filterFor(mode);
    // White under the filter shows as black, like the page background of the shader path.
    this.ctx.fillStyle = mode === 0 ? '#000' : '#fff';
    this.ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
    if (!part || !this.tile) return;
    const { rect, source } = part;
    const { width, height } = this.tile;
    // source is y-up like the shader's texture coordinates.
    this.ctx.drawImage(this.tile,
      source[0] * width, (1 - source[1] - source[3]) * height, source[2] * width, source[3] * height,
      rect.x, rect.y, rect.width, rect.height);
  }

  thumbnail(analysis, width, mode) {
    const height = Math.round(width * analysis.pageSize[1] / analysis.pageSize[0]);
    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d');
    ctx.filter = filterFor(mode);
    ctx.drawImage(analysis.page, 0, 0, width, height);
    return ctx.getImageData(0, 0, width, height);
  }
}
