// Synthetic test images for the shader spike. Everything is generated from a seeded
// random source so the samples are reproducible and contain no real documents.

function mulberry32(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// Smooth value noise on a lattice; fbm() stacks octaves for natural-looking texture.
function makeNoise(seed) {
  const rnd = mulberry32(seed);
  const size = 256;
  const lattice = new Float32Array(size * size);
  for (let i = 0; i < lattice.length; i++) lattice[i] = rnd();
  const at = (x, y) => lattice[(y & 255) * size + (x & 255)];
  const smooth = (t) => t * t * (3 - 2 * t);
  const noise = (x, y) => {
    const x0 = Math.floor(x), y0 = Math.floor(y);
    const fx = smooth(x - x0), fy = smooth(y - y0);
    const top = at(x0, y0) * (1 - fx) + at(x0 + 1, y0) * fx;
    const bottom = at(x0, y0 + 1) * (1 - fx) + at(x0 + 1, y0 + 1) * fx;
    return top * (1 - fy) + bottom * fy;
  };
  return (x, y, octaves = 5) => {
    let sum = 0, amp = 0.5, norm = 0;
    for (let o = 0; o < octaves; o++) {
      sum += noise(x, y) * amp;
      norm += amp;
      x *= 2; y *= 2; amp *= 0.5;
    }
    return sum / norm;
  };
}

function gauss(rnd) {
  return Math.sqrt(-2 * Math.log(1 - rnd())) * Math.cos(2 * Math.PI * rnd());
}

const mix = (a, b, t) => a + (b - a) * t;
const mix3 = (a, b, t) => [mix(a[0], b[0], t), mix(a[1], b[1], t), mix(a[2], b[2], t)];
const sstep = (lo, hi, x) => {
  const t = Math.min(1, Math.max(0, (x - lo) / (hi - lo)));
  return t * t * (3 - 2 * t);
};

// Landscape with sky, clouds, mountains, meadow, lake and a red roof: colourful regions,
// neutral regions (clouds, road) and sensor noise, like a real photo.
function makePhoto(width, height, seed = 7) {
  const canvas = document.createElement('canvas');
  canvas.width = width; canvas.height = height;
  const ctx = canvas.getContext('2d');
  const image = ctx.createImageData(width, height);
  const fbm = makeNoise(seed);
  const rnd = mulberry32(seed + 1);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const u = x / width, v = y / height;
      let c = mix3([58, 118, 201], [176, 208, 235], sstep(0, 0.55, v));
      const cloud = sstep(0.52, 0.7, fbm(u * 5, v * 9 + 3, 5)) * (1 - sstep(0.3, 0.55, v));
      c = mix3(c, [246, 246, 244], cloud);
      const sun = Math.exp(-((u - 0.8) ** 2 + (v - 0.16) ** 2) * 260);
      c = mix3(c, [255, 236, 170], Math.min(1, sun * 1.4));
      const ridge = 0.5 - 0.16 * fbm(u * 3.1 + 9, 0.5, 4);
      if (v > ridge) c = mix3([92, 108, 140], [128, 140, 160], fbm(u * 30, v * 30, 3));
      const meadowTop = 0.6 + 0.05 * Math.sin(u * 7) + 0.03 * fbm(u * 6, 2, 3);
      if (v > meadowTop) {
        const grass = fbm(u * 60, v * 90, 5);
        c = mix3([44, 112, 38], [150, 186, 70], grass);
        const flowers = fbm(u * 140 + 5, v * 140, 2);
        if (flowers > 0.72) c = mix3(c, [236, 204, 60], 0.8);
      }
      const lake = ((u - 0.3) / 0.22) ** 2 + ((v - 0.8) / 0.07) ** 2;
      if (lake < 1) c = mix3([40, 96, 170], [120, 170, 214], fbm(u * 40, v * 160, 3));
      const road = Math.abs(u - (0.72 + 0.25 * (v - 0.6))) < 0.03 * (v - 0.5) * 3;
      if (road && v > meadowTop) c = mix3([120, 118, 114], [150, 148, 144], fbm(u * 80, v * 80, 2));
      if (u > 0.52 && u < 0.64 && v > 0.52 && v < 0.63) {
        const roof = v < 0.57 - Math.abs(u - 0.58) * 0.1;
        c = roof ? [176, 44, 34] : [232, 222, 200];
      }
      const vignette = 1 - 0.25 * ((u - 0.5) ** 2 + (v - 0.5) ** 2) * 2;
      const i = (y * width + x) * 4;
      for (let k = 0; k < 3; k++) image.data[i + k] = c[k] * vignette + gauss(rnd) * 2.5;
      image.data[i + 3] = 255;
    }
  }
  ctx.putImageData(image, 0, 0);
  return canvas;
}

// Grey scan of a typed letter: warm-grey paper with uneven lighting, noise, slightly rotated
// soft text, punch holes, a fold shadow and a blue ink stamp. An optional photo is printed
// onto the page before it is "scanned", so it gets the same shading and grain.
function makeScan(width, height, lines, photo = null, seed = 21) {
  const canvas = document.createElement('canvas');
  canvas.width = width; canvas.height = height;
  const ctx = canvas.getContext('2d');
  ctx.fillStyle = 'rgb(206,203,196)';
  ctx.fillRect(0, 0, width, height);

  ctx.save();
  ctx.translate(width / 2, height / 2);
  ctx.rotate(0.011);
  ctx.translate(-width / 2, -height / 2);
  ctx.filter = 'blur(0.7px)';
  ctx.fillStyle = 'rgb(52,50,48)';
  let y = 210;
  for (const line of lines) {
    ctx.font = `${line.bold ? 'bold ' : ''}${line.size || 25}px "Courier New", monospace`;
    ctx.fillText(line.text, 150, y);
    y += (line.size || 25) * 1.62;
  }
  if (photo) ctx.drawImage(photo.canvas, photo.x, photo.y, photo.width, photo.height);
  ctx.strokeStyle = 'rgba(36,58,150,0.8)';
  ctx.fillStyle = 'rgba(36,58,150,0.8)';
  ctx.lineWidth = 4;
  ctx.translate(820, 1290);
  ctx.rotate(-0.2);
  ctx.strokeRect(-170, -62, 340, 124);
  ctx.font = 'bold 34px Arial, sans-serif';
  ctx.fillText('EINGEGANGEN', -135, -8);
  ctx.font = '26px Arial, sans-serif';
  ctx.fillText('14. März 2031', -90, 36);
  ctx.restore();

  ctx.fillStyle = 'rgb(38,38,40)';
  for (const holeY of [height * 0.36, height * 0.64]) {
    ctx.beginPath();
    ctx.arc(46, holeY, 17, 0, Math.PI * 2);
    ctx.fill();
  }

  const image = ctx.getImageData(0, 0, width, height);
  const fbm = makeNoise(seed);
  const rnd = mulberry32(seed + 1);
  for (let py = 0; py < height; py++) {
    for (let px = 0; px < width; px++) {
      const u = px / width, v = py / height;
      let shade = 0.9 + 0.14 * fbm(u * 2.2, v * 2.8, 3);
      shade *= 1 - 0.1 * sstep(0.78, 1, u) - 0.07 * sstep(0.86, 1, v);
      shade *= 1 - 0.08 * Math.exp(-(((v - 0.5) * height) ** 2) / 40);
      const grain = gauss(rnd) * 3.2;
      const i = (py * width + px) * 4;
      for (let k = 0; k < 3; k++) image.data[i + k] = image.data[i + k] * shade + grain + gauss(rnd) * 0.8;
    }
  }
  ctx.putImageData(image, 0, 0);
  return canvas.toDataURL('image/jpeg', 0.62);
}
