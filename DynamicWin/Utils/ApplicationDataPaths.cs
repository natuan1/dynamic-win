using System.IO;

namespace DynamicWin.Utils;

internal static class ApplicationDataPaths
{
    public static string SettingsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicWin");
}
