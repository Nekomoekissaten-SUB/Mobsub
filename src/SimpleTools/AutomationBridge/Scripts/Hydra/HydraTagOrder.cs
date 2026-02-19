using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.AutomationBridge.Scripts.Hydra;

internal static class HydraTagOrder
{
    // Ported from HYDRA's tag sort order (order="\\r\\an\\q...").
    //
    // We intentionally map only the tags that exist in the order list.
    // Tags not in the list remain in their original order (and are kept after the sorted head).
    public const int SlotCount = 42;

    public static int GetSlot(AssTag tag)
    {
        // Keep this in sync with hydra_chi.lua's `order` string:
        // \r\an\q\blur\be\fn\b\i\u\s\frz\fs\fscx\fscy\fad\fade\c\2c\3c\4c\alpha\1a\2a\3a\4a\bord\xbord\ybord\shad\xshad\yshad\fsp\frx\fry\fax\fay\org\pos\move\clip\iclip\p
        return tag switch
        {
            AssTag.Reset => 0,
            AssTag.Alignment => 1,
            AssTag.WrapStyle => 2,
            AssTag.BlurEdgesGaussian => 3,
            AssTag.BlueEdges => 4,
            AssTag.FontName => 5,
            AssTag.Bold => 6,
            AssTag.Italic => 7,
            AssTag.Underline => 8,
            AssTag.Strikeout => 9,
            AssTag.FontRotationZ => 10,
            AssTag.FontSize => 11,
            AssTag.FontScaleX => 12,
            AssTag.FontScaleY => 13,
            AssTag.Fad => 14,
            AssTag.Fade => 15,
            AssTag.ColorPrimaryAbbreviation => 16,
            AssTag.ColorPrimary => 16, // \1c is normalized to the same slot as \c in HYDRA.
            AssTag.ColorSecondary => 17,
            AssTag.ColorBorder => 18,
            AssTag.ColorShadow => 19,
            AssTag.Alpha => 20,
            AssTag.AlphaPrimary => 21,
            AssTag.AlphaSecondary => 22,
            AssTag.AlphaBorder => 23,
            AssTag.AlphaShadow => 24,
            AssTag.Border => 25,
            AssTag.BorderX => 26,
            AssTag.BorderY => 27,
            AssTag.Shadow => 28,
            AssTag.ShadowX => 29,
            AssTag.ShadowY => 30,
            AssTag.FontSpacing => 31,
            AssTag.FontRotationX => 32,
            AssTag.FontRotationY => 33,
            AssTag.FontShiftX => 34,
            AssTag.FontShiftY => 35,
            AssTag.OriginRotation => 36,
            AssTag.Position => 37,
            AssTag.Movement => 38,
            AssTag.Clip => 39,
            AssTag.InverseClip => 40,
            AssTag.Polygon => 41,
            _ => -1
        };
    }
}

