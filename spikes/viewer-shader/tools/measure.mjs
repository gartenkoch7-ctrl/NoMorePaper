// Runs the spike in an installed Chrome or Edge (real GPU) and either measures or takes the
// screenshots for the docs. The server must be running: node tools/serve.mjs
//   node tools/measure.mjs bench [chrome|edge] [headful]
//   node tools/measure.mjs screens
//   node tools/measure.mjs shot <file.png> "<query>" [x y width height]
//   node tools/measure.mjs js "<query>" "<expression>"
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { launch } from './cdp.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const base = 'http://127.0.0.1:8377/index.html';
const [command = 'bench', ...args] = process.argv.slice(2);

async function withSession(options, work) {
  const session = await launch(options);
  try {
    return await work(session);
  } finally {
    await session.close();
  }
}

async function bench() {
  const browser = args.find((a) => a === 'edge') ?? 'chrome';
  await withSession({ browser, headless: !args.includes('headful') }, async (session) => {
    await session.open(`${base}?plain`);
    const result = await session.evaluate('window.spike.bench()');
    result.browser = await session.evaluate('navigator.userAgent');
    writeFileSync(join(root, 'screens', `bench-${browser}.json`), JSON.stringify(result, null, 1));
    console.log(JSON.stringify(result, null, 1));
  });
}

// [name, query, clip]: clip 'page' = the whole page at 60 % zoom to keep the PNGs small;
// a detail clip is a fixed 720 x 450 crop from the middle of the window.
const detail = { x: 592, y: 268, width: 720, height: 450 };
const shots = [
  ['01-brief-original', 'doc=01&mode=0', 'page'],
  ['01-brief-intelligent', 'doc=01&mode=2', 'page'],
  ['01-brief-klassisch', 'doc=01&mode=1', 'page'],
  ['01-brief-css-fallback', 'doc=01&mode=2&webgl=0', 'page'],
  ['02-rechnung-original', 'doc=02&mode=0', 'page'],
  ['02-rechnung-intelligent', 'doc=02&mode=2', 'page'],
  ['02-rechnung-css-fallback', 'doc=02&mode=2&webgl=0', 'page'],
  ['03-foto-intelligent', 'doc=03&mode=2', 'page'],
  ['03-foto-nur-pixelmaske', 'doc=03&mode=2&frames=0', 'page'],
  ['04-scan-intelligent', 'doc=04&mode=2', 'page'],
  ['05-text-intelligent', 'doc=05&mode=2', 'page'],
  ['06-scan-foto-intelligent', 'doc=06&mode=2', 'page'],
  ['06-scan-foto-maske', 'doc=06&mode=3', 'page'],
  ['01-brief-logo-400', 'doc=01&mode=2&zoom=4&x=0.3&y=0.1', detail],
  ['02-rechnung-400', 'doc=02&mode=2&zoom=4&x=0.5&y=0.47', detail],
  ['03-foto-rand-400', 'doc=03&mode=2&zoom=4&x=0.2&y=0.52', detail],
  ['05-text-1600', 'doc=05&mode=2&zoom=16&x=0.145&y=0.704', detail],
  ['06-scan-foto-rand-400', 'doc=06&mode=2&zoom=4&x=0.2&y=0.555', detail],
];

async function screens() {
  mkdirSync(join(root, 'screens'), { recursive: true });
  await withSession({}, async (session) => {
    for (const [name, query, clip] of shots) {
      const whole = clip === 'page';
      await session.open(`${base}?plain&${query}${whole ? '&zoom=0.6' : ''}`);
      const area = whole ? await session.evaluate('window.spike.pageRect()') : clip;
      await session.screenshot(join(root, 'screens', `${name}.png`), area);
      console.log(name);
    }
  });
}

async function shot() {
  const [file, query, x, y, width, height] = args;
  await withSession({}, async (session) => {
    await session.open(`${base}?plain&${query}`);
    const clip = x === undefined ? undefined : { x: +x, y: +y, width: +width, height: +height };
    await session.screenshot(resolve(file), clip);
  });
}

async function evaluate() {
  const [query, expression] = args;
  await withSession({}, async (session) => {
    await session.open(`${base}?plain&${query}`);
    console.log(JSON.stringify(await session.evaluate(expression)));
  });
}

await { bench, screens, shot, js: evaluate }[command]();
