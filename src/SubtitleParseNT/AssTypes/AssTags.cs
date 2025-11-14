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

public static class AssTagRegistry
{
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
            //[AssTag.Clip] = new("clip"u8.ToArray(), typeof(string),
            //    AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable | AssTagKind.ShouldBeFunction),
            //[AssTag.InverseClip] = new("iclip"u8.ToArray(), typeof(string),
            //    AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable | AssTagKind.ShouldBeFunction),
            [AssTag.ColorPrimaryAbbreviation] = new("c"u8.ToArray(), typeof(AssRGB8),
                AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable),
            //[AssTag.Fade] = new("fade"u8.ToArray(), typeof(string),
            //    AssTagKind.Ignored),
            //[AssTag.Fad] = new("fad"u8.ToArray(), typeof(double[]),
            //    AssTagKind.Ignored),
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
            //[AssTag.Movement] = new("move"u8.ToArray(), typeof(string),
            //    AssTagKind.Ignored),
            //[AssTag.OriginRotation] = new("org"u8.ToArray(), typeof(string),
            //    AssTagKind.Ignored),
            //[AssTag.Position] = new("pos"u8.ToArray(), typeof(Vector2),
            //    AssTagKind.LineOnlyRenderFirst | AssTagKind.ShouldBeFunction),
            //[AssTag.PolygonBaselineOffset] = new("pbo"u8.ToArray(), typeof(Vector2),
            //    AssTagKind.BlockOnlyRenderLatest),
            [AssTag.PolygonBaselineOffset] = new("p"u8.ToArray(), typeof(int),
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
    private static readonly FrozenDictionary<ReadOnlyMemory<byte>, (AssTag tag, AssTagDescriptor desc)>
        byNameBytes = BuildNameByteIndex();
    private static FrozenDictionary<ReadOnlyMemory<byte>, (AssTag, AssTagDescriptor)> BuildNameByteIndex()
    {
        var dict = new Dictionary<ReadOnlyMemory<byte>, (AssTag, AssTagDescriptor)>(new RomByteComparer());
        foreach (var kv in tags)
        {
            dict[kv.Value.Name] = (kv.Key, kv.Value);
        }
        return FrozenDictionary.ToFrozenDictionary(dict);
    }

    public static bool TryGet(AssTag tag, out AssTagDescriptor desc)
        => tags.TryGetValue(tag, out desc);
    public static bool TryGetByNameBytes(ReadOnlySpan<byte> name, out AssTag tag, out AssTagDescriptor desc)
    {
        foreach (var kv in byNameBytes)
        {
            if (name.SequenceEqual(kv.Key.Span))
            {
                (tag, desc) = kv.Value;
                return true;
            }
        }
        tag = default;
        desc = default!;
        return false;
    }

    private sealed class RomByteComparer : IEqualityComparer<ReadOnlyMemory<byte>>
    {
        public bool Equals(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y)
            => x.Span.SequenceEqual(y.Span);

        public int GetHashCode(ReadOnlyMemory<byte> obj)
        {
            var s = obj.Span;
            unchecked
            {
                int h = 17;
                for (int i = 0; i < s.Length; i++) h = h * 31 + s[i];
                return h;
            }
        }
    }
}

