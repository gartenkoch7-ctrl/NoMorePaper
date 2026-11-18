# Shader-Spike: PDF „intelligent invertiert“ (M1, Paket P1)

Prototyp zu ADR-0011: PDF.js rendert eine Seite in ein Canvas, ein WebGL2-Shader dreht die
Helligkeit, hält den Farbton und lässt Fotos original. **Nicht Teil der Solution**, keine
npm-Abhängigkeiten. Ergebnis und Werte: [docs/kb/darstellung.md](../../docs/kb/darstellung.md).

## Starten

```powershell
node spikes/viewer-shader/tools/serve.mjs      # http://127.0.0.1:8377/
```

Dann `http://127.0.0.1:8377/` in Chrome oder Edge öffnen.

- Oben die sechs Testdokumente als Vorschaubilder (laufen durch denselben Shader).
- **Darstellung:** Intelligent / Original / Klassisch invertiert / Intelligent + Maske
  (rot = Maske, gelb = Fotokern, blau = Bildrahmen aus PDF.js).
- **Zoom:** 25–1600 %, Tasten `+` `-` `0`, `Strg` + Mausrad. Gerendert wird immer nur der sichtbare
  Ausschnitt.
- **Regler** für alle Schwellen; „Werte zurücksetzen“ stellt die dokumentierten Werte wieder her.
- „Messung starten“ misst alle Dokumente bei 100 %, 400 % und 1600 %.

Adresszeile: `?doc=03&mode=2&zoom=4&x=0.2&y=0.5` (Dokument, Darstellung, Zoom, Seitenanteil in der
Mitte), `&plain` (ohne Seitenleiste), `&webgl=0` (Rückfall mit CSS-Filter), `&frames=0` (ohne
Bildrahmen aus PDF.js), dazu jeder Regler mit seinem Namen, z. B. `&gamma=0.8&lift=0`.

## Dateien

| Datei | Inhalt |
|---|---|
| `index.html`, `viewer.js` | Oberfläche, Regler, Zoom |
| `pageview.js` | Seite laden, Analyse einmal je Seite, sichtbaren Ausschnitt rendern |
| `gl.js`, `glutil.js` | WebGL2: drei Durchgänge, Vorschaubild, GPU-Zeitmessung |
| `shader.frag` | Anzeige: Invertieren in OKLab, Fotos aussparen |
| `analyze.frag`, `fill.frag` | Foto-Erkennung je Zelle, Maske |
| `oklab.glsl` | sRGB ↔ OKLab, Farbraum-Anpassung |
| `paper.js` | Papierweiß und Tintenschwarz je Seite |
| `images.js` | Bildrahmen aus PDF.js einordnen, Foto-Kästen füllen |
| `fallback.js` | Rückfall ohne WebGL (CSS-Filter) |
| `bench.js`, `tools/measure.mjs`, `tools/cdp.mjs` | Messung und Screenshots im echten Chrome/Edge |
| `samples/` | 6 Test-PDFs, erzeugt aus `samples/src/*.html` (alles frei erfunden) |
| `lib/pdfjs/` | PDF.js 6.5.438 (`pdfjs-dist`, Apache-2.0), unverändert aus dem npm-Paket |
| `screens/` | Screenshots der Testdokumente, Messwerte als JSON |

## Neu erzeugen

```powershell
node spikes/viewer-shader/tools/make-samples.mjs            # Test-PDFs (Chrome, headless)
node spikes/viewer-shader/tools/measure.mjs screens         # Screenshots (Server muss laufen)
node spikes/viewer-shader/tools/measure.mjs bench chrome    # Messung, auch: edge, headful
```

## Messwerte

Stand 2026-11-18, RTX 3060 Ti, Chrome 157, Fenster 1904 × 985, Pixelverhältnis 1: ein neues Bild
kostet bei 100 %, 400 % und 1600 % jeweils 11–22 ms, davon der Shader unter 1 ms. Details und
Rohdaten: KB und `screens/bench-*.json`.
