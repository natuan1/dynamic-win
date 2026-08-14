using DynamicWin.Utils;

namespace DynamicWin.UI;

public static class UiLayout
{
    public static Vec2 ResolvePosition(
        Vec2 rawPosition,
        Vec2 size,
        Vec2 anchor,
        UIAlignment alignment,
        Vec2 containerPosition,
        Vec2 containerSize)
    {
        var alignmentOffset = alignment switch
        {
            UIAlignment.TopLeft => new Vec2(0, 0),
            UIAlignment.TopCenter => new Vec2(0.5f, 0),
            UIAlignment.TopRight => new Vec2(1, 0),
            UIAlignment.MiddleLeft => new Vec2(0, 0.5f),
            UIAlignment.Center => new Vec2(0.5f, 0.5f),
            UIAlignment.MiddleRight => new Vec2(1, 0.5f),
            UIAlignment.BottomLeft => new Vec2(0, 1),
            UIAlignment.BottomCenter => new Vec2(0.5f, 1),
            UIAlignment.BottomRight => new Vec2(1, 1),
            _ => new Vec2(0, 0)
        };

        return containerPosition + rawPosition + (containerSize * alignmentOffset) - (size * anchor);
    }
}
