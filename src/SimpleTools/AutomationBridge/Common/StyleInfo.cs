#nullable enable

using MessagePack;

namespace Mobsub.AutomationBridge.Common;

[LuaPackMode(LuaPackMode.Default)]
[MessagePackObject(AllowPrivate = true)]
internal readonly partial record struct StyleInfo(
    [property: Key(0)] int Align = 7,
    [property: Key(1)] int MarginL = 0,
    [property: Key(2)] int MarginR = 0,
    [property: Key(3)] int MarginT = 0,
    [property: Key(4)] double ScaleX = 100,
    [property: Key(5), LuaAltKeys("scale_x")] double ScaleY = 100,
    [property: Key(6)] double Outline = 0,
    [property: Key(7)] double Shadow = 0,
    [property: Key(8)] double Angle = 0
);

