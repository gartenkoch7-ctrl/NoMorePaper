// Timing of a re-render at several zoom levels. Results go into docs/kb/darstellung.md.

const median = (values) => {
  const sorted = values.filter((v) => v != null).sort((a, b) => a - b);
  return sorted.length ? sorted[Math.floor(sorted.length / 2)] : null;
};
const round = (value) => (value == null ? null : Math.round(value * 100) / 100);

export async function runBench(view, samples, options = {}) {
  const zooms = options.zooms ?? [1, 4, 16];
  const runs = options.runs ?? 9;
  const result = {
    renderer: view.renderer.rendererName,
    devicePixelRatio: window.devicePixelRatio,
    viewport: [view.scroller.clientWidth, view.scroller.clientHeight],
    runs,
    samples: [],
  };
  const previousMode = view.mode;
  view.mode = 2;
  for (const sample of samples) {
    await view.open(`samples/${sample.file}`);
    const entry = {
      file: sample.file,
      analysisRenderMs: round(view.analysisMs.render),
      maskMs: round(view.analysisMs.mask),
      zooms: [],
    };
    for (const zoom of zooms) {
      // Centre of the upper third: text or picture content in every sample.
      view.setZoom(zoom, [0.5, 0.3]);
      const pdf = [], shader = [], redraw = [], gpu = [];
      let tile = null;
      for (let run = 0; run < runs + 1; run++) {
        // A scroll event from setZoom may have started a render of its own; let it finish.
        while (view.busy) await new Promise((resolve) => setTimeout(resolve, 5));
        const timings = await view.render();
        const redrawMs = view.redraw();
        const gpuMs = await view.redrawGpuTime();
        if (run === 0) continue; // warm-up: fonts, operator list, shader compile
        tile = timings.tile;
        pdf.push(timings.pdfMs);
        shader.push(timings.shaderMs);
        redraw.push(redrawMs);
        gpu.push(gpuMs);
      }
      entry.zooms.push({
        zoom, tile,
        pdfMs: round(median(pdf)),
        uploadAndShaderMs: round(median(shader)),
        shaderOnlyMs: round(median(redraw)),
        gpuTimerMs: round(median(gpu)),
      });
    }
    if (options.fullPage !== false) {
      entry.fullPage = [];
      for (const zoom of [1, 4]) {
        const runsFull = [];
        for (let run = 0; run < 4; run++) runsFull.push(await view.measureFullPage(zoom));
        entry.fullPage.push({
          zoom, size: runsFull[0].size,
          pdfMs: round(median(runsFull.slice(1).map((r) => r.pdfMs))),
          uploadMs: round(median(runsFull.slice(1).map((r) => r.uploadMs))),
        });
      }
    }
    result.samples.push(entry);
  }
  view.mode = previousMode;
  return result;
}
