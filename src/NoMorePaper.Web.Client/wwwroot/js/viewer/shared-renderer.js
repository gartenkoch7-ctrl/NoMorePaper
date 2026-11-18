// The one WebGL context of the page (see shader-renderer.js) and what happens when the browser
// takes it away (GPU reset, driver update, too many contexts in other tabs). The lost event is
// cancelled so the browser may give the context back; then everything is rebuilt. If that fails,
// or the context is not back in time, viewers switch to the CSS fallback.
import { ShaderRenderer } from './shader-renderer.js';

// How long a lost context may take to come back before the fallback takes over.
const RESTORE_TIMEOUT_MS = 5000;

let shared;
const listeners = new Set();

// The shared renderer, or { renderer: null, reason } when WebGL2 is missing or broken.
export function sharedRenderer() {
  if (!shared) {
    shared = ShaderRenderer.tryCreate();
    if (shared.renderer) watch(shared.renderer);
  }
  return shared;
}

// listener(event, renderer, reason) for each change of a shared renderer:
// 'lost'     the context is gone; it draws nothing and reads back black.
// 'restored' it is rebuilt; every analysis made before is invalid and must be redone.
// 'failed'   it is not coming back (reason 'context-lost' or 'restore-failed'); use the fallback.
// Returns a function that removes the listener.
export function onRendererChange(listener) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function notify(event, renderer, reason = null) {
  for (const listener of [...listeners]) listener(event, renderer, reason);
}

function watch(renderer) {
  const canvas = renderer.canvas;
  let timer = 0;

  const giveUp = (reason) => {
    clearTimeout(timer);
    canvas.removeEventListener('webglcontextlost', onLost);
    canvas.removeEventListener('webglcontextrestored', onRestored);
    // The next viewer or thumbnail tries a new context; this one is done.
    if (shared?.renderer === renderer) shared = undefined;
    notify('failed', renderer, reason);
  };

  const onLost = (event) => {
    // Without this the browser never restores the context.
    event.preventDefault();
    renderer.markLost();
    clearTimeout(timer);
    timer = setTimeout(() => giveUp('context-lost'), RESTORE_TIMEOUT_MS);
    notify('lost', renderer);
  };

  const onRestored = () => {
    clearTimeout(timer);
    try {
      renderer.build();
    } catch {
      giveUp('restore-failed');
      return;
    }
    notify('restored', renderer);
  };

  canvas.addEventListener('webglcontextlost', onLost);
  canvas.addEventListener('webglcontextrestored', onRestored);
}
