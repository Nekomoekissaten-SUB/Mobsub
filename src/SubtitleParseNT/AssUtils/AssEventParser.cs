using Mobsub.SubtitleParseNT2.AssTypes;
using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Mobsub.SubtitleParseNT2.AssUtils;

public static class AssEventParser
{
    public struct AssEventSegmentBuffer : IDisposable
    {
        private AssEventSegment[]? _buffer;
        private int _count;

        internal AssEventSegmentBuffer(AssEventSegment[] buffer, int count)
        {
            _buffer = buffer;
            _count = count;
        }

        public int Count => _count;
        public ReadOnlySpan<AssEventSegment> Span => _buffer == null ? ReadOnlySpan<AssEventSegment>.Empty : _buffer.AsSpan(0, _count);

        public void Dispose()
        {
            if (_buffer == null)
                return;

            for (int i = 0; i < _count; i++)
            {
                var tags = _buffer[i].Tags;
                if (tags.HasValue && tags.Value.Length > 0 &&
                    MemoryMarshal.TryGetArray(tags.Value, out var seg) &&
                    seg.Array != null && seg.Array.Length > 0)
                {
                    ArrayPool<AssTagSpan>.Shared.Return(seg.Array, clearArray: true);
                }
            }

            ArrayPool<AssEventSegment>.Shared.Return(_buffer, clearArray: true);
            _buffer = null;
            _count = 0;
        }
    }

    public static ReadOnlyMemory<AssEventSegment> ParseLine(ReadOnlyMemory<byte> line)
        => ParseLineInternal(line.Span, line);

    public static ReadOnlyMemory<AssEventSegment> ParseLine(ReadOnlySpan<byte> line)
        => ParseLineInternal(line, default);

    public static AssEventSegmentBuffer ParseLinePooled(ReadOnlyMemory<byte> line)
        => ParseLineInternalPooled(line.Span, line);

    public static AssEventSegmentBuffer ParseLinePooled(ReadOnlySpan<byte> line)
        => ParseLineInternalPooled(line, default);

    public static void WithParsedSegments(ReadOnlyMemory<byte> line, Action<ReadOnlySpan<AssEventSegment>> action)
    {
        using var buffer = ParseLinePooled(line);
        action(buffer.Span);
    }

    public static void WithParsedSegments(ReadOnlySpan<byte> line, Action<ReadOnlySpan<AssEventSegment>> action)
    {
        using var buffer = ParseLinePooled(line);
        action(buffer.Span);
    }

    private static ReadOnlyMemory<AssEventSegment> ParseLineInternal(ReadOnlySpan<byte> line, ReadOnlyMemory<byte> lineMemory)
    {
        var buffer = ParseLineToPool(line, lineMemory, out int count, pooledTags: false);

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
                    var tags = ParseTagBlock(block, i + 1, lineMemory);

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
        ArrayPool<AssEventSegment>.Shared.Return(buffer, clearArray: true);

        return result;
    }

    private static AssEventSegmentBuffer ParseLineInternalPooled(ReadOnlySpan<byte> line, ReadOnlyMemory<byte> lineMemory)
    {
        var buffer = ParseLineToPool(line, lineMemory, out int count, pooledTags: true);
        return new AssEventSegmentBuffer(buffer, count);
    }

    private static AssEventSegment[] ParseLineToPool(ReadOnlySpan<byte> line, ReadOnlyMemory<byte> lineMemory, out int count, bool pooledTags)
    {
        var pool = ArrayPool<AssEventSegment>.Shared;
        var buffer = pool.Rent(32);
        count = 0;

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
                    var tags = pooledTags
                        ? ParseTagBlockPooled(block, i + 1, lineMemory)
                        : ParseTagBlock(block, i + 1, lineMemory);

                    AddSegment(ref buffer, ref count, new AssEventSegment(new Range(i, j + 1), AssEventSegmentKind.TagBlock, tags));

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

        return buffer;
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

    private static ReadOnlyMemory<AssTagSpan> ParseTagBlock(ReadOnlySpan<byte> block, int absoluteStart, ReadOnlyMemory<byte> lineMemory)
        => ParseTagBlockInternal(block, absoluteStart, lineMemory, pooled: false);

    private static ReadOnlyMemory<AssTagSpan> ParseTagBlockPooled(ReadOnlySpan<byte> block, int absoluteStart, ReadOnlyMemory<byte> lineMemory)
        => ParseTagBlockInternal(block, absoluteStart, lineMemory, pooled: true);

    private static ReadOnlyMemory<AssTagSpan> ParseTagBlockInternal(ReadOnlySpan<byte> block, int absoluteStart, ReadOnlyMemory<byte> lineMemory, bool pooled)
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
                int paramStart = actualParamStart;
                int paramLength;
                ReadOnlyMemory<byte> paramMemory = default;

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
                    paramLength = j - actualParamStart;
                    paramBytes = block.Slice(paramStart, paramLength);
                    i = j;
                }
                else
                {
                    paramLength = paramEnd - actualParamStart;
                    paramBytes = block.Slice(paramStart, paramLength);
                    i = paramEnd;
                }

                if (!lineMemory.IsEmpty)
                {
                    paramMemory = lineMemory.Slice(absoluteStart + paramStart, paramLength);
                }

                var value = ParseValue(desc, paramBytes, paramMemory);
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

        if (pooled)
        {
            return new ReadOnlyMemory<AssTagSpan>(buffer, 0, count);
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

    private static AssTagValue ParseValue(AssTagDescriptor desc, ReadOnlySpan<byte> param, ReadOnlyMemory<byte> paramMemory)
    {
        Utils.TrimSpaces(param, out int start, out int length);
        if (length == 0) return AssTagValue.Empty;

        var trimmedSpan = param.Slice(start, length);
        var trimmedMemory = paramMemory.IsEmpty ? default : paramMemory.Slice(start, length);

        if (desc.ValueType == typeof(int) && Utf8Parser.TryParse(trimmedSpan, out int iv, out _)) return AssTagValue.FromInt(iv);
        if (desc.ValueType == typeof(double) && Utf8Parser.TryParse(trimmedSpan, out double dv, out _)) return AssTagValue.FromDouble(dv);
        if (desc.ValueType == typeof(bool))
        {
            // Semantics: only 0/1 are explicit; any other number (including -1) and any non-number => reset.
            if (Utf8Parser.TryParse(trimmedSpan, out int bv, out _))
            {
                return bv switch
                {
                    0 => AssTagValue.FromBool(false),
                    1 => AssTagValue.FromBool(true),
                    _ => AssTagValue.Empty,
                };
            }
            return AssTagValue.Empty;
        }
        if (desc.ValueType == typeof(byte) && Utf8Parser.TryParse(trimmedSpan, out int byv, out _)) return AssTagValue.FromByte((byte)byv);
        if (desc.ValueType == typeof(AssRGB8)) return AssTagValue.FromColor(AssRGB8.Parse(trimmedSpan));
        if (desc.ValueType == typeof(ReadOnlyMemory<byte>))
        {
            return AssTagValue.FromBytes(trimmedMemory.IsEmpty ? trimmedSpan.ToArray() : trimmedMemory);
        }
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

