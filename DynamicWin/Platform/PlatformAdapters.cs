using DynamicWin.Utils;
using System.Drawing;
using ThumbnailGenerator;

namespace DynamicWin.Platform;

public interface IMediaTransport { void PlayPause(); void Next(); void Previous(); }
internal interface IDisplayBrightness { int Get(); void Set(int brightness); }
internal interface IDeviceUsageReader { bool IsMicrophoneInUse(); bool IsWebcamInUse(); }
internal interface IStartupShortcutAdapter { void CreateShortcut(); bool RemoveShortcut(); }
internal interface IFileThumbnailAdapter { Bitmap GetThumbnail(string fileName, int width, int height, ThumbnailOptions options); }
internal interface IHardwareUsageReader { string CurrentUsage { get; } }

internal interface IPlatformAdapters
{
    IMediaTransport Media { get; }
    IDisplayBrightness Brightness { get; }
    IDeviceUsageReader DeviceUsage { get; }
    IStartupShortcutAdapter StartupShortcuts { get; }
    IFileThumbnailAdapter FileThumbnails { get; }
    IHardwareUsageReader HardwareUsage { get; }
}

internal static class PlatformAdapters
{
    public static IPlatformAdapters Current { get; set; } = new WindowsPlatformAdapters();
}

internal sealed class WindowsPlatformAdapters : IPlatformAdapters
{
    public IMediaTransport Media { get; } = new MediaController();
    public IDisplayBrightness Brightness { get; } = new WindowsDisplayBrightness();
    public IDeviceUsageReader DeviceUsage { get; } = new WindowsDeviceUsageReader();
    public IStartupShortcutAdapter StartupShortcuts { get; } = new WindowsStartupShortcutAdapter();
    public IFileThumbnailAdapter FileThumbnails { get; } = new WindowsFileThumbnailAdapter();
    public IHardwareUsageReader HardwareUsage { get; } = new HardwareUsageReader();
}

internal sealed class WindowsDeviceUsageReader : IDeviceUsageReader
{
    public bool IsMicrophoneInUse() => DeviceUsageChecker.IsMicrophoneInUse();
    public bool IsWebcamInUse() => DeviceUsageChecker.IsWebcamInUse();
}

internal sealed class WindowsStartupShortcutAdapter : IStartupShortcutAdapter
{
    public void CreateShortcut() => StartupShortcutManager.CreateShortcut();
    public bool RemoveShortcut() => StartupShortcutManager.RemoveShortcut();
}

internal sealed class WindowsFileThumbnailAdapter : IFileThumbnailAdapter
{
    public Bitmap GetThumbnail(string fileName, int width, int height, ThumbnailOptions options) => WindowsThumbnailProvider.GetThumbnail(fileName, width, height, options);
}

internal sealed class HardwareUsageReader : IHardwareUsageReader
{
    public string CurrentUsage => HardwareMonitor.usageString;
}
