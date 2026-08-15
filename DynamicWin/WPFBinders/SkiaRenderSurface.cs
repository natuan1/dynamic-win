using SkiaSharp;

namespace DynamicWin.WPFBinders;

internal sealed class SkiaRenderSurface : IDisposable
{
    private SKSurface? surface;

    private SkiaRenderSurface(SKSurface surface, SKImageInfo info)
    {
        this.surface = surface;
        Info = info;
    }

    public SKImageInfo Info { get; }

    internal SKSurface Surface => surface ?? throw new ObjectDisposedException(nameof(SkiaRenderSurface));

    public SKCanvas Canvas => surface?.Canvas ?? throw new ObjectDisposedException(nameof(SkiaRenderSurface));

    public static SkiaRenderSurface CreateRaster(int width, int height)
    {
        var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        return new SkiaRenderSurface(SKSurface.Create(info) ?? throw new InvalidOperationException("Unable to create a Skia raster surface."), info);
    }

    public static SkiaRenderSurface Create(SKImageInfo info, IntPtr pixels, int rowBytes)
    {
        return new SkiaRenderSurface(SKSurface.Create(info, pixels, rowBytes) ?? throw new InvalidOperationException("Unable to create a Skia WPF surface."), info);
    }

    public void Dispose()
    {
        surface?.Dispose();
        surface = null;
    }
}
