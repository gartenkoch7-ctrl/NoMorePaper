// One PDF page in a scrollable view (docs/kb/darstellung.md "Kacheln bei hohem Zoom"). PDF.js
// renders only the visible part of the page plus a margin of half a window height, at the
// current zoom, so 1600 % costs about the same pixels as 100 %. Scrolling inside the margin and
// zoom gestures only rerun the shader on the existing tile; a sharp render follows.
import { CSS_PER_POINT, PAGE_MARGIN, ZOOM_MAX, ZOOM_MIN } from './defaults.js';
import { isCancelled } from './pdf-document.js';
import { clamp, covers, tileRegion, visiblePart } from './tile-geometry.js';

// Quiet time after a zoom gesture before the page is rendered sharp again.
const SHARP_DELAY_MS = 100;

export class PageView {
  // callbacks: onRendered(ms), onLayout(offsetCss, scale), onError(error)
  constructor(renderer, scroller, spacer, callbacks) {
    this.renderer = renderer;
    this.scroller = scroller;
    this.spacer = spacer;
    this.callbacks = callbacks;
    this.tileCanvas = document.createElement('canvas');
    this.zoom = 1;
    this.mode = 2;
    this.page = null;
    this.analysis = null;
    this.points = [1, 1];
    this.pageCss = null;
    this.offset = [0, 0];
    this.tile = null;      // last uploaded tile: region in device px at its zoom
    this.inflight = null;  // region of the render that is running
    this.task = null;
    this.busy = false;
    this.again = false;
    this.sharpTimer = 0;
  }

  show(page, analysis) {
    // A render of the previous page still running is stale: cancel it and render again.
    if (this.busy) {
      this.again = true;
      this.task?.cancel();
    }
    this.inflight = null;
    this.page = page;
    this.analysis = analysis;
    this.tile = null;
    const size = page.getViewport({ scale: 1 });
    this.points = [size.width, size.height];
    this.pageCss = null;
    // A new page starts at its top, centred horizontally.
    this.layout({ fraction: [0.5, 0] });
    this.draw();
    this.render();
  }

  // After the renderer was rebuilt (lost WebGL context) or replaced by the fallback: same page,
  // same place and zoom, drawn and rendered anew. The old tile died with the old renderer.
  resume(renderer, analysis) {
    this.renderer = renderer;
    this.analysis = analysis;
    this.tile = null;
    this.inflight = null;
    if (!this.page) return;
    this.draw();
    this.render();
  }

  get fitWidthZoom() {
    return clamp((this.scroller.clientWidth - 2 * PAGE_MARGIN) / (this.points[0] * CSS_PER_POINT), ZOOM_MIN, ZOOM_MAX);
  }

  // Sizes the scroll area for the current zoom. anchor.fraction (page fraction) ends up at
  // anchor.screen (CSS px in the scroller), by default in the centre of the view.
  layout(anchor) {
    const view = [this.scroller.clientWidth, this.scroller.clientHeight];
    const centre = [view[0] / 2, view[1] / 2];
    const fraction = anchor?.fraction ?? this.#fractionAt(centre);
    const screen = anchor?.screen ?? centre;
    const page = this.points.map((p) => p * CSS_PER_POINT * this.zoom);
    const area = [Math.max(view[0], page[0] + 2 * PAGE_MARGIN), Math.max(view[1], page[1] + 2 * PAGE_MARGIN)];
    this.spacer.style.width = `${area[0]}px`;
    this.spacer.style.height = `${area[1]}px`;
    this.pageCss = page;
    this.offset = [(area[0] - page[0]) / 2, (area[1] - page[1]) / 2];
    this.scroller.scrollLeft = this.offset[0] + fraction[0] * page[0] - screen[0];
    this.scroller.scrollTop = this.offset[1] + fraction[1] * page[1] - screen[1];
    this.callbacks.onLayout(this.offset, CSS_PER_POINT * this.zoom);
  }

  #fractionAt(screen) {
    if (!this.pageCss) return [0.5, 0];
    return [
      (this.scroller.scrollLeft + screen[0] - this.offset[0]) / this.pageCss[0],
      (this.scroller.scrollTop + screen[1] - this.offset[1]) / this.pageCss[1],
    ];
  }

  // screen: zoom centre in CSS px of the scroller (pointer or gesture), default the view centre.
  // gesture: show the scaled tile now and render sharp after a short quiet time.
  setZoom(zoom, screen, gesture = false) {
    if (!this.page) return;
    const next = clamp(zoom, ZOOM_MIN, ZOOM_MAX);
    const point = screen ?? [this.scroller.clientWidth / 2, this.scroller.clientHeight / 2];
    const fraction = this.#fractionAt(point);
    this.zoom = next;
    this.layout({ fraction, screen: point });
    this.draw();
    clearTimeout(this.sharpTimer);
    this.sharpTimer = 0;
    if (gesture) {
      this.sharpTimer = setTimeout(() => { this.sharpTimer = 0; this.render(); }, SHARP_DELAY_MS);
    } else {
      this.render();
    }
  }

  setMode(mode) {
    this.mode = mode;
    this.draw();
  }

  onScroll() {
    if (!this.page) return;
    this.draw();
    if (!this.sharpTimer && !this.#covers(this.tile)) this.render();
  }

  onResize() {
    if (!this.page) return;
    this.layout();
    this.draw();
    this.render();
  }

  // Visible area of the page in device px.
  #geometry() {
    const ratio = window.devicePixelRatio || 1;
    return {
      ratio,
      view: [Math.round(this.scroller.clientWidth * ratio), Math.round(this.scroller.clientHeight * ratio)],
      page: this.pageCss.map((v) => Math.round(v * ratio)),
      left: Math.round((this.scroller.scrollLeft - this.offset[0]) * ratio),
      top: Math.round((this.scroller.scrollTop - this.offset[1]) * ratio),
    };
  }

  #covers(region) {
    return !!this.pageCss && covers(region, this.#geometry(), this.zoom);
  }

  // Shader only: the last tile at its place on the screen, scaled while a gesture runs.
  draw() {
    if (!this.pageCss) return;
    const g = this.#geometry();
    this.renderer.resize(g.view[0], g.view[1]);
    this.renderer.canvas.style.width = `${this.scroller.clientWidth}px`;
    this.renderer.canvas.style.height = `${this.scroller.clientHeight}px`;
    this.renderer.draw(visiblePart(this.tile, g, this.zoom), this.analysis, this.mode);
  }

  // Renders the visible part plus margin with PDF.js. Calls while a render runs are merged into
  // one follow-up render; a running render that no longer covers the view is cancelled.
  render() {
    if (!this.page) return;
    if (this.busy) {
      if (!this.#covers(this.inflight)) {
        this.again = true;
        this.task?.cancel();
      }
      return;
    }
    this.busy = true;
    this.#loop();
  }

  async #loop() {
    try {
      do {
        this.again = false;
        const ms = await this.#renderOnce();
        if (ms !== null) this.callbacks.onRendered(ms);
      } while (this.again && this.page);
    } catch (error) {
      this.callbacks.onError(error);
    } finally {
      this.busy = false;
      this.inflight = null;
    }
  }

  async #renderOnce() {
    const page = this.page;
    const g = this.#geometry();
    const region = tileRegion(g, this.zoom, Math.round(g.view[1] / 2));
    if (!region) return null;
    this.inflight = region;
    const started = performance.now();
    this.tileCanvas.width = region.size[0];
    this.tileCanvas.height = region.size[1];
    this.task = page.render({
      canvas: this.tileCanvas,
      viewport: page.getViewport({ scale: CSS_PER_POINT * this.zoom * g.ratio }),
      // Shifts the page so that the wanted part lands at the canvas origin.
      transform: [1, 0, 0, 1, -region.origin[0], -region.origin[1]],
    });
    try {
      await this.task.promise;
    } catch (error) {
      if (isCancelled(error)) return null;
      throw error;
    } finally {
      this.task = null;
    }
    // The page may have changed while PDF.js was busy; that render is stale.
    if (page !== this.page) return null;
    this.renderer.uploadTile(this.tileCanvas);
    this.tile = region;
    this.draw();
    return performance.now() - started;
  }

  dispose() {
    clearTimeout(this.sharpTimer);
    this.task?.cancel();
    this.page = null;
    this.analysis = null;
    this.tileCanvas.width = 0;
    this.tileCanvas.height = 0;
  }
}
