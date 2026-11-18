// Display pass: "intelligent invert" of one rendered tile (spike: shader.frag, values in
// docs/kb/darstellung.md). Inverts lightness in OKLab, keeps hue, and leaves photo regions
// (frames from PDF.js and the page mask) untouched. Mode 0 shows the original colours.
// Changes against the spike: only modes 0 and 2, photo frames always on, and uSource so that
// only the visible part of a tile (rendered with a margin) runs through the shader.

export const MAX_RECTS = 8;

export const DISPLAY_FRAG = `
in vec2 vUV;
out vec4 outColor;

uniform sampler2D uPage;   // rendered tile, sRGB encoded
uniform sampler2D uMask;   // page mask: r = photo weight (grown), g = photo core
uniform vec4 uSource;      // drawn part of the tile texture (y up): xy = origin, zw = size
uniform vec4 uTile;        // the same part inside the page in page UV (y up)
uniform vec2 uMaskScale;   // page UV -> mask UV (the last mask cell may reach past the page)
uniform vec2 uPagePixels;  // page size in output pixels, for a one-pixel edge on photo frames
uniform vec4 uPhotoRects[${MAX_RECTS}];  // images PDF.js drew that are photos, page UV
uniform int uPhotoCount;
uniform vec4 uScanRects[${MAX_RECTS}];   // other images (scans): only inside them the pixel mask applies
uniform int uScanCount;
uniform int uMode;         // 0 original, 2 intelligent
uniform float uWhite;      // paper white as OKLab L; everything brighter counts as paper
uniform float uBlack;      // ink black as OKLab L; everything darker counts as ink
uniform float uGamma;      // < 1 lifts mid tones after the inversion (contrast on black)
uniform float uChroma;     // chroma scale after the inversion
uniform float uLift;       // extra lightness for colourful pixels (yellow stays visible)
uniform vec2 uNeutral;     // chroma range in which a pixel counts as neutral (paper, ink)
uniform vec2 uBlend;       // mask range mapped to 0..1: width of the soft photo edge

vec3 smartInvert(vec3 lab) {
  float lightness = clamp((lab.x - uBlack) / (uWhite - uBlack), 0.0, 1.0);
  float inverted = pow(1.0 - lightness, uGamma);
  inverted += uLift * length(lab.yz) * (1.0 - inverted);
  return linearToSrgb(fitGamut(vec3(inverted, lab.yz * uChroma)));
}

// 1 inside one of the rectangles, 0 outside, antialiased over one output pixel.
float insideAny(vec2 uv, vec4 rects[${MAX_RECTS}], int count) {
  float inside = 0.0;
  for (int i = 0; i < ${MAX_RECTS}; i++) {
    if (i >= count) break;
    vec2 d = min(uv - rects[i].xy, rects[i].xy + rects[i].zw - uv) * uPagePixels;
    inside = max(inside, clamp(min(d.x, d.y) + 0.5, 0.0, 1.0));
  }
  return inside;
}

void main() {
  vec3 src = texture(uPage, uSource.xy + vUV * uSource.zw).rgb;
  if (uMode == 0) { outColor = vec4(src, 1.0); return; }

  vec3 lab = linearToOklab(srgbToLinear(src));
  vec3 inverted = smartInvert(lab);
  vec2 pageUV = uTile.xy + vUV * uTile.zw;
  vec2 mask = texture(uMask, pageUV * uMaskScale).rg;

  // Outside the photo core, neutral pixels are paper or ink and always follow the document.
  // Without this the soft edge leaves a grey halo around every photo.
  float neutral = 1.0 - smoothstep(uNeutral.x, uNeutral.y, length(lab.yz));
  float soft = smoothstep(uBlend.x, uBlend.y, mask.r) * (1.0 - neutral * (1.0 - mask.g));
  // Text and vector graphics are never photos, so the pixel mask only counts inside images.
  soft *= insideAny(pageUV, uScanRects, uScanCount);
  float frame = insideAny(pageUV, uPhotoRects, uPhotoCount);
  float photo = max(soft, frame);

  outColor = vec4(mix(inverted, src, photo), 1.0);
}
`;
