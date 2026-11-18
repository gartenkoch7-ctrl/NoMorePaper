// Fallback without WebGL (ADR-0011): the tile is drawn with a 2D context and a CSS filter
// inverts it while turning the hue back. No photo detection, no paper white point.
// Same interface as ShaderRenderer so PageView does not care which one it gets.

const FILTER = 'invert(1) hue-rotate(180deg)';

export class FallbackRenderer {
  constructor(canvas) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.rendererName = 'CSS filter fallback (no WebGL)';
    this.tile = null;
    this.page = null;
  }

  setPage(canvas) { this.page = canvas; }

  analyzePage() { return null; }

  buildMask() {}

  uploadTile(canvas) { this.tile = canvas; }

  draw(rect, _uv, _pagePixels, mode) {
    // "Classic invert" has no hue rotation; original has no filter at all.
    this.canvas.style.filter = mode === 0 ? 'none' : mode === 1 ? 'invert(1)' : FILTER;
    this.ctx.fillStyle = mode === 0 ? '#000' : '#fff';
    this.ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
    if (this.tile && rect.width >= 1 && rect.height >= 1) this.ctx.drawImage(this.tile, rect.x, rect.y);
  }

  thumbnail(width, mode) {
    const height = Math.round(width * this.page.height / this.page.width);
    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d');
    ctx.filter = mode === 0 ? 'none' : mode === 1 ? 'invert(1)' : FILTER;
    ctx.drawImage(this.page, 0, 0, width, height);
    return ctx.getImageData(0, 0, width, height);
  }

  sync() {}

  async gpuTime(work) { work(); return null; }
}
