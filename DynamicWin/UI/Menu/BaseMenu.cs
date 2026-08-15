using DynamicWin.Main;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicWin.UI.Menu
{
    public class BaseMenu : IDisposable
    {
        private List<UIObject> uiObjects = new List<UIObject>();
        private IUiRuntime? runtime;
        private IslandObject? attachedIsland;
        protected IApplicationServices Services => Runtime.Services;
        protected IUiRuntime Runtime => runtime
            ?? throw new InvalidOperationException("Menu must be attached to the UI runtime before use.");

        public List<UIObject> UiObjects { get { return uiObjects; } }

        public BaseMenu()
        {
        }

        internal void Attach(IUiRuntime runtime, IslandObject island)
        {
            if (this.runtime == runtime && attachedIsland == island && uiObjects.Count != 0) return;

            this.runtime = runtime;
            attachedIsland = island;
            uiObjects = InitializeMenu(island);
        }

        public virtual Vec2 IslandSize() { return new Vec2(200, 45); }
        public virtual Vec2 IslandSizeBig() { return IslandSize(); }

        public virtual Col IslandBorderColor() { return Col.Transparent; }

        public virtual List<UIObject> InitializeMenu(IslandObject island) { return new List<UIObject>(); }

        public virtual void Update() { }
        public virtual void OnLoad() { }

        public virtual void OnDeload() { }

        public void Dispose()
        {
            uiObjects.ForEach(obj => obj.DestroyCall());
            uiObjects.Clear();
        }
    }
}
