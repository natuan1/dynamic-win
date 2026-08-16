using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using DynamicWin.Resources;
using DynamicWin.Platform;
using DynamicWin.UI;
using DynamicWin.UI.Menu;
using DynamicWin.UI.Menu.Menus;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;
using DynamicWin.WPFBinders;
using SkiaSharp;

namespace DynamicWin.Main
{
    internal class RendererMain : SKElement, IRenderState
    {
        private readonly UiRuntime runtime;
        private IslandObject islandObject;
        public IslandObject MainIsland => islandObject;
        private MenuManager menuManager;
        private List<UIObject> objects => menuManager.ActiveMenu.UiObjects;

        public Vec2 renderOffset = Vec2.zero;
        public Vec2 scaleOffset = Vec2.one;
        public float blurOverride = 0f;
        public float alphaOverride = 1f;

        float IRenderState.BlurOverride { get => blurOverride; set => blurOverride = value; }
        float IRenderState.AlphaOverride { get => alphaOverride; set => alphaOverride = value; }
        Vec2 IRenderState.ScaleOffset { get => scaleOffset; set => scaleOffset = value; }

        public Action<float> onUpdate;
        public Action<SKCanvas> onDraw;

        private Stopwatch? updateStopwatch;
        private int initialScreenBrightness = 0;
        private readonly IDisplayBrightness brightness;
        private float deltaTime = 0f;
        public float DeltaTime => deltaTime;

        private bool isInitialized = false;
        public int canvasWithoutClip;
        private GRContext Context;

        public RendererMain(UiRuntime runtime)
        {
            this.runtime = runtime;
            brightness = runtime.Services.Platform.Brightness;
            menuManager = new MenuManager(this, runtime);
            islandObject = new IslandObject(runtime);
            runtime.Attach(this, menuManager, islandObject);
            menuManager.Init();

            initialScreenBrightness = brightness.Get();
            KeyHandler.onKeyDown += OnKeyRegistered;

            // Get refresh rate
            int refreshRate = GetRefreshRate();
            Debug.WriteLine($"Monitor Refresh Rate: {refreshRate} Hz");

            CompositionTarget.Rendering += OnRendering;

            isInitialized = true;
        }

        public void Destroy()
        {
            CompositionTarget.Rendering -= OnRendering;
            // if (fallbackTimer != null) fallbackTimer.Stop();

            KeyHandler.onKeyDown -= OnKeyRegistered;
            menuManager.Dispose();
            runtime.Detach(this);
        }

        private void OnRendering(object sender, EventArgs e)
        {
            Update();
            Render();
        }

        private void OnKeyRegistered(Keys key, KeyModifier modifier)
        {
            if (key == Keys.LWin && modifier.isCtrlDown)
            {
                islandObject.hidden = !islandObject.hidden;
            }

            if ((key == Keys.VolumeDown || key == Keys.VolumeMute || key == Keys.VolumeUp) && PopupOptions.saveData.volumePopup)
            {
                if (menuManager.ActiveMenu is HomeMenu)
                {
                    menuManager.OpenOverlayMenu(new VolumeAdjustMenu(), 100f);
                }
                else if (VolumeAdjustMenu.timerUntilClose != null)
                {
                    VolumeAdjustMenu.timerUntilClose = 0f;
                }
            }

            if (key == Keys.MediaNextTrack || key == Keys.MediaPreviousTrack)
            {
                if (menuManager.ActiveMenu is HomeMenu)
                {
                    if (key == Keys.MediaNextTrack) Res.HomeMenu.NextSong();
                    else Res.HomeMenu.PrevSong();
                }
            }
        }

        private void Update()
        {
            if (updateStopwatch != null)
            {
                updateStopwatch.Stop();
                deltaTime = updateStopwatch.ElapsedMilliseconds / 1000f;
            }
            else
            {
                deltaTime = 1f / 1000f;
            }

            updateStopwatch = Stopwatch.StartNew();

            onUpdate?.Invoke(DeltaTime);

            if (brightness.Get() != initialScreenBrightness && PopupOptions.saveData.brightnessPopup)
            {
                initialScreenBrightness = brightness.Get();
                if (menuManager.ActiveMenu is HomeMenu)
                {
                    menuManager.OpenOverlayMenu(new BrightnessAdjustMenu(), 100f);
                }
                else if (BrightnessAdjustMenu.timerUntilClose != null)
                {
                    BrightnessAdjustMenu.PressBK();
                    BrightnessAdjustMenu.timerUntilClose = 0f;
                }
            }

            menuManager.Update(DeltaTime);

            if (menuManager.ActiveMenu != null)
            {
                menuManager.ActiveMenu.Update();

                if (menuManager.ActiveMenu is DropFileMenu && !runtime.IsDragging)
                    menuManager.OpenMenu(Res.HomeMenu);
            }

            islandObject.UpdateCall(DeltaTime);

            if (MainIsland.hidden) return;

            // Snapshot: a mouse event inside UpdateCall can swap menus or rebuild
            // the renderer (drag-out), which would mutate the list mid-enumeration.
            foreach (UIObject uiObject in objects.ToArray())
            {
                uiObject.UpdateCall(DeltaTime);
            }
        }

        private void Render()
        {
            Dispatcher.Invoke(() => InvalidateVisual());
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            base.OnPaintSurface(e);

            if (!isInitialized) return;

            SKSurface surface = e.Surface;
            SKCanvas canvas = surface.Canvas;

            canvas.Clear(SKColors.Transparent);

            double dpiFactor = System.Windows.PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;
            canvas.Scale((float)dpiFactor, (float)dpiFactor);

            canvasWithoutClip = canvas.Save();

            if (islandObject.maskInToIsland) Mask(canvas);
            islandObject.DrawCall(canvas);

            if (MainIsland.hidden) return;

            bool hasContextMenu = false;
            foreach (UIObject uiObject in objects.ToArray())
            {
                canvas.RestoreToCount(canvasWithoutClip);
                canvasWithoutClip = canvas.Save();

                if (uiObject.TryGetHoveredContextMenu(out var contextMenu))
                {
                    hasContextMenu = true;
                    ContextMenu = contextMenu;
                }

                if (uiObject.maskInToIsland)
                {
                    Mask(canvas);
                }

                canvas.Scale(scaleOffset.X, scaleOffset.Y, islandObject.Position.X + islandObject.Size.X / 2, islandObject.Position.Y + islandObject.Size.Y / 2);
                canvas.Translate(renderOffset.X, renderOffset.Y);

                uiObject.DrawCall(canvas);
            }

            onDraw?.Invoke(canvas);

            if (!hasContextMenu) ContextMenu = null;

            canvas.Flush();
        }

        private void Mask(SKCanvas canvas)
        {
            var islandMask = GetMask();
            canvas.ClipRoundRect(islandMask);
        }

        public SKRoundRect GetMask()
        {
            var islandMask = islandObject.GetRect();
            islandMask.Deflate(new SKSize(1, 1));
            return islandMask;
        }

        // Native PInvoke to get monitor refresh rate
        private const int ENUM_CURRENT_SETTINGS = -1;
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DEVMODE
        {
            private const int CCHDEVICENAME = 32;
            private const int CCHFORMNAME = 32;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
            public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        private int GetRefreshRate()
        {
            DEVMODE devMode = new DEVMODE();
            devMode.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode))
                return (int)devMode.dmDisplayFrequency;
            return 60;
        }
    }
}
