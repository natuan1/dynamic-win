using System;
using System.Collections.Generic;
using System.IO;

namespace DynamicWin.Utils;

/// <summary>Compatibility facade for the application's settings store.</summary>
internal static class SaveManager
{
    public static string SavePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicWin");

    private static ISettingsStore? store;

    internal static void Configure(ISettingsStore settingsStore)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        store = settingsStore;
    }

    private static ISettingsStore Store => store ?? throw new InvalidOperationException("The settings store has not been configured.");

    public static void LoadData() => Store.Load();

    public static void SaveAll() => Store.Save();

    public static void Add(string key, object value) => Store.Set(key, value);

    public static void Remove(string key) => Store.Remove(key);

    public static object? Get(string key) => Store.Get(key);

    public static T? Get<T>(string key) => Store.Get<T>(key);

    public static bool Contains(string key) => Store.Contains(key);
}
