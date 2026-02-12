﻿using System;

using System.Runtime.CompilerServices;

namespace Mobsub.SubtitleParse.AssTypes;

[Flags]
public enum AssTagKind : byte
{
    BlockOnlyRenderLatest = 0,
    LineOnlyRenderFirst = 0b_0000_0001,
    Animateable = 0b_0000_0010,
    ShouldBeFunction = 0b_0000_0100,
    LineOnlyRenderLatest = 0b_0000_1000,
    Ignored = 0b_0001_0000,

    IsVsFilterMod = 0b_1000_0000
}

public enum AssTag
{
    [AssTagSpec("1c", AssTagValueKind.Color, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    ColorPrimary,
    [AssTagSpec("2c", AssTagValueKind.Color, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    ColorSecondary,
    [AssTagSpec("3c", AssTagValueKind.Color, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    ColorBorder,
    [AssTagSpec("4c", AssTagValueKind.Color, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    ColorShadow,
    [AssTagSpec("c", AssTagValueKind.Color, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    ColorPrimaryAbbreviation,

    [AssTagSpec("1a", AssTagValueKind.Byte, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    AlphaPrimary,
    [AssTagSpec("2a", AssTagValueKind.Byte, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    AlphaSecondary,
    [AssTagSpec("3a", AssTagValueKind.Byte, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    AlphaBorder,
    [AssTagSpec("4a", AssTagValueKind.Byte, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    AlphaShadow,
    [AssTagSpec("alpha", AssTagValueKind.Byte, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    Alpha,

    [AssTagSpec("an", AssTagValueKind.Byte, AssTagKind.LineOnlyRenderFirst,
        IntMin = 1, IntMax = 9,
        IntRangeDiagnosticCode = "ass.override.alignRange",
        IntRangeMessage = "Alignment should be in [1..9].")]
    Alignment,
    [AssTagSpec("a", AssTagValueKind.Byte, AssTagKind.LineOnlyRenderFirst,
        ObsoleteReplacementName = "an",
        IntAllowedMask = 0xEEE,
        IntAllowedMaskDiagnosticCode = "ass.override.alignLegacyAllowed",
        IntAllowedMaskMessage = "Legacy alignment (\\\\a) valid values are 1..3, 5..7, 9..11.",
        IntMin = 1, IntMax = 11,
        IntRangeDiagnosticCode = "ass.override.alignRange",
        IntRangeMessage = "Legacy alignment (\\\\a) should be in [1..11] (excluding 4,8).")]
    AlignmentLegacy,

    [AssTagSpec("be", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable,
        DoubleMin = 0,
        DoubleRangeDiagnosticCode = "ass.override.blurRange",
        DoubleRangeMessage = "Blur value should be >= 0.")]
    BlueEdges,
    [AssTagSpec("blur", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable,
        DoubleMin = 0,
        DoubleRangeDiagnosticCode = "ass.override.blurRange",
        DoubleRangeMessage = "Blur value should be >= 0.")]
    BlurEdgesGaussian,

    [AssTagSpec("bord", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable,
        DoubleMin = 0,
        DoubleRangeDiagnosticCode = "ass.override.borderRange",
        DoubleRangeMessage = "Border value should be >= 0.")]
    Border,
    [AssTagSpec("xbord", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable,
        DoubleMin = 0,
        DoubleRangeDiagnosticCode = "ass.override.borderRange",
        DoubleRangeMessage = "Border value should be >= 0.")]
    BorderX,
    [AssTagSpec("ybord", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable,
        DoubleMin = 0,
        DoubleRangeDiagnosticCode = "ass.override.borderRange",
        DoubleRangeMessage = "Border value should be >= 0.")]
    BorderY,

    [AssTagSpec("b", AssTagValueKind.Int, AssTagKind.BlockOnlyRenderLatest,
        IntMin = -1, IntMax = 1000,
        IntRangeDiagnosticCode = "ass.override.boldWeightRange",
        IntRangeMessage = "Bold weight (\\\\b) should be in [-1..1000] (0=off, 1/-1=on, >1=weight).")]
    Bold,

    [AssTagSpec("clip", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable | AssTagKind.ShouldBeFunction, AssTagFunctionKind.ClipRect)]
    Clip,
    [AssTagSpec("iclip", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable | AssTagKind.ShouldBeFunction, AssTagFunctionKind.ClipRect)]
    InverseClip,

    [AssTagSpec("fade", AssTagValueKind.Function, AssTagKind.Ignored | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Fade)]
    Fade,
    [AssTagSpec("fad", AssTagValueKind.Function, AssTagKind.Ignored | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Fad)]
    Fad,

    [AssTagSpec("fax", AssTagValueKind.Double, AssTagKind.Ignored)]
    FontShiftX,
    [AssTagSpec("fay", AssTagValueKind.Double, AssTagKind.Ignored)]
    FontShiftY,

    [AssTagSpec("fe", AssTagValueKind.Int, AssTagKind.BlockOnlyRenderLatest,
        IntMin = 0, IntMax = 255,
        IntRangeDiagnosticCode = "ass.override.fontEncodingRange",
        IntRangeMessage = "Font encoding (\\\\fe) should be in [0..255].")]
    FontEncoding,
    [AssTagSpec("fn", AssTagValueKind.Bytes, AssTagKind.BlockOnlyRenderLatest)]
    FontName,

    [AssTagSpec("frx", AssTagValueKind.Double, AssTagKind.Ignored,
        DoubleMin = -360, DoubleMax = 360,
        DoubleRangeDiagnosticCode = "ass.override.rotationRange",
        DoubleRangeMessage = "Rotation should be in [-360..360].")]
    FontRotationX,
    [AssTagSpec("fry", AssTagValueKind.Double, AssTagKind.Ignored,
        DoubleMin = -360, DoubleMax = 360,
        DoubleRangeDiagnosticCode = "ass.override.rotationRange",
        DoubleRangeMessage = "Rotation should be in [-360..360].")]
    FontRotationY,
    [AssTagSpec("frz", AssTagValueKind.Double, AssTagKind.Ignored,
        DoubleMin = -360, DoubleMax = 360,
        DoubleRangeDiagnosticCode = "ass.override.rotationRange",
        DoubleRangeMessage = "Rotation should be in [-360..360].")]
    FontRotationZ,
    [AssTagSpec("fr", AssTagValueKind.Double, AssTagKind.Ignored,
        DoubleMin = -360, DoubleMax = 360,
        DoubleRangeDiagnosticCode = "ass.override.rotationRange",
        DoubleRangeMessage = "Rotation should be in [-360..360].")]
    FontRotationZSimple,

    [AssTagSpec("fscx", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable,
        DoubleMin = 0,
        DoubleRangeDiagnosticCode = "ass.override.scaleRange",
        DoubleRangeMessage = "Scale value should be >= 0.")]
    FontScaleX,
    [AssTagSpec("fscy", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable,
        DoubleMin = 0,
        DoubleRangeDiagnosticCode = "ass.override.scaleRange",
        DoubleRangeMessage = "Scale value should be >= 0.")]
    FontScaleY,
    [AssTagSpec("fsc", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable,
        SpecialRule = AssTagSpecialRule.FontScaleFsc,
        DoubleMin = 0,
        DoubleRangeDiagnosticCode = "ass.override.scaleRange",
        DoubleRangeMessage = "Scale value should be >= 0.")]
    FontScale, // reset (VSFilter/libass); VSFilterMod overload: fsc<scale>

    [AssTagSpec("fsp", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    FontSpacing,
    [AssTagSpec("fs", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    FontSize,
    [AssTagSpec("i", AssTagValueKind.Bool, AssTagKind.BlockOnlyRenderLatest,
        IntMin = -1, IntMax = 1,
        IntRangeDiagnosticCode = "ass.override.boolRange",
        IntRangeMessage = "Bool tag value should be -1, 0 or 1.")]
    Italic,

    [AssTagSpec("ko", AssTagValueKind.Int, AssTagKind.Ignored,
        IntMin = 0,
        IntRangeDiagnosticCode = "ass.override.karaokeRange",
        IntRangeMessage = "Karaoke duration must be >= 0.")]
    KaraokeO,
    [AssTagSpec("kf", AssTagValueKind.Int, AssTagKind.Ignored,
        IntMin = 0,
        IntRangeDiagnosticCode = "ass.override.karaokeRange",
        IntRangeMessage = "Karaoke duration must be >= 0.")]
    KaraokeF,
    [AssTagSpec("K", AssTagValueKind.Int, AssTagKind.Ignored,
        IntMin = 0,
        IntRangeDiagnosticCode = "ass.override.karaokeRange",
        IntRangeMessage = "Karaoke duration must be >= 0.")]
    KaraokeFSimple,
    [AssTagSpec("k", AssTagValueKind.Int, AssTagKind.Ignored,
        IntMin = 0,
        IntRangeDiagnosticCode = "ass.override.karaokeRange",
        IntRangeMessage = "Karaoke duration must be >= 0.")]
    Karaoke,
    [AssTagSpec("kt", AssTagValueKind.Int, AssTagKind.Ignored,
        IntMin = 0,
        IntRangeDiagnosticCode = "ass.override.karaokeRange",
        IntRangeMessage = "Karaoke duration must be >= 0.")]
    KaraokeT,

    [AssTagSpec("move", AssTagValueKind.Function, AssTagKind.Ignored | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Move)]
    Movement,
    [AssTagSpec("org", AssTagValueKind.Function, AssTagKind.Ignored | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Org)]
    OriginRotation,
    [AssTagSpec("pos", AssTagValueKind.Function, AssTagKind.LineOnlyRenderFirst | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Pos)]
    Position,

    [AssTagSpec("pbo", AssTagValueKind.Int, AssTagKind.BlockOnlyRenderLatest)]
    PolygonBaselineOffset,
    [AssTagSpec("p", AssTagValueKind.Int, AssTagKind.BlockOnlyRenderLatest,
        IntMin = 0,
        IntRangeDiagnosticCode = "ass.override.polygonModeRange",
        IntRangeMessage = "Polygon mode (\\\\p) should be >= 0.")]
    Polygon,
    [AssTagSpec("q", AssTagValueKind.Byte, AssTagKind.LineOnlyRenderLatest,
        IntMin = 0, IntMax = 3,
        IntRangeDiagnosticCode = "ass.override.wrapStyleRange",
        IntRangeMessage = "WrapStyle (\\\\q) should be in [0..3].")]
    WrapStyle,

    [AssTagSpec("r", AssTagValueKind.Bytes, AssTagKind.BlockOnlyRenderLatest)]
    Reset, // rnd

    [AssTagSpec("shad", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable,
        DoubleMin = 0,
        DoubleRangeDiagnosticCode = "ass.override.shadowRange",
        DoubleRangeMessage = "Shadow value should be >= 0.")]
    Shadow,
    [AssTagSpec("xshad", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable,
        DoubleMin = 0,
        DoubleRangeDiagnosticCode = "ass.override.shadowRange",
        DoubleRangeMessage = "Shadow value should be >= 0.")]
    ShadowX,
    [AssTagSpec("yshad", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable,
        DoubleMin = 0,
        DoubleRangeDiagnosticCode = "ass.override.shadowRange",
        DoubleRangeMessage = "Shadow value should be >= 0.")]
    ShadowY,

    [AssTagSpec("s", AssTagValueKind.Bool, AssTagKind.BlockOnlyRenderLatest,
        IntMin = -1, IntMax = 1,
        IntRangeDiagnosticCode = "ass.override.boolRange",
        IntRangeMessage = "Bool tag value should be -1, 0 or 1.")]
    Strikeout,
    [AssTagSpec("t", AssTagValueKind.Function, AssTagKind.ShouldBeFunction, AssTagFunctionKind.Transform)]
    Transform,
    [AssTagSpec("u", AssTagValueKind.Bool, AssTagKind.BlockOnlyRenderLatest,
        IntMin = -1, IntMax = 1,
        IntRangeDiagnosticCode = "ass.override.boolRange",
        IntRangeMessage = "Bool tag value should be -1, 0 or 1.")]
    Underline,

    // --- VSFilterMod tags (only recognized in mod_mode) ---

    [AssTagSpec("ortho", AssTagValueKind.Bool, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod,
        IntMin = 0, IntMax = 1,
        IntRangeDiagnosticCode = "ass.override.bool01Range",
        IntRangeMessage = "Bool tag value should be 0 or 1.")]
    Ortho,

    [AssTagSpec("z", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.Animateable)]
    ZDepth,

    [AssTagSpec("xblur", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.Animateable,
        DoubleMin = 0,
        DoubleRangeDiagnosticCode = "ass.override.blurRange",
        DoubleRangeMessage = "Blur value should be >= 0.")]
    BlurX,
    [AssTagSpec("yblur", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.Animateable,
        DoubleMin = 0,
        DoubleRangeDiagnosticCode = "ass.override.blurRange",
        DoubleRangeMessage = "Blur value should be >= 0.")]
    BlurY,

    [AssTagSpec("frs", AssTagValueKind.Double, AssTagKind.Ignored | AssTagKind.IsVsFilterMod | AssTagKind.Animateable,
        DoubleMin = -360, DoubleMax = 360,
        DoubleRangeDiagnosticCode = "ass.override.rotationRange",
        DoubleRangeMessage = "Rotation should be in [-360..360].")]
    FontRotationS,

    [AssTagSpec("fsvp", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.Animateable)]
    FontSpacingVertical,
    [AssTagSpec("fshp", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.Animateable)]
    FontSpacingHorizontalParagraph,

    [AssTagSpec("blend", AssTagValueKind.Bytes, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod,
        SpecialRule = AssTagSpecialRule.BlendMode,
        BytesAllowedKeywords = new[] { "over", "add", "sub", "mult", "scr", "diff", "rsub", "isub" })]
    BlendMode,

    [AssTagSpec("distort", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Distort)]
    Distort,
    [AssTagSpec("jitter", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Jitter)]
    Jitter,
    [AssTagSpec("mover", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Mover)]
    Mover,
    [AssTagSpec("moves3", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Moves3)]
    Moves3,
    [AssTagSpec("moves4", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Moves4)]
    Moves4,
    [AssTagSpec("movevc", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.MoveVC)]
    MoveVC,

    [AssTagSpec("rndx", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.Animateable)]
    RandomX,
    [AssTagSpec("rndy", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.Animateable)]
    RandomY,
    [AssTagSpec("rndz", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.Animateable)]
    RandomZ,
    [AssTagSpec("rnd", AssTagValueKind.Double, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.Animateable)]
    Random,
    [AssTagSpec("rnds", AssTagValueKind.Int, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.Animateable,
        SpecialRule = AssTagSpecialRule.HexInt32)]
    RandomSeed,

    [AssTagSpec("lua", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Lua)]
    Lua,

    [AssTagSpec("1img", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Img)]
    Image1,
    [AssTagSpec("2img", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Img)]
    Image2,
    [AssTagSpec("3img", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Img)]
    Image3,
    [AssTagSpec("4img", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Img)]
    Image4,

    [AssTagSpec("1vc", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Vc)]
    GradientColor1,
    [AssTagSpec("2vc", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Vc)]
    GradientColor2,
    [AssTagSpec("3vc", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Vc)]
    GradientColor3,
    [AssTagSpec("4vc", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Vc)]
    GradientColor4,

    [AssTagSpec("1va", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Va)]
    GradientAlpha1,
    [AssTagSpec("2va", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Va)]
    GradientAlpha2,
    [AssTagSpec("3va", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Va)]
    GradientAlpha3,
    [AssTagSpec("4va", AssTagValueKind.Function, AssTagKind.BlockOnlyRenderLatest | AssTagKind.IsVsFilterMod | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Va)]
    GradientAlpha4

    // https://sourceforge.net/p/guliverkli2/code/HEAD/tree/src/subtitles/RTS.cpp#l1383
    // libass ass_types.h ass_render.h ass_parse.c
}

public enum AssTagValueKind : byte
{
    None = 0,
    Int,
    Double,
    Bool,
    Byte,
    Color,
    Bytes,
    Function
}

public enum AssTagFunctionKind : byte
{
    None = 0,
    Pos,
    Org,
    Move,
    Fade,
    Fad,
    ClipRect,
    ClipDrawing,
    Transform,

    // VSFilterMod
    Distort,
    Jitter,
    Mover,
    Moves3,
    Moves4,
    MoveVC,
    Lua,
    Img,
    Vc,
    Va
}

public readonly struct AssTagFunctionValue
{
    public AssTagFunctionKind Kind { get; init; }
    public double X1 { get; init; }
    public double Y1 { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
    public int A1 { get; init; }
    public int A2 { get; init; }
    public int A3 { get; init; }
    public int T1 { get; init; }
    public int T2 { get; init; }
    public int T3 { get; init; }
    public int T4 { get; init; }
    public int Scale { get; init; }
    public bool HasTimes { get; init; }
    public bool HasAccel { get; init; }
    public double Accel { get; init; }
    public ReadOnlyMemory<byte> Drawing { get; init; }
    public ReadOnlyMemory<byte> TagPayload { get; init; }
}

public readonly struct AssTagValue
{
    public AssTagValueKind Kind { get; init; }
    public int IntValue { get; init; }
    public double DoubleValue { get; init; }
    public bool BoolValue { get; init; }
    public byte ByteValue { get; init; }
    public AssColor32 ColorValue { get; init; }
    public ReadOnlyMemory<byte> BytesValue { get; init; }
    public AssTagFunctionValue FunctionValue { get; init; }

    public static AssTagValue FromInt(int v) => new() { Kind = AssTagValueKind.Int, IntValue = v };
    public static AssTagValue FromDouble(double v) => new() { Kind = AssTagValueKind.Double, DoubleValue = v };
    public static AssTagValue FromBool(bool v) => new() { Kind = AssTagValueKind.Bool, BoolValue = v };
    public static AssTagValue FromByte(byte v) => new() { Kind = AssTagValueKind.Byte, ByteValue = v };
    public static AssTagValue FromColor(AssColor32 v) => new() { Kind = AssTagValueKind.Color, ColorValue = v };
    public static AssTagValue FromBytes(ReadOnlyMemory<byte> v) => new() { Kind = AssTagValueKind.Bytes, BytesValue = v };
    public static AssTagValue FromFunction(AssTagFunctionValue v) => new() { Kind = AssTagValueKind.Function, FunctionValue = v };
    public static AssTagValue Empty => new() { Kind = AssTagValueKind.None };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet<T>(out T result)
    {
        if (typeof(T) == typeof(int) && Kind == AssTagValueKind.Int)
        {
            int v = IntValue;
            result = Unsafe.As<int, T>(ref v);
            return true;
        }
        if (typeof(T) == typeof(double) && Kind == AssTagValueKind.Double)
        {
            double v = DoubleValue;
            result = Unsafe.As<double, T>(ref v);
            return true;
        }
        if (typeof(T) == typeof(bool) && Kind == AssTagValueKind.Bool)
        {
            bool v = BoolValue;
            result = Unsafe.As<bool, T>(ref v);
            return true;
        }
        if (typeof(T) == typeof(byte) && Kind == AssTagValueKind.Byte)
        {
            byte v = ByteValue;
            result = Unsafe.As<byte, T>(ref v);
            return true;
        }
        if (typeof(T) == typeof(AssColor32) && Kind == AssTagValueKind.Color)
        {
            AssColor32 v = ColorValue;
            result = Unsafe.As<AssColor32, T>(ref v);
            return true;
        }
        if (typeof(T) == typeof(ReadOnlyMemory<byte>) && Kind == AssTagValueKind.Bytes)
        {
            ReadOnlyMemory<byte> v = BytesValue;
            result = Unsafe.As<ReadOnlyMemory<byte>, T>(ref v);
            return true;
        }
        if (typeof(T) == typeof(AssTagFunctionValue) && Kind == AssTagValueKind.Function)
        {
            AssTagFunctionValue v = FunctionValue;
            result = Unsafe.As<AssTagFunctionValue, T>(ref v);
            return true;
        }

        result = default!;
        return false;
    }

}

public static partial class AssTagRegistry
{
    public static ReadOnlySpan<byte> GetNameBytes(AssTag tag)
    {
        int i = (int)tag;
        if ((uint)i >= (uint)s_nameLenByTag.Length)
            return default;

        int len = s_nameLenByTag[i];
        if (len == 0)
            return default;

        int start = s_nameStartByTag[i];
        return s_nameBytes.AsSpan(start, len);
    }

    internal static bool TryGetTagKind(AssTag tag, out AssTagKind kind)
    {
        int i = (int)tag;
        if ((uint)i >= (uint)s_tagKindByTag.Length)
        {
            kind = default;
            return false;
        }

        kind = (AssTagKind)s_tagKindByTag[i];
        return true;
    }

    internal static bool TryGetSpecialRule(AssTag tag, out AssTagSpecialRule rule)
    {
        int i = (int)tag;
        if ((uint)i >= (uint)s_specialRuleByTag.Length)
        {
            rule = default;
            return false;
        }

        rule = (AssTagSpecialRule)s_specialRuleByTag[i];
        return rule != AssTagSpecialRule.None;
    }

    internal static bool TryGetAllowedKeywords(AssTag tag, out ReadOnlySpan<byte> keywords)
    {
        int i = (int)tag;
        if ((uint)i >= (uint)s_allowedKeywordLenByTag.Length)
        {
            keywords = default;
            return false;
        }

        int len = s_allowedKeywordLenByTag[i];
        if (len == 0)
        {
            keywords = default;
            return false;
        }

        int start = s_allowedKeywordStartByTag[i];
        keywords = s_allowedKeywordBytes.AsSpan(start, len);
        return true;
    }

    internal static bool TryGetObsoleteReplacement(AssTag tag, out ReadOnlySpan<byte> replacementName)
    {
        int i = (int)tag;
        if ((uint)i >= (uint)s_obsoleteReplacementNameLenByTag.Length)
        {
            replacementName = default;
            return false;
        }

        int len = s_obsoleteReplacementNameLenByTag[i];
        if (len == 0)
        {
            replacementName = default;
            return false;
        }

        int start = s_obsoleteReplacementNameStartByTag[i];
        replacementName = s_obsoleteReplacementNameBytes.AsSpan(start, len);
        return true;
    }

    internal static bool TryGetIntAllowedMask(AssTag tag, out ulong mask, out string? code, out string? message)
    {
        int i = (int)tag;
        if ((uint)i >= (uint)s_intAllowedMaskByTag.Length)
        {
            mask = 0;
            code = null;
            message = null;
            return false;
        }

        mask = s_intAllowedMaskByTag[i];
        if (mask == 0)
        {
            code = null;
            message = null;
            return false;
        }

        code = s_intAllowedMaskCodeByTag[i];
        message = s_intAllowedMaskMessageByTag[i];
        return true;
    }

    internal static bool TryGetValueKind(AssTag tag, out AssTagValueKind kind)
    {
        int i = (int)tag;
        if ((uint)i >= (uint)s_valueKindByTag.Length)
        {
            kind = default;
            return false;
        }

        kind = (AssTagValueKind)s_valueKindByTag[i];
        return kind != AssTagValueKind.None;
    }

    internal static bool IsAlphaTag(AssTag tag)
    {
        int i = (int)tag;
        return (uint)i < (uint)s_isAlphaTagByTag.Length && s_isAlphaTagByTag[i] != 0;
    }

    internal static bool TryGetIntRange(AssTag tag, out int min, out int max, out string? code, out string? message)
    {
        int i = (int)tag;
        if ((uint)i >= (uint)s_intMinByTag.Length)
        {
            min = max = 0;
            code = null;
            message = null;
            return false;
        }

        code = s_intRangeCodeByTag[i];
        message = s_intRangeMessageByTag[i];
        if (code == null)
        {
            min = max = 0;
            return false;
        }

        min = s_intMinByTag[i];
        max = s_intMaxByTag[i];
        return true;
    }

    internal static bool TryGetDoubleRange(AssTag tag, out double min, out double max, out string? code, out string? message)
    {
        int i = (int)tag;
        if ((uint)i >= (uint)s_doubleMinByTag.Length)
        {
            min = max = 0;
            code = null;
            message = null;
            return false;
        }

        code = s_doubleRangeCodeByTag[i];
        message = s_doubleRangeMessageByTag[i];
        if (code == null)
        {
            min = max = 0;
            return false;
        }

        min = s_doubleMinByTag[i];
        max = s_doubleMaxByTag[i];
        return true;
    }

    internal static bool TryMapLegacyAlignmentToAn(int legacyAlignment, out int anAlignment)
    {
        // SSA legacy \a numbering (used by old VSFilter/SSA):
        // - 1..3: bottom left/center/right
        // - 5..7: top left/center/right (add 4 from bottom row)
        // - 9..11: middle left/center/right (add 8 from bottom row)
        // Note: 4 and 8 are unused/invalid.
        if (legacyAlignment is >= 1 and <= 3)
        {
            anAlignment = legacyAlignment;
            return true;
        }

        if (legacyAlignment is >= 5 and <= 7)
        {
            // 5..7 => 7..9
            anAlignment = legacyAlignment + 2;
            return true;
        }

        if (legacyAlignment is >= 9 and <= 11)
        {
            // 9..11 => 4..6
            anAlignment = legacyAlignment - 5;
            return true;
        }

        anAlignment = 0;
        return false;
    }

    internal static string? GetFunctionSignature(AssTagFunctionKind kind)
        => kind switch
        {
            AssTagFunctionKind.Pos => "pos(x, y)",
            AssTagFunctionKind.Org => "org(x, y)",
            AssTagFunctionKind.Move => "move(x1, y1, x2, y2[, t1, t2])",
            AssTagFunctionKind.Fade => "fade(a1, a2, a3, t1, t2, t3, t4)",
            AssTagFunctionKind.Fad => "fad(t1, t2)",
            AssTagFunctionKind.ClipRect => "clip(x1, y1, x2, y2) | clip(scale, drawing) | clip(drawing)",
            AssTagFunctionKind.ClipDrawing => "clip(scale, drawing) | clip(drawing)",
            AssTagFunctionKind.Transform => "t([t1, t2,][accel,] \\tags)",

            // VSFilterMod
            AssTagFunctionKind.Distort => "distort(u1, v1, u2, v2, u3, v3)",
            AssTagFunctionKind.Jitter => "jitter(left, right, up, down, period[, seed])",
            AssTagFunctionKind.Mover => "mover(x1, y1, x2, y2, angle1, angle2, radius1, radius2[, t1, t2])",
            AssTagFunctionKind.Moves3 => "moves3(x1, y1, x2, y2, x3, y3[, t1, t2])",
            AssTagFunctionKind.Moves4 => "moves4(x1, y1, x2, y2, x3, y3, x4, y4[, t1, t2])",
            AssTagFunctionKind.MoveVC => "movevc(x1, y1[, x2, y2[, t1, t2]])",
            AssTagFunctionKind.Lua => "lua(method, args...)",
            AssTagFunctionKind.Img => "Nimg(path[, xoffset, yoffset])",
            AssTagFunctionKind.Vc => "Nvc(c1, c2, c3, c4)",
            AssTagFunctionKind.Va => "Nva(a1, a2, a3, a4)",
            _ => null
        };

    internal static bool TryGetFunctionKind(AssTag tag, out AssTagFunctionKind kind)
    {
        int i = (int)tag;
        if ((uint)i >= (uint)s_functionKindByTag.Length)
        {
            kind = default;
            return false;
        }

        kind = (AssTagFunctionKind)s_functionKindByTag[i];
        return kind != AssTagFunctionKind.None;
    }

    public static bool TryMatch(ReadOnlySpan<byte> span, out AssTag tag, out int matchedLength)
        => TryMatch(span, options: default, out tag, out matchedLength);

    public static bool TryMatch(ReadOnlySpan<byte> span, in AssTextOptions options, out AssTag tag, out int matchedLength)
        => TryMatch(span, options, out tag, out matchedLength, out _);

    internal static bool TryMatch(ReadOnlySpan<byte> span, in AssTextOptions options, out AssTag tag, out int matchedLength, out int gatedMatchedLength)
    {
        tag = default;
        matchedLength = 0;
        gatedMatchedLength = 0;

        int node = 0;
        for (int i = 0; i < span.Length; i++)
        {
            int next = FindNext(node, span[i]);
            if (next < 0)
                break;

            node = next;
            int terminal = s_terminalTag[node];
            if (terminal < 0)
                continue;

            // Dialect gating: VSFilterMod tags are only recognized when Mod mode is enabled.
            if (!options.ModMode && ((AssTagKind)s_tagKindByTag[terminal] & AssTagKind.IsVsFilterMod) != 0)
            {
                int len = i + 1;
                if (len > gatedMatchedLength)
                    gatedMatchedLength = len;
                continue;
            }

            tag = (AssTag)terminal;
            matchedLength = i + 1;
        }

        return matchedLength > 0;
    }
}
