# Darstellung: PDF intelligent invertiert

Ergebnis des Shader-Spikes (M1, Paket P1, 2026-11-18) zu [ADR-0011](../adr/0011-pdf-darstellung-intelligent-invertiert.md).
Prototyp: [spikes/viewer-shader/](../../spikes/viewer-shader/README.md), Start mit
`node spikes/viewer-shader/tools/serve.mjs`, dann `http://127.0.0.1:8377/`.
Alle Werte sind an **sechs selbst erzeugten Testdokumenten** bestimmt (Brief mit Logo, Rechnung mit
farbiger Tabelle, Dokument mit Foto, grauer Scan, reiner Text, Scan mit Foto). Die Sichtprüfung
durch den Nutzer steht noch aus (siehe „Offene Punkte“).

## Ergebnis in Kürze
- **Renderer:** PDF.js 6.5.438 (`pdfjs-dist`, Apache-2.0) + WebGL2, drei Durchgänge.
- **Farbraum:** OKLab. Helligkeit wird gedreht, der Farbton bleibt auf etwa 1° erhalten.
- **Fotos:** zweistufig. (1) PDF.js meldet, wo es Bilder zeichnet; ein Bild, das bunt und
  detailreich ist, bleibt als **exakter Rahmen** original. (2) In Scans (ein Bild = ganze Seite)
  findet eine **Pixelmaske** bunte, detailreiche Bereiche. Text und Vektorgrafik sind nie Foto.
- **Scans:** Papierweiß und Tintenschwarz werden je Seite geschätzt, graues Papier wird schwarz.
- **Zeit:** ein neues Bild kostet bei 100 %, 400 % und 1600 % jeweils 11–22 ms, der Shader < 1 ms.

## Ablauf je Seite
1. **Analyse, einmal je Seite, unabhängig vom Zoom:** PDF.js rendert die ganze Seite mit 144 dpi
   (2 px je Punkt, A4 = 1190 × 1684) und `recordImages: true`. Daraus entstehen Papierweiß und
   Tintenschwarz (CPU, `paper.js`), die Fotomaske (GPU, `analyze.frag` → CPU `images.js` →
   `fill.frag`) und das Vorschaubild.
2. **Anzeige, bei jedem Zoom- oder Scrollschritt:** PDF.js rendert nur den **sichtbaren Ausschnitt**
   in ein Canvas → Textur → `shader.frag` → Bildschirm. Die Maske wird in **Seitenkoordinaten**
   gelesen, deshalb kippt beim Zoomen keine Entscheidung um.
3. Ändert sich nur Darstellung oder ein Regler, läuft nur der Shader neu (0,2–1,1 ms).

## Farbraum und Formeln
OKLab (Björn Ottosson, 2020), gerechnet aus linearem sRGB. `L` = Helligkeit 0–1, `(a, b)` = Farbe,
Chroma `C = length(a, b)`.

```glsl
vec3 smartInvert(vec3 lab) {                 // lab = OKLab des Pixels
  float lightness = clamp((lab.x - uBlack) / (uWhite - uBlack), 0.0, 1.0);  // Tinte 0, Papier 1
  float inverted = pow(1.0 - lightness, uGamma);                            // drehen, Mitten heben
  inverted += uLift * length(lab.yz) * (1.0 - inverted);                    // Farbiges aufhellen
  return linearToSrgb(fitGamut(vec3(inverted, lab.yz * uChroma)));          // a, b bleiben
}

vec3 fitGamut(vec3 lab) {      // L und Farbton fest, nur Chroma schrumpft (6 Halbierungen)
  vec3 rgb = oklabToLinear(lab);
  if (inGamut(rgb)) return rgb;
  float lo = 0.0, hi = 1.0;
  for (int i = 0; i < 6; i++) {
    float mid = 0.5 * (lo + hi);
    if (inGamut(oklabToLinear(vec3(lab.x, lab.yz * mid)))) lo = mid; else hi = mid;
  }
  return oklabToLinear(vec3(lab.x, lab.yz * lo));
}
```

Umrechnungen sRGB ↔ OKLab: `spikes/viewer-shader/oklab.glsl`. Ausgabe: `mix(invertiert, original, foto)`.

| Wert | Ab Werk | Warum |
|---|---|---|
| `uWhite` (Papierweiß) | 1,00 digital; Scan: Papier-L − 0,05 | graues Papier samt Rauschen wird schwarz |
| `uBlack` (Tintenschwarz) | 0,15 digital; Scan: 1,5-%-Perzentil, 0,15–0,40 | OKLab ist nahe Schwarz steil: `#111` hat L = 0,18 und würde sonst nur hellgrau |
| `uGamma` | 0,65 | hält das Kontrastverhältnis zum Hintergrund etwa wie im Original (WCAG-Formel); 1,0 = reine Spiegelung, graue Schrift wird dann zu dunkel |
| `uLift` | 0,75 | helle Farben (Gelb) haben kein dunkles Gegenstück und würden schwarz; farbige Tabellenzeilen bleiben unterscheidbar |
| `uChroma` | 1,0 | Sättigung unverändert |

Papier und Tinte je Seite (`paper.js`): Papier = häufigster heller Ton einer auf 96 px Breite
verkleinerten Kopie (Histogramm über L, obere Hälfte, mindestens 15 % der Fläche). Liegt er über
L = 0,985, gilt die Seite als digital. Gemessen an den Scans: Papier L = 0,81–0,83 → Weißpunkt
0,76–0,78, Schwarzpunkt 0,40.

Gerechnete Beispiele (JS-Nachbau der Formeln, vier Logo-Farben gegen echte Shader-Pixel geprüft:
identisch). Kontrast = WCAG-Verhältnis, im Original gegen Weiß, danach gegen Schwarz.

| Farbe | Original | Intelligent | Farbton | Kontrast | CSS-Rückfall | Farbton dort |
|---|---|---|---|---|---|---|
| Papier | `#ffffff` | `#000000` | – | – | `#000000` | – |
| Schrift dunkelgrau | `#111111` | `#f8f8f8` | – | 18,9 → 19,8 | `#eeeeee` | – |
| Schrift grau | `#767676` | `#8e8e8e` | – | 4,5 → 6,4 | `#898989` | – |
| Logo rot | `#c62828` | `#fe7368` | 27° → 27° | 5,6 → 7,9 | `#ff9494` | 20° |
| Logo blau | `#1565c0` | `#6aaaff` | 256° → 256° | 5,7 → 8,8 | `#5fafff` | 250° |
| Logo grün | `#2e7d32` | `#6bba6c` | 144° → 144° | 5,1 → 8,9 | `#5fae63` | 145° |
| Logo gelb | `#fdd835` | `#493c00` | 95° → 95° | 1,4 → 1,9 | `#542f00` | 65° |
| Orange | `#e65100` | `#ea550b` | 40° → 40° | 3,8 → 5,8 | `#ff7a29` | 47° |
| Reinblau | `#0000ff` | `#a3c1fe` | 264° → 264° | 8,6 → 11,6 | `#dadaff` | 285° |
| Zeile hellblau | `#e3f2fd` | `#061017` | 239° → 239° | – | `#03121d` | 240° |
| Kopf dunkelblau | `#1a237e` | `#c9d6fe` | 270° → 271° | 13,2 → 14,5 | `#cad3ff` | 276° |

## Foto-Kriterium
**Stufe 1 – Bildrahmen aus PDF.js** (`images.js`). `RenderTask.imageCoordinates` liefert je Bild
drei Ecken (0–1). Ein Bild ist ein **Foto**, wenn es höchstens 60 % der Seite bedeckt und
mindestens 25 % seiner Zellen den Foto-Wert der Pixelanalyse erreichen. Der Shader lässt den Rahmen
ganz original, die Kante ist auf 1 Pixel genau – bei jedem Zoom, ohne Löcher, ohne Saum. Alle
anderen Bilder (Scans, Grafiken) gelten als Dokument; nur **in ihnen** zählt Stufe 2.

**Stufe 2 – Pixelmaske** für Fotos in gescannten Seiten:
- Zelle = 12 Analyse-Pixel (2,1 mm Papier), A4 → 100 × 141 Zellen. Jede Zelle sieht 8 × 8 Proben
  im Abstand 4,5 px, also 36 px Umfeld (6,4 mm).
- **Bunt:** Anteil der Proben mit Chroma über 0,025–0,06, gemessen relativ zum Papier (getöntes
  Papier ist neutral). Nötig: Anteil 0,35–0,60 (weiche Schwelle).
- **Detailreich:** Anteil der bunten Proben, die sich vom rechten oder unteren Nachbarpixel
  unterscheiden (mehr als 0,6/255). Vektorflächen sind exakt gleichfarbig, Fotos nie. Nötig: 0,35–0,60.
- Foto-Wert der Zelle = Produkt beider weichen Schwellen.
- **Kästen füllen (CPU):** Zusammenhängende Foto-Zellen (Wert ≥ 0,5, mindestens 24 Zellen ≈ 1 cm²),
  die ≥ 55 % ihres umschließenden Rechtecks füllen, bekommen das ganze Rechteck. So bleiben weiße
  Wolken und graue Flächen im Foto original.
- **Maske:** Mittel über 5 × 5 Zellen; `smoothstep(0,15; 0,45)` = gewachsene Maske, `smoothstep(0,55; 0,80)` = Kern.

**Weicher Rand.** Die gewachsene Maske reicht etwa 2 Zellen (4 mm) über das Foto hinaus und fällt
dort über etwa 1 Zelle ab: **2,1 mm Papier, 8 px bei 100 %** (`smoothstep(0,25; 0,75)` auf der
bilinear gelesenen Maske). Außerhalb des Kerns folgen **neutrale** Pixel (Chroma unter 0,02–0,05)
immer dem Dokument: Papier wird schwarz, die Bildunterschrift weiß, es bleibt kein grauer Saum.

Ohne Stufe 1 (nur Pixelmaske) wird die Bildunterschrift direkt unter einem Foto angegriffen, und
ohne die Regel „nur in Bildern“ galten gelb markierter Text und dichte Schrift auf farbigem Grund
teilweise als Foto. Beides ist in den Testdokumenten belegt (`03-foto-nur-pixelmaske.png`).

## Rückfall ohne WebGL
`filter: invert(1) hue-rotate(180deg)` auf dem Canvas (`fallback.js`, im Prototyp mit `?webgl=0`).
Text und Tabellen sehen ähnlich aus. Grenzen: **Fotos werden mit umgekehrt**, kein Papierweiß
(grauer Scan wird dunkelgrau statt schwarz), Farbton wandert um bis zu 30° (Gelb 95° → 65°, Reinblau 264° → 285°),
Rot wird rosa.

## Messwerte
Windows-PC des Nutzers, NVIDIA RTX 3060 Ti (ANGLE, Direct3D 11), Chrome 157 und Edge 157 jeweils
`--headless=new` mit echter GPU, Fenster 1904 × 985, Pixelverhältnis 1. Median aus 9 Läufen je
Dokument, angegeben ist die Spanne über die sechs Dokumente. Rohdaten:
`spikes/viewer-shader/screens/bench-*.json`, Befehl: `node spikes/viewer-shader/tools/measure.mjs bench chrome`.

| Zoom | gerenderte Pixel | Chrome gesamt | Edge gesamt | davon Shader (Wanduhr) | GPU-Zeit Shader |
|---|---|---|---|---|---|
| 100 % | 793 × 969 | 11,0–12,2 ms | 10,6–11,8 ms | 0,2–0,8 ms | 0,03–0,15 ms |
| 400 % | 1904 × 985 | 10,7–14,6 ms | 11,3–16,7 ms | 0,3–0,9 ms | 0,06–0,30 ms |
| 1600 % | 1904 × 985 | 10,7–20,4 ms | 11,2–15,4 ms | 0,3–1,2 ms | 0,06–0,64 ms |

- „gesamt“ = PDF.js-Zeichenaufrufe (2,7–8,4 ms) + Hochladen der Textur samt Shader (3,0–12,4 ms).
  Chrome rastert das Canvas erst beim Hochladen, deshalb ist nur die Summe aussagekräftig.
  Zwischen drei Messläufen schwankte der schlechteste Wert bei 1600 % zwischen 15 und 22 ms.
- GPU-Zeit: `EXT_disjoint_timer_query_webgl2` um den Anzeige-Durchgang.
- **Analyse je Seite (einmal):** 52–97 ms in Chrome, 60–136 ms in Edge (PDF.js bei 144 dpi 17–56 ms,
  Papier/Tinte und Maske 16–80 ms).
- **Zum Vergleich ganze Seite statt Ausschnitt:** 100 % (793 × 1123) 10–17 ms; 400 % (3173 × 4490 =
  14 Megapixel) **85–119 ms** und 57 MB je Seite. 1600 % wären 228 Megapixel – nicht machbar.

## Grenzen
- **Gelb und andere von Natur aus helle Farben** werden dunkel (Oliv). Der Farbton stimmt, wirkt
  aber anders. Unvermeidbar, solange Schrift auf farbigem Grund lesbar bleiben soll.
- **Schwarz-Weiß-Fotos** sind nicht bunt und werden umgekehrt (Negativ) – in beiden Stufen.
- **Bilder mit Farbe und Rauschen, die keine Fotos sind** (Logo als JPEG, Bildschirmfoto, Diagramm
  als Bitmap), gelten als Foto und bleiben samt hellem Hintergrund original.
- **In Scans:** Fotokante nur auf etwa eine Zelle (2 mm) genau, schmaler heller Streifen möglich;
  neutrale Pixel am äußersten Fotorand können umgekehrt werden; farbige Flächen ab etwa 1 cm²
  (Logo, Textmarker) bleiben original; ungleichmäßig beleuchtete Ecken bleiben als grauer Schimmer.
- **Dunkle Vorlagen** (heller Text auf dunklem Grund) werden hell. Es fehlt eine Regel „Seite ist
  schon dunkel → nicht invertieren“.
- Höchstens 8 Fotorahmen je Seite; gedrehte Bilder nutzen ihr umschließendes Rechteck.
- Nur an **künstlichen** Fotos und Scans kalibriert.

## Empfehlungen für den Viewer (P6)
**Kacheln bei hohem Zoom**
- Immer nur den sichtbaren Ausschnitt rendern: Canvas = Fenstergröße × Pixelverhältnis, PDF.js mit
  `transform: [1, 0, 0, 1, -x, -y]` und dem Viewport der Zielskala. Kostet bei jedem Zoom gleich viel.
- Für ruhiges Scrollen den Ausschnitt mit Rand rendern (etwa eine halbe Fensterhöhe), das alte Bild
  verschieben und erst neu rendern, wenn der Rand aufgebraucht ist. Laufende Aufgabe mit
  `renderTask.cancel()` abbrechen; je Canvas nur eine Aufgabe.
- Während einer Zoom-Geste die vorhandene Textur im Shader skalieren (< 1 ms) und nach etwa 100 ms
  Ruhe scharf neu rendern.
- Ein festes Kachelraster mit Zwischenspeicher erst, wenn das Scrollen trotzdem ruckelt.
- Analyse-Textur (8 MB je A4-Seite) nach Maske und Vorschaubild freigeben; die Maske selbst hat 56 KB.

**Textebene für Screenreader**
- Die Textebene von PDF.js (`TextLayer`) als durchsichtiges DOM über dem Canvas, für die **ganze**
  Seite, auch wenn das Canvas nur den Ausschnitt zeigt. Sie braucht den Shader nicht.
  ⚠️ unverifiziert: im Spike nicht gebaut.
- Bei Scans kommt der Text aus der OCR-Schicht des PDF/A. Hat eine Seite keinen Text, das ansagen.
- Auszuliefern sind neben `pdf.min.mjs` und `pdf.worker.min.mjs` auch `cmaps/`, `standard_fonts/`
  und `wasm/` (JBIG2, JPEG 2000 – typisch für Scans). ⚠️ unverifiziert: die Testdokumente brauchen sie nicht.

**Vorschaubilder**
- Derselbe Anzeige-Shader, Eingabe ist das Analyse-Bild (mit Mipmaps), Ziel ein Framebuffer, danach
  `readPixels` in ein 2D-Canvas. **Ein** WebGL-Kontext für Viewer und alle Vorschaubilder (Browser
  erlauben nur wenige Kontexte). Im Prototyp: `ShaderRenderer.thumbnail`.
- Kosten je Vorschaubild = die Analyse (52–136 ms): erst rendern, wenn das Bild sichtbar wird, und
  im Speicher behalten. Nichts Gefiltertes auf dem Server speichern (ADR-0011).

**Sonstiges**
- Schwellen sind an 144 dpi und 12-px-Zellen gebunden; die Analyse-Auflösung nicht ändern, ohne
  neu zu kalibrieren.
- Der Schalter „Originalfarben“ ist im Shader `uMode = 0`, kein zweiter Renderweg.
- Pixeltest aus M1 Aufgabe 8: die Tabelle oben liefert Sollwerte (Weiß → `#000000`, Logo rot
  `#c62828` → `#fe7368`, Pixel im Fotorahmen unverändert).

## Screenshots
Nur Testdokumente, in `spikes/viewer-shader/screens/` (neu erzeugen: `tools/measure.mjs screens`):
`01-brief-*` (Original, intelligent, klassisch, CSS-Rückfall, Logo bei 400 %), `02-rechnung-*`,
`03-foto-*` (mit Rahmen, nur Pixelmaske, Rand bei 400 %), `04-scan-intelligent`, `05-text-*`
(1600 %), `06-scan-foto-*` (intelligent, Maske, Rand bei 400 %).

## Offene Punkte
- **Sichtprüfung durch den Nutzer** an seinem Bildschirm: Gamma 0,65, Aufhellen 0,75 und
  Tintenschwarz 0,15 sind nach Kontrastrechnung und Screenshots gewählt, nicht nach seinem Sehen.
- **Echte Dokumente:** Schwellen der Pixelmaske mit echten Scans und Fotos gegenprüfen.
- **Schwarz-Weiß-Fotos** und **dunkle Vorlagen** (siehe Grenzen) brauchen je eine eigene Regel.
- **Ungleichmäßig beleuchtete Scans und Handyfotos:** Papierweiß je Bereich statt je Seite.
- **Onboard-Grafik, iPhone/Safari, Pixelverhältnis 2:** nicht gemessen. Bei Pixelverhältnis 2
  vervierfachen sich die Pixel; der Shader hat Reserve, PDF.js und das Hochladen eher nicht.
- **Mehrseitige Dokumente** und Scrollen über Seitengrenzen: im Spike nur Seite 1.
- **PDF.js im Worker rendern** (neu in 6.x, `GlobalWorkerOptions.rendererSrc`): nicht geprüft; würde
  den Hauptthread beim Rendern entlasten.
- **Nachtrag zu ADR-0011** und ADR-0017 (PDF.js) schreibt die steuernde Sitzung; dieser Spike
  durfte nur `spikes/viewer-shader/` und diese Datei ändern.
