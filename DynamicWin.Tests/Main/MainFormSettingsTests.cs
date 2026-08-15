using DynamicWin.Main;
using DynamicWin.Platform;
using DynamicWin.Resources;
using DynamicWin.UI;
using DynamicWin.Utils;
using System.Drawing;
using Forms = System.Windows.Forms;
using System.Windows.Controls;
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

    [Fact]
    public void MainForm_loads_resources_outside_application_working_directory()
    {
        RunOnSta(() =>
        {
            Directory.CreateDirectory(settingsDirectory);
            Environment.CurrentDirectory = settingsDirectory;

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

                Assert.True(form.IsVisible);
                Assert.True(File.Exists(Res.GetPath("icons", "TrayIcon.ico")));
            }
            finally
            {
                form.Close();
            }
        });
    }

    [Fact]
    public void Renderer_restart_keeps_cursor_position_available_for_hover()
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
            var previousCursor = Forms.Cursor.Position;

            try
            {
                form.Show();
                form.UpdateLayout();
                Forms.Cursor.Position = new Point((int)form.Left + 400, (int)form.Top + 200);
                DrainRenderQueue();

                form.AddRenderer();
                DrainRenderQueue();

                var runtimeField = typeof(MainForm).GetField(
                    "runtime",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                var runtime = (IUiRuntime)runtimeField.GetValue(form)!;
                var cursor = runtime.CursorPosition;
                Assert.InRange(cursor.X, 395, 405);
                Assert.InRange(cursor.Y, 195, 205);
            }
            finally
            {
                Forms.Cursor.Position = previousCursor;
                form.Close();
            }
        });
    }

    [Fact]
    public void Renderer_restart_is_deferred_until_the_current_dispatcher_operation_finishes()
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
                var contentBeforeRestart = form.Content;
                var runtimeField = typeof(MainForm).GetField(
                    "runtime",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                var runtime = (IUiRuntime)runtimeField.GetValue(form)!;

                runtime.RestartRenderer();

                Assert.Same(contentBeforeRestart, form.Content);
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                Assert.NotSame(contentBeforeRestart, form.Content);
            }
            finally
            {
                form.Close();
            }
        });
    }

    [Fact]
    public void Top_level_ui_object_exposes_its_overridden_context_menu()
    {
        RunOnSta(() =>
        {
            var uiObject = new ContextMenuObject();
            var hoveringField = typeof(UIObject).GetField(
                "isHovering",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            hoveringField.SetValue(uiObject, true);

            var found = uiObject.TryGetHoveredContextMenu(out var menu);

            Assert.True(found);
            Assert.Same(uiObject.Menu, menu);
        });
    }

    [Fact]
    public void Deepest_hovered_ui_object_wins_context_menu_hit_test()
    {
        RunOnSta(() =>
        {
            var parent = new ContextMenuObject();
            var child = new ContextMenuObject();
            parent.AddChild(child);
            SetHovering(parent, true);
            SetHovering(child, true);

            var found = parent.TryGetHoveredContextMenu(out var menu);

            Assert.True(found);
            Assert.Same(child.Menu, menu);
        });
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = previousDirectory;
        if (Directory.Exists(settingsDirectory))
            Directory.Delete(settingsDirectory, recursive: true);
    }

    private static void DrainRenderQueue() => Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));

    private static void SetHovering(UIObject uiObject, bool value)
    {
        var hoveringField = typeof(UIObject).GetField(
            "isHovering",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        hoveringField.SetValue(uiObject, value);
    }

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

    private sealed class ContextMenuObject : UIObject
    {
        public ContextMenuObject() : base(null, Vec2.zero, Vec2.one) { }
        public ContextMenu Menu { get; } = new();
        public override ContextMenu? GetContextMenu() => Menu;
        public void AddChild(UIObject child) => AddLocalObject(child);
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
