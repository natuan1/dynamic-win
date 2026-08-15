using DynamicWin.Platform;
using DynamicWin.Resources;
using DynamicWin.Utils;

namespace DynamicWin.Main;

internal sealed class ApplicationCompositionRoot(IPlatformAdapters platformAdapters)
{
    public ApplicationRuntime Create(
        Action initializeAudio,
        Action updateStartupShortcut,
        Action showWindow,
        Action disposeWindow)
    {
        HardwareMonitor? hardwareMonitor = null;

        return new ApplicationRuntime(
            new DelegateApplicationComponent(
                () =>
                {
                    PlatformAdapters.Configure(platformAdapters);
                    SaveManager.Configure(new JsonSettingsStore(SaveManager.SavePath, "Settings.json"));
                    SaveManager.LoadData();
                },
                SaveManager.SaveAll),
            new DelegateApplicationComponent(
                () =>
                {
                    Res.Load();
                    _ = new Theme();
                },
                () => { }),
            new DelegateApplicationComponent(Settings.InitializeSettings, Settings.Save),
            new DelegateApplicationComponent(initializeAudio, () => { }),
            new DelegateApplicationComponent(KeyHandler.Start, KeyHandler.Stop),
            new DelegateApplicationComponent(
                () => hardwareMonitor = new HardwareMonitor(),
                () => hardwareMonitor?.Dispose()),
            new DelegateApplicationComponent(updateStartupShortcut, () => { }),
            new DelegateApplicationComponent(showWindow, disposeWindow));
    }
}
