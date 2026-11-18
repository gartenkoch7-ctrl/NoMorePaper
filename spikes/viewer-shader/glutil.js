// Small WebGL2 helpers: shader loading, program setup and GPU timing.

export async function loadText(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`Cannot load ${url}`);
  return response.text();
}

function compile(gl, type, source) {
  const shader = gl.createShader(type);
  gl.shaderSource(shader, source);
  gl.compileShader(shader);
  if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
    throw new Error(gl.getShaderInfoLog(shader));
  }
  return shader;
}

export function createProgram(gl, vertexSource, fragmentSource) {
  const program = gl.createProgram();
  gl.attachShader(program, compile(gl, gl.VERTEX_SHADER, vertexSource));
  gl.attachShader(program, compile(gl, gl.FRAGMENT_SHADER, fragmentSource));
  gl.bindAttribLocation(program, 0, 'aPos');
  gl.linkProgram(program);
  if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
    throw new Error(gl.getProgramInfoLog(program));
  }
  const locations = new Map();
  const location = (name) => {
    if (!locations.has(name)) locations.set(name, gl.getUniformLocation(program, name));
    return locations.get(name);
  };
  return { program, location };
}

// GPU time of work() in milliseconds via timer query; null if the browser hides it.
export async function gpuTime(gl, ext, work) {
  if (!ext) { work(); return null; }
  const query = gl.createQuery();
  gl.beginQuery(ext.TIME_ELAPSED_EXT, query);
  work();
  gl.endQuery(ext.TIME_ELAPSED_EXT);
  gl.flush();
  // The result arrives a few tasks later; never block on it.
  for (let attempt = 0; attempt < 120; attempt++) {
    await new Promise((resolve) => setTimeout(resolve, 4));
    if (gl.getQueryParameter(query, gl.QUERY_RESULT_AVAILABLE)) {
      const disjoint = gl.getParameter(ext.GPU_DISJOINT_EXT);
      const nanoseconds = gl.getQueryParameter(query, gl.QUERY_RESULT);
      gl.deleteQuery(query);
      return disjoint ? null : nanoseconds / 1e6;
    }
  }
  gl.deleteQuery(query);
  return null;
}
