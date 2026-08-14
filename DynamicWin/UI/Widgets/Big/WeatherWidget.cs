using DynamicWin.Main;
using DynamicWin.Resources;
using DynamicWin.UI.Menu.Menus;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;
using Newtonsoft.Json;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;
using static DynamicWin.UI.Widgets.Small.RegisterUsedDevicesOptions;

namespace DynamicWin.UI.Widgets.Big
{
    class RegisterWeatherWidget : IRegisterableWidget
    {
        public bool IsSmallWidget => false;
        public string WidgetName => "Weather";

        public WidgetBase CreateWidgetInstance(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter)
        {
            return new WeatherWidget(parent, position, alignment);
        }
    }

    class RegisterWeatherWidgetSettings : IRegisterableSetting
    {
        public string SettingID => "weatherwidget";

        public string SettingTitle => "Weather Widget";

        public static WeatherWidgetSaveData saveData;

        public struct WeatherWidgetSaveData
        {
            public bool hideLocation;
            public bool useCelcius;
        }

        public void LoadSettings()
        {
            if (SaveManager.Contains(SettingID))
                saveData = JsonConvert.DeserializeObject<WeatherWidgetSaveData>((string)SaveManager.Get(SettingID));
            else
            {
                saveData = new WeatherWidgetSaveData() { useCelcius = true };
            }
        }

        public void SaveSettings()
        {
            SaveManager.Add(SettingID, JsonConvert.SerializeObject(saveData));
        }

        public List<UIObject> SettingsObjects()
        {
            var objects = new List<UIObject>();

            var hideLocationCheckbox = new Checkbox(null, "Hide current location", new Vec2(25, 0), new Vec2(25, 25), null, alignment: UIAlignment.TopLeft);
            hideLocationCheckbox.IsChecked = saveData.hideLocation;

            hideLocationCheckbox.clickCallback += () =>
            {
                saveData.hideLocation = hideLocationCheckbox.IsChecked;
            };

            objects.Add(hideLocationCheckbox);

            var useCelciusCheckbox = new Checkbox(null, "Use Celsius as temperature measurement", new Vec2(25, 0), new Vec2(25, 25), null, alignment: UIAlignment.TopLeft);
            useCelciusCheckbox.IsChecked = saveData.useCelcius;

            useCelciusCheckbox.clickCallback += () =>
            {
                saveData.useCelcius = useCelciusCheckbox.IsChecked;
            };

            objects.Add(useCelciusCheckbox);

            return objects;
        }
    }

    public class WeatherWidget : WidgetBase
    {
        DWText temperatureText;
        DWText weatherText;
        DWText locationText;

        UIObject locationTextReplacement;

        static WeatherFetcher weatherFetcher;

        DWImage weatherTypeIcon;

        public WeatherWidget(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter) : base(parent, position, alignment)
        {
            AddLocalObject(new DWImage(this, Res.Location, new Vec2(20, 17.5f), new Vec2(12.5f, 12.5f), UIAlignment.TopLeft)
            {
                Color = Theme.TextSecond,
                allowIconThemeColor = true
            });

            locationText = new DWText(this, "--", new Vec2(32.5f, 17.5f), UIAlignment.TopLeft)
            {
                TextSize = 15,
                Anchor = new Vec2(0, 0.5f),
                Color = Theme.TextSecond
            };
            AddLocalObject(locationText);

            locationTextReplacement = new UIObject(this, new Vec2(32.5f, 17.5f), new Vec2(75, 15), UIAlignment.TopLeft)
            {
                roundRadius = 5f,
                Anchor = new Vec2(0, 0.5f),
                Color = Theme.TextSecond
            };
            AddLocalObject(locationTextReplacement);

            AddLocalObject(new DWImage(this, Res.Weather, new Vec2(20, 37.5f), new Vec2(12.5f, 12.5f), UIAlignment.TopLeft)
            {
                Color = Theme.TextThird,
                allowIconThemeColor = true
            });

            weatherText = new DWText(this, "--", new Vec2(32.5f, 37.5f), UIAlignment.TopLeft)
            {
                TextSize = 13,
                Font = Res.InterBold,
                Anchor = new Vec2(0, 0.5f),
                Color = Theme.TextThird
            };
            AddLocalObject(weatherText);


            temperatureText = new DWText(this, "--", new Vec2(15, -27.5f), UIAlignment.BottomLeft)
            {
                TextSize = 34,
                Anchor = new Vec2(0, 0.5f),
                Color = Theme.TextMain
            };
            AddLocalObject(temperatureText);

            weatherTypeIcon = new DWImage(this, Res.Weather, new Vec2(0, 0), new Vec2(100, 100), UIAlignment.MiddleRight)
            {
                Color = Theme.TextThird,
                allowIconThemeColor = true
            };

            if(weatherFetcher == null)
                weatherFetcher = new WeatherFetcher();

            weatherFetcher.onWeatherDataReceived += OnWeatherDataReceived;
            weatherFetcher.Fetch();

            locationTextReplacement.SilentSetActive(RegisterWeatherWidgetSettings.saveData.hideLocation);
            locationText.SilentSetActive(!RegisterWeatherWidgetSettings.saveData.hideLocation);
        }

        public override void OnDestroy()
        {
            weatherFetcher.onWeatherDataReceived -= OnWeatherDataReceived;
            if (!weatherFetcher.HasSubscribers)
                weatherFetcher.Stop();
            base.OnDestroy();
        }

        public override ContextMenu? GetContextMenu()
        {
            var ctx = new ContextMenu();

            var hideLocationItem = new MenuItem() { Header = "Hide Location", IsCheckable = true, IsChecked = RegisterWeatherWidgetSettings.saveData.hideLocation };
            hideLocationItem.Click += (x, y) =>
            {
                RegisterWeatherWidgetSettings.saveData.hideLocation = hideLocationItem.IsChecked;

                locationTextReplacement.SetActive(RegisterWeatherWidgetSettings.saveData.hideLocation);
                locationText.SetActive(!RegisterWeatherWidgetSettings.saveData.hideLocation);

                new RegisterWeatherWidgetSettings().SaveSettings();
                SaveManager.SaveAll();
            };

            ctx.Items.Add(hideLocationItem);

            return ctx;
        }

        WeatherData lastWeatherData;

        void OnWeatherDataReceived(WeatherData weatherData)
        {
            lastWeatherData = weatherData;
            
            weatherText.SetText(weatherData.weatherText);
            locationText.SetText(weatherData.city);

            UpdateIcon(weatherData.weatherText);
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            temperatureText.SetText(RegisterWeatherWidgetSettings.saveData.useCelcius ? lastWeatherData.temperatureCelcius : lastWeatherData.temperatureFahrenheit);
        }

        void UpdateIcon(string weather)
        {
            if (weather.ToLower().Contains("sun") || weather.ToLower().Contains("clear"))
                weatherTypeIcon.Image = Res.Sunny;
            else if (weather.ToLower().Contains("cloud") || weather.ToLower().Contains("overcast"))
                weatherTypeIcon.Image = Res.Cloudy;
            else if (weather.ToLower().Contains("rain") || weather.ToLower().Contains("shower"))
                weatherTypeIcon.Image = Res.Rainy;
            else if (weather.ToLower().Contains("thunder"))
                weatherTypeIcon.Image = Res.Thunderstorm;
            else if (weather.ToLower().Contains("snow"))
                weatherTypeIcon.Image = Res.Snowy;
            else if (weather.ToLower().Contains("sleet"))
                weatherTypeIcon.Image = Res.Rainy;
            else if (weather.ToLower().Contains("fog") || weather.ToLower().Contains("haze") || weather.ToLower().Contains("mist"))
                weatherTypeIcon.Image = Res.Foggy;
            else if (weather.ToLower().Contains("windy") || weather.ToLower().Contains("breezy"))
                weatherTypeIcon.Image = Res.Windy;
            else
                weatherTypeIcon.Image = Res.SevereWeatherWarning;
        }

        public override void DrawWidget(SKCanvas canvas)
        {
            base.DrawWidget(canvas);

            var paint = GetPaint();
            paint.Color = GetColor(Theme.WidgetBackground).Value();
            canvas.DrawRoundRect(GetRect(), paint);

            canvas.ClipRoundRect(GetRect(), SKClipOperation.Intersect, true);
            weatherTypeIcon.DrawCall(canvas);
        }
    }

    public class WeatherFetcher
    {
        private static readonly HttpClient httpClient = new();
        private WeatherData weatherData = new WeatherData();
        public WeatherData Weather { get => weatherData; }

        public Action<WeatherData> onWeatherDataReceived;
        private CancellationTokenSource? refreshCancellation;
        public bool HasSubscribers => onWeatherDataReceived is not null;

        public void Fetch()
        {
            if (refreshCancellation is not null)
                return;

            refreshCancellation = new CancellationTokenSource();
            _ = RefreshLoopAsync(refreshCancellation.Token);
        }

        public void Stop()
        {
            refreshCancellation?.Cancel();
            refreshCancellation?.Dispose();
            refreshCancellation = null;
        }

        private async Task RefreshLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var response = await httpClient.GetStringAsync("https://ipinfo.io/geo", cancellationToken);
                    var location = JsonConvert.DeserializeObject<Location>(response);
                    var coordinates = location.loc?.Split(',');
                    if (coordinates is not { Length: 2 })
                        throw new InvalidDataException("Weather location is unavailable.");

                    var weatherXml = await httpClient.GetStringAsync(
                        $"https://tile-service.weather.microsoft.com/livetile/front/{coordinates[0]},{coordinates[1]}", cancellationToken);
                    var (temperature, weather) = ReadWeather(weatherXml);
                    var fahrenheit = temperature.Replace("°", string.Empty, StringComparison.Ordinal);
                    if (!double.TryParse(fahrenheit, NumberStyles.Float, CultureInfo.InvariantCulture, out var fahrenheitValue))
                        throw new InvalidDataException("Weather temperature is invalid.");

                    var celsius = ((fahrenheitValue - 32.0) * 5 / 9).ToString("#.#", CultureInfo.InvariantCulture);
                    weatherData = new WeatherData
                    {
                        city = location.city,
                        region = location.region,
                        temperatureCelcius = celsius + "°C",
                        temperatureFahrenheit = fahrenheit + "F",
                        weatherText = weather
                    };
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => onWeatherDataReceived?.Invoke(weatherData));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Debug.WriteLine($"Weather refresh failed: {exception.Message}");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private static (string Temperature, string Weather) ReadWeather(string weatherXml)
        {
            using var reader = XmlReader.Create(new StringReader(weatherXml));
            var index = 0;
            string? temperature = null;
            string? weather = null;
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Text)
                    continue;
                if (index == 1) temperature = reader.Value;
                if (index == 2) weather = reader.Value;
                index++;
            }

            if (temperature is null || weather is null)
                throw new InvalidDataException("Weather response is incomplete.");
            return (temperature, weather);
        }
    }

    struct Location
    {
        public string city;
        public string region;
        public string country;
        public string loc;
    }

    public struct WeatherData
    {
        public string city;
        public string region;
        public string weatherText;
        public string temperatureCelcius;
        public string temperatureFahrenheit;
    }
}
