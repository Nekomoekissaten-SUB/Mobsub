using Mobsub.SubtitleParseNT2.AssTypes;
using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;

namespace Mobsub.SubtitleParseNT2.AssUtils;

public static class AssEventParser
{
    public static ReadOnlyMemory<AssEventSegment> ParseLine(ReadOnlySpan<byte> line)
    {
        var pool = ArrayPool<AssEventSegment>.Shared;
        AssEventSegment[] buffer = pool.Rent(32);
        int count = 0;

        int i = 0;
        int runStart = 0;

        while (i < line.Length)
        {
            if (line[i] == (byte)'{')
            {
                // 尝试寻找闭合 '}'
                int j = i + 1;
                bool foundClose = false;
                while (j < line.Length)
                {
                    if (line[j] == (byte)'}')
                    {
                        foundClose = true;
                        break;
                    }
                    if (line[j] == (byte)'{')
                    {
                        // 遇到第二个 '{'，判定为非法
                        foundClose = false;
                        break;
                    }
                    j++;
                }

                if (foundClose)
                {
                    // 先提交前面的文本段
                    if (i > runStart)
                    {
                        ParseTextSegment(line[runStart..i], runStart, ref buffer, ref count);
                    }

                    // 解析 tag block
                    var block = line.Slice(i + 1, j - i - 1);
                    var tags = ParseTagBlock(block, i + 1);

                    AddSegment(ref buffer, ref count,
                        new AssEventSegment(new Range(i, j + 1),
                                            AssEventSegmentKind.TagBlock,
                                            tags));

                    i = j + 1;
                    runStart = i;
                }
                else
                {
                    // 没有闭合，把 '{' 当作普通文本继续
                    i++;
                }
            }
            else
            {
                i++;
            }
        }

        if (i > runStart)
        {
            ParseTextSegment(line[runStart..i], runStart, ref buffer, ref count);
        }

        var result = new AssEventSegment[count];
        Array.Copy(buffer, result, count);
        pool.Return(buffer, clearArray: true);

        return result;
    }

    private static void ParseTextSegment(ReadOnlySpan<byte> text, int baseIndex,  ref AssEventSegment[] buffer, ref int count)
    {
        int i = 0;
        int runStart = 0;

        while (i < text.Length)
        {
            if (text[i] == (byte)'\\' && i + 1 < text.Length)
            {
                if (i > runStart)
                {
                    AddSegment(ref buffer, ref count, new AssEventSegment(new Range(baseIndex + runStart, baseIndex + i), AssEventSegmentKind.Text));
                }

                if (TextLookup.TryGetValue(text[i + 1], out var kind))
                {
                    AddSegment(ref buffer, ref count,  new AssEventSegment(new Range(baseIndex + i, baseIndex + i + 2), kind));
                    i += 2;
                }
                else i++;
                runStart = i;
            }
            else i++;
        }

        if (i > runStart)
        {
            AddSegment(ref buffer, ref count,  new AssEventSegment(new Range(baseIndex + runStart, baseIndex + i), AssEventSegmentKind.Text));
        }
    }
    private static readonly Dictionary<byte, AssEventSegmentKind> TextLookup = new()
    {
        [(byte)'N'] = AssEventSegmentKind.HardLineBreaker,
        [(byte)'n'] = AssEventSegmentKind.SoftLineBreaker,
        [(byte)'h'] = AssEventSegmentKind.NonBreakingSpace,
    };

    private static ReadOnlyMemory<AssTagSpan> ParseTagBlock(ReadOnlySpan<byte> block, int absoluteStart)
    {
        var pool = ArrayPool<AssTagSpan>.Shared;
        AssTagSpan[] buffer = pool.Rent(8);
        int count = 0;

        int i = 0;
        while (i < block.Length)
        {
            if (block[i] != (byte)'\\') { i++; continue; }

            int tagStart = i;
            i++; // skip '\'

            int nameStart = i;
            while (i < block.Length && IsAsciiLetterOrDigit(block[i])) i++;
            int nameLen = i - nameStart;

            //int paramStart = i;
            while (i < block.Length && block[i] != (byte)'\\') i++;
            int paramEnd = i;

            if (nameLen == 0) continue;

            if (AssTagRegistry.TryMatch(block.Slice(nameStart, nameLen), out var tagEnum, out var desc, out var matchedLength))
            {
                ReadOnlySpan<byte> paramBytes;

                int actualParamStart = nameStart + matchedLength;

                if (desc.TagType.HasFlag(AssTagKind.ShouldBeFunction) &&
                    actualParamStart < block.Length && block[actualParamStart] == (byte)'(')
                {
                    int j = actualParamStart + 1;
                    int depth = 1;
                    while (j < block.Length && depth > 0)
                    {
                        if (block[j] == (byte)'(') depth++;
                        else if (block[j] == (byte)')') depth--;
                        j++;
                    }
                    paramBytes = block[actualParamStart..j];
                    i = j;
                }
                else
                {
                    paramBytes = block[actualParamStart..paramEnd];
                    i = paramEnd;
                }

                object? value = ParseValue(desc, paramBytes);
                AddTag(ref buffer, ref count, new AssTagSpan(tagEnum, new Range(absoluteStart + tagStart, absoluteStart + i), value));
            }
        }

        var result = new AssTagSpan[count];
        Array.Copy(buffer, result, count);
        pool.Return(buffer, clearArray: true);

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiLetterOrDigit(byte b) =>
        (b >= (byte)'A' && b <= (byte)'Z') ||
        (b >= (byte)'a' && b <= (byte)'z') ||
        (b >= (byte)'0' && b <= (byte)'9');

    private static void AddTag(ref AssTagSpan[] buffer, ref int count, AssTagSpan tag)
    {
        if (count >= buffer.Length)
        {
            var pool = ArrayPool<AssTagSpan>.Shared;
            var newBuffer = pool.Rent(buffer.Length * 2);
            Array.Copy(buffer, newBuffer, buffer.Length);
            pool.Return(buffer, clearArray: true);
            buffer = newBuffer;
        }
        buffer[count++] = tag;
    }

    private static void AddSegment(ref AssEventSegment[] buffer, ref int count, AssEventSegment seg)
    {
        if (count >= buffer.Length)
        {
            var pool = ArrayPool<AssEventSegment>.Shared;
            var newBuffer = pool.Rent(buffer.Length * 2);
            Array.Copy(buffer, newBuffer, buffer.Length);
            pool.Return(buffer, clearArray: true);
            buffer = newBuffer;
        }

        buffer[count++] = seg;
    }

    private static object? ParseValue(AssTagDescriptor desc, ReadOnlySpan<byte> param)
    {
        param = Utils.TrimSpaces(param);
        if (param.Length == 0) return null;
        if (desc.ValueType == typeof(int) && Utf8Parser.TryParse(param, out int iv, out _)) return iv;
        if (desc.ValueType == typeof(double) && Utf8Parser.TryParse(param, out double dv, out _)) return dv;
        if (desc.ValueType == typeof(bool) && Utf8Parser.TryParse(param, out int bv, out _)) return bv != 0;
        if (desc.ValueType == typeof(byte) && Utf8Parser.TryParse(param, out int byv, out _)) return byv;
        if (desc.ValueType == typeof(AssRGB8)) return AssRGB8.Parse(param);
        if (desc.ValueType == typeof(ReadOnlyMemory<byte>)) return param.ToArray();
        return null;
    }

    internal static T FindLastTag<T>(ReadOnlySpan<AssEventSegment> segments, AssTag target, T defaultValue, out bool found)
    {
        for (int i = segments.Length - 1; i >= 0; i--)
        {
            ref readonly var seg = ref segments[i];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tagsSpan = seg.Tags.Value.Span;
            for (int j = tagsSpan.Length - 1; j >= 0; j--)
            {
                ref readonly var tagSpan = ref tagsSpan[j];
                if (tagSpan.Tag == target)
                {
                    found = true;
                    if (tagSpan.Value is T value)
                        return value;
                    return defaultValue;
                }
            }
        }
        found = false;
        return defaultValue;
    }

    public static short GetWrapStyle(ReadOnlySpan<AssEventSegment> segments, short infoValue)
    {
        return FindLastTag(segments, AssTag.WrapStyle, infoValue, out _);
    }
    public static bool HasPolygon(ReadOnlySpan<AssEventSegment> segments)
    {
        FindLastTag<int>(segments, AssTag.Polygon, default, out bool found);
        return found;
    }
}

