// WebGL2 renderer: photo analysis once per page (analyze -> CPU -> fill) and the display pass.
// One context serves the viewer and every thumbnail, because browsers allow only a few WebGL
// contexts per page. The viewer moves the shared canvas into its stage; thumbnails render into
// an offscreen framebuffer and read the pixels back.
import { CELL, CORE, GROW, PARAMS, SPACING } from './defaults.js';
import { FRAGMENT_HEADER, QUAD_VERT } from './glsl-common.js';
import { ANALYZE_FRAG, FILL_FRAG } from './glsl-analysis.js';
import { DISPLAY_FRAG } from './glsl-display.js';
import { createProgram, createTarget, createTexture, deleteTarget } from './gl-util.js';
import { classifyImages, fillPhotoBoxes } from './images.js';

let shared;

// The shared renderer, or { renderer: null, reason } when WebGL2 is missing or broken.
export function sharedRenderer() {
  if (!shared) shared = ShaderRenderer.tryCreate();
  return shared;
}

export class ShaderRenderer {
  static tryCreate() {
    const canvas = document.createElement('canvas');
    const gl = canvas.getContext('webgl2', { alpha: false, antialias: false, depth: false });
    if (!gl) return { renderer: null, reason: 'no-webgl2' };
    try {
      const renderer = new ShaderRenderer(gl, canvas);
      // A lost context cannot draw again; the next viewer or thumbnail starts a new one.
      canvas.addEventListener('webglcontextlost', () => { shared = undefined; });
      return { renderer, reason: null };
    } catch {
      return { renderer: null, reason: 'shader-error' };
    }
  }

  constructor(gl, canvas) {
    this.gl = gl;
    this.canvas = canvas;
    this.kind = 'webgl2';
    this.display = createProgram(gl, QUAD_VERT, FRAGMENT_HEADER + DISPLAY_FRAG);
    this.analyze = createProgram(gl, QUAD_VERT, FRAGMENT_HEADER + ANALYZE_FRAG);
    this.fill = createProgram(gl, QUAD_VERT, FRAGMENT_HEADER + FILL_FRAG);

    const quad = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, quad);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([0, 0, 1, 0, 0, 1, 1, 1]), gl.STATIC_DRAW);
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
    // Canvas rows start at the top, GL rows at the bottom: flip once at upload, then every
    // texture and every pass shares the same orientation.
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
    this.tile = createTexture(gl, gl.NEAREST, gl.NEAREST);
  }

  // The photo mask needs the analysis render of every page the viewer shows.
  get needsAnalysis() { return true; }

  #bind(unit, texture) {
    this.gl.activeTexture(this.gl.TEXTURE0 + unit);
    this.gl.bindTexture(this.gl.TEXTURE_2D, texture);
  }

  // Whole page at analysis resolution -> photo mask. keepPage keeps the page texture for a
  // thumbnail; otherwise it is freed at once (8 MB per A4 page, the mask has 56 KB).
  analyzePage(pageCanvas, paper, imageCoordinates, keepPage) {
    const gl = this.gl;
    const page = createTexture(gl, gl.LINEAR_MIPMAP_LINEAR, gl.LINEAR);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, gl.RGBA, gl.UNSIGNED_BYTE, pageCanvas);
    gl.generateMipmap(gl.TEXTURE_2D);
    const pageSize = [pageCanvas.width, pageCanvas.height];
    const cells = [Math.ceil(pageSize[0] / CELL), Math.ceil(pageSize[1] / CELL)];
    // The last cell may reach past the page; without this factor the mask drifts.
    const maskScale = [pageSize[0] / (cells[0] * CELL), pageSize[1] / (cells[1] * CELL)];
    const raw = createTarget(gl, gl.NEAREST, cells[0], cells[1]);
    const mask = createTarget(gl, gl.LINEAR, cells[0], cells[1]);

    // Pass 1: raw photo score per cell, read back because the decision per image and the
    // filling of photo boxes run on the CPU.
    gl.bindFramebuffer(gl.FRAMEBUFFER, raw.framebuffer);
    gl.viewport(0, 0, cells[0], cells[1]);
    const a = this.analyze;
    gl.useProgram(a.program);
    this.#bind(0, page);
    gl.uniform1i(a.location('uPage'), 0);
    gl.uniform3fv(a.location('uPaper'), paper.linear);
    gl.uniform1f(a.location('uCell'), CELL);
    gl.uniform1f(a.location('uSpacing'), SPACING);
    gl.uniform2f(a.location('uChromaRange'), PARAMS.chromaLo, PARAMS.chromaHi);
    gl.uniform2f(a.location('uShareRange'), PARAMS.shareLo, PARAMS.shareHi);
    gl.uniform2f(a.location('uDetailRange'), PARAMS.detailLo, PARAMS.detailHi);
    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
    const pixels = new Uint8Array(cells[0] * cells[1] * 4);
    gl.readPixels(0, 0, cells[0], cells[1], gl.RGBA, gl.UNSIGNED_BYTE, pixels);
    const images = classifyImages(imageCoordinates, pixels, cells, maskScale);
    fillPhotoBoxes(pixels, cells);

    // Pass 2: the mask the display pass reads. The bytes come from readPixels and are already
    // bottom row first.
    this.#bind(0, raw.texture);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
    gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, cells[0], cells[1], gl.RGBA, gl.UNSIGNED_BYTE, pixels);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
    gl.bindFramebuffer(gl.FRAMEBUFFER, mask.framebuffer);
    gl.viewport(0, 0, cells[0], cells[1]);
    gl.useProgram(this.fill.program);
    gl.uniform1i(this.fill.location('uRaw'), 0);
    gl.uniform2fv(this.fill.location('uGrow'), GROW);
    gl.uniform2fv(this.fill.location('uCore'), CORE);
    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
    deleteTarget(gl, raw);
    if (!keepPage) gl.deleteTexture(page);

    return {
      page: keepPage ? page : null,
      mask,
      maskScale,
      pageSize,
      images,
      tone: { white: paper.white, black: paper.black },
    };
  }

  release(analysis) {
    if (!analysis) return;
    if (analysis.page) this.gl.deleteTexture(analysis.page);
    deleteTarget(this.gl, analysis.mask);
    analysis.page = null;
    analysis.mask = null;
  }

  resize(width, height) {
    if (this.canvas.width !== width || this.canvas.height !== height) {
      this.canvas.width = width;
      this.canvas.height = height;
    }
  }

  uploadTile(canvas) {
    const gl = this.gl;
    this.#bind(0, this.tile);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, gl.RGBA, gl.UNSIGNED_BYTE, canvas);
  }

  #drawDisplay(texture, part, analysis, mode) {
    const gl = this.gl;
    const u = this.display.location;
    gl.useProgram(this.display.program);
    this.#bind(0, texture);
    this.#bind(1, analysis.mask.texture);
    gl.uniform1i(u('uPage'), 0);
    gl.uniform1i(u('uMask'), 1);
    gl.uniform4fv(u('uSource'), part.source);
    gl.uniform4fv(u('uTile'), part.uv);
    gl.uniform2fv(u('uMaskScale'), analysis.maskScale);
    gl.uniform2fv(u('uPagePixels'), part.pagePixels);
    const { photos, scans } = analysis.images;
    gl.uniform1i(u('uPhotoCount'), photos.length);
    gl.uniform1i(u('uScanCount'), scans.length);
    if (photos.length) gl.uniform4fv(u('uPhotoRects[0]'), photos.flat());
    if (scans.length) gl.uniform4fv(u('uScanRects[0]'), scans.flat());
    gl.uniform1i(u('uMode'), mode);
    gl.uniform1f(u('uWhite'), analysis.tone.white);
    gl.uniform1f(u('uBlack'), Math.min(analysis.tone.black, analysis.tone.white - 0.05));
    gl.uniform1f(u('uGamma'), PARAMS.gamma);
    gl.uniform1f(u('uChroma'), PARAMS.chroma);
    gl.uniform1f(u('uLift'), PARAMS.lift);
    gl.uniform2f(u('uNeutral'), PARAMS.neutralLo, PARAMS.neutralHi);
    gl.uniform2f(u('uBlend'), PARAMS.blendLo, PARAMS.blendHi);
    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
  }

  // part: the visible piece of the last uploaded tile (see page-view.js), or null for black.
  draw(part, analysis, mode) {
    const gl = this.gl;
    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    gl.viewport(0, 0, gl.drawingBufferWidth, gl.drawingBufferHeight);
    gl.clearColor(0, 0, 0, 1);
    gl.clear(gl.COLOR_BUFFER_BIT);
    if (!part || !analysis?.mask) return;
    const { rect } = part;
    gl.viewport(rect.x, gl.drawingBufferHeight - rect.y - rect.height, rect.width, rect.height);
    this.#drawDisplay(this.tile, part, analysis, mode);
  }

  // Same display shader on the analysis render, into an offscreen target (needs keepPage).
  thumbnail(analysis, width, mode) {
    const gl = this.gl;
    const height = Math.round(width * analysis.pageSize[1] / analysis.pageSize[0]);
    const target = createTarget(gl, gl.NEAREST, width, height);
    gl.bindFramebuffer(gl.FRAMEBUFFER, target.framebuffer);
    gl.viewport(0, 0, width, height);
    const whole = [0, 0, 1, 1];
    this.#drawDisplay(analysis.page, { source: whole, uv: whole, pagePixels: [width, height] }, analysis, mode);
    const pixels = new Uint8ClampedArray(width * height * 4);
    gl.readPixels(0, 0, width, height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
    deleteTarget(gl, target);
    const image = new ImageData(width, height);
    for (let y = 0; y < height; y++) {
      image.data.set(pixels.subarray((height - 1 - y) * width * 4, (height - y) * width * 4), y * width * 4);
    }
    return image;
  }
}
