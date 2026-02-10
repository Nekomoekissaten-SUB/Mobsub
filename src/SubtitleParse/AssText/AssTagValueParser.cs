using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

public static class AssTagValueParser
{
    public static int? TryParseAlign(AssEventTextRead read)
        => TryParseAlign(read.Segments);

    public static int? TryParseAlign(string text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        using var read = AssEventTextRead.Parse(text);
        return TryParseAlign(read);
    }

    public static bool TryParsePosOrMoveBase(AssEventTextRead read, out double x, out double y)
        => TryParsePosOrMoveBase(read.Segments, out x, out y);

    public static bool TryParsePosOrMoveBase(string text, out double x, out double y)
    {
        if (string.IsNullOrEmpty(text))
        {
            x = 0;
            y = 0;
            return false;
        }

        using var read = AssEventTextRead.Parse(text);
        return TryParsePosOrMoveBase(read, out x, out y);
    }

    public static double? TryParseTagNumber(AssEventTextRead read, string tag)
    {
        if (!TryMapTagName(tag, out var t))
            return null;
        return TryParseTagNumber(read, t);
    }

    public static double? TryParseTagNumber(string text, string tag)
    {
        if (!TryMapTagName(tag, out var t))
            return null;
        return TryParseTagNumber(text, t);
    }

    public static double? TryParseTagNumber(AssEventTextRead read, AssTag tag)
        => TryParseTagNumber(read.Segments, tag);

    public static double? TryParseTagNumber(string text, AssTag tag)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        using var read = AssEventTextRead.Parse(text);
        return TryParseTagNumber(read, tag);
    }

    public static bool TryParseOrg(AssEventTextRead read, out double x, out double y)
        => TryParseOrg(read.Segments, out x, out y);

    public static bool TryParseOrg(string text, out double x, out double y)
    {
        if (string.IsNullOrEmpty(text))
        {
            x = 0;
            y = 0;
            return false;
        }

        using var read = AssEventTextRead.Parse(text);
        return TryParseOrg(read, out x, out y);
    }

    private static int? TryParseAlign(ReadOnlySpan<AssEventSegment> segments)
    {
        for (int s = 0; s < segments.Length; s++)
        {
            ref readonly var seg = ref segments[s];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tags = seg.Tags.Value.Span;
            for (int i = 0; i < tags.Length; i++)
            {
                ref readonly var t = ref tags[i];
                if (t.Tag == AssTag.Alignment)
                {
                    if (t.TryGet<byte>(out var an) && an is >= 1 and <= 9)
                        return an;
                    continue;
                }

                if (t.Tag == AssTag.AlignmentLegacy)
                {
                    if (t.TryGet<byte>(out var legacy) && AssTagRegistry.TryMapLegacyAlignmentToAn(legacy, out int an))
                        return an;
                    continue;
                }
            }
        }

        return null;
    }

    private static bool TryParsePosOrMoveBase(ReadOnlySpan<AssEventSegment> segments, out double x, out double y)
    {
        for (int s = 0; s < segments.Length; s++)
        {
            ref readonly var seg = ref segments[s];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tags = seg.Tags.Value.Span;
            for (int i = 0; i < tags.Length; i++)
            {
                ref readonly var t = ref tags[i];
                if (t.Tag != AssTag.Position && t.Tag != AssTag.Movement)
                    continue;
                if (!t.TryGet<AssTagFunctionValue>(out var func))
                    continue;

                if (func.Kind == AssTagFunctionKind.Pos)
                {
                    x = func.X1;
                    y = func.Y1;
                    return true;
                }

                if (func.Kind == AssTagFunctionKind.Move)
                {
                    x = func.X1;
                    y = func.Y1;
                    return true;
                }
            }
        }

        x = 0;
        y = 0;
        return false;
    }

    private static bool TryParseOrg(ReadOnlySpan<AssEventSegment> segments, out double x, out double y)
    {
        for (int s = 0; s < segments.Length; s++)
        {
            ref readonly var seg = ref segments[s];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tags = seg.Tags.Value.Span;
            for (int i = 0; i < tags.Length; i++)
            {
                ref readonly var t = ref tags[i];
                if (t.Tag != AssTag.OriginRotation)
                    continue;
                if (!t.TryGet<AssTagFunctionValue>(out var func))
                    continue;

                if (func.Kind == AssTagFunctionKind.Org)
                {
                    x = func.X1;
                    y = func.Y1;
                    return true;
                }
            }
        }

        x = 0;
        y = 0;
        return false;
    }

    private static double? TryParseTagNumber(ReadOnlySpan<AssEventSegment> segments, AssTag tag)
    {
        for (int s = 0; s < segments.Length; s++)
        {
            ref readonly var seg = ref segments[s];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tags = seg.Tags.Value.Span;
            for (int i = 0; i < tags.Length; i++)
            {
                ref readonly var t = ref tags[i];
                if (t.Tag != tag)
                    continue;

                if (t.TryGet<double>(out var dv))
                    return dv;
                if (t.TryGet<int>(out var iv))
                    return iv;
                if (t.TryGet<byte>(out var bv))
                    return bv;
            }
        }

        return null;
    }

    private static bool TryMapTagName(string name, out AssTag tag)
    {
        tag = default;
        if (string.IsNullOrEmpty(name))
            return false;

        int start = name[0] == '\\' ? 1 : 0;
        if (start >= name.Length)
            return false;

        ReadOnlySpan<char> s = name.AsSpan(start);
        Span<byte> bytes = s.Length <= 64 ? stackalloc byte[s.Length] : new byte[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c > 0x7F)
                return false;
            bytes[i] = (byte)c;
        }

        if (!AssTagRegistry.TryMatch(bytes, out var t, out _, out int matched) || matched != bytes.Length)
            return false;

        tag = t;
        return true;
    }
}
