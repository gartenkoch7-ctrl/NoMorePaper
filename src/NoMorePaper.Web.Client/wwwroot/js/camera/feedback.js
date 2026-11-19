// Sound and touch for the camera page, like a parking aid: short beeps whose pitch (300–900 Hz) and
// pace (every 600–120 ms) rise with the quality of the framing, silence while no sheet is found, a
// click when a page is taken, and a short vibration where the browser has it (not on iOS).
//
// Browsers (iOS above all) only let a page play sound after a user gesture. The camera page calls
// prepareAudio() when it is shown, before its start button can be pressed, so that press, or any later
// pointer, key or touch event (VoiceOver's double tap too), makes or resumes the one audio context.

const LOW_HZ = 300;
const HIGH_HZ = 900;
const SLOW_MS = 600;
const FAST_MS = 120;
const BEEP_MS = 60;
const VOLUME = 0.2;
const GESTURES = ['pointerdown', 'keydown', 'touchend', 'click'];

let context = null;

function unlock() {
  try {
    context ??= new (globalThis.AudioContext ?? globalThis.webkitAudioContext)();
    if (context.state === 'suspended') {
      context.resume().catch(() => {});
    }
  } catch {
    // No Web Audio: the announcements and the vibration still guide.
    context = null;
  }
  if (context?.state === 'running') {
    for (const type of GESTURES) {
      document.removeEventListener(type, unlock, { capture: true });
    }
  }
}

function listenForGesture() {
  for (const type of GESTURES) {
    document.addEventListener(type, unlock, { capture: true });
  }
}

/** Waits for the next user gesture to allow sound; the page calls it when it is shown, before its start button. */
export function prepareAudio() {
  if (context?.state !== 'running') {
    listenForGesture();
  }
}

export class Feedback {
  constructor(enabled) {
    this.enabled = enabled;
    this.quality = 0;
    this.timer = 0;
    this.disposed = false;
    prepareAudio();
  }

  setEnabled(enabled) {
    this.enabled = enabled;
    if (enabled) {
      this.setQuality(this.quality);
    } else {
      this.#stopBeeping();
    }
  }

  /** 0 = silence; otherwise the beeps follow the quality (0–1). */
  setQuality(quality) {
    this.quality = Math.max(0, Math.min(1, quality));
    if (this.quality === 0 || !this.enabled) {
      this.#stopBeeping();
    } else if (!this.timer) {
      this.#beepLoop();
    }
  }

  /** The shutter: a click, and a short vibration where available. */
  shutter() {
    navigator.vibrate?.(60);
    if (this.enabled) {
      tone(1500, 25, 0.35, 'square');
    }
  }

  /** Stops the beeps; the audio context is only suspended, so the next visit needs no new context. */
  dispose() {
    this.disposed = true;
    this.#stopBeeping();
    if (context?.state === 'running') {
      context.suspend().catch(() => {});
    }
  }

  #beepLoop() {
    if (this.disposed || this.quality === 0 || !this.enabled) {
      this.timer = 0;
      return;
    }
    tone(LOW_HZ + (HIGH_HZ - LOW_HZ) * this.quality, BEEP_MS, VOLUME, 'sine');
    const pause = SLOW_MS - (SLOW_MS - FAST_MS) * this.quality;
    this.timer = setTimeout(() => this.#beepLoop(), pause);
  }

  #stopBeeping() {
    clearTimeout(this.timer);
    this.timer = 0;
  }
}

// A short tone with a soft start and end, so it does not crackle.
function tone(frequency, durationMs, volume, type) {
  if (!context || context.state !== 'running') {
    return;
  }
  const start = context.currentTime;
  const end = start + durationMs / 1000;
  const oscillator = context.createOscillator();
  const gain = context.createGain();
  oscillator.type = type;
  oscillator.frequency.value = frequency;
  gain.gain.setValueAtTime(0, start);
  gain.gain.linearRampToValueAtTime(volume, start + 0.005);
  gain.gain.linearRampToValueAtTime(0, end);
  oscillator.connect(gain).connect(context.destination);
  oscillator.start(start);
  oscillator.stop(end + 0.01);
  oscillator.onended = () => gain.disconnect();
}
