# Rendering stack

DynamicWin uses SkiaSharp 4.150.1 through the WPF `SKElement` adapter.

`SkiaRenderSurface` owns each native Skia surface used by the adapter and is the rendering verification seam. The smoke test hosts a visible WPF window on an STA thread, then exercises initial allocation, resize, drawing, and disposal.

Text rendering uses `SKFont` for metrics and text blobs, as required by SkiaSharp 4. New rendering work should keep font measurement in `SKFont` and use `SKPaint` only for paint state.
