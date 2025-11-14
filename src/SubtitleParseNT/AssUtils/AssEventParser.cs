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
                if (i > runStart)
                {
                    ParseTextSegment(line[runStart..i], runStart, ref buffer, ref count);
                }

                int blockEnd = line[(i + 1)..].IndexOf((byte)'}');
                if (blockEnd < 0) break;
                blockEnd += i + 1;

                var block = line.Slice(i + 1, blockEnd - i - 1);
                var tags = ParseTagBlock(block, i + 1);

                AddSegment(ref buffer, ref count,
                    new AssEventSegment(new Range(i, blockEnd + 1),
                                        AssEventSegmentKind.TagBlock,
                                        tags));

                i = blockEnd + 1;
                runStart = i;
            }
            else i++;
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
                    AddSegment(ref buffer, ref count,
                        new AssEventSegment(new Range(baseIndex + runStart, baseIndex + i),
                                            AssEventSegmentKind.Text));
                }

                switch (text[i + 1])
                {
                    case (byte)'N':
                        AddSegment(ref buffer, ref count,
                            new AssEventSegment(new Range(baseIndex + i, baseIndex + i + 2),
                                                AssEventSegmentKind.HardLineBreaker));
                        i += 2;
                        break;
                    case (byte)'n':
                        AddSegment(ref buffer, ref count,
                            new AssEventSegment(new Range(baseIndex + i, baseIndex + i + 2),
                                                AssEventSegmentKind.SoftLineBreaker));
                        i += 2;
                        break;
                    case (byte)'h':
                        AddSegment(ref buffer, ref count,
                            new AssEventSegment(new Range(baseIndex + i, baseIndex + i + 2),
                                                AssEventSegmentKind.NonBreakingSpace));
                        i += 2;
                        break;
                    
                    default:
                        i++;
                        break;
                }

                runStart = i;
            }
            else
            {
                i++;
            }
        }

        if (i > runStart)
        {
            AddSegment(ref buffer, ref count,
                new AssEventSegment(new Range(baseIndex + runStart, baseIndex + i),
                                    AssEventSegmentKind.Text));
        }
    }

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
            while (i < block.Length && IsAsciiLetter(block[i])) i++;
            var nameBytes = block.Slice(nameStart, i - nameStart);

            // 参数部分：直到下一个 '\' 或块结束
            int paramStart = i;
            while (i < block.Length && block[i] != (byte)'\\') i++;
            var paramBytes = block.Slice(paramStart, i - paramStart);

            // 优先匹配最长合法 tag
            if (AssTagRegistry.TryGetByNameBytes(nameBytes, out var tagEnum, out var desc))
            {
                object? value = ParseValue(desc, paramBytes);
                AddTag(ref buffer, ref count,
                    new AssTagSpan(tagEnum,
                                   new Range(absoluteStart + tagStart, absoluteStart + i),
                                   value));
            }
            else
            {
                // 尝试退回短匹配（例如 \rCmt_JP -> tag=r, value=Cmt_JP）
                if (nameBytes.Length > 1)
                {
                    var shortName = nameBytes.Slice(0, 1);
                    var restParam = block.Slice(nameStart + 1, (i - nameStart - 1));

                    if (AssTagRegistry.TryGetByNameBytes(shortName, out tagEnum, out desc))
                    {
                        object? value = ParseValue(desc, restParam);
                        AddTag(ref buffer, ref count,
                            new AssTagSpan(tagEnum,
                                           new Range(absoluteStart + tagStart, absoluteStart + i),
                                           value));
                    }
                }
                // 否则跳过未知标签
            }
        }

        var result = new AssTagSpan[count];
        Array.Copy(buffer, result, count);
        pool.Return(buffer, clearArray: true);

        return result;
    }
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiLetter(byte b)
    => (b >= (byte)'a' && b <= (byte)'z') || (b >= (byte)'A' && b <= (byte)'Z');

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiDigit(byte b)
        => (b >= (byte)'0' && b <= (byte)'9');

    public static short GetWrapStyle(ReadOnlyMemory<AssEventSegment> segments, short infoValue)
    {
        var segmentsSpan = segments.Span;
        for (int i = segmentsSpan.Length - 1; i >= 0; i--)
        {
            ref readonly var seg = ref segmentsSpan[i];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tags = seg.Tags;
            if (tags == null)
                continue;
            var tagsSpan = tags.Value.Span;

            for (int j = tagsSpan!.Length - 1; j >= 0; j--)
            {
                ref readonly var tagSpan = ref tagsSpan[j];
                if (tagSpan.Tag == AssTag.WrapStyle)
                {
                    return tagSpan.Value is short s ? s : infoValue;
                }
            }
        }

        return infoValue;
    }

}

