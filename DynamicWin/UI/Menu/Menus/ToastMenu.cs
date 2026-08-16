using DynamicWin.UI.UIElements;
using DynamicWin.Utils;

namespace DynamicWin.UI.Menu.Menus
{
    public class ToastMenu : BaseMenu
    {
        readonly string message;

        public ToastMenu(string message)
        {
            this.message = message;
        }

        public override List<UIObject> InitializeMenu(IslandObject island)
        {
            var objects = base.InitializeMenu(island);

            objects.Add(new DWText(island, message, Vec2.zero, UIAlignment.Center)
            {
                TextSize = 13,
                Font = Resources.Res.InterBold,
                Color = Theme.TextMain
            });

            return objects;
        }

        public override Vec2 IslandSize()
        {
            return new Vec2(Math.Max(230, message.Length * 7.2f), 50);
        }
    }
}
