// Photo detection passes, once per page (spike: analyze.frag and fill.frag).

// One output texel per mask cell: is the neighbourhood colourful AND detailed, i.e. a photo?
export const ANALYZE_FRAG = `
out vec4 outColor;

uniform sampler2D uPage;     // whole page at analysis resolution, sRGB encoded
uniform vec3 uPaper;         // paper colour in linear RGB; tinted paper counts as neutral
uniform float uCell;         // cell size in analysis pixels
uniform float uSpacing;      // distance between two samples in analysis pixels
uniform vec2 uChromaRange;   // OKLab chroma at which a sample counts as colourful
uniform vec2 uShareRange;    // share of colourful samples needed for a photo
uniform vec2 uDetailRange;   // share of non-flat colourful samples needed for a photo

const int GRID = 8;
// Vector graphics render flat areas with identical pixels; photos never do (noise, texture).
const float SAME = 0.6 / 255.0;

vec3 fetch(ivec2 p, ivec2 size) {
  return texelFetch(uPage, clamp(p, ivec2(0), size - 1), 0).rgb;
}

bool differs(vec3 a, vec3 b) {
  vec3 d = abs(a - b);
  return max(d.r, max(d.g, d.b)) > SAME;
}

void main() {
  ivec2 size = textureSize(uPage, 0);
  vec2 center = gl_FragCoord.xy * uCell;
  float colourful = 0.0;
  float detailed = 0.0;
  for (int y = 0; y < GRID; y++) {
    for (int x = 0; x < GRID; x++) {
      vec2 offset = (vec2(x, y) - 0.5 * float(GRID - 1)) * uSpacing;
      ivec2 p = ivec2(floor(center + offset));
      vec3 texel = fetch(p, size);
      vec3 lab = linearToOklab(min(srgbToLinear(texel) / uPaper, 1.0));
      float weight = smoothstep(uChromaRange.x, uChromaRange.y, length(lab.yz));
      bool rough = differs(texel, fetch(p + ivec2(1, 0), size)) || differs(texel, fetch(p + ivec2(0, 1), size));
      colourful += weight;
      detailed += weight * float(rough);
    }
  }
  float share = colourful / float(GRID * GRID);
  float detail = detailed / max(colourful, 0.5);
  float photo = smoothstep(uShareRange.x, uShareRange.y, share) * smoothstep(uDetailRange.x, uDetailRange.y, detail);
  outColor = vec4(photo, share, detail, 1.0);
}
`;

// Mask pass: a 5 x 5 mean closes small holes (a neutral cloud inside a photo) and drops single
// cells (a coloured icon). Two thresholds on that mean give a grown mask and a tighter core.
export const FILL_FRAG = `
out vec4 outColor;

uniform sampler2D uRaw;   // output of the analysis pass after the CPU step, r = photo score
uniform vec2 uGrow;       // mean range for the grown mask (reaches past the photo edge)
uniform vec2 uCore;       // mean range for the core (starts inside the photo)

void main() {
  ivec2 size = textureSize(uRaw, 0);
  ivec2 cell = ivec2(gl_FragCoord.xy);
  float sum = 0.0;
  for (int y = -2; y <= 2; y++) {
    for (int x = -2; x <= 2; x++) {
      sum += texelFetch(uRaw, clamp(cell + ivec2(x, y), ivec2(0), size - 1), 0).r;
    }
  }
  float mean = sum / 25.0;
  outColor = vec4(smoothstep(uGrow.x, uGrow.y, mean), smoothstep(uCore.x, uCore.y, mean), mean, 1.0);
}
`;
