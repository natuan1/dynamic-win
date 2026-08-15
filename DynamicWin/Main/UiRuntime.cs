using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DynamicWin.UI.Menu;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;

namespace DynamicWin.Main
{
    internal sealed class UiRuntime : IUiRuntime
    {
        private readonly MainForm window;
        private RendererMain? renderer;
        private MenuManager? menus;
        private IslandObject? mainIsland;

        public UiRuntime(MainForm window, IApplicationServices services)
        {
            this.window = window;
            Services = services;
        }

        public IApplicationServices Services { get; }
        public IslandObject MainIsland => mainIsland ?? throw new InvalidOperationException("UI runtime has no island attached.");
        public BaseMenu ActiveMenu => Menus.ActiveMenu;
        public Vec2 ScreenDimensions => new Vec2(window.Width, window.Height);
        public Vec2 CursorPosition
        {
            get
            {
                var pos = Mouse.GetPosition(window);
                return new Vec2(pos.X, pos.Y);
            }
        }

        public float DeltaTime => renderer?.DeltaTime ?? 0f;
        public float AlphaOverride => renderer?.alphaOverride ?? 1f;
        public float BlurOverride => renderer?.blurOverride ?? 0f;
        public bool IsDragging => window.isDragging;
        public ContextMenu? ContextMenu => renderer?.ContextMenu;

        public event ContextMenuEventHandler ContextMenuOpening
        {
            add => window.ContextMenuOpening += value;
            remove => window.ContextMenuOpening -= value;
        }

        public event ContextMenuEventHandler ContextMenuClosing
        {
            add => window.ContextMenuClosing += value;
            remove => window.ContextMenuClosing -= value;
        }

        public event Action<MouseWheelEventArgs>? Scroll;

        private MenuManager Menus => menus ?? throw new InvalidOperationException("UI runtime has no menu manager attached.");

        public void Attach(RendererMain renderer, MenuManager menuManager, IslandObject mainIsland)
        {
            this.renderer = renderer;
            this.menus = menuManager;
            this.mainIsland = mainIsland;
        }

        public void Detach(RendererMain renderer)
        {
            if (this.renderer == renderer)
            {
                this.renderer = null;
                this.menus = null;
                this.mainIsland = null;
            }
        }

        public void OnScroll(MouseWheelEventArgs e) => Scroll?.Invoke(e);
        public void OpenMenu(BaseMenu menu) => Menus.OpenMenu(menu);
        public void OpenOverlayMenu(BaseMenu menu, float time = 5f) => Menus.OpenOverlayMenu(menu, time);
        public void CloseOverlay() => Menus.CloseOverlay();
        public void QueueOpenMenu(BaseMenu menu) => Menus.QueueOpenMenu(menu);
        public void SetMonitor(int monitorIndex) => window.SetMonitor(monitorIndex);
        public int GetMonitorCount() => MainForm.GetMonitorCount();
        public void RestartRenderer() => window.AddRenderer();
        public void StartDrag(string[] files, Action callback) => window.StartDrag(files, callback);
        public void SetOverlayOpacity(double opacity) => window.Opacity = opacity;
    }
}
