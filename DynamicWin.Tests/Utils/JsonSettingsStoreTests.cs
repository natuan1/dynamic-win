using DynamicWin.Utils;

namespace DynamicWin.Tests.Utils;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "DynamicWin.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Save_then_load_preserves_values_across_store_instances()
    {
        var writer = SettingsStore.Open(directory, "settings.json");
        writer.Set("settings.theme", 2);
        writer.Set("settings.widgets", new[] { "clock", "weather" });
        writer.Save();

        var reader = SettingsStore.Open(directory, "settings.json");
        reader.Load();

        Assert.Equal(2, reader.Get<int>("settings.theme"));
        Assert.Equal(["clock", "weather"], reader.Get<string[]>("settings.widgets")!);
    }

    [Fact]
    public void Load_with_no_file_leaves_store_empty()
    {
        var store = SettingsStore.Open(directory, "settings.json");

        store.Load();

        Assert.False(store.Contains("settings.theme"));
        Assert.Null(store.Get("settings.theme"));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
