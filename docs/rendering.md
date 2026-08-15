# Rendering stack

DynamicWin uses SkiaSharp 4.150.1 through the WPF `SKElement` adapter.

`SkiaRenderSurface` owns each native Skia surface used by the adapter and is the rendering verification seam. Its smoke test exercises initial allocation, a resized surface, drawing, and disposal without requiring a visible WPF window.

Text rendering uses `SKFont` for metrics and text blobs, as required by SkiaSharp 4. New rendering work should keep font measurement in `SKFont` and use `SKPaint` only for paint state.
