// Pure geometry of the page view, all in device pixels. "view" is what the scroller shows, a
// "region" is a rendered tile: its origin and size inside the page at the zoom it was made for.

export const clamp = (value, min, max) => Math.min(max, Math.max(min, value));

// Part of the page to render: the visible part plus margin on every side, clamped to the page.
export function tileRegion(g, zoom, margin) {
  const x0 = clamp(g.left - margin, 0, g.page[0]), x1 = clamp(g.left + g.view[0] + margin, 0, g.page[0]);
  const y0 = clamp(g.top - margin, 0, g.page[1]), y1 = clamp(g.top + g.view[1] + margin, 0, g.page[1]);
  if (x1 - x0 < 1 || y1 - y0 < 1) return null;
  return { origin: [x0, y0], size: [x1 - x0, y1 - y0], zoom, ratio: g.ratio, page: g.page };
}

// True when region (made at the current zoom) contains everything visible of the page.
export function covers(region, g, zoom) {
  if (!region || region.zoom !== zoom || region.ratio !== g.ratio) return false;
  const x0 = clamp(g.left, 0, g.page[0]), x1 = clamp(g.left + g.view[0], 0, g.page[0]);
  const y0 = clamp(g.top, 0, g.page[1]), y1 = clamp(g.top + g.view[1], 0, g.page[1]);
  return x0 >= region.origin[0] && y0 >= region.origin[1]
    && x1 <= region.origin[0] + region.size[0] && y1 <= region.origin[1] + region.size[1];
}

// Where the tile lands on the screen now, scaled while a zoom gesture runs, cropped to the view:
// rect on the canvas (top-left origin), source = drawn part of the tile texture and uv = the same
// part in the page (both y-up as in GL textures), or null when nothing of it is visible.
export function visiblePart(tile, g, zoom) {
  if (!tile || tile.ratio !== g.ratio) return null;
  const s = zoom / tile.zoom;
  const x = tile.origin[0] * s - g.left, y = tile.origin[1] * s - g.top;
  const w = tile.size[0] * s, h = tile.size[1] * s;
  const cx0 = Math.max(0, Math.round(x)), cy0 = Math.max(0, Math.round(y));
  const cx1 = Math.min(g.view[0], Math.round(x + w)), cy1 = Math.min(g.view[1], Math.round(y + h));
  if (cx1 - cx0 < 1 || cy1 - cy0 < 1) return null;
  const fx0 = (cx0 - x) / w, fx1 = (cx1 - x) / w, fy0 = (cy0 - y) / h, fy1 = (cy1 - y) / h;
  return {
    rect: { x: cx0, y: cy0, width: cx1 - cx0, height: cy1 - cy0 },
    source: [fx0, 1 - fy1, fx1 - fx0, fy1 - fy0],
    uv: [
      (tile.origin[0] + fx0 * tile.size[0]) / tile.page[0],
      1 - (tile.origin[1] + fy1 * tile.size[1]) / tile.page[1],
      (fx1 - fx0) * tile.size[0] / tile.page[0],
      (fy1 - fy0) * tile.size[1] / tile.page[1],
    ],
    pagePixels: [tile.page[0] * s, tile.page[1] * s],
  };
}
