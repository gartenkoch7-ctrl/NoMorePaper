// WebGL2 part of the spike: three passes (analyze -> fill -> display).
import { createProgram, gpuTime, loadText } from './glutil.js';

export const ANALYSIS_SCALE = 2.0; // analysis pixels per PDF point (144 dpi)
export const CELL = 12;            // analysis pixels per mask cell (about 2.1 mm on paper)
const SPACING = 4.5;               // 8 samples x 4.5 px: each cell looks at 36 px (6.4 mm)
const GROW = [0.15, 0.45];
const CORE = [0.55, 0.8];

export class ShaderRenderer {
  static async create(canvas) {
    const gl = canvas.getContext('webgl2', { alpha: false, antialias: false, depth: false });
    if (!gl) return null;
    const [vertex, oklab, display, analyze, fill] = await Promise.all(
      ['quad.vert', 'oklab.glsl', 'shader.frag', 'analyze.frag', 'fill.frag'].map(loadText));
    const header = `#version 300 es\nprecision highp float;\n${oklab}\n`;
    return new ShaderRenderer(gl, vertex, {
      display: header + display, analyze: header + analyze, fill: header + fill,
    });
  }

  constructor(gl, vertex, fragments) {
    this.gl = gl;
    this.display = createProgram(gl, vertex, fragments.display);
    this.analyze = createProgram(gl, vertex, fragments.analyze);
    this.fill = createProgram(gl, vertex, fragments.fill);
    this.timerExt = gl.getExtension('EXT_disjoint_timer_query_webgl2');

    const quad = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, quad);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([0, 0, 1, 0, 0, 1, 1, 1]), gl.STATIC_DRAW);
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
    // Canvas rows start at the top, GL rows at the bottom: flip once at upload, then every
    // texture and every pass shares the same orientation.
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);

    this.tile = this.#texture(gl.NEAREST, gl.NEAREST);
    this.page = this.#texture(gl.LINEAR_MIPMAP_LINEAR, gl.LINEAR);
    this.raw = this.#target(gl.NEAREST);
    this.mask = this.#target(gl.LINEAR);
    this.thumb = this.#target(gl.NEAREST);
    this.pageSize = [1, 1];
    this.maskScale = [1, 1];
    this.images = { photos: [], scans: [] };
  }

  get rendererName() {
    const info = this.gl.getExtension('WEBGL_debug_renderer_info');
    return info ? this.gl.getParameter(info.UNMASKED_RENDERER_WEBGL) : this.gl.getParameter(this.gl.RENDERER);
  }

  #texture(minFilter, magFilter) {
    const gl = this.gl;
    const texture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, minFilter);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, magFilter);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    return texture;
  }

  #target(filter) {
    const gl = this.gl;
    return { texture: this.#texture(filter, filter), framebuffer: gl.createFramebuffer(), width: 0, height: 0 };
  }

  #resize(target, width, height) {
    const gl = this.gl;
    if (target.width === width && target.height === height) return;
    gl.bindTexture(gl.TEXTURE_2D, target.texture);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, width, height, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
    gl.bindFramebuffer(gl.FRAMEBUFFER, target.framebuffer);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, target.texture, 0);
    target.width = width;
    target.height = height;
  }

  #bind(unit, texture) {
    this.gl.activeTexture(this.gl.TEXTURE0 + unit);
    this.gl.bindTexture(this.gl.TEXTURE_2D, texture);
  }

  // Whole page at analysis resolution: source for the mask and for thumbnails.
  setPage(canvas) {
    const gl = this.gl;
    this.#bind(0, this.page);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, gl.RGBA, gl.UNSIGNED_BYTE, canvas);
    gl.generateMipmap(gl.TEXTURE_2D);
    this.pageSize = [canvas.width, canvas.height];
    const cells = [Math.ceil(canvas.width / CELL), Math.ceil(canvas.height / CELL)];
    this.#resize(this.raw, cells[0], cells[1]);
    this.#resize(this.mask, cells[0], cells[1]);
    // The last cell may reach past the page; without this factor the mask drifts.
    this.maskScale = [canvas.width / (cells[0] * CELL), canvas.height / (cells[1] * CELL)];
  }

  // Pass 1: raw photo score per cell. Returned as RGBA bytes (bottom row first) because the
  // decision per image and the filling of photo boxes run on the CPU (images.js).
  analyzePage(params, paper) {
    const gl = this.gl;
    gl.bindFramebuffer(gl.FRAMEBUFFER, this.raw.framebuffer);
    gl.viewport(0, 0, this.raw.width, this.raw.height);
    gl.useProgram(this.analyze.program);
    this.#bind(0, this.page);
    gl.uniform1i(this.analyze.location('uPage'), 0);
    gl.uniform3fv(this.analyze.location('uPaper'), paper);
    gl.uniform1f(this.analyze.location('uCell'), CELL);
    gl.uniform1f(this.analyze.location('uSpacing'), SPACING);
    gl.uniform2f(this.analyze.location('uChromaRange'), params.chromaLo, Math.max(params.chromaHi, params.chromaLo + 1e-4));
    gl.uniform2f(this.analyze.location('uShareRange'), params.shareLo, Math.max(params.shareHi, params.shareLo + 1e-4));
    gl.uniform2f(this.analyze.location('uDetailRange'), params.detailLo, Math.max(params.detailHi, params.detailLo + 1e-4));
    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
    const pixels = new Uint8Array(this.raw.width * this.raw.height * 4);
    gl.readPixels(0, 0, this.raw.width, this.raw.height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
    return { pixels, cells: [this.raw.width, this.raw.height], maskScale: this.maskScale };
  }

  // Pass 2: the mask the display pass reads, from the raw scores after the CPU step.
  buildMask(pixels) {
    const gl = this.gl;
    this.#bind(0, this.raw.texture);
    // The bytes come from readPixels and are already bottom row first.
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
    gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, this.raw.width, this.raw.height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
    gl.bindFramebuffer(gl.FRAMEBUFFER, this.mask.framebuffer);
    gl.viewport(0, 0, this.mask.width, this.mask.height);
    gl.useProgram(this.fill.program);
    gl.uniform1i(this.fill.location('uRaw'), 0);
    gl.uniform2fv(this.fill.location('uGrow'), GROW);
    gl.uniform2fv(this.fill.location('uCore'), CORE);
    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
  }

  uploadTile(canvas) {
    const gl = this.gl;
    this.#bind(0, this.tile);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, gl.RGBA, gl.UNSIGNED_BYTE, canvas);
  }

  #drawDisplay(texture, uv, pagePixels, mode, params, tone) {
    const gl = this.gl;
    const u = this.display.location;
    gl.useProgram(this.display.program);
    this.#bind(0, texture);
    this.#bind(1, this.mask.texture);
    gl.uniform1i(u('uPage'), 0);
    gl.uniform1i(u('uMask'), 1);
    gl.uniform4fv(u('uTile'), uv);
    gl.uniform2fv(u('uMaskScale'), this.maskScale);
    gl.uniform2fv(u('uPagePixels'), pagePixels);
    const { photos, scans } = this.images;
    gl.uniform1i(u('uPhotoCount'), params.frames ? photos.length : 0);
    gl.uniform1i(u('uScanCount'), params.frames ? scans.length : -1);
    if (photos.length) gl.uniform4fv(u('uPhotoRects[0]'), photos.flat());
    if (scans.length) gl.uniform4fv(u('uScanRects[0]'), scans.flat());
    gl.uniform1i(u('uMode'), mode);
    gl.uniform1f(u('uWhite'), tone.white);
    gl.uniform1f(u('uBlack'), Math.min(tone.black, tone.white - 0.05));
    gl.uniform1f(u('uGamma'), params.gamma);
    gl.uniform1f(u('uChroma'), params.chroma);
    gl.uniform1f(u('uLift'), params.lift);
    gl.uniform2f(u('uNeutral'), params.neutralLo, Math.max(params.neutralHi, params.neutralLo + 1e-4));
    gl.uniform2f(u('uBlend'), params.blendLo, Math.max(params.blendHi, params.blendLo + 1e-4));
    gl.uniform1f(u('uPhotos'), params.photos ? 1 : 0);
    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
  }

  // rect: tile on the canvas in device pixels (top-left origin);
  // uv: the same tile inside the page as [x, y, width, height] with y pointing up.
  // pagePixels: size of the whole page at the current zoom in device pixels.
  draw(rect, uv, pagePixels, mode, params, tone) {
    const gl = this.gl;
    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    gl.viewport(0, 0, gl.drawingBufferWidth, gl.drawingBufferHeight);
    gl.clearColor(0, 0, 0, 1);
    gl.clear(gl.COLOR_BUFFER_BIT);
    if (rect.width < 1 || rect.height < 1) return;
    gl.viewport(rect.x, gl.drawingBufferHeight - rect.y - rect.height, rect.width, rect.height);
    this.#drawDisplay(this.tile, uv, pagePixels, mode, params, tone);
  }

  // Thumbnails reuse the analysis render and the same display shader in an offscreen target,
  // so one WebGL context serves the viewer and every preview image.
  thumbnail(width, mode, params, tone) {
    const gl = this.gl;
    const height = Math.round(width * this.pageSize[1] / this.pageSize[0]);
    this.#resize(this.thumb, width, height);
    gl.bindFramebuffer(gl.FRAMEBUFFER, this.thumb.framebuffer);
    gl.viewport(0, 0, width, height);
    this.#drawDisplay(this.page, [0, 0, 1, 1], [width, height], mode, params, tone);
    const pixels = new Uint8ClampedArray(width * height * 4);
    gl.readPixels(0, 0, width, height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
    const image = new ImageData(width, height);
    for (let y = 0; y < height; y++) {
      image.data.set(pixels.subarray((height - 1 - y) * width * 4, (height - y) * width * 4), y * width * 4);
    }
    return image;
  }

  // Blocks until the GPU has finished, so wall-clock timing includes the shader work.
  sync() {
    this.gl.readPixels(0, 0, 1, 1, this.gl.RGBA, this.gl.UNSIGNED_BYTE, new Uint8Array(4));
  }

  gpuTime(work) {
    return gpuTime(this.gl, this.timerExt, work);
  }
}
