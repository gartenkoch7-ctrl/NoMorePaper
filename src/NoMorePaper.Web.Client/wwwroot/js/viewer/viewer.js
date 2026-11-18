// Entry point of the document viewer for Blazor (Viewer/ViewerInterop.cs). The page renders the
// stage with an empty .viewer-canvas and a focusable .viewer-scroller; everything inside them
// belongs to this module. Reports go to .NET via the DotNetObjectReference:
// OnViewChanged(zoomPercent, page, pageCount, hasText), OnRendered(ms), OnError(errorName).
import { analyzePage } from './analysis.js';
import { CSS_PER_POINT, MODE_INTELLIGENT, MODE_ORIGINAL } from './defaults.js';
import { FallbackRenderer } from './fallback.js';
import { attachInput, zoomStep } from './input.js';
import { PageView } from './page-view.js';
import { errorName, openDocument } from './pdf-document.js';
import { sharedRenderer } from './shader-renderer.js';
import { PageText } from './text-layer.js';

class Viewer {
  constructor(host, dotnet, originalColors) {
    const shared = sharedRenderer();
    this.renderer = shared.renderer ?? new FallbackRenderer(shared.reason);
    this.dotnet = dotnet;
    this.disposed = false;
    this.pageNumber = 0;
    this.pageCount = 0;
    this.pageToken = 0;
    this.hasText = null;
    this.reported = '';
    this.forceReport = false;
    this.analysis = null;
    this.page = null;

    this.canvasHost = host.querySelector('.viewer-canvas');
    this.scroller = host.querySelector('.viewer-scroller');
    this.spacer = document.createElement('div');
    this.spacer.className = 'viewer-spacer';
    this.scroller.append(this.spacer);
    this.canvasHost.append(this.renderer.canvas);
    this.text = new PageText(this.spacer);
    this.view = new PageView(this.renderer, this.scroller, this.spacer, {
      onRendered: (ms) => this.#rendered(ms),
      onLayout: (offset, scale) => this.text.place(offset, scale),
      onError: (error) => this.#call('OnError', errorName(error)),
    });
    this.view.mode = originalColors ? MODE_ORIGINAL : MODE_INTELLIGENT;

    const view = this.view;
    this.detachInput = attachInput(this.scroller, {
      get zoom() { return view.zoom; },
      zoomIn: () => this.zoomIn(),
      zoomOut: () => this.zoomOut(),
      reset: () => this.reset(),
      zoomTo: (zoom, screen) => view.setZoom(zoom, screen, true),
      nextPage: () => this.setPage(this.pageNumber + 1),
      previousPage: () => this.setPage(this.pageNumber - 1),
    });
    this.onScroll = () => view.onScroll();
    this.scroller.addEventListener('scroll', this.onScroll, { passive: true });
    this.resizeObserver = new ResizeObserver(() => view.onResize());
    this.resizeObserver.observe(this.scroller);
  }

  // Never throws: the result is { pageCount, renderer, fallbackReason } or { error }.
  async open(url) {
    try {
      this.loading = openDocument(url);
      this.document = await this.loading.promise;
      this.pageCount = this.document.numPages;
      await this.#showPage(1);
      return { pageCount: this.pageCount, renderer: this.renderer.kind, fallbackReason: this.renderer.reason ?? null };
    } catch (error) {
      return { error: errorName(error) };
    }
  }

  // Never throws (keys and buttons call it); a failure is reported to .NET.
  async setPage(number) {
    try {
      await this.#showPage(number);
    } catch (error) {
      this.#call('OnError', errorName(error));
    }
  }

  async #showPage(number) {
    if (!this.document || this.disposed) return;
    const target = Math.min(this.pageCount, Math.max(1, Math.round(number)));
    if (target === this.pageNumber) {
      // Still announce, so Page Down on the last page is not silent.
      this.#report(true);
      return;
    }
    const token = ++this.pageToken;
    const page = await this.document.getPage(target);
    const analysis = await analyzePage(page, this.renderer);
    if (token !== this.pageToken || this.disposed) {
      this.renderer.release(analysis);
      return;
    }
    const previous = { analysis: this.analysis, page: this.page };
    this.pageNumber = target;
    this.page = page;
    this.analysis = analysis;
    this.hasText = null;
    this.view.show(page, analysis);
    this.renderer.release(previous.analysis);
    previous.page?.cleanup();
    const hasText = await this.#textOf(page);
    if (token !== this.pageToken || this.disposed || hasText === null) return;
    this.hasText = hasText;
    this.#report();
  }

  // True or false, or null when replaced by the next page. The text layer is for screen
  // readers; if it fails, the page is still shown and the user hears it has no readable text.
  async #textOf(page) {
    try {
      return await this.text.show(page, CSS_PER_POINT * this.view.zoom);
    } catch (error) {
      this.#call('OnError', errorName(error));
      return false;
    }
  }

  // Commands are always answered, even when the zoom cannot change any more (1600 %).
  zoomIn() { this.#command(zoomStep(this.view.zoom, 1)); }

  zoomOut() { this.#command(zoomStep(this.view.zoom, -1)); }

  reset() { this.#command(1); }

  fitWidth() { this.#command(this.view.fitWidthZoom); }

  #command(zoom) {
    this.forceReport = true;
    this.view.setZoom(zoom);
  }

  setOriginalColors(original) {
    this.view.setMode(original ? MODE_ORIGINAL : MODE_INTELLIGENT);
  }

  #rendered(ms) {
    this.#call('OnRendered', Math.round(ms));
    this.#report();
  }

  // Tells .NET the settled state once page text and a sharp render are there; only changes,
  // unless force (a repeated command should still be answered).
  #report(force = false) {
    if (this.hasText === null || !this.view.tile) return;
    const zoom = Math.round(this.view.zoom * 100);
    const key = `${zoom}|${this.pageNumber}|${this.pageCount}|${this.hasText}`;
    if (key === this.reported && !force && !this.forceReport) return;
    this.reported = key;
    this.forceReport = false;
    this.#call('OnViewChanged', zoom, this.pageNumber, this.pageCount, this.hasText);
  }

  #call(method, ...args) {
    if (this.disposed) return;
    this.dotnet.invokeMethodAsync(method, ...args).catch(() => {
      // The page was left while the call was on its way.
    });
  }

  dispose() {
    this.disposed = true;
    this.detachInput();
    this.scroller.removeEventListener('scroll', this.onScroll);
    this.resizeObserver.disconnect();
    this.view.dispose();
    this.text.dispose();
    this.renderer.release(this.analysis);
    this.analysis = null;
    // The shared WebGL canvas leaves the page but keeps its context for the next viewer.
    this.renderer.canvas.remove();
    this.spacer.remove();
    this.loading?.destroy();
  }
}

export function createViewer(host, dotnet, originalColors) {
  return new Viewer(host, dotnet, originalColors);
}
