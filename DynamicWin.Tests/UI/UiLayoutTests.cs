using DynamicWin.UI;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;

namespace DynamicWin.Tests.UI;

public class UiLayoutTests
{
    [Fact]
    public void ResolvePosition_places_centered_object_relative_to_its_container()
    {
        var result = UiLayout.ResolvePosition(
            rawPosition: new Vec2(10, 5),
            size: new Vec2(20, 10),
            anchor: new Vec2(0.5f, 0.5f),
            alignment: UIAlignment.Center,
            containerPosition: new Vec2(100, 50),
            containerSize: new Vec2(200, 100));

        Assert.Equal(200, result.X);
        Assert.Equal(100, result.Y);
    }
}
