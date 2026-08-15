using DynamicWin.Main;
using DynamicWin.UI.Menu.Menus;
using DynamicWin.Utils;
using System;
using System.Collections.Generic;

namespace DynamicWin.UI.Menu
{
    public class MenuManager : IDisposable
    {
        private readonly IRenderState renderState;
        private readonly IUiRuntime runtime;
        private BaseMenu activeMenu;
        public BaseMenu ActiveMenu { get => activeMenu; }

        public Action<BaseMenu, BaseMenu> onMenuChange;
        public Action<BaseMenu> onMenuChangeEnd;

        internal MenuManager(IRenderState renderState, IUiRuntime runtime)
        {
            this.renderState = renderState;
            this.runtime = runtime;
        }

        public void Init()
        {
            Resources.Res.CreateStaticMenus();
            activeMenu = Resources.Res.HomeMenu;
            activeMenu.Attach(runtime, runtime.MainIsland);
            activeMenu.OnLoad();
        }

        public void OpenMenu(BaseMenu newActiveMenu)
        {
            Open(newActiveMenu);
        }

        private void Open(BaseMenu newActiveMenu)
        {
            SetActiveMenu(newActiveMenu);
        }

        public void OpenOverlayMenu(BaseMenu newActiveMenu, float time = 5f)
        {
            OpenOverlay(newActiveMenu, time);
        }

        private BaseMenu? overlayReturnMenu;
        private DateTime overlayDeadlineUtc;
        private bool overlayIsOpen;

        public void CloseOverlay()
        {
            CloseOverlayInternal();
        }

        private void OpenOverlay(BaseMenu newActiveMenu, float time)
        {
            overlayReturnMenu = activeMenu;
            overlayDeadlineUtc = DateTime.UtcNow.AddSeconds(time);
            overlayIsOpen = true;
            QueueOpenMenu(newActiveMenu);
        }

        private void CloseOverlayInternal()
        {
            if (overlayIsOpen)
                overlayDeadlineUtc = DateTime.UtcNow;
        }

        List<BaseMenu> menuLoadQueue = new List<BaseMenu>();

        Animator menuAnimatorOut;

        public void Update(float deltaTime)
        {
            if (menuAnimatorOut != null)
                menuAnimatorOut.Update(deltaTime);

            if (overlayIsOpen && DateTime.UtcNow >= overlayDeadlineUtc)
            {
                overlayIsOpen = false;
                if (overlayReturnMenu != null)
                    QueueOpenMenu(overlayReturnMenu);
                overlayReturnMenu = null;
            }
        }

        private void SetActiveMenu(BaseMenu newActiveMenu)
        {
            if (menuAnimatorOut != null && menuAnimatorOut.IsRunning) return;
            onMenuChange?.Invoke(activeMenu, newActiveMenu);

            menuAnimatorOut = new Animator(300, 1);

            renderState.BlurOverride = 35f;

            if (activeMenu != null) activeMenu.OnDeload();
            newActiveMenu.Attach(runtime, runtime.MainIsland);
            activeMenu = newActiveMenu;
            activeMenu.OnLoad();

            menuAnimatorOut.onAnimationUpdate += (t) =>
            {
                float easedTime = Easings.EaseOutCubic(t);
                float easedTime2 = Easings.EaseOutQuint(t);
                float blurSize = Mathf.Lerp(35f, 0f, easedTime);
                float alpha = Mathf.Lerp(0f, 1f, easedTime2);

                var canvasSize = Vec2.lerp(Vec2.one * 0.7f, Vec2.one, easedTime2);

                renderState.BlurOverride = blurSize;
                renderState.AlphaOverride = alpha;
                renderState.ScaleOffset = canvasSize;
            };

            menuAnimatorOut.onAnimationEnd += () =>
            {
                LoadMenuEnd();
            };

            menuAnimatorOut.Start();
        }

        void LoadMenuEnd()
        {
            onMenuChangeEnd?.Invoke(activeMenu);

            if (menuLoadQueue.Count != 0)
            {
                var queueObj = menuLoadQueue[0];

                if (queueObj == activeMenu)
                {
                    menuLoadQueue.Remove(queueObj);
                    return;
                }
                else OpenMenu(queueObj);

                menuLoadQueue.Remove(queueObj);
            }

            renderState.BlurOverride = 0f;
            renderState.AlphaOverride = 1f;
            renderState.ScaleOffset = Vec2.one;

            menuAnimatorOut = null;
        }

        public void QueueOpenMenu(BaseMenu menu)
        {
            if (menuAnimatorOut == null) OpenMenu(menu);
            else
            {
                menuLoadQueue.Add(menu);
            }
        }

        public void Dispose()
        {
            activeMenu?.OnDeload();
            activeMenu?.Dispose();
            overlayReturnMenu?.Dispose();
            menuLoadQueue.ForEach(menu => menu.Dispose());
            menuLoadQueue.Clear();
        }
    }
}
