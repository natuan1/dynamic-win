using DynamicWin.WPFBinders;
using SkiaSharp;
using System.Windows;
using System.Windows.Threading;

namespace DynamicWin.Tests.WPFBinders;

[Collection("WPF")]
public class SkiaRenderSurfaceTests
{
    [Fact]
    public void SkElement_renders_at_startup_and_after_resize_in_a_wpf_window()
    {
        RunOnSta(() =>
        {
            var element = new SKElement();
            var paintCount = 0;
            var lastSize = SKSizeI.Empty;
            element.PaintSurface += (_, eventArgs) =>
            {
                paintCount++;
                lastSize = eventArgs.Info.Size;
                eventArgs.Surface.Canvas.Clear(SKColors.Transparent);
            };

            var window = new Window
            {
                Content = element,
                Width = 80,
                Height = 40,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.ToolWindow
            };

            try
            {
                window.Show();
                window.UpdateLayout();
                DrainRenderQueue();
                var startupSize = lastSize;

                window.Width = 160;
                window.Height = 80;
                window.UpdateLayout();
                DrainRenderQueue();

                Assert.True(paintCount >= 2);
                Assert.True(lastSize.Width > startupSize.Width);
                Assert.True(lastSize.Height > startupSize.Height);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void DrainRenderQueue() => Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw new Xunit.Sdk.XunitException(exception.ToString());
    }
}
