// Finds the sheet of paper in a (small) video frame with OpenCV.js: grey, blur, Canny edges, outer
// contours, and the largest convex quadrilateral among them. OpenCV.js (Apache-2.0, 4.12.0) is served
// from wwwroot/lib/opencv and loaded only by the camera page, once per app session.
//
// Every Mat is freed in a finally block: OpenCV.js allocates in the WebAssembly heap, which the
// JavaScript garbage collector never sees.

const OPENCV_URL = 'lib/opencv/opencv.js';

/** Ignore outlines smaller than this share of the frame: text blocks, shadows, noise. */
const MIN_CONTOUR_AREA = 0.05;

/** Compiling the WebAssembly inside opencv.js takes seconds on an old phone; past this it has failed. */
const LOAD_TIMEOUT_MS = 60_000;

let loading = null;

/** Loads OpenCV.js once; resolves to the ready `cv` object, rejects if it cannot be loaded. */
export function loadOpenCv() {
  loading ??= new Promise((resolve, reject) => {
    const script = document.createElement('script');
    script.src = OPENCV_URL;
    script.async = true;
    script.onload = () => whenReady(globalThis.cv).then(resolve, reject);
    script.onerror = () => reject(new Error('opencv-script'));
    document.head.append(script);
  }).catch((error) => {
    loading = null;
    throw error;
  });
  return loading;
}

// The official 4.12.0 build is an old-style Emscripten module: `cv` is the module itself, ready at once
// or after onRuntimeInitialized, and it has a `then` method that hands out the module again. Resolving
// a promise with such a "thenable" calls `then` forever and freezes the page, so `then` is removed
// before the module is handed on. A build that sets `cv` to a real promise is handled too.
function whenReady(cv) {
  if (cv instanceof Promise) {
    return cv.then((module) => whenReady(module));
  }
  return new Promise((resolve, reject) => {
    if (!cv) {
      reject(new Error('opencv-missing'));
      return;
    }
    const timer = setTimeout(() => reject(new Error('opencv-timeout')), LOAD_TIMEOUT_MS);
    const ready = () => {
      clearTimeout(timer);
      delete cv.then;
      resolve(cv);
    };
    if (typeof cv.Mat === 'function') {
      ready();
      return;
    }
    const before = cv.onRuntimeInitialized;
    cv.onRuntimeInitialized = () => {
      before?.();
      ready();
    };
    cv.onAbort = () => {
      clearTimeout(timer);
      reject(new Error('opencv-abort'));
    };
  });
}

/**
 * @param cv the ready OpenCV.js object.
 * @param source a canvas holding the frame (any size; ~640 px wide keeps it fast).
 * @returns the corners normalized 0–1, ordered top-left, top-right, bottom-right, bottom-left, or null.
 */
export function findQuad(cv, source) {
  const width = source.width;
  const height = source.height;
  const mats = [];
  const track = (mat) => {
    mats.push(mat);
    return mat;
  };
  try {
    const rgba = track(cv.imread(source));
    const gray = track(new cv.Mat());
    cv.cvtColor(rgba, gray, cv.COLOR_RGBA2GRAY);
    cv.GaussianBlur(gray, gray, new cv.Size(5, 5), 0);
    const edges = track(new cv.Mat());
    cv.Canny(gray, edges, 50, 150);
    // Closes small gaps in the outline (a finger, a fold) so the contour stays one piece.
    const kernel = track(cv.Mat.ones(3, 3, cv.CV_8U));
    cv.dilate(edges, edges, kernel);

    const contours = track(new cv.MatVector());
    const hierarchy = track(new cv.Mat());
    cv.findContours(edges, contours, hierarchy, cv.RETR_EXTERNAL, cv.CHAIN_APPROX_SIMPLE);
    return largestQuad(cv, contours, width, height, track);
  } finally {
    for (const mat of mats) {
      mat.delete();
    }
  }
}

function largestQuad(cv, contours, width, height, track) {
  const minArea = MIN_CONTOUR_AREA * width * height;
  let best = null;
  let bestArea = 0;
  for (let i = 0; i < contours.size(); i++) {
    const contour = track(contours.get(i));
    const area = cv.contourArea(contour);
    if (area < minArea || area <= bestArea) {
      continue;
    }
    const approx = track(new cv.Mat());
    cv.approxPolyDP(contour, approx, 0.02 * cv.arcLength(contour, true), true);
    if (approx.rows === 4 && cv.isContourConvex(approx)) {
      best = approx.data32S.slice(0, 8);
      bestArea = area;
    }
  }
  if (!best) {
    return null;
  }
  const points = [];
  for (let i = 0; i < 4; i++) {
    points.push({ x: best[2 * i] / width, y: best[2 * i + 1] / height });
  }
  return orderCorners(points);
}

/** Orders four points top-left, top-right, bottom-right, bottom-left (by x + y and y − x). */
export function orderCorners(points) {
  const bySum = [...points].sort((a, b) => a.x + a.y - (b.x + b.y));
  const byDiff = [...points].sort((a, b) => a.y - a.x - (b.y - b.x));
  return [bySum[0], byDiff[0], bySum[3], byDiff[3]];
}
