using DynamicWin.Platform;
using DynamicWin.Resources;
using DynamicWin.Utils;

namespace DynamicWin.Main;

internal sealed class ApplicationCompositionRoot(IPlatformAdapters platformAdapters, Action updateStartupShortcut)
{
    public ApplicationRuntime Create(
        Action initializeAudio,
        Action<IApplicationServices> showWindow,
        Action disposeWindow)
    {
        HardwareMonitor? hardwareMonitor = null;
        var settingsDirectory = ApplicationDataPaths.SettingsDirectory;
        var settingsStore = new JsonSettingsStore(settingsDirectory, "Settings.json");
        var services = new ApplicationServices(platformAdapters, settingsStore, settingsDirectory, updateStartupShortcut);

        return new ApplicationRuntime(
            new DelegateApplicationComponent(
                () =>
                {
                    settingsStore.Load();
                },
                settingsStore.Save),
            new DelegateApplicationComponent(
                () =>
                {
                    Res.Load(settingsDirectory);
                    _ = new Theme(settingsDirectory);
                },
                () => { }),
            new DelegateApplicationComponent(() => Settings.InitializeSettings(settingsStore), () => Settings.Save(settingsStore)),
            new DelegateApplicationComponent(initializeAudio, () => { }),
            new DelegateApplicationComponent(KeyHandler.Start, KeyHandler.Stop),
            new DelegateApplicationComponent(
                () => hardwareMonitor = new HardwareMonitor(),
                () => hardwareMonitor?.Dispose()),
            new DelegateApplicationComponent(services.UpdateStartupShortcut, () => { }),
            new DelegateApplicationComponent(() => showWindow(services), disposeWindow));
    }
}
