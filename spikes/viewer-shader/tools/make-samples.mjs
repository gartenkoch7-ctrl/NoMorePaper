// Prints the HTML sources in samples/src/ to PDF with headless Chrome.
// Usage: node spikes/viewer-shader/tools/make-samples.mjs
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readdirSync, rmSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { findBrowser } from './browser.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const sourceDir = join(root, 'samples', 'src');
const profile = mkdtempSync(join(tmpdir(), 'nmp-samples-'));

try {
  for (const file of readdirSync(sourceDir).filter((f) => f.endsWith('.html')).sort()) {
    const target = join(root, 'samples', file.replace(/\.html$/, '.pdf'));
    execFileSync(findBrowser('chrome'), [
      '--headless=new',
      '--disable-gpu',
      '--no-pdf-header-footer',
      `--user-data-dir=${profile}`,
      // The synthetic images are drawn by script; give it time before printing.
      '--virtual-time-budget=20000',
      `--print-to-pdf=${target}`,
      pathToFileURL(join(sourceDir, file)).href,
    ], { stdio: 'ignore' });
    console.log(`${file} -> ${Math.round(statSync(target).size / 1024)} KB`);
  }
} finally {
  rmSync(profile, { recursive: true, force: true, maxRetries: 5 });
}
