// Guidance for the camera page: from the sheet found in a video frame to a hint, a quality for the
// tones and whether the picture may be taken. Pure: no DOM, no clock (the time comes as an argument),
// so the rules are tested as a table (tests/NoMorePaper.UI.Tests/Camera/GuidanceTests.cs).
//
// A quad is four corners {x, y}, normalized 0–1 to the frame, ordered top-left, top-right,
// bottom-right, bottom-left; null when no sheet was found. History entries are { t, quad } of
// earlier frames, oldest first (see remember()).

/** The sheet's centre may be this far (share of the frame) from the frame's centre before a direction is hinted. */
export const CENTER_TOLERANCE = 0.12;
/** The sheet must cover at least this share of the frame. */
export const MIN_AREA = 0.35;
/** A corner closer than this to the frame's border is probably cut off. */
export const EDGE_MARGIN = 0.02;
/** Corners moving less than this (share of the frame) count as still. */
export const STEADY_MOVE = 0.015;
/** How long the corners must stay still before the picture is taken. */
export const STEADY_MS = 800;
/** After a picture, the next one waits until the sheet moved this far (share of the frame)… */
export const NEW_PAGE_MOVE = 0.1;
/** …or was not found for this long: a new page. Shorter gaps are a flickering detection, not a new sheet. */
export const NEW_PAGE_ABSENT_MS = 500;

/** The hints, in the order they win: a cut-off sheet first, then size, then position, then stillness. */
export const HINTS = Object.freeze(['search', 'farther', 'closer', 'left', 'right', 'up', 'down', 'hold', 'ready']);

/**
 * @returns {{ hint: string, quality: number, ready: boolean }} hint (see HINTS); quality 0–1 for the
 * tones (0 = nothing found); ready = all corners inside, large enough and still for STEADY_MS.
 */
export function assess(quad, history, nowMs) {
  if (!quad) {
    return { hint: 'search', quality: 0, ready: false };
  }

  const area = quadArea(quad);
  const center = centerOf(quad);
  const dx = center.x - 0.5;
  const dy = center.y - 0.5;
  const atEdge = quad.some((p) => p.x < EDGE_MARGIN || p.y < EDGE_MARGIN || p.x > 1 - EDGE_MARGIN || p.y > 1 - EDGE_MARGIN);
  const steady = isSteady(quad, history, nowMs);
  const ready = !atEdge && area >= MIN_AREA && steady;
  return { hint: hintFor(atEdge, area, dx, dy, steady), quality: qualityOf(atEdge, area, dx, dy, ready), ready };
}

/** The history with this frame added and entries older than twice STEADY_MS dropped; the input stays unchanged. */
export function remember(history, quad, nowMs) {
  const keepFrom = nowMs - 2 * STEADY_MS;
  return [...history.filter((entry) => entry.t >= keepFrom), { t: nowMs, quad }];
}

/**
 * After a picture of capturedQuad: true once the camera sees a new page, so the next picture may be
 * taken. absentMs = how long no sheet has been found (0 while one is found).
 */
export function isNewPage(capturedQuad, quad, absentMs) {
  if (!quad) {
    return absentMs >= NEW_PAGE_ABSENT_MS;
  }
  return !capturedQuad || maxCornerMove(capturedQuad, quad) > NEW_PAGE_MOVE;
}

/** Area of the quad as a share of the frame (shoelace formula). */
export function quadArea(quad) {
  let twice = 0;
  for (let i = 0; i < quad.length; i++) {
    const a = quad[i];
    const b = quad[(i + 1) % quad.length];
    twice += a.x * b.y - b.x * a.y;
  }
  return Math.abs(twice) / 2;
}

function hintFor(atEdge, area, dx, dy, steady) {
  if (atEdge) {
    return 'farther';
  }
  if (area < MIN_AREA) {
    return 'closer';
  }
  // The rear camera is not mirrored: a sheet left in the picture lies left of the phone.
  if (Math.max(Math.abs(dx), Math.abs(dy)) > CENTER_TOLERANCE) {
    return Math.abs(dx) >= Math.abs(dy) ? (dx < 0 ? 'left' : 'right') : (dy < 0 ? 'up' : 'down');
  }
  return steady ? 'ready' : 'hold';
}

// Rises as the sheet fills the frame and moves to its centre; 1 when the picture is about to be taken.
function qualityOf(atEdge, area, dx, dy, ready) {
  if (ready) {
    return 1;
  }
  if (atEdge) {
    return 0.15;
  }
  const size = Math.min(area / MIN_AREA, 1);
  const centred = 1 - Math.min(Math.hypot(dx, dy) / 0.5, 1);
  return 0.15 + 0.75 * size * (0.4 + 0.6 * centred);
}

// Still: frames cover the last STEADY_MS, every one of them found the sheet, and no corner moved more
// than STEADY_MOVE against the current frame.
function isSteady(quad, history, nowMs) {
  const from = nowMs - STEADY_MS;
  let covered = false;
  for (let i = history.length - 1; i >= 0; i--) {
    const entry = history[i];
    if (!entry.quad || maxCornerMove(entry.quad, quad) >= STEADY_MOVE) {
      return false;
    }
    if (entry.t <= from) {
      covered = true;
      break;
    }
  }
  return covered;
}

function centerOf(quad) {
  return {
    x: quad.reduce((sum, p) => sum + p.x, 0) / quad.length,
    y: quad.reduce((sum, p) => sum + p.y, 0) / quad.length,
  };
}

function maxCornerMove(a, b) {
  let max = 0;
  for (let i = 0; i < a.length; i++) {
    max = Math.max(max, Math.hypot(a[i].x - b[i].x, a[i].y - b[i].y));
  }
  return max;
}
