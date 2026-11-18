# ADR-0011: PDF-Darstellung: intelligent invertieren per WebGL-Shader

- **Status:** Angenommen
- **Datum:** 2026-11-18

## Kontext
PDFs sollen immer mit schwarzem Hintergrund und weißer Schrift erscheinen, Farben sollen
erkennbar bleiben. Vorbild ist das eigene Verfahren aus ZoomIN (dort ADR-0024).

## Entscheidung
- Das PDF wird im Browser gerendert, ein **WebGL-Shader** invertiert die **Helligkeit** und
  erhält den **Farbton** (helle Flächen → schwarz, dunkle Schrift → weiß, ein rotes Logo bleibt rot).
- **Fotos und Bilder** (bunt, detailreich) werden erkannt und bleiben original.
- Die Darstellung wirkt **überall in der App**: im Viewer, in Vorschaubildern, Suchtreffern und der
  Kamera-Vorschau. Je Gerät lässt sie sich abschalten.
- **Download und Export** liefern immer das unveränderte Original bzw. PDF/A.
- Renderer, Schwellen und Foto-Kriterium legt ein Spike in M1 fest. Fehlt WebGL, gibt es einen
  Rückfall auf einfaches Invertieren mit erhaltenem Farbton.

## Verworfen
Invertieren und Farbton um 180° zurückdrehen (Farben weniger treu), Text neu setzen (Layout
und Tabellen gehen verloren), Fotos invertieren oder abdunkeln.

## Folgen
Vorschaubilder werden im Client gefiltert, nicht auf dem Server gespeichert. Der Shader-Pfad
braucht eigene Tests mit Beispiel-PDFs.
