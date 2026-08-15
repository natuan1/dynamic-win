using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;

namespace DynamicWin.Utils;

public interface ISettingsStore
{
    void Load();
    void Save();
    bool Contains(string key);
    object? Get(string key);
    T? Get<T>(string key);
    void Set(string key, object value);
    void Remove(string key);
}

public static class SettingsStore
{
    public static ISettingsStore Open(string directory, string fileName) => new JsonSettingsStore(directory, fileName);
}

internal sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string filePath;
    private readonly Dictionary<string, JToken> values = new();

    public JsonSettingsStore(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        filePath = Path.Combine(directory, fileName);
    }

    public void Load()
    {
        values.Clear();
        if (!File.Exists(filePath))
            return;

        var json = File.ReadAllText(filePath);
        var loaded = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(json);
        if (loaded is not null)
        {
            foreach (var pair in loaded)
                values[pair.Key] = pair.Value;
        }
    }

    public void Save()
    {
        var temporaryPath = filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(values, Formatting.Indented));
        File.Move(temporaryPath, filePath, overwrite: true);
    }

    public bool Contains(string key) => values.ContainsKey(key);

    public object? Get(string key) => values.TryGetValue(key, out var value) ? value.ToObject<object>() : null;

    public T? Get<T>(string key) => values.TryGetValue(key, out var value) ? value.ToObject<T>() : default;

    public void Set(string key, object value) => values[key] = JToken.FromObject(value);

    public void Remove(string key) => values.Remove(key);
}
