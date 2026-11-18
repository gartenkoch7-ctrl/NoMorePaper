#version 300 es
// One full-target quad; the caller picks the target area with gl.viewport.
in vec2 aPos;
out vec2 vUV;

void main() {
  vUV = aPos;
  gl_Position = vec4(aPos * 2.0 - 1.0, 0.0, 1.0);
}
