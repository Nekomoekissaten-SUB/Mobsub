using Mobsub.SubtitleParseNT2.AssTypes;
using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Mobsub.SubtitleParseNT2.AssUtils;

public static class AssEventParser
{
    public delegate void AssEventSegmentSpanAction(ReadOnlySpan<AssEventSegment> segments, ReadOnlySpan<byte> line);
    public static ILogger? Logger { get; set; }

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

    // Prefer the ReadOnlyMemory overload when possible to keep tag payloads as slices.
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

    // Prefer the ReadOnlyMemory overload when possible to keep tag payloads as slices.
    public static void WithParsedSegments(ReadOnlySpan<byte> line, Action<ReadOnlySpan<AssEventSegment>> action)
    {
        using var buffer = ParseLinePooled(line);
        action(buffer.Span);
    }

    public static void WithParsedSegments(ReadOnlyMemory<byte> line, AssEventSegmentSpanAction action)
    {
        using var buffer = ParseLinePooled(line);
        action(buffer.Span, line.Span);
    }

    public static void WithParsedSegments(ReadOnlySpan<byte> line, AssEventSegmentSpanAction action)
    {
        using var buffer = ParseLinePooled(line);
        action(buffer.Span, line);
    }

    private static ReadOnlyMemory<AssEventSegment> ParseLineInternal(ReadOnlySpan<byte> line, ReadOnlyMemory<byte> lineMemory)
    {
        var buffer = ParseLineToPool(line, lineMemory, out int count, pooledTags: false);

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
            int openIndex = line.Slice(i).IndexOf((byte)'{');
            if (openIndex == -1)
            {
                i = line.Length;
                break;
            }

            i += openIndex;
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
            if (backslashPos + 1 < textLength && TryGetTextSegmentKind(text[backslashPos + 1], out var kind))
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
        while (i < block.Length)
        {
            int tagStartOffset = block[i..].IndexOf((byte)'\\');
            if (tagStartOffset == -1)
            {
                break;
            }

            i += tagStartOffset;
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

                int parenStart = actualParamStart;
                if ((desc.TagType & AssTagKind.ShouldBeFunction) != 0)
                {
                    while (parenStart < block.Length && block[parenStart] == (byte)' ') parenStart++;
                }

                if (((desc.TagType & AssTagKind.ShouldBeFunction) != 0) && parenStart < block.Length && block[parenStart] == (byte)'(')
                {
                    int j = parenStart + 1;
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

                var value = ParseValue(tagEnum, desc, paramBytes, paramMemory);
                AddTag(ref buffer, ref count, new AssTagSpan(tagEnum, new Range(absoluteStart + tagStart, absoluteStart + i), value));
            }
            else
            {
                i = paramEnd;
            }
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

    private static bool TryGetTextSegmentKind(byte b, out AssEventSegmentKind kind)
    {
        switch (b)
        {
            case (byte)'N':
                kind = AssEventSegmentKind.HardLineBreaker;
                return true;
            case (byte)'n':
                kind = AssEventSegmentKind.SoftLineBreaker;
                return true;
            case (byte)'h':
                kind = AssEventSegmentKind.NonBreakingSpace;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    internal static void LogWarning(string message)
    {
        Logger?.LogWarning(message);
    }

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

    private static AssTagValue ParseValue(AssTag tag, AssTagDescriptor desc, ReadOnlySpan<byte> param, ReadOnlyMemory<byte> paramMemory)
    {
        Utils.TrimSpaces(param, out int start, out int length);
        if (length == 0) return AssTagValue.Empty;

        var trimmedSpan = param.Slice(start, length);
        var trimmedMemory = paramMemory.IsEmpty ? default : paramMemory.Slice(start, length);

        if (tag == AssTag.FontScale)
            return AssTagValue.Empty;

        if (IsAlphaTag(tag) && AssRGB8.TryParseAlphaByte(trimmedSpan, out var alpha, out var invalidAlpha))
        {
            if (invalidAlpha && Logger != null)
            {
                LogWarning($"Invalid alpha value for \\{Utils.GetString(desc.Name)}: '{Utils.GetString(trimmedSpan)}', treated as 0.");
            }
            return AssTagValue.FromByte(alpha);
        }

        if (IsFunctionTag(tag) && TryParseFunctionTag(tag, trimmedSpan, trimmedMemory, out var funcValue))
            return AssTagValue.FromFunction(funcValue);

        if (desc.ValueType == typeof(int) && Utils.TryParseIntLoose(trimmedSpan, out int iv, out var invalidInt))
        {
            if (invalidInt && Logger != null)
            {
                LogWarning($"Invalid integer value for \\{Utils.GetString(desc.Name)}: '{Utils.GetString(trimmedSpan)}', treated as 0.");
            }
            return AssTagValue.FromInt(iv);
        }
        if (desc.ValueType == typeof(double) && Utils.TryParseDoubleLoose(trimmedSpan, out double dv, out var invalidDouble))
        {
            if (invalidDouble && Logger != null)
            {
                LogWarning($"Invalid number value for \\{Utils.GetString(desc.Name)}: '{Utils.GetString(trimmedSpan)}', treated as 0.");
            }
            return AssTagValue.FromDouble(dv);
        }
        if (desc.ValueType == typeof(bool))
        {
            // Semantics: only 0/1 are explicit; any other number (including -1) => reset.
            if (Utils.TryParseIntLoose(trimmedSpan, out int bv, out var invalidBool))
            {
                if (invalidBool && Logger != null)
                {
                    LogWarning($"Invalid bool value for \\{Utils.GetString(desc.Name)}: '{Utils.GetString(trimmedSpan)}', treated as 0.");
                }
                return bv switch
                {
                    0 => AssTagValue.FromBool(false),
                    1 => AssTagValue.FromBool(true),
                    _ => AssTagValue.Empty,
                };
            }
            return AssTagValue.Empty;
        }
        if (desc.ValueType == typeof(byte) && Utils.TryParseIntLoose(trimmedSpan, out int byv, out var invalidByte))
        {
            if (invalidByte && Logger != null)
            {
                LogWarning($"Invalid byte value for \\{Utils.GetString(desc.Name)}: '{Utils.GetString(trimmedSpan)}', treated as 0.");
            }
            return AssTagValue.FromByte((byte)byv);
        }
        if (desc.ValueType == typeof(AssRGB8))
        {
            if (AssRGB8.TryParseTagColor(trimmedSpan, out var color, out var ignoredHighByte, out var invalidColor))
            {
                if (invalidColor && Logger != null)
                {
                    LogWarning($"Invalid color value for \\{Utils.GetString(desc.Name)}: '{Utils.GetString(trimmedSpan)}', treated as 0.");
                }
                if (ignoredHighByte && Logger != null)
                {
                    LogWarning($"ASS color tag \\{Utils.GetString(desc.Name)} has more than 6 hex digits; high byte ignored.");
                }
                return AssTagValue.FromColor(color);
            }
            return AssTagValue.Empty;
        }
        if (desc.ValueType == typeof(ReadOnlyMemory<byte>))
        {
            return AssTagValue.FromBytes(trimmedMemory.IsEmpty ? trimmedSpan.ToArray() : trimmedMemory);
        }
        return AssTagValue.Empty;
    }

    private static bool TryParseFunctionTag(AssTag tag, ReadOnlySpan<byte> param, ReadOnlyMemory<byte> paramMemory, out AssTagFunctionValue value)
    {
        value = default;
        switch (tag)
        {
            case AssTag.Position:
                if (AssFunctionTagParsers.TryParsePos(param, out var x, out var y))
                {
                    value = new AssTagFunctionValue { Kind = AssTagFunctionKind.Pos, X1 = x, Y1 = y };
                    return true;
                }
                return false;
            case AssTag.OriginRotation:
                if (AssFunctionTagParsers.TryParseOrg(param, out var ox, out var oy))
                {
                    value = new AssTagFunctionValue { Kind = AssTagFunctionKind.Org, X1 = ox, Y1 = oy };
                    return true;
                }
                return false;
            case AssTag.Movement:
                if (AssFunctionTagParsers.TryParseMove(param, out var x1, out var y1, out var x2, out var y2, out var t1, out var t2, out var hasTimes))
                {
                    value = new AssTagFunctionValue
                    {
                        Kind = AssTagFunctionKind.Move,
                        X1 = x1,
                        Y1 = y1,
                        X2 = x2,
                        Y2 = y2,
                        T1 = t1,
                        T2 = t2,
                        HasTimes = hasTimes
                    };
                    return true;
                }
                return false;
            case AssTag.Fade:
                if (AssFunctionTagParsers.TryParseFade(param, out var a1, out var a2, out var a3, out var ft1, out var ft2, out var ft3, out var ft4))
                {
                    value = new AssTagFunctionValue
                    {
                        Kind = AssTagFunctionKind.Fade,
                        A1 = a1,
                        A2 = a2,
                        A3 = a3,
                        T1 = ft1,
                        T2 = ft2,
                        T3 = ft3,
                        T4 = ft4
                    };
                    return true;
                }
                return false;
            case AssTag.Fad:
                if (AssFunctionTagParsers.TryParseFad(param, out var fadT1, out var fadT2))
                {
                    value = new AssTagFunctionValue { Kind = AssTagFunctionKind.Fad, T1 = fadT1, T2 = fadT2 };
                    return true;
                }
                return false;
            case AssTag.Clip:
            case AssTag.InverseClip:
                if (AssFunctionTagParsers.TryParseClip(param, out var clipKind, out var cx1, out var cy1, out var cx2, out var cy2, out var scale, out var drawing))
                {
                    if (clipKind == AssFunctionTagParsers.AssClipKind.Rect)
                    {
                        value = new AssTagFunctionValue
                        {
                            Kind = AssTagFunctionKind.ClipRect,
                            X1 = cx1,
                            Y1 = cy1,
                            X2 = cx2,
                            Y2 = cy2
                        };
                    }
                    else
                    {
                        value = new AssTagFunctionValue
                        {
                            Kind = AssTagFunctionKind.ClipDrawing,
                            Scale = scale,
                            Drawing = GetSliceMemory(param, paramMemory, drawing)
                        };
                    }
                    return true;
                }
                return false;
            case AssTag.Transform:
                if (AssFunctionTagParsers.TryParseTransform(param, out var tt1, out var tt2, out var hasTimesT, out var accel, out var hasAccel, out var tagPayload))
                {
                    value = new AssTagFunctionValue
                    {
                        Kind = AssTagFunctionKind.Transform,
                        T1 = tt1,
                        T2 = tt2,
                        HasTimes = hasTimesT,
                        Accel = accel,
                        HasAccel = hasAccel,
                        TagPayload = GetSliceMemory(param, paramMemory, tagPayload)
                    };
                    return true;
                }
                return false;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsFunctionTag(AssTag tag)
        => tag is AssTag.Position or AssTag.OriginRotation or AssTag.Movement or AssTag.Fade or AssTag.Fad
            or AssTag.Clip or AssTag.InverseClip or AssTag.Transform;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAlphaTag(AssTag tag)
        => tag is AssTag.Alpha or AssTag.AlphaPrimary or AssTag.AlphaSecondary or AssTag.AlphaBorder or AssTag.AlphaShadow;

    private static ReadOnlyMemory<byte> GetSliceMemory(ReadOnlySpan<byte> fullSpan, ReadOnlyMemory<byte> fullMemory, ReadOnlySpan<byte> sliceSpan)
    {
        if (sliceSpan.IsEmpty)
            return ReadOnlyMemory<byte>.Empty;

        if (fullMemory.IsEmpty)
            return sliceSpan.ToArray();

        ref byte fullRef = ref MemoryMarshal.GetReference(fullSpan);
        ref byte sliceRef = ref MemoryMarshal.GetReference(sliceSpan);
        int offset = (int)Unsafe.ByteOffset(ref fullRef, ref sliceRef);
        if ((uint)offset > (uint)fullSpan.Length || offset + sliceSpan.Length > fullSpan.Length)
            return sliceSpan.ToArray();

        return fullMemory.Slice(offset, sliceSpan.Length);
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

