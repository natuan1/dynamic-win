using DynamicWin.UI;
using DynamicWin.UI.Menu.Menus;
using DynamicWin.UI.UIElements;
using DynamicWin.UI.Widgets;
using DynamicWin.Utils;
using Newtonsoft.Json;

namespace DynamicWin.LocalSend;

public class LocalSendSettingsData
{
    public bool enabled = true;
    public bool requirePin = false;
}

public class RegisterLocalSendSettings : IRegisterableSetting
{
    public string SettingID => "localsend";
    public string SettingTitle => "LocalSend";

    public static LocalSendSettingsData saveData = new();

    public void LoadSettings(ISettingsStore settings)
    {
        if (settings.Contains(SettingID))
            saveData = JsonConvert.DeserializeObject<LocalSendSettingsData>((string)settings.Get(SettingID)) ?? new LocalSendSettingsData();
        else
            saveData = new LocalSendSettingsData();
    }

    public void SaveSettings(ISettingsStore settings)
    {
        settings.Set(SettingID, JsonConvert.SerializeObject(saveData));
    }

    public List<UIObject> SettingsObjects()
    {
        var objects = new List<UIObject>();

        var enableCheckbox = new Checkbox(null, "Enable LocalSend (restart app to apply)", new Vec2(25, 0), new Vec2(25, 25), null, alignment: UIAlignment.TopLeft)
        {
            IsChecked = saveData.enabled
        };
        enableCheckbox.clickCallback += () => saveData.enabled = enableCheckbox.IsChecked;
        objects.Add(enableCheckbox);

        var hint = new DWText(null, "Send tray files to nearby devices running the LocalSend app. Receiving files arrives in the next update.", new Vec2(25, 0), UIAlignment.TopLeft);
        objects.Add(hint);

        return objects;
    }
}
