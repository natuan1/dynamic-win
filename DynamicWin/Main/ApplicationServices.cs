using DynamicWin.Platform;
using DynamicWin.Utils;

namespace DynamicWin.Main;

/// <summary>Dependencies supplied by the application's composition root to UI features.</summary>
public interface IApplicationServices
{
    IPlatformAdapters Platform { get; }
    ISettingsStore Settings { get; }
    string SettingsDirectory { get; }
    void UpdateStartupShortcut();
}

internal sealed class ApplicationServices(IPlatformAdapters platform, ISettingsStore settings, string settingsDirectory, Action updateStartupShortcut) : IApplicationServices
{
    public IPlatformAdapters Platform { get; } = platform;
    public ISettingsStore Settings { get; } = settings;
    public string SettingsDirectory { get; } = settingsDirectory;
    public void UpdateStartupShortcut() => updateStartupShortcut();
}
