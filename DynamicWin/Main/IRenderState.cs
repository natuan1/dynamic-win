using DynamicWin.Utils;

namespace DynamicWin.Main;

internal interface IRenderState
{
    float BlurOverride { get; set; }
    float AlphaOverride { get; set; }
    Vec2 ScaleOffset { get; set; }
}
