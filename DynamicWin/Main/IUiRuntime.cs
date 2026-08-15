using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DynamicWin.UI.Menu;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;

namespace DynamicWin.Main
{
    public interface IUiRuntime
    {
        IApplicationServices Services { get; }
        IslandObject MainIsland { get; }
        BaseMenu ActiveMenu { get; }
        Vec2 ScreenDimensions { get; }
        Vec2 CursorPosition { get; }
        float DeltaTime { get; }
        float AlphaOverride { get; }
        float BlurOverride { get; }
        bool IsDragging { get; }
        ContextMenu? ContextMenu { get; }

        event ContextMenuEventHandler ContextMenuOpening;
        event ContextMenuEventHandler ContextMenuClosing;
        event Action<MouseWheelEventArgs> Scroll;

        void OpenMenu(BaseMenu menu);
        void OpenOverlayMenu(BaseMenu menu, float time = 5f);
        void CloseOverlay();
        void QueueOpenMenu(BaseMenu menu);
        void SetMonitor(int monitorIndex);
        int GetMonitorCount();
        void RestartRenderer();
        void StartDrag(string[] files, Action callback);
        void SetOverlayOpacity(double opacity);
    }
}
