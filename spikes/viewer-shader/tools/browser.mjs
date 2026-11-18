// Locates an installed Chrome or Edge. The spike has no npm dependencies on purpose.
import { existsSync } from 'node:fs';

const candidates = {
  chrome: [
    'C:/Program Files/Google/Chrome/Application/chrome.exe',
    'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
  ],
  edge: [
    'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
    'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
  ],
};

export function findBrowser(name = 'chrome') {
  const path = (candidates[name] ?? []).find((p) => existsSync(p));
  if (!path) throw new Error(`Browser not found: ${name}`);
  return path;
}
