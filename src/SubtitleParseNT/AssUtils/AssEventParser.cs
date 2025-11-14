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

            // 读取候选标签名：字母或数字（支持 1c、2c 等）
            int nameStart = i;
            while (i < block.Length && IsAsciiLetterOrDigit(block[i])) i++;
            int nameLen = i - nameStart;

            // 参数：直到下一个 '\' 或块结束
            int paramStart = i;
            while (i < block.Length && block[i] != (byte)'\\') i++;
            int paramEnd = i;

            if (nameLen == 0)
            {
                // 没有合法标签名，跳过这一段（把后面的非 '\' 内容当作参数区）
                continue;
            }

            // 最长前缀优先，逐步退回
            bool matched = false;
            for (int len = nameLen; len >= 1; len--)
            {
                var nameBytes = block.Slice(nameStart, len);
                if (AssTagRegistry.TryGetByNameBytes(nameBytes, out var tagEnum, out var desc))
                {
                    ReadOnlySpan<byte> paramBytes;

                    if (desc.TagType.HasFlag(AssTagKind.ShouldBeFunction) && paramStart < block.Length && block[paramStart] == (byte)'(')
                    {
                        // 特殊处理 \t(...) 函数式参数
                        int j = paramStart + 1;
                        int depth = 1;
                        while (j < block.Length && depth > 0)
                        {
                            if (block[j] == (byte)'(') depth++;
                            else if (block[j] == (byte)')') depth--;
                            j++;
                        }
                        if (depth != 0)
                        {
                            // 没有闭合，整个剩余部分当作参数
                            paramBytes = block.Slice(paramStart);
                            i = block.Length;
                        }
                        else
                        {
                            paramBytes = block.Slice(paramStart, j - paramStart);
                            i = j; // 跳过整个函数参数
                        }
                    }
                    else
                    {
                        // 普通参数：直到下一个 '\' 或块结束
                        paramBytes = block.Slice(nameStart + nameBytes.Length, paramEnd - (nameStart + nameBytes.Length));
                        i = paramEnd;
                    }

                    object? value = ParseValue(desc, paramBytes);
                    AddTag(ref buffer, ref count,
                        new AssTagSpan(tagEnum,
                                       new Range(absoluteStart + tagStart, absoluteStart + i),
                                       value));
                    matched = true;
                    break;
                }
            }

            // 未匹配到已知标签：跳过当前片段（把它当作未知控制序列）
            // 此时 i 已经在 paramEnd 或下一 '\' 处，继续外层循环
            if (!matched)
            {
                // 可选：也可以将未知序列作为文本忽略，这里直接跳过
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

    public static short GetWrapStyle(ReadOnlySpan<AssEventSegment> segments, short infoValue)
    {
        for (int i = segments.Length - 1; i >= 0; i--)
        {
            ref readonly var seg = ref segments[i];
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

    public static bool HasPolygon(ReadOnlySpan<AssEventSegment> segments)
    {
        for (int i = segments.Length - 1; i >= 0; i--)
        {
            ref readonly var seg = ref segments[i];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tags = seg.Tags;
            if (tags == null)
                continue;
            var tagsSpan = tags.Value.Span;

            for (int j = 0; j < tagsSpan!.Length; j++)
            {
                ref readonly var tagSpan = ref tagsSpan[j];
                if (tagSpan.Tag == AssTag.Polygon)
                {
                    return true;
                }
            }
        }

        return false;
    }

}

