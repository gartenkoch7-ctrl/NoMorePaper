// One PDF page in a scrollable view. Only the visible part of the page ("viewport tile") is
// rendered by PDF.js at the current zoom, so 1600 % costs the same pixels as 100 %.
import * as pdfjs from './lib/pdfjs/pdf.min.mjs';
import { ANALYSIS_SCALE } from './gl.js';
import { estimatePaper } from './paper.js';
import { classifyImages, fillPhotoBoxes } from './images.js';

pdfjs.GlobalWorkerOptions.workerSrc = './lib/pdfjs/pdf.worker.min.mjs';

const CSS_PER_POINT = 96 / 72;
const MARGIN = 16;

export const pdfjsVersion = pdfjs.version;

export class PageView {
  constructor(renderer, glCanvas, scroller, spacer) {
    this.renderer = renderer;
    this.glCanvas = glCanvas;
    this.scroller = scroller;
    this.spacer = spacer;
    this.tileCanvas = document.createElement('canvas');
    this.zoom = 1;
    this.mode = 2;
    this.params = null;
    this.page = null;
    this.paper = { linear: [1, 1, 1], lightness: 1, white: 1, black: 0.15 };
    this.imageCoordinates = [];
    this.task = null;
    this.busy = false;
    this.again = false;
    this.last = null;
    this.analysisMs = { render: 0, mask: 0 };
  }

  // White and black point of the page: estimated per page or set by the sliders.
  get tone() {
    return this.params.autoTone
      ? { white: this.paper.white, black: this.paper.black }
      : { white: this.params.white, black: this.params.black };
  }

  async open(url, pageNumber = 1) {
    // Frees the worker memory of the previous document. No page while loading, so scroll
    // and resize events cannot render from a destroyed document.
    this.page = null;
    this.tileInfo = null;
    this.task?.cancel();
    while (this.busy) await new Promise((resolve) => setTimeout(resolve, 5));
    await this.loading?.destroy();
    this.loading = pdfjs.getDocument({ url });
    this.document = await this.loading.promise;
    const page = await this.document.getPage(pageNumber);
    const size = page.getViewport({ scale: 1 });
    this.points = [size.width, size.height];
    await this.#analyze(page);
    this.page = page;
    this.layout();
  }

  // Low-resolution render of the whole page, once per page and independent of the zoom:
  // source for the paper estimate, the photo mask and thumbnails.
  async #analyze(page) {
    const started = performance.now();
    const viewport = page.getViewport({ scale: ANALYSIS_SCALE });
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(viewport.width);
    canvas.height = Math.round(viewport.height);
    const task = page.render({ canvas, viewport, recordImages: true });
    await task.promise;
    this.imageCoordinates = Array.from(task.imageCoordinates ?? []);
    const rendered = performance.now();
    this.paper = estimatePaper(canvas);
    this.renderer.setPage(canvas);
    this.rebuildMask();
    this.renderer.sync();
    this.analysisMs = { render: rendered - started, mask: performance.now() - rendered };
  }

  rebuildMask() {
    const raw = this.renderer.analyzePage(this.params, this.paper.linear);
    if (!raw) return;
    this.renderer.images = classifyImages(this.imageCoordinates, raw.pixels, raw.cells, raw.maskScale);
    fillPhotoBoxes(raw.pixels, raw.cells);
    this.renderer.buildMask(raw.pixels);
  }

  // Sizes the scroll area for the current zoom. anchor = page fraction to keep in the centre.
  layout(anchor) {
    const view = [this.scroller.clientWidth, this.scroller.clientHeight];
    const keep = anchor ?? this.#centerFraction(view);
    const page = this.points.map((p) => p * CSS_PER_POINT * this.zoom);
    const area = [Math.max(view[0], page[0] + 2 * MARGIN), Math.max(view[1], page[1] + 2 * MARGIN)];
    this.spacer.style.width = `${area[0]}px`;
    this.spacer.style.height = `${area[1]}px`;
    this.pageCss = page;
    this.offset = [(area[0] - page[0]) / 2, (area[1] - page[1]) / 2];
    this.scroller.scrollLeft = this.offset[0] + keep[0] * page[0] - view[0] / 2;
    this.scroller.scrollTop = this.offset[1] + keep[1] * page[1] - view[1] / 2;
  }

  #centerFraction(view) {
    if (!this.pageCss) return [0.5, 0];
    return [
      (this.scroller.scrollLeft + view[0] / 2 - this.offset[0]) / this.pageCss[0],
      (this.scroller.scrollTop + view[1] / 2 - this.offset[1]) / this.pageCss[1],
    ];
  }

  setZoom(zoom, anchor) {
    this.zoom = zoom;
    this.layout(anchor);
  }

  // Visible part of the page in device pixels: where it sits on the canvas and in the page.
  #visibleTile() {
    const ratio = window.devicePixelRatio || 1;
    const view = [Math.round(this.scroller.clientWidth * ratio), Math.round(this.scroller.clientHeight * ratio)];
    const page = this.pageCss.map((v) => Math.round(v * ratio));
    const left = Math.round((this.scroller.scrollLeft - this.offset[0]) * ratio);
    const top = Math.round((this.scroller.scrollTop - this.offset[1]) * ratio);
    const x0 = Math.min(page[0], Math.max(0, left));
    const y0 = Math.min(page[1], Math.max(0, top));
    const x1 = Math.min(page[0], Math.max(0, left + view[0]));
    const y1 = Math.min(page[1], Math.max(0, top + view[1]));
    return {
      view, page, ratio,
      origin: [x0, y0],
      size: [x1 - x0, y1 - y0],
      rect: { x: x0 - left, y: y0 - top, width: x1 - x0, height: y1 - y0 },
      uv: [x0 / page[0], 1 - y1 / page[1], (x1 - x0) / page[0], (y1 - y0) / page[1]],
    };
  }

  // Re-renders the visible tile with PDF.js and runs the shader. Calls while a render is
  // running are merged into one follow-up render.
  async render() {
    if (!this.page) return null;
    if (this.busy) {
      this.again = true;
      this.task?.cancel();
      return null;
    }
    this.busy = true;
    try {
      do {
        this.again = false;
        this.last = await this.#renderOnce();
      } while (this.again && this.page);
      return this.last;
    } finally {
      this.busy = false;
    }
  }

  async #renderOnce() {
    const tile = this.#visibleTile();
    if (this.glCanvas.width !== tile.view[0] || this.glCanvas.height !== tile.view[1]) {
      this.glCanvas.width = tile.view[0];
      this.glCanvas.height = tile.view[1];
    }
    const timings = { zoom: this.zoom, tile: tile.size, pdfMs: 0, shaderMs: 0 };
    if (tile.size[0] < 1 || tile.size[1] < 1) {
      this.renderer.draw(tile.rect, tile.uv, tile.page, this.mode, this.params, this.tone);
      return timings;
    }
    const started = performance.now();
    this.tileCanvas.width = tile.size[0];
    this.tileCanvas.height = tile.size[1];
    const viewport = this.page.getViewport({ scale: CSS_PER_POINT * this.zoom * tile.ratio });
    this.task = this.page.render({
      canvas: this.tileCanvas,
      viewport,
      // Shifts the page so that the visible part lands at the canvas origin.
      transform: [1, 0, 0, 1, -tile.origin[0], -tile.origin[1]],
    });
    try {
      await this.task.promise;
    } catch (error) {
      if (error?.name === 'RenderingCancelledException') return this.last;
      throw error;
    }
    const rendered = performance.now();
    this.renderer.uploadTile(this.tileCanvas);
    this.tileInfo = tile;
    this.renderer.draw(tile.rect, tile.uv, tile.page, this.mode, this.params, this.tone);
    this.renderer.sync();
    timings.pdfMs = rendered - started;
    timings.shaderMs = performance.now() - rendered;
    return timings;
  }

  // Shader only, for a changed mode or slider: the tile texture is reused.
  redraw() {
    if (!this.tileInfo) return 0;
    const started = performance.now();
    this.renderer.draw(this.tileInfo.rect, this.tileInfo.uv, this.tileInfo.page, this.mode, this.params, this.tone);
    this.renderer.sync();
    return performance.now() - started;
  }

  async redrawGpuTime() {
    if (!this.tileInfo) return null;
    return this.renderer.gpuTime(() =>
      this.renderer.draw(this.tileInfo.rect, this.tileInfo.uv, this.tileInfo.page, this.mode, this.params, this.tone));
  }

  // Comparison for the docs: the whole page in one canvas instead of a viewport tile.
  async measureFullPage(zoom) {
    const viewport = this.page.getViewport({ scale: CSS_PER_POINT * zoom });
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(viewport.width);
    canvas.height = Math.round(viewport.height);
    const started = performance.now();
    await this.page.render({ canvas, viewport }).promise;
    const rendered = performance.now();
    this.renderer.uploadTile(canvas);
    this.renderer.sync();
    this.tileInfo = null;
    return { size: [canvas.width, canvas.height], pdfMs: rendered - started, uploadMs: performance.now() - rendered };
  }

  thumbnail(width) {
    return this.renderer.thumbnail(width, this.mode === 3 ? 2 : this.mode, this.params, this.tone);
  }
}
