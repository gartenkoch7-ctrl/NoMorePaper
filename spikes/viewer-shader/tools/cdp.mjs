// Tiny Chrome DevTools Protocol client (Node 22 has WebSocket built in), enough to open the
// spike in a real Chrome or Edge with its GPU, run script and save screenshots.
import { spawn } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { findBrowser } from './browser.mjs';

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

export async function launch({ browser = 'chrome', headless = true, width = 1920, height = 1080, port = 9377 } = {}) {
  const profile = mkdtempSync(join(tmpdir(), 'nmp-spike-'));
  const child = spawn(findBrowser(browser), [
    `--remote-debugging-port=${port}`,
    `--user-data-dir=${profile}`,
    `--window-size=${width},${height}`,
    '--force-device-scale-factor=1',
    '--no-first-run',
    '--no-default-browser-check',
    '--hide-scrollbars',
    ...(headless ? ['--headless=new'] : []),
    'about:blank',
  ], { stdio: 'ignore' });

  let target = null;
  for (let attempt = 0; attempt < 100 && !target; attempt++) {
    await sleep(100);
    try {
      const list = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
      target = list.find((entry) => entry.type === 'page');
    } catch { /* browser still starting */ }
  }
  if (!target) throw new Error('Browser did not open a debugging port');

  const socket = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    socket.addEventListener('open', resolve, { once: true });
    socket.addEventListener('error', reject, { once: true });
  });
  const waiting = new Map();
  let nextId = 1;
  socket.addEventListener('message', (event) => {
    const message = JSON.parse(event.data);
    const entry = waiting.get(message.id);
    if (!entry) return;
    waiting.delete(message.id);
    if (message.error) entry.reject(new Error(message.error.message)); else entry.resolve(message.result);
  });
  const send = (method, params = {}) => new Promise((resolve, reject) => {
    const id = nextId++;
    waiting.set(id, { resolve, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });

  // Evaluates an expression in the page and returns its (awaited) value.
  const evaluate = async (expression) => {
    const result = await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
    if (result.exceptionDetails) {
      throw new Error(result.exceptionDetails.exception?.description ?? result.exceptionDetails.text);
    }
    return result.result.value;
  };

  return {
    evaluate,
    async open(url) {
      await send('Page.navigate', { url });
      for (let attempt = 0; attempt < 300; attempt++) {
        await sleep(100);
        if (await evaluate('location.href !== "about:blank" && window.spike?.ready === true').catch(() => false)) return;
      }
      throw new Error(`Page did not get ready: ${url}`);
    },
    // clip: { x, y, width, height, scale } in CSS pixels of the page.
    async screenshot(file, clip) {
      // Two frames so the compositor has presented the last WebGL draw.
      await evaluate('new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)))');
      const shot = await send('Page.captureScreenshot', { format: 'png', clip: clip && { scale: 1, ...clip } });
      writeFileSync(file, Buffer.from(shot.data, 'base64'));
    },
    async close() {
      await send('Browser.close').catch(() => {});
      await sleep(500);
      child.kill();
      rmSync(profile, { recursive: true, force: true, maxRetries: 10, retryDelay: 200 });
    },
  };
}
