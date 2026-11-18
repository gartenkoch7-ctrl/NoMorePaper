// UI wiring of the spike: samples, modes, zoom, sliders. Rendering lives in pageview.js.
import { ShaderRenderer } from './gl.js';
import { FallbackRenderer } from './fallback.js';
import { PageView, pdfjsVersion } from './pageview.js';
import { MODES, SAMPLES, SLIDERS, ZOOM_MAX, ZOOM_MIN, defaultParams } from './params.js';
import { runBench } from './bench.js';

const $ = (id) => document.getElementById(id);
const query = new URLSearchParams(location.search);
const zoomSteps = [0.25, 0.5, 0.75, 1, 1.5, 2, 3, 4, 6, 8, 12, 16];

const glCanvas = $('gl');
const renderer = (query.get('webgl') !== '0' && await ShaderRenderer.create(glCanvas)) || new FallbackRenderer(glCanvas);
const view = new PageView(renderer, glCanvas, $('scroller'), $('spacer'));
view.params = defaultParams();
// Any slider can be preset in the URL, e.g. ?gamma=0.8&lift=1 (used for the screenshots).
for (const slider of SLIDERS) {
  if (query.has(slider.id)) view.params[slider.id] = Number(query.get(slider.id));
}
// A preset white or black point replaces the estimate per page.
if (query.has('white') || query.has('black')) view.params.autoTone = false;
view.mode = Number(query.get('mode') ?? 2);
view.zoom = Number(query.get('zoom') ?? 1);
if (query.has('plain')) document.body.classList.add('plain');

let current = SAMPLES.find((s) => s.file.startsWith(query.get('doc') ?? '01')) ?? SAMPLES[0];

function status(timings) {
  const lines = [
    `PDF.js ${pdfjsVersion} · ${renderer.rendererName}`,
    `Papier erkannt: L = ${view.paper.lightness.toFixed(2)} · Weißpunkt ${view.tone.white.toFixed(2)} · Schwarzpunkt ${view.tone.black.toFixed(2)}`,
    `Bilder: ${renderer.images?.photos.length ?? 0} Foto, ${renderer.images?.scans.length ?? 0} Scan/Grafik`,
  ];
  if (timings) {
    lines.push(`Zoom ${Math.round(view.zoom * 100)} % · Kachel ${timings.tile[0]} × ${timings.tile[1]} px`);
    lines.push(`PDF.js ${timings.pdfMs.toFixed(1)} ms · Shader ${timings.shaderMs.toFixed(1)} ms`);
  }
  $('status').textContent = lines.join('\n');
}

async function render() {
  const timings = await view.render();
  if (timings) status(timings);
}

function redraw() {
  view.redraw();
}

function setZoom(zoom, anchor) {
  view.setZoom(Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, zoom)), anchor);
  $('zoom').value = String(view.zoom);
  if ($('zoom').value !== String(view.zoom)) $('zoom').selectedIndex = -1;
  return render();
}

function stepZoom(direction) {
  const next = direction > 0
    ? zoomSteps.find((z) => z > view.zoom + 1e-6) ?? ZOOM_MAX
    : [...zoomSteps].reverse().find((z) => z < view.zoom - 1e-6) ?? ZOOM_MIN;
  return setZoom(next);
}

async function openSample(sample) {
  current = sample;
  for (const button of $('samples').children) {
    button.setAttribute('aria-pressed', String(button.dataset.file === sample.file));
  }
  $('sample-name').textContent = sample.label;
  await view.open(`samples/${sample.file}`);
  syncToneSliders();
  await render();
}

function syncToneSliders() {
  if (!view.params.autoTone) return;
  for (const id of ['white', 'black']) {
    $(id).value = String(view.tone[id]);
    $(`${id}-value`).textContent = view.tone[id].toFixed(3);
  }
}

function buildModes() {
  for (const mode of MODES) {
    const label = document.createElement('label');
    label.className = 'inline';
    const input = Object.assign(document.createElement('input'), { type: 'radio', name: 'mode', value: mode.id });
    input.checked = mode.id === view.mode;
    input.addEventListener('change', () => { view.mode = mode.id; redraw(); });
    label.append(input, ` ${mode.label}`);
    $('modes').append(label);
  }
  $('photos').addEventListener('change', (event) => { view.params.photos = event.target.checked; redraw(); });
  $('frames').addEventListener('change', (event) => { view.params.frames = event.target.checked; redraw(); });
  view.params.frames = query.get('frames') !== '0';
  $('frames').checked = view.params.frames;
}

function buildZoom() {
  for (const zoom of zoomSteps) {
    $('zoom').append(new Option(`${Math.round(zoom * 100)} %`, String(zoom)));
  }
  $('zoom').value = String(view.zoom);
  $('zoom').addEventListener('change', (event) => setZoom(Number(event.target.value)));
  $('zoom-in').addEventListener('click', () => stepZoom(1));
  $('zoom-out').addEventListener('click', () => stepZoom(-1));
  document.addEventListener('keydown', (event) => {
    if (event.target.matches('input, select') || event.ctrlKey || event.metaKey) return;
    if (event.key === '+') stepZoom(1);
    else if (event.key === '-') stepZoom(-1);
    else if (event.key === '0') setZoom(1);
  });
  $('scroller').addEventListener('wheel', (event) => {
    if (!event.ctrlKey) return;
    event.preventDefault();
    stepZoom(event.deltaY < 0 ? 1 : -1);
  }, { passive: false });
  $('scroller').addEventListener('scroll', render, { passive: true });
  new ResizeObserver(() => { if (view.page) { view.layout(); render(); } }).observe($('scroller'));
}

function buildSliders() {
  const groups = new Map();
  for (const slider of SLIDERS) {
    if (!groups.has(slider.group)) {
      const fieldset = document.createElement('fieldset');
      fieldset.append(Object.assign(document.createElement('legend'), { textContent: slider.group }));
      groups.set(slider.group, fieldset);
      $('sliders').append(fieldset);
    }
    const label = document.createElement('label');
    label.htmlFor = slider.id;
    const output = Object.assign(document.createElement('output'), { id: `${slider.id}-value`, textContent: view.params[slider.id].toFixed(3) });
    label.append(`${slider.label}: `, output);
    const input = Object.assign(document.createElement('input'), {
      type: 'range', id: slider.id, min: slider.min, max: slider.max, step: slider.step, value: view.params[slider.id],
    });
    input.addEventListener('input', () => {
      view.params[slider.id] = Number(input.value);
      output.textContent = Number(input.value).toFixed(3);
      if (slider.id === 'white' || slider.id === 'black') {
        // Leaving the automatic mode keeps the other estimated value as the starting point.
        if (view.params.autoTone) Object.assign(view.params, view.tone, { [slider.id]: Number(input.value) });
        view.params.autoTone = false;
        $('auto-tone').checked = false;
      }
      if (slider.group === 'Foto-Erkennung') view.rebuildMask();
      redraw();
    });
    groups.get(slider.group).append(label, input);
    if (slider.id === 'black') {
      const auto = document.createElement('label');
      auto.innerHTML = '<input type="checkbox" id="auto-tone" checked> Papier und Tinte je Seite automatisch';
      groups.get(slider.group).append(auto);
      auto.firstChild.addEventListener('change', (event) => {
        view.params.autoTone = event.target.checked;
        syncToneSliders();
        redraw();
      });
    }
  }
  $('reset').addEventListener('click', () => {
    Object.assign(view.params, defaultParams());
    for (const slider of SLIDERS) {
      $(slider.id).value = String(slider.value);
      $(`${slider.id}-value`).textContent = slider.value.toFixed(3);
    }
    $('auto-tone').checked = true;
    $('photos').checked = true;
    $('frames').checked = true;
    syncToneSliders();
    view.rebuildMask();
    redraw();
  });
}

// Every thumbnail goes through the same shader and the same WebGL context as the viewer.
async function buildSamples() {
  for (const sample of SAMPLES) {
    const button = Object.assign(document.createElement('button'), { title: sample.label });
    button.dataset.file = sample.file;
    button.setAttribute('aria-label', sample.label);
    const canvas = document.createElement('canvas');
    button.append(canvas);
    button.addEventListener('click', () => openSample(sample));
    $('samples').append(button);
    await view.open(`samples/${sample.file}`);
    const image = view.thumbnail(120);
    canvas.width = image.width;
    canvas.height = image.height;
    canvas.getContext('2d').putImageData(image, 0, 0);
  }
}

buildModes();
buildZoom();
buildSliders();
if (!query.has('plain')) await buildSamples();
await openSample(current);
if (query.has('x') || query.has('y')) {
  await setZoom(view.zoom, [Number(query.get('x') ?? 0.5), Number(query.get('y') ?? 0.5)]);
}

$('bench').addEventListener('click', async () => {
  $('bench-result').textContent = 'Messung läuft …';
  const result = await runBench(view, SAMPLES);
  $('bench-result').textContent = JSON.stringify(result, null, 1);
  await openSample(current);
});

// Hook for tools/measure.mjs and for manual experiments in the console.
window.spike = {
  view, renderer, samples: SAMPLES,
  openSample: (prefix) => openSample(SAMPLES.find((s) => s.file.startsWith(prefix))),
  setZoom, setMode: (mode) => { view.mode = mode; redraw(); },
  bench: (options) => runBench(view, SAMPLES, options),
  // Visible part of the page in CSS pixels of the window, for screenshots.
  pageRect: () => {
    const stage = $('stage').getBoundingClientRect();
    const { rect, ratio } = view.tileInfo;
    return { x: stage.x + rect.x / ratio, y: stage.y + rect.y / ratio, width: rect.width / ratio, height: rect.height / ratio };
  },
  ready: true,
};
