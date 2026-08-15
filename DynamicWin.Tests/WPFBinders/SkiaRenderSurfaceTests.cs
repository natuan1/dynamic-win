using DynamicWin.WPFBinders;
using SkiaSharp;

namespace DynamicWin.Tests.WPFBinders;

public class SkiaRenderSurfaceTests
{
    [Fact]
    public void Raster_surface_supports_startup_resize_and_disposal()
    {
        using var startupSurface = SkiaRenderSurface.CreateRaster(80, 40);
        startupSurface.Canvas.Clear(SKColors.Transparent);
        startupSurface.Canvas.DrawCircle(20, 20, 10, new SKPaint { Color = SKColors.CornflowerBlue });
        Assert.Equal(new SKSizeI(80, 40), startupSurface.Info.Size);

        using var resizedSurface = SkiaRenderSurface.CreateRaster(160, 80);
        Assert.Equal(new SKSizeI(160, 80), resizedSurface.Info.Size);

        resizedSurface.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _ = resizedSurface.Canvas);
    }
}
