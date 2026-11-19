// Entry point of the camera page for Blazor (Camera/CameraInterop.cs). The page renders a host with a
// <video>; everything inside belongs to this module. Reports go to .NET via the DotNetObjectReference:
// OnHint(hint) when the hint changes (see guidance.js HINTS), OnCaptured(pageCount, elapsedMs) after a
// page was taken, OnError(code) when the camera stops working.
//
// The pages stay in this module as JPEG blobs until the page fetches them for the upload (page(i));
// .NET only holds them while it sends them. They outlive a camera that stopped (iOS ends the camera
// when the phone locks), so a restarted camera adds to them; the page clears them when it is left.
import { capturePage } from './capture.js';
import { findQuad, loadOpenCv } from './edges.js';
import { Feedback, prepareAudio } from './feedback.js';
import { assess, isNewPage, remember } from './guidance.js';

export { prepareAudio };

/** Frames are analysed this small: enough for the outline of a sheet, fast on a phone. */
const ANALYSIS_WIDTH = 640;
/** About 8 analyses per second; the video itself keeps its frame rate. */
const ANALYSIS_INTERVAL_MS = 125;
/** The server takes at most 50 pages per document (PageUploadForm.MaxPages). */
const MAX_PAGES = 50;

let pages = [];
let active = null;

export function createCamera(host, dotnet, options) {
  active?.dispose();
  active = new Camera(host, dotnet, options ?? {});
  return active;
}

/** @returns the number of pages left. A running camera may take the same sheet again. */
export function discardLast() {
  pages.pop();
  active?.rearm();
  return pages.length;
}

/** The JPEG of page `index` (0-based), as bytes for .NET. */
export async function page(index) {
  return new Uint8Array(await pages[index].arrayBuffer());
}

/**
 * Drops the first `count` pages (all when count is null): after the upload only those sent, since the
 * camera may have taken the next page meanwhile; all when the page is left. @returns the pages left.
 */
export function clearPages(count) {
  pages = count == null ? [] : pages.slice(count);
  return pages.length;
}

class Camera {
  constructor(host, dotnet, options) {
    this.video = host.querySelector('video');
    this.dotnet = dotnet;
    this.feedback = new Feedback(options.tones !== false);
    this.analysis = document.createElement('canvas');
    this.history = [];
    this.lastHint = null;
    this.lastQuad = null;
    this.captured = undefined; // quad of the last picture while waiting for a new page; undefined = armed
    this.absentSince = null;
    this.lastAnalysis = -Infinity;
    this.frameHandle = 0;
    this.busy = false;
    this.disposed = false;
    this.frames = 0;
    this.analysisMs = 0;
  }

  // Never throws: { width, height, openCvMs } or { error } (unsupported, denied, no-camera, busy,
  // opencv-failed, failed, stopped).
  async start() {
    if (!navigator.mediaDevices?.getUserMedia) {
      return { error: 'unsupported' }; // no secure context (http) or no camera API at all
    }
    const started = performance.now();
    const openCv = loadOpenCv();
    openCv.catch(() => {}); // awaited below; only keeps an early failure from being "unhandled"
    try {
      this.stream = await navigator.mediaDevices.getUserMedia({
        audio: false,
        video: { facingMode: 'environment', width: { ideal: 3840 }, height: { ideal: 2160 } },
      });
      if (this.disposed) {
        return this.#stopped();
      }
      this.video.muted = true;
      this.video.playsInline = true;
      this.video.srcObject = this.stream;
      await this.video.play();
    } catch (error) {
      this.#stopStream();
      return { error: mediaError(error) };
    }

    try {
      this.cv = await openCv;
    } catch {
      this.#stopStream();
      return { error: 'opencv-failed' };
    }
    if (this.disposed) {
      return this.#stopped();
    }
    for (const track of this.stream.getVideoTracks()) {
      track.addEventListener('ended', () => this.#call('OnError', 'ended'));
    }
    this.#schedule();
    return { width: this.video.videoWidth, height: this.video.videoHeight, openCvMs: Math.round(performance.now() - started) };
  }

  /** The manual shutter: the sheet found last, or the whole frame if none was found. */
  async capture() {
    if (!this.cv || this.disposed) {
      return false;
    }
    return await this.#capture(this.lastQuad);
  }

  /** After a page was discarded: the sheet in front of the camera may be taken again. */
  rearm() {
    this.captured = undefined;
    this.history = [];
    this.lastHint = null;
  }

  setTones(enabled) {
    this.feedback.setEnabled(enabled);
  }

  /** Average time of one analysis so far, for the log when the page is left. */
  stats() {
    return { frames: this.frames, averageMs: this.frames === 0 ? 0 : Math.round(this.analysisMs / this.frames) };
  }

  dispose() {
    if (active === this) {
      active = null;
    }
    this.disposed = true;
    if (this.frameHandle) {
      if (this.video.cancelVideoFrameCallback) {
        this.video.cancelVideoFrameCallback(this.frameHandle);
      } else {
        cancelAnimationFrame(this.frameHandle);
      }
    }
    this.#stopStream();
    this.feedback.dispose();
    this.analysis.width = 0;
  }

  #stopped() {
    this.#stopStream();
    return { error: 'stopped' };
  }

  // Stops every track, so the camera light goes out at once (privacy, battery).
  #stopStream() {
    for (const track of this.stream?.getTracks() ?? []) {
      track.stop();
    }
    this.stream = null;
    if (this.video) {
      this.video.srcObject = null;
    }
  }

  #schedule() {
    if (this.disposed) {
      return;
    }
    this.frameHandle = this.video.requestVideoFrameCallback
      ? this.video.requestVideoFrameCallback((now) => this.#onFrame(now))
      : requestAnimationFrame((now) => this.#onFrame(now));
  }

  #onFrame(now) {
    this.frameHandle = 0;
    if (this.disposed) {
      return;
    }
    if (!this.busy && now - this.lastAnalysis >= ANALYSIS_INTERVAL_MS) {
      this.lastAnalysis = now;
      this.#analyse(now);
    }
    this.#schedule();
  }

  #analyse(now) {
    const quad = this.#findQuad();
    if (!quad) {
      this.absentSince ??= now;
    } else {
      this.absentSince = null;
    }
    this.lastQuad = quad;

    if (this.captured !== undefined) {
      // Just took a picture: quiet until the next page is in front of the camera.
      if (!isNewPage(this.captured, quad, quad ? 0 : now - this.absentSince)) {
        return;
      }
      this.captured = undefined;
      this.history = [];
      this.lastHint = null;
    }

    const result = assess(quad, this.history, now);
    this.history = remember(this.history, quad, now);
    this.feedback.setQuality(result.quality);
    if (result.hint !== this.lastHint) {
      this.lastHint = result.hint;
      this.#call('OnHint', result.hint);
    }
    if (result.ready) {
      this.#capture(quad);
    }
  }

  #findQuad() {
    const video = this.video;
    if (!video.videoWidth) {
      return null;
    }
    const canvas = this.analysis;
    canvas.width = ANALYSIS_WIDTH;
    canvas.height = Math.round((video.videoHeight * ANALYSIS_WIDTH) / video.videoWidth);
    canvas.getContext('2d', { willReadFrequently: true }).drawImage(video, 0, 0, canvas.width, canvas.height);
    const started = performance.now();
    try {
      return findQuad(this.cv, canvas);
    } catch {
      return null;
    } finally {
      this.frames++;
      this.analysisMs += performance.now() - started;
    }
  }

  async #capture(quad) {
    if (this.busy) {
      return false;
    }
    this.captured = quad;
    if (pages.length >= MAX_PAGES) {
      // Told once per sheet: the camera waits for the next page before it tries again.
      this.#call('OnError', 'too-many-pages');
      return false;
    }
    this.busy = true;
    this.feedback.setQuality(0);
    this.feedback.shutter();
    const started = performance.now();
    try {
      const taken = await capturePage(this.cv, this.video, quad);
      if (this.disposed) {
        return false;
      }
      pages.push(taken);
      this.#call('OnCaptured', pages.length, Math.round(performance.now() - started));
      return true;
    } catch {
      // Stays waiting for a new page: a failure on every frame would repeat the error without end.
      // "Auslösen" tries again at once.
      this.#call('OnError', 'capture-failed');
      return false;
    } finally {
      this.busy = false;
    }
  }

  #call(method, ...args) {
    if (!this.disposed) {
      this.dotnet.invokeMethodAsync(method, ...args).catch(() => {});
    }
  }
}

function mediaError(error) {
  switch (error?.name) {
    case 'NotAllowedError':
    case 'SecurityError':
      return 'denied';
    case 'NotFoundError':
    case 'OverconstrainedError':
      return 'no-camera';
    case 'NotReadableError':
    case 'AbortError':
      return 'busy';
    case 'NotSupportedError':
      return 'unsupported'; // e.g. a browser without any capture support (headless Chromium)
    default:
      return 'failed';
  }
}
