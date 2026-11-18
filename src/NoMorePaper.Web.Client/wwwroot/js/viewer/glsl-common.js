// Shared GLSL: the full-target quad and the colour helpers prepended to every fragment shader.
// Ported unchanged from the approved shader spike (spikes/viewer-shader, docs/kb/darstellung.md).
// OKLab: Björn Ottosson, "A perceptual color space for image processing" (2020).

export const QUAD_VERT = `#version 300 es
// One full-target quad; the caller picks the target area with gl.viewport.
in vec2 aPos;
out vec2 vUV;

void main() {
  vUV = aPos;
  gl_Position = vec4(aPos * 2.0 - 1.0, 0.0, 1.0);
}`;

export const OKLAB = `
vec3 srgbToLinear(vec3 c) {
  return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

vec3 linearToSrgb(vec3 c) {
  c = clamp(c, 0.0, 1.0);
  return mix(c * 12.92, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
}

vec3 linearToOklab(vec3 c) {
  vec3 lms = vec3(
    0.4122214708 * c.r + 0.5363325363 * c.g + 0.0514459929 * c.b,
    0.2119034982 * c.r + 0.6806995451 * c.g + 0.1073969566 * c.b,
    0.0883024619 * c.r + 0.2817188376 * c.g + 0.6299787005 * c.b);
  lms = pow(max(lms, 0.0), vec3(1.0 / 3.0));
  return vec3(
    0.2104542553 * lms.x + 0.7936177850 * lms.y - 0.0040720468 * lms.z,
    1.9779984951 * lms.x - 2.4285922050 * lms.y + 0.4505937099 * lms.z,
    0.0259040371 * lms.x + 0.7827717662 * lms.y - 0.8086757660 * lms.z);
}

vec3 oklabToLinear(vec3 lab) {
  vec3 lms = vec3(
    lab.x + 0.3963377774 * lab.y + 0.2158037573 * lab.z,
    lab.x - 0.1055613458 * lab.y - 0.0638541728 * lab.z,
    lab.x - 0.0894841775 * lab.y - 1.2914855480 * lab.z);
  lms = lms * lms * lms;
  return vec3(
     4.0767416621 * lms.x - 3.3077115913 * lms.y + 0.2309699292 * lms.z,
    -1.2684380046 * lms.x + 2.6097574011 * lms.y - 0.3413193965 * lms.z,
    -0.0041960863 * lms.x - 0.7034186147 * lms.y + 1.7076147010 * lms.z);
}

bool inGamut(vec3 c) {
  return all(greaterThanEqual(c, vec3(-0.0005))) && all(lessThanEqual(c, vec3(1.0005)));
}

// Lightness and hue are fixed; only chroma shrinks until the colour fits into sRGB.
// Clipping the channels instead would shift the hue (blue turns purple).
vec3 fitGamut(vec3 lab) {
  vec3 rgb = oklabToLinear(lab);
  if (inGamut(rgb)) return rgb;
  float lo = 0.0;
  float hi = 1.0;
  for (int i = 0; i < 6; i++) {
    float mid = 0.5 * (lo + hi);
    if (inGamut(oklabToLinear(vec3(lab.x, lab.yz * mid)))) lo = mid; else hi = mid;
  }
  return oklabToLinear(vec3(lab.x, lab.yz * lo));
}
`;

// Every fragment shader starts with this header (version, precision, colour helpers).
export const FRAGMENT_HEADER = `#version 300 es\nprecision highp float;\n${OKLAB}\n`;
