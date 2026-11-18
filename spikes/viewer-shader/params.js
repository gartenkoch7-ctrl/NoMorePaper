// Tunable values of the spike. The defaults are the result documented in docs/kb/darstellung.md.

export const SLIDERS = [
  { id: 'white', label: 'Papierweiß (OKLab L)', min: 0.5, max: 1, step: 0.01, value: 1, group: 'Invertieren' },
  { id: 'black', label: 'Tintenschwarz (OKLab L)', min: 0, max: 0.45, step: 0.01, value: 0.15, group: 'Invertieren' },
  { id: 'gamma', label: 'Gamma nach dem Invertieren', min: 0.4, max: 1.2, step: 0.01, value: 0.65, group: 'Invertieren' },
  { id: 'lift', label: 'Farbige Flächen aufhellen', min: 0, max: 3, step: 0.05, value: 0.75, group: 'Invertieren' },
  { id: 'chroma', label: 'Sättigung', min: 0, max: 1.5, step: 0.05, value: 1, group: 'Invertieren' },
  { id: 'chromaLo', label: 'Bunt ab Chroma', min: 0, max: 0.2, step: 0.005, value: 0.025, group: 'Foto-Erkennung' },
  { id: 'chromaHi', label: 'Sicher bunt ab Chroma', min: 0, max: 0.2, step: 0.005, value: 0.06, group: 'Foto-Erkennung' },
  { id: 'shareLo', label: 'Anteil bunt: unten', min: 0, max: 1, step: 0.01, value: 0.35, group: 'Foto-Erkennung' },
  { id: 'shareHi', label: 'Anteil bunt: oben', min: 0, max: 1, step: 0.01, value: 0.6, group: 'Foto-Erkennung' },
  { id: 'detailLo', label: 'Anteil Detail: unten', min: 0, max: 1, step: 0.01, value: 0.35, group: 'Foto-Erkennung' },
  { id: 'detailHi', label: 'Anteil Detail: oben', min: 0, max: 1, step: 0.01, value: 0.6, group: 'Foto-Erkennung' },
  { id: 'blendLo', label: 'Weicher Rand: Beginn', min: 0, max: 1, step: 0.01, value: 0.25, group: 'Foto-Rand' },
  { id: 'blendHi', label: 'Weicher Rand: Ende', min: 0, max: 1, step: 0.01, value: 0.75, group: 'Foto-Rand' },
  { id: 'neutralLo', label: 'Neutral bis Chroma', min: 0, max: 0.2, step: 0.005, value: 0.02, group: 'Foto-Rand' },
  { id: 'neutralHi', label: 'Farbig ab Chroma', min: 0, max: 0.2, step: 0.005, value: 0.05, group: 'Foto-Rand' },
];

export const MODES = [
  { id: 2, label: 'Intelligent' },
  { id: 0, label: 'Original' },
  { id: 1, label: 'Klassisch invertiert' },
  { id: 3, label: 'Intelligent + Maske' },
];

export const SAMPLES = [
  { file: '01-brief-logo.pdf', label: 'Brief mit farbigem Logo' },
  { file: '02-rechnung-tabelle.pdf', label: 'Rechnung mit farbiger Tabelle' },
  { file: '03-dokument-foto.pdf', label: 'Dokument mit Foto' },
  { file: '04-scan-grau.pdf', label: 'Grauer Scan' },
  { file: '05-text.pdf', label: 'Reiner Text' },
  { file: '06-scan-foto.pdf', label: 'Scan mit Foto' },
];

export const ZOOM_MIN = 0.25;
export const ZOOM_MAX = 16;

export function defaultParams() {
  const params = { photos: true, frames: true, autoTone: true };
  for (const slider of SLIDERS) params[slider.id] = slider.value;
  return params;
}
