// Mask pass: turns the raw photo score per cell into the mask the display pass reads.
// A 5 x 5 mean closes small holes (a neutral cloud inside a photo) and drops single cells
// (a coloured icon). Two thresholds on that mean give a grown mask and a tighter core.
out vec4 outColor;

uniform sampler2D uRaw;   // output of analyze.frag, r = photo score
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
