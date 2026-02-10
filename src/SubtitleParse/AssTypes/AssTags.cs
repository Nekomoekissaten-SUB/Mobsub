﻿using System;

namespace Mobsub.SubtitleParse.AssTypes;

public sealed class AssTagDescriptor(ReadOnlyMemory<byte> name, Type valueType, AssTagKind tagType)
{
    public ReadOnlyMemory<byte> Name = name;
    public Type ValueType  = valueType;
    public AssTagKind TagType = tagType;
}

[Flags]
public enum AssTagKind : byte
{
    BlockOnlyRenderLatest = 0,
    LineOnlyRenderFirst = 0b_1,
    Animateable = 0b_10,
    ShouldBeFunction = 0b_100,
    LineOnlyRenderLatest = 0b_1000,
    
    IsVsFilterMod = 0b_10000000,
    Ignored
}

public enum AssTag
{
    [AssTagSpec("1c", typeof(AssColor32), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    ColorPrimary,
    [AssTagSpec("2c", typeof(AssColor32), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    ColorSecondary,
    [AssTagSpec("3c", typeof(AssColor32), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    ColorBorder,
    [AssTagSpec("4c", typeof(AssColor32), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    ColorShadow,
    [AssTagSpec("c", typeof(AssColor32), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    ColorPrimaryAbbreviation,

    [AssTagSpec("1a", typeof(byte), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    AlphaPrimary,
    [AssTagSpec("2a", typeof(byte), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    AlphaSecondary,
    [AssTagSpec("3a", typeof(byte), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    AlphaBorder,
    [AssTagSpec("4a", typeof(byte), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    AlphaShadow,
    [AssTagSpec("alpha", typeof(byte), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    Alpha,

    [AssTagSpec("an", typeof(byte), AssTagKind.LineOnlyRenderFirst)]
    Alignment,
    [AssTagSpec("a", typeof(byte), AssTagKind.LineOnlyRenderFirst)]
    AlignmentLegacy,

    [AssTagSpec("be", typeof(double), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    BlueEdges,
    [AssTagSpec("blur", typeof(double), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    BlurEdgesGaussian,

    [AssTagSpec("bord", typeof(double), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    Border,
    [AssTagSpec("xbord", typeof(double), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    BorderX,
    [AssTagSpec("ybord", typeof(double), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    BorderY,

    [AssTagSpec("b", typeof(int), AssTagKind.BlockOnlyRenderLatest)]
    Bold,

    [AssTagSpec("clip", typeof(ReadOnlyMemory<byte>), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable | AssTagKind.ShouldBeFunction, AssTagFunctionKind.ClipRect)]
    Clip,
    [AssTagSpec("iclip", typeof(ReadOnlyMemory<byte>), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable | AssTagKind.ShouldBeFunction, AssTagFunctionKind.ClipRect)]
    InverseClip,

    [AssTagSpec("fade", typeof(ReadOnlyMemory<byte>), AssTagKind.Ignored, AssTagFunctionKind.Fade)]
    Fade,
    [AssTagSpec("fad", typeof(ReadOnlyMemory<byte>), AssTagKind.Ignored, AssTagFunctionKind.Fad)]
    Fad,

    [AssTagSpec("fax", typeof(double), AssTagKind.Ignored)]
    FontShiftX,
    [AssTagSpec("fay", typeof(double), AssTagKind.Ignored)]
    FontShiftY,

    [AssTagSpec("fe", typeof(int), AssTagKind.BlockOnlyRenderLatest)]
    FontEncoding,
    [AssTagSpec("fn", typeof(ReadOnlyMemory<byte>), AssTagKind.BlockOnlyRenderLatest)]
    FontName,

    [AssTagSpec("frx", typeof(double), AssTagKind.Ignored)]
    FontRotationX,
    [AssTagSpec("fry", typeof(double), AssTagKind.Ignored)]
    FontRotationY,
    [AssTagSpec("frz", typeof(double), AssTagKind.Ignored)]
    FontRotationZ,
    [AssTagSpec("fr", typeof(double), AssTagKind.Ignored)]
    FontRotationZSimple,

    [AssTagSpec("fscx", typeof(double), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    FontScaleX,
    [AssTagSpec("fscy", typeof(double), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    FontScaleY,
    [AssTagSpec("fsc", typeof(double), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    FontScale, // scale x/y together

    [AssTagSpec("fsp", typeof(double), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    FontSpacing,
    [AssTagSpec("fs", typeof(double), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    FontSize,
    [AssTagSpec("i", typeof(bool), AssTagKind.BlockOnlyRenderLatest)]
    Italic,

    [AssTagSpec("ko", typeof(int), AssTagKind.Ignored)]
    KaraokeO,
    [AssTagSpec("kf", typeof(int), AssTagKind.Ignored)]
    KaraokeF,
    [AssTagSpec("K", typeof(int), AssTagKind.Ignored)]
    KaraokeFSimple,
    [AssTagSpec("k", typeof(int), AssTagKind.Ignored)]
    Karaoke,
    [AssTagSpec("kt", typeof(int), AssTagKind.Ignored)]
    KaraokeT,

    [AssTagSpec("move", typeof(ReadOnlyMemory<byte>), AssTagKind.Ignored, AssTagFunctionKind.Move)]
    Movement,
    [AssTagSpec("org", typeof(ReadOnlyMemory<byte>), AssTagKind.Ignored, AssTagFunctionKind.Org)]
    OriginRotation,
    [AssTagSpec("pos", typeof(ReadOnlyMemory<byte>), AssTagKind.LineOnlyRenderFirst | AssTagKind.ShouldBeFunction, AssTagFunctionKind.Pos)]
    Position,

    [AssTagSpec("pbo", typeof(int), AssTagKind.BlockOnlyRenderLatest)]
    PolygonBaselineOffset,
    [AssTagSpec("p", typeof(int), AssTagKind.BlockOnlyRenderLatest)]
    Polygon,
    [AssTagSpec("q", typeof(byte), AssTagKind.LineOnlyRenderLatest)]
    WrapStyle,

    [AssTagSpec("r", typeof(ReadOnlyMemory<byte>), AssTagKind.BlockOnlyRenderLatest)]
    Reset, // rnd

    [AssTagSpec("shad", typeof(double), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    Shadow,
    [AssTagSpec("xshad", typeof(double), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    ShadowX,
    [AssTagSpec("yshad", typeof(double), AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
    ShadowY,

    [AssTagSpec("s", typeof(bool), AssTagKind.BlockOnlyRenderLatest)]
    Strikeout,
    [AssTagSpec("t", typeof(ReadOnlyMemory<byte>), AssTagKind.ShouldBeFunction, AssTagFunctionKind.Transform)]
    Transform,
    [AssTagSpec("u", typeof(bool), AssTagKind.BlockOnlyRenderLatest)]
    Underline

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
    Transform
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

}

public static partial class AssTagRegistry
{
    internal static bool TryGetObsoleteReplacement(AssTag tag, out ReadOnlySpan<byte> replacementName)
    {
        // \a is legacy alignment tag, replaced by \an.
        if (tag == AssTag.AlignmentLegacy)
        {
            replacementName = "an"u8;
            return true;
        }

        replacementName = default;
        return false;
    }

    internal static bool IsAlphaTag(AssTag tag)
        => tag is AssTag.Alpha or AssTag.AlphaPrimary or AssTag.AlphaSecondary or AssTag.AlphaBorder or AssTag.AlphaShadow;

    internal static bool TryMapLegacyAlignmentToAn(int legacyAlignment, out int anAlignment)
    {
        // Legacy \a uses a different numbering scheme than \an (numpad).
        // Mapping (SSA legacy) -> (\an numpad):
        // 1..3 = bottom row (same), 4..6 = top row, 7..9 = middle row.
        // => 4..6 => 7..9 (add 3), 7..9 => 4..6 (sub 3).
        if (legacyAlignment is >= 1 and <= 3)
        {
            anAlignment = legacyAlignment;
            return true;
        }

        if (legacyAlignment is >= 4 and <= 6)
        {
            anAlignment = legacyAlignment + 3;
            return true;
        }

        if (legacyAlignment is >= 7 and <= 9)
        {
            anAlignment = legacyAlignment - 3;
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

    public static bool TryGet(AssTag tag, out AssTagDescriptor? desc)
    {
        int i = (int)tag;
        if ((uint)i >= (uint)s_descByTag.Length)
        {
            desc = null;
            return false;
        }

        desc = s_descByTag[i];
        return desc != null;
    }

    public static bool TryMatch(ReadOnlySpan<byte> span, out AssTag tag, out AssTagDescriptor desc, out int matchedLength)
    {
        tag = default;
        desc = default!;
        matchedLength = 0;

        int node = 0;
        for (int i = 0; i < span.Length; i++)
        {
            int next = FindNext(node, span[i]);
            if (next < 0)
                break;

            node = next;
            int terminal = s_terminalTag[node];
            if (terminal >= 0)
            {
                tag = (AssTag)terminal;
                desc = s_descByTag[terminal]!;
                matchedLength = i + 1;
            }
        }

        return matchedLength > 0;
    }
}

