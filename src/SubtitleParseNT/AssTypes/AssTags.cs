using System;
using System.Buffers.Text;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Mobsub.SubtitleParseNT2.AssTypes;

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
    ColorPrimary, ColorSecondary, ColorBorder, ColorShadow, ColorPrimaryAbbreviation,
    AlphaPrimary, AlphaSecondary, AlphaBorder, AlphaShadow, Alpha,
    Alignment, AlignmentLegacy,
    BlueEdges, BlurEdgesGaussian,
    Border, BorderX, BorderY,
    Bold,
    Clip, InverseClip,
    Fade, Fad,
    FontShiftX, FontShiftY,
    FontEncoding, FontName,
    FontRotationX, FontRotationY, FontRotationZ, FontRotationZSimple,
    FontScaleX, FontScaleY, // FontSizeScale
    FontSpacing, FontSize, Italic,
    KaraokeO, KaraokeF, KaraokeFSimple, Karaoke, // KaraokeT
    Movement, OriginRotation, Position,
    PolygonBaselineOffset, Polygon, WrapStyle,
    Reset, // rnd
    Shadow, ShadowX, ShadowY,
    Strikeout, Transform, Underline

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
    Bytes
}

public readonly struct AssTagValue
{
    public AssTagValueKind Kind { get; init; }
    public int IntValue { get; init; }
    public double DoubleValue { get; init; }
    public bool BoolValue { get; init; }
    public byte ByteValue { get; init; }
    public AssRGB8 ColorValue { get; init; }
    public ReadOnlyMemory<byte> BytesValue { get; init; }

    public static AssTagValue FromInt(int v) => new() { Kind = AssTagValueKind.Int, IntValue = v };
    public static AssTagValue FromDouble(double v) => new() { Kind = AssTagValueKind.Double, DoubleValue = v };
    public static AssTagValue FromBool(bool v) => new() { Kind = AssTagValueKind.Bool, BoolValue = v };
    public static AssTagValue FromByte(byte v) => new() { Kind = AssTagValueKind.Byte, ByteValue = v };
    public static AssTagValue FromColor(AssRGB8 v) => new() { Kind = AssTagValueKind.Color, ColorValue = v };
    public static AssTagValue FromBytes(ReadOnlyMemory<byte> v) => new() { Kind = AssTagValueKind.Bytes, BytesValue = v };
    public static AssTagValue Empty => new() { Kind = AssTagValueKind.None };

}

public static class AssTagRegistry
{
    private struct UnImp { private string v; };
    private static readonly FrozenDictionary<AssTag, AssTagDescriptor> tags = FrozenDictionary.ToFrozenDictionary(
        new Dictionary<AssTag, AssTagDescriptor>
        {
            [AssTag.ColorPrimary] = new("1c"u8.ToArray(), typeof(AssRGB8),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.ColorSecondary] = new("2c"u8.ToArray(), typeof(AssRGB8),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.ColorBorder] = new("3c"u8.ToArray(), typeof(AssRGB8),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.ColorShadow] = new("4c"u8.ToArray(), typeof(AssRGB8),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.AlphaPrimary] = new("1a"u8.ToArray(), typeof(AssRGB8),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.AlphaSecondary] = new("2a"u8.ToArray(), typeof(AssRGB8),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.AlphaBorder] = new("3a"u8.ToArray(), typeof(AssRGB8),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.AlphaShadow] = new("4a"u8.ToArray(), typeof(AssRGB8),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.Alpha] = new("alpha"u8.ToArray(), typeof(AssRGB8),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.Alignment] = new("an"u8.ToArray(), typeof(byte),
                AssTagKind.LineOnlyRenderFirst),
            [AssTag.AlignmentLegacy] = new("a"u8.ToArray(), typeof(byte),
                AssTagKind.LineOnlyRenderFirst),
            [AssTag.BlueEdges] = new("be"u8.ToArray(), typeof(double),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.BlurEdgesGaussian] = new("blur"u8.ToArray(), typeof(double),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.Border] = new("bord"u8.ToArray(), typeof(double),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.BorderX] = new("xbord"u8.ToArray(), typeof(double),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.BorderY] = new("ybord"u8.ToArray(), typeof(double),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.Bold] = new("b"u8.ToArray(), typeof(int),
                AssTagKind.BlockOnlyRenderLatest),
            [AssTag.Clip] = new("clip"u8.ToArray(), typeof(UnImp),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable | AssTagKind.ShouldBeFunction),
            [AssTag.InverseClip] = new("iclip"u8.ToArray(), typeof(UnImp),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable | AssTagKind.ShouldBeFunction),
            [AssTag.ColorPrimaryAbbreviation] = new("c"u8.ToArray(), typeof(AssRGB8),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.Fade] = new("fade"u8.ToArray(), typeof(UnImp),
                AssTagKind.Ignored),
            [AssTag.Fad] = new("fad"u8.ToArray(), typeof(UnImp), // double[]
                AssTagKind.Ignored),
            [AssTag.FontShiftX] = new("fax"u8.ToArray(), typeof(double),
                AssTagKind.Ignored),
            [AssTag.FontShiftY] = new("fay"u8.ToArray(), typeof(double),
                AssTagKind.Ignored),
            [AssTag.FontEncoding] = new("fe"u8.ToArray(), typeof(int),
                AssTagKind.BlockOnlyRenderLatest),
            [AssTag.FontName] = new("fn"u8.ToArray(), typeof(ReadOnlyMemory<byte>),
                AssTagKind.BlockOnlyRenderLatest),
            [AssTag.FontRotationX] = new("frx"u8.ToArray(), typeof(double),
                AssTagKind.Ignored),
            [AssTag.FontRotationY] = new("fry"u8.ToArray(), typeof(double),
                AssTagKind.Ignored),
            [AssTag.FontRotationZ] = new("frz"u8.ToArray(), typeof(double),
                AssTagKind.Ignored),
            [AssTag.FontRotationZSimple] = new("fr"u8.ToArray(), typeof(double),
                AssTagKind.Ignored),
            [AssTag.FontScaleX] = new("fscx"u8.ToArray(), typeof(double),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.FontScaleY] = new("fscy"u8.ToArray(), typeof(double),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.FontSpacing] = new("fsp"u8.ToArray(), typeof(double),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.FontSize] = new("fs"u8.ToArray(), typeof(double),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.Italic] = new("i"u8.ToArray(), typeof(bool),
                AssTagKind.BlockOnlyRenderLatest),
            [AssTag.KaraokeO] = new("ko"u8.ToArray(), typeof(int),
                AssTagKind.Ignored),
            [AssTag.KaraokeF] = new("kf"u8.ToArray(), typeof(int),
                AssTagKind.Ignored),
            [AssTag.KaraokeFSimple] = new("K"u8.ToArray(), typeof(int),
                AssTagKind.Ignored),
            [AssTag.Karaoke] = new("k"u8.ToArray(), typeof(int),
                AssTagKind.Ignored),
            [AssTag.Movement] = new("move"u8.ToArray(), typeof(UnImp),
                AssTagKind.Ignored),
            [AssTag.OriginRotation] = new("org"u8.ToArray(), typeof(UnImp),
                AssTagKind.Ignored),
            [AssTag.Position] = new("pos"u8.ToArray(), typeof(UnImp), // vector2
                AssTagKind.LineOnlyRenderFirst | AssTagKind.ShouldBeFunction),
            [AssTag.PolygonBaselineOffset] = new("pbo"u8.ToArray(), typeof(UnImp),
                AssTagKind.BlockOnlyRenderLatest),
            [AssTag.Polygon] = new("p"u8.ToArray(), typeof(int),
                AssTagKind.BlockOnlyRenderLatest),
            [AssTag.WrapStyle] = new("q"u8.ToArray(), typeof(byte),
                AssTagKind.LineOnlyRenderLatest),
            [AssTag.Reset] = new("r"u8.ToArray(), typeof(ReadOnlyMemory<byte>),
                AssTagKind.BlockOnlyRenderLatest),
            [AssTag.Shadow] = new("shad"u8.ToArray(), typeof(double),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.ShadowX] = new("xshad"u8.ToArray(), typeof(double),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.ShadowY] = new("yshad"u8.ToArray(), typeof(double),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            [AssTag.Strikeout] = new("s"u8.ToArray(), typeof(bool),
                AssTagKind.BlockOnlyRenderLatest),
            [AssTag.Transform] = new("t"u8.ToArray(), typeof(string),
                AssTagKind.ShouldBeFunction),
            [AssTag.Underline] = new("u"u8.ToArray(), typeof(bool),
                AssTagKind.BlockOnlyRenderLatest),
        });

    public static bool TryGet(AssTag tag, out AssTagDescriptor? desc)
        => tags.TryGetValue(tag, out desc);

    private class Node
    {
        public Dictionary<byte, Node> Children = new();
        public AssTagDescriptor? Descriptor;
        public AssTag? TagEnum;
    }

    private static readonly Node root = new();

    static AssTagRegistry()
    {
        foreach (var kv in tags)
        {
            Insert(kv.Value.Name.Span, kv.Key, kv.Value);
        }
    }

    private static void Insert(ReadOnlySpan<byte> name, AssTag tag, AssTagDescriptor desc)
    {
        var node = root;
        foreach (var b in name)
        {
            if (!node.Children.TryGetValue(b, out var child))
            {
                child = new Node();
                node.Children[b] = child;
            }
            node = child;
        }
        node.TagEnum = tag;
        node.Descriptor = desc;
    }
    
    public static bool TryMatch(ReadOnlySpan<byte> span, out AssTag tag, out AssTagDescriptor desc, out int matchedLength)
    {
        var node = root;
        tag = default;
        desc = default!;
        matchedLength = 0;

        for (int i = 0; i < span.Length; i++)
        {
            if (!node.Children.TryGetValue(span[i], out var child))
                break;

            node = child;
            if (node.Descriptor != null)
            {
                tag = node.TagEnum!.Value;
                desc = node.Descriptor!;
                matchedLength = i + 1;
            }
        }

        return matchedLength > 0;
    }

}

