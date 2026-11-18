// PDF.js 6.5.438, served from wwwroot/lib/pdfjs (no CDN). The worker, character maps,
// standard fonts and the wasm decoders (JBIG2, JPEG 2000 - typical for scans) come from there.
import * as pdfjs from '../../lib/pdfjs/pdf.min.mjs';

const base = new URL('../../lib/pdfjs/', import.meta.url);
pdfjs.GlobalWorkerOptions.workerSrc = new URL('pdf.worker.min.mjs', base).href;

export { pdfjs };

// One worker for the viewer and every thumbnail; destroying a document leaves it running, so
// a list of thumbnails does not start a new worker per document.
let worker;

// Returns the PDF.js loading task; await .promise for the document, call .destroy() to free
// its memory in the worker.
export function openDocument(url) {
  worker ??= new pdfjs.PDFWorker();
  return pdfjs.getDocument({
    worker,
    url: new URL(url, document.baseURI).href,
    cMapUrl: new URL('cmaps/', base).href,
    cMapPacked: true,
    standardFontDataUrl: new URL('standard_fonts/', base).href,
    wasmUrl: new URL('wasm/', base).href,
  });
}

// Short technical name of a PDF.js or network error, safe to log (never a URL or message).
export function errorName(error) {
  const name = error?.name;
  return typeof name === 'string' && /^[A-Za-z]{1,40}$/.test(name) ? name : 'Error';
}

export const isCancelled = (error) => error?.name === 'RenderingCancelledException';
