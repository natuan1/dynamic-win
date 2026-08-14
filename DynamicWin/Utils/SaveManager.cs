using System;
using System.Collections.Generic;
using System.IO;

namespace DynamicWin.Utils;

/// <summary>Compatibility facade for the application's settings store.</summary>
internal static class SaveManager
{
    public static string SavePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicWin");

    private static readonly ISettingsStore store = new JsonSettingsStore(SavePath, "Settings.json");

    public static void LoadData() => store.Load();

    public static void SaveAll() => store.Save();

    public static void Add(string key, object value) => store.Set(key, value);

    public static void Remove(string key) => store.Remove(key);

    public static object? Get(string key) => store.Get(key);

    public static T? Get<T>(string key) => store.Get<T>(key);

    public static bool Contains(string key) => store.Contains(key);
}
