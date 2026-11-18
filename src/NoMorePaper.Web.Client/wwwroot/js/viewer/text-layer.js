// Text of the page for screen readers and text selection: the PDF.js TextLayer as transparent
// DOM over the canvas, always for the whole page, even though the canvas shows only a part.
// For scans the text comes from the OCR layer of the PDF/A. It needs no shader.
import { pdfjs } from './pdf-document.js';

export class PageText {
  constructor(spacer) {
    this.container = document.createElement('div');
    this.container.className = 'viewer-text';
    spacer.append(this.container);
    this.layer = null;
    this.page = null;
  }

  // Shows the text of page; resolves to true when the page has any text, null if replaced.
  async show(page, scale) {
    this.layer?.cancel();
    this.container.replaceChildren();
    this.page = page;
    this.#scale(scale);
    const layer = new pdfjs.TextLayer({
      textContentSource: page.streamTextContent({ includeMarkedContent: true }),
      container: this.container,
      viewport: page.getViewport({ scale }),
    });
    this.layer = layer;
    try {
      await layer.render();
    } catch (error) {
      if (error?.name === 'AbortException') return null;
      throw error;
    }
    return layer.textContentItemsStr.some((text) => text.trim().length > 0);
  }

  // Follows the page: top-left corner at offset (CSS px in the scroll area), scale in CSS px per point.
  place(offset, scale) {
    this.container.style.left = `${offset[0]}px`;
    this.container.style.top = `${offset[1]}px`;
    this.#scale(scale);
    if (this.page) this.layer?.update({ viewport: this.page.getViewport({ scale }) });
  }

  // Span positions are percentages and font sizes follow this variable, so a zoom only sets it.
  #scale(scale) {
    this.container.style.setProperty('--total-scale-factor', String(scale));
  }

  dispose() {
    this.layer?.cancel();
    this.layer = null;
    this.page = null;
    this.container.remove();
  }
}
