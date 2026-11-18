// Keyboard, wheel and pinch input of the viewer. Ctrl + plus/minus/0 zoom, Page Up/Down change
// the page, Ctrl + mouse wheel and the two-finger gesture (touchpads send it as Ctrl + wheel,
// Safari as gesture events) zoom around the pointer. Arrow keys scroll natively.
import { ZOOM_MAX, ZOOM_MIN, ZOOM_STEPS } from './defaults.js';

// Wheel deltas from this size on are mouse wheel notches (one step each); smaller ones come
// from touchpads and zoom smoothly.
const NOTCH = 50;
const SMOOTH_PER_PIXEL = 0.01;

export function zoomStep(zoom, direction) {
  return direction > 0
    ? ZOOM_STEPS.find((z) => z > zoom + 1e-6) ?? ZOOM_MAX
    : [...ZOOM_STEPS].reverse().find((z) => z < zoom - 1e-6) ?? ZOOM_MIN;
}

function isTextEntry(target) {
  return target instanceof HTMLElement
    && (target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName));
}

// actions: zoomIn(), zoomOut(), reset(), zoomTo(zoom, screen), zoom (getter), nextPage(), previousPage()
// Returns a function that removes every listener.
export function attachInput(scroller, actions) {
  const onKey = (event) => {
    if (event.altKey || event.defaultPrevented) return;
    const command = event.ctrlKey || event.metaKey
      ? { '+': actions.zoomIn, '=': actions.zoomIn, '-': actions.zoomOut, '0': actions.reset }[event.key]
      : !event.shiftKey && !isTextEntry(event.target)
        ? { PageDown: actions.nextPage, PageUp: actions.previousPage }[event.key]
        : undefined;
    if (!command) return;
    // Ctrl + plus would otherwise zoom the whole browser page, which only stretches pixels.
    event.preventDefault();
    command();
  };

  const pointIn = (clientX, clientY) => {
    const box = scroller.getBoundingClientRect();
    return [clientX - box.left, clientY - box.top];
  };

  const onWheel = (event) => {
    if (!event.ctrlKey) return;
    event.preventDefault();
    const screen = pointIn(event.clientX, event.clientY);
    const pixels = event.deltaMode === 0 ? event.deltaY : event.deltaY * 40;
    const zoom = Math.abs(pixels) >= NOTCH
      ? zoomStep(actions.zoom, -Math.sign(pixels))
      : actions.zoom * Math.exp(-pixels * SMOOTH_PER_PIXEL);
    actions.zoomTo(zoom, screen);
  };

  // Safari (macOS trackpad, iOS): scale is relative to the start of the gesture.
  let gestureStart = 1;
  const onGestureStart = (event) => {
    event.preventDefault();
    gestureStart = actions.zoom;
  };
  const onGestureChange = (event) => {
    event.preventDefault();
    actions.zoomTo(gestureStart * event.scale, pointIn(event.clientX, event.clientY));
  };

  document.addEventListener('keydown', onKey);
  scroller.addEventListener('wheel', onWheel, { passive: false });
  scroller.addEventListener('gesturestart', onGestureStart);
  scroller.addEventListener('gesturechange', onGestureChange);
  return () => {
    document.removeEventListener('keydown', onKey);
    scroller.removeEventListener('wheel', onWheel);
    scroller.removeEventListener('gesturestart', onGestureStart);
    scroller.removeEventListener('gesturechange', onGestureChange);
  };
}
