# ADR-0019: Darstellung: Werte aus dem Shader-Spike

- **Status:** Angenommen
- **Datum:** 2026-11-18
- **Ergänzt:** ADR-0011 (bleibt gültig)

## Kontext
ADR-0011 hat die Festlegung von Renderer, Schwellen und Foto-Kriterium einem Spike überlassen.
Der Spike in M1 hat die Werte ermittelt, die Details stehen in [darstellung.md](../kb/darstellung.md).

## Entscheidung
- Die **Werte aus dem Spike** sind die Standardwerte des Shaders.
- Der Nutzer hat den Prototyp am 2026-11-18 visuell freigegeben („passt so“).
- **Fotoerkennung:** PDF.js liefert die Koordinaten der Bilder. Innerhalb gescannter Bilder
  wird zusätzlich eine Pixelmaske verwendet, um Fotos zu finden.

## Verworfen
Nicht gesondert bewertet. Die Alternativen des Spikes sind nicht Teil dieses ADR.

## Folgen
Bekannte Grenzen der Darstellung:
- Gelb wird zu dunklem Oliv.
- Schwarzweiß-Fotos werden invertiert.
- Dunkle Vorlagen werden hell.
- Pro Seite werden höchstens **8 Fotorahmen** erkannt.
