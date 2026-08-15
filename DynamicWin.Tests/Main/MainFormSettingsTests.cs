using DynamicWin.Main;
using DynamicWin.Platform;
using DynamicWin.Resources;
using DynamicWin.Utils;
using System.Drawing;
using System.Windows.Threading;
using ThumbnailGenerator;

namespace DynamicWin.Tests.Main;

[Collection("WPF")]
public sealed class MainFormSettingsTests : IDisposable
{
    private readonly string previousDirectory = Environment.CurrentDirectory;
    private readonly string settingsDirectory = Path.Combine(Path.GetTempPath(), "DynamicWin.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void MainForm_opens_settings_menu_without_runtime_errors()
    {
        RunOnSta(() =>
        {
            Environment.CurrentDirectory = AppContext.BaseDirectory;

            var settings = SettingsStore.Open(settingsDirectory, "Settings.json");
            settings.Load();
            Res.Load(settingsDirectory);
            _ = new Theme(settingsDirectory);
            Settings.InitializeSettings(settings);
            Settings.smallWidgetsLeft = [];
            Settings.smallWidgetsRight = [];
            Settings.smallWidgetsMiddle = [];
            Settings.bigWidgets = [];

            var form = new MainForm(new TestApplicationServices(settingsDirectory, settings));

            try
            {
                form.Show();
                form.UpdateLayout();

                form.OpenSettingsMenu();
                DrainRenderQueue();

                Assert.True(form.IsVisible);
            }
            finally
            {
                form.Close();
            }
        });
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = previousDirectory;
        if (Directory.Exists(settingsDirectory))
            Directory.Delete(settingsDirectory, recursive: true);
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

    private sealed class TestApplicationServices(string settingsDirectory, ISettingsStore settings) : IApplicationServices
    {
        public IPlatformAdapters Platform { get; } = new TestPlatformAdapters();
        public ISettingsStore Settings { get; } = settings;
        public string SettingsDirectory { get; } = settingsDirectory;
        public void UpdateStartupShortcut() { }
    }

    private sealed class TestPlatformAdapters : IPlatformAdapters
    {
        public IMediaTransport Media { get; } = new TestMediaTransport();
        public IDisplayBrightness Brightness { get; } = new TestDisplayBrightness();
        public IDeviceUsageReader DeviceUsage { get; } = new TestDeviceUsageReader();
        public IStartupShortcutAdapter StartupShortcuts { get; } = new TestStartupShortcutAdapter();
        public IFileThumbnailAdapter FileThumbnails { get; } = new TestFileThumbnailAdapter();
        public IHardwareUsageReader HardwareUsage { get; } = new TestHardwareUsageReader();
    }

    private sealed class TestMediaTransport : IMediaTransport
    {
        public void PlayPause() { }
        public void Next() { }
        public void Previous() { }
    }

    private sealed class TestDisplayBrightness : IDisplayBrightness
    {
        public int Get() => 50;
        public void Set(int brightness) { }
    }

    private sealed class TestDeviceUsageReader : IDeviceUsageReader
    {
        public bool IsMicrophoneInUse() => false;
        public bool IsWebcamInUse() => false;
    }

    private sealed class TestStartupShortcutAdapter : IStartupShortcutAdapter
    {
        public void CreateShortcut() { }
        public bool RemoveShortcut() => true;
    }

    private sealed class TestFileThumbnailAdapter : IFileThumbnailAdapter
    {
        public Bitmap GetThumbnail(string fileName, int width, int height, ThumbnailOptions options) => new(width, height);
    }

    private sealed class TestHardwareUsageReader : IHardwareUsageReader
    {
        public string CurrentUsage => string.Empty;
    }
}
