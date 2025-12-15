using Mobsub.SubtitleParseNT2.AssTypes;
using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Text;

namespace Mobsub.SubtitleParseNT2.AssUtils;

public static class AssEventParser
{
    public static ReadOnlyMemory<AssEventSegment> ParseLine(ReadOnlySpan<byte> line)
    {
        var pool = ArrayPool<AssEventSegment>.Shared;
        var buffer = pool.Rent(32);
        int count = 0;

        int i = 0;
        int runStart = 0;

        while (i < line.Length)
        {
            if (line[i] == (byte)'{')
            {
                var searchSpan = line[(i + 1)..];
                int k = searchSpan.IndexOfAny((byte)'}', (byte)'{');

                if (k != -1 && searchSpan[k] == (byte)'}')
                {
                    int j = i + 1 + k;

                    if (i > runStart)
                    {
                        ParseTextSegment(line[runStart..i], runStart, ref buffer, ref count);
                    }

                    var block = line.Slice(i + 1, j - i - 1);
                    var tags = ParseTagBlock(block, i + 1);

                    AddSegment(ref buffer, ref count,  new AssEventSegment(new Range(i, j + 1), AssEventSegmentKind.TagBlock, tags));

                    i = j + 1;
                    runStart = i;
                }
                else
                {
                    // not found '}' or found another '{' first
                    i++;
                }
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

    private static void ParseTextSegment(ReadOnlySpan<byte> text, int baseIndex, ref AssEventSegment[] buffer, ref int count)
    {
        int i = 0;
        int runStart = 0;

        int textLength = text.Length;

        while (i < textLength)
        {
            int nextBackslash = text.Slice(i).IndexOf((byte)'\\');
            if (nextBackslash == -1)
            {
                i = textLength;
                break;
            }

            int backslashPos = i + nextBackslash;
            if (backslashPos + 1 < textLength && TextLookup.TryGetValue(text[backslashPos + 1], out var kind))
            {
                if (backslashPos > runStart)
                {
                    AddSegment(ref buffer, ref count, new AssEventSegment(new Range(baseIndex + runStart, baseIndex + backslashPos), AssEventSegmentKind.Text));
                }
                AddSegment(ref buffer, ref count, new AssEventSegment(new Range(baseIndex + backslashPos, baseIndex + backslashPos + 2), kind));
                i = backslashPos + 2;
                runStart = i;
            }
            else
            {
                i = backslashPos + 1;
            }
        }

        if (textLength > runStart)
        {
            AddSegment(ref buffer, ref count, new AssEventSegment(new Range(baseIndex + runStart, baseIndex + textLength), AssEventSegmentKind.Text));
        }
    }

    private static ReadOnlyMemory<AssTagSpan> ParseTagBlock(ReadOnlySpan<byte> block, int absoluteStart)
    {
        var pool = ArrayPool<AssTagSpan>.Shared;
        AssTagSpan[] buffer = pool.Rent(8);
        int count = 0;

        int i = 0;
        var remainingBlock = block;

        while (!remainingBlock.IsEmpty)
        {
            int tagStartOffset = remainingBlock.IndexOf((byte)'\\');
            if (tagStartOffset == -1)
            {
                break;
            }

            i = (int)(block.Length - remainingBlock.Length) + tagStartOffset;
            int tagStart = i;

            i++; // skip '\\'
            if (i >= block.Length)
            {
                break;
            }

            int nameStart = i;
            while (i < block.Length && IsAsciiLetterOrDigit(block[i])) i++;
            int nameLen = i - nameStart;
            if (nameLen == 0)
            {
                remainingBlock = block[i..];
                continue;
            }

            var paramSearchSpan = block[i..];
            int nextBackslash = paramSearchSpan.IndexOf((byte)'\\');

            int paramEnd;
            if (nextBackslash == -1)
            {
                paramEnd = block.Length;
            }
            else
            {
                paramEnd = i + nextBackslash;
            }

            if (AssTagRegistry.TryMatch(block.Slice(nameStart, nameLen), out var tagEnum, out var desc, out var matchedLength))
            {
                ReadOnlySpan<byte> paramBytes;
                int actualParamStart = nameStart + matchedLength;

                if (((desc.TagType & AssTagKind.ShouldBeFunction) != 0) && actualParamStart < block.Length && block[actualParamStart] == (byte)'(')
                {
                    int j = actualParamStart + 1;
                    int depth = 1;
                    var funcSearchSpan = block[j..];

                    while (!funcSearchSpan.IsEmpty && depth > 0)
                    {
                        int braceIndex = funcSearchSpan.IndexOfAny((byte)'(', (byte)')');
                        if (braceIndex == -1)
                        {
                            j = block.Length;
                            break;
                        }

                        j = (int)(block.Length - funcSearchSpan.Length) + braceIndex;

                        if (block[j] == (byte)'(') depth++;
                        else if (block[j] == (byte)')') depth--;

                        j++;
                        funcSearchSpan = block[j..];
                    }
                    paramBytes = block[actualParamStart..j];
                    i = j;
                }
                else
                {
                    paramBytes = block[actualParamStart..paramEnd];
                    i = paramEnd;
                }

                var value = ParseValue(desc, paramBytes);
                AddTag(ref buffer, ref count, new AssTagSpan(tagEnum, new Range(absoluteStart + tagStart, absoluteStart + i), value));
            }
            else
            {
                i = paramEnd;
            }
            remainingBlock = block.Slice(i);
        }

        if (count == 0)
        {
            // Return shared buffer and avoid allocating a new zero-length array repeatedly
            pool.Return(buffer, clearArray: true);
            return Array.Empty<AssTagSpan>();
        }

        var result = new AssTagSpan[count];
        Array.Copy(buffer, result, count);
        pool.Return(buffer, clearArray: true);
        return result;
    }

    private static readonly Dictionary<byte, AssEventSegmentKind> TextLookup = new()
    {
        [(byte)'N'] = AssEventSegmentKind.HardLineBreaker,
        [(byte)'n'] = AssEventSegmentKind.SoftLineBreaker,
        [(byte)'h'] = AssEventSegmentKind.NonBreakingSpace,
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiLetterOrDigit(byte b) => _asciiLetterOrDigit.Contains(b);
    private static readonly SearchValues<byte> _asciiLetterOrDigit =  SearchValues.Create(Encoding.ASCII.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"));

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

    private static AssTagValue ParseValue(AssTagDescriptor desc, ReadOnlySpan<byte> param)
    {
        param = Utils.TrimSpaces(param);
        if (param.Length == 0) return AssTagValue.Empty;
        if (desc.ValueType == typeof(int) && Utf8Parser.TryParse(param, out int iv, out _)) return AssTagValue.FromInt(iv);
        if (desc.ValueType == typeof(double) && Utf8Parser.TryParse(param, out double dv, out _)) return AssTagValue.FromDouble(dv);
        if (desc.ValueType == typeof(bool) && Utf8Parser.TryParse(param, out int bv, out _)) return AssTagValue.FromBool(bv != 0);
        if (desc.ValueType == typeof(byte) && Utf8Parser.TryParse(param, out int byv, out _)) return AssTagValue.FromByte((byte)byv);
        if (desc.ValueType == typeof(AssRGB8)) return AssTagValue.FromColor(AssRGB8.Parse(param));
        if (desc.ValueType == typeof(ReadOnlyMemory<byte>)) return AssTagValue.FromBytes(param.ToArray());
        return AssTagValue.Empty;
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
                    if (tagSpan.TryGet<T>(out var v))
                        return v!;
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

