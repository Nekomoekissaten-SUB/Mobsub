﻿using Mobsub.SubtitleParse.AssTypes;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Mobsub.SubtitleParse.AssText;

public static class AssEventTextParser
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

    public static ReadOnlyMemory<AssEventSegment> ParseLine(ReadOnlyMemory<byte> line, in AssTextOptions options = default)
        => ParseLineInternal(line.Span, line, options);

    // Prefer the ReadOnlyMemory overload when possible to keep tag payloads as slices.
    public static ReadOnlyMemory<AssEventSegment> ParseLine(ReadOnlySpan<byte> line, in AssTextOptions options = default)
        => ParseLineInternal(line, default, options);

    public static AssEventSegmentBuffer ParseLinePooled(ReadOnlyMemory<byte> line, in AssTextOptions options = default)
        => ParseLineInternalPooled(line.Span, line, options);

    public static AssEventSegmentBuffer ParseLinePooled(ReadOnlySpan<byte> line, in AssTextOptions options = default)
        => ParseLineInternalPooled(line, default, options);

    public static void WithParsedSegments(ReadOnlyMemory<byte> line, Action<ReadOnlySpan<AssEventSegment>> action, in AssTextOptions options = default)
    {
        using var buffer = ParseLinePooled(line, options);
        action(buffer.Span);
    }

    // Prefer the ReadOnlyMemory overload when possible to keep tag payloads as slices.
    public static void WithParsedSegments(ReadOnlySpan<byte> line, Action<ReadOnlySpan<AssEventSegment>> action, in AssTextOptions options = default)
    {
        using var buffer = ParseLinePooled(line, options);
        action(buffer.Span);
    }

    public static void WithParsedSegments(ReadOnlyMemory<byte> line, AssEventSegmentSpanAction action, in AssTextOptions options = default)
    {
        using var buffer = ParseLinePooled(line, options);
        action(buffer.Span, line.Span);
    }

    public static void WithParsedSegments(ReadOnlySpan<byte> line, AssEventSegmentSpanAction action, in AssTextOptions options = default)
    {
        using var buffer = ParseLinePooled(line, options);
        action(buffer.Span, line);
    }

    private static ReadOnlyMemory<AssEventSegment> ParseLineInternal(ReadOnlySpan<byte> line, ReadOnlyMemory<byte> lineMemory, in AssTextOptions options)
    {
        var buffer = ParseLineToPool(line, lineMemory, out int count, pooledTags: false, options);

        var result = buffer.AsSpan(0, count).ToArray();
        ArrayPool<AssEventSegment>.Shared.Return(buffer, clearArray: true);

        return result;
    }

    private static AssEventSegmentBuffer ParseLineInternalPooled(ReadOnlySpan<byte> line, ReadOnlyMemory<byte> lineMemory, in AssTextOptions options)
    {
        var buffer = ParseLineToPool(line, lineMemory, out int count, pooledTags: true, options);
        return new AssEventSegmentBuffer(buffer, count);
    }

    private static AssEventSegment[] ParseLineToPool(ReadOnlySpan<byte> line, ReadOnlyMemory<byte> lineMemory, out int count, bool pooledTags, in AssTextOptions options)
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
                    ? ParseTagBlockPooled(block, i + 1, lineMemory, options)
                    : ParseTagBlock(block, i + 1, lineMemory, options);

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

    private static ReadOnlyMemory<AssTagSpan> ParseTagBlock(ReadOnlySpan<byte> block, int absoluteStart, ReadOnlyMemory<byte> lineMemory, in AssTextOptions options)
        => ParseTagBlockInternal(block, absoluteStart, lineMemory, pooled: false, options);

    private static ReadOnlyMemory<AssTagSpan> ParseTagBlockPooled(ReadOnlySpan<byte> block, int absoluteStart, ReadOnlyMemory<byte> lineMemory, in AssTextOptions options)
        => ParseTagBlockInternal(block, absoluteStart, lineMemory, pooled: true, options);

    private static ReadOnlyMemory<AssTagSpan> ParseTagBlockInternal(ReadOnlySpan<byte> block, int absoluteStart, ReadOnlyMemory<byte> lineMemory, bool pooled, in AssTextOptions options)
    {
        var pool = ArrayPool<AssTagSpan>.Shared;
        AssTagSpan[] buffer = pool.Rent(8);
        int count = 0;

        var scanner = new AssTagBlockScanner(block, absoluteStart, lineMemory, options);
        while (scanner.MoveNext(out var token))
        {
            if (!token.IsKnown)
                continue;

            var value = ParseValue(token.Tag, token.Param, token.ParamMemory, options);
            AddTag(ref buffer, ref count, new AssTagSpan(token.Tag, new Range(token.TagStart, token.TagEnd), value));
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

    private static AssTagValue ParseValue(AssTag tag, ReadOnlySpan<byte> param, ReadOnlyMemory<byte> paramMemory, in AssTextOptions options)
    {
        Utils.TrimSpaces(param, out int start, out int length);
        if (length == 0) return AssTagValue.Empty;

        var trimmedSpan = param.Slice(start, length);
        var trimmedMemory = paramMemory.IsEmpty ? default : paramMemory.Slice(start, length);

        if (AssTagRegistry.TryGetSpecialRule(tag, out var specialRule))
        {
            // VSFilter/libass: \fsc always resets scale (payload ignored); VSFilterMod enables \fsc<scale> overload.
            if (specialRule == AssTagSpecialRule.FontScaleFsc && !options.ModMode)
                return AssTagValue.Empty;

            if (specialRule == AssTagSpecialRule.HexInt32 && Utils.TryParseHexIntLoose(trimmedSpan, out int hex, out var invalidHex))
            {
                if (invalidHex && Logger != null)
                    LogWarning($"Invalid hex integer value for \\{Utils.GetString(AssTagRegistry.GetNameBytes(tag))}: '{Utils.GetString(trimmedSpan)}', treated as 0.");

                return AssTagValue.FromInt(hex);
            }
        }

        if (AssTagRegistry.IsAlphaTag(tag) && AssColor32.TryParseAlphaByte(trimmedSpan, out var alpha, out var invalidAlpha))
        {
            if (invalidAlpha && Logger != null)
            {
                LogWarning($"Non-canonical alpha value for \\{Utils.GetString(AssTagRegistry.GetNameBytes(tag))}: '{Utils.GetString(trimmedSpan)}', parsed as 0x{alpha:X2}.");
            }
            return AssTagValue.FromByte(alpha);
        }

        if (AssTagRegistry.TryGetFunctionKind(tag, out var functionKind) && TryParseFunctionTag(functionKind, trimmedSpan, trimmedMemory, out var funcValue, options))
            return AssTagValue.FromFunction(funcValue);

        if (!AssTagRegistry.TryGetValueKind(tag, out var valueKind))
            return AssTagValue.Empty;

        switch (valueKind)
        {
            case AssTagValueKind.Int:
                if (Utils.TryParseIntLoose(trimmedSpan, out int iv, out var invalidInt))
                {
                    if (invalidInt && Logger != null)
                    {
                        LogWarning($"Invalid integer value for \\{Utils.GetString(AssTagRegistry.GetNameBytes(tag))}: '{Utils.GetString(trimmedSpan)}', treated as 0.");
                    }
                    return AssTagValue.FromInt(iv);
                }
                return AssTagValue.Empty;
            case AssTagValueKind.Double:
                if (Utils.TryParseDoubleLoose(trimmedSpan, out double dv, out var invalidDouble))
                {
                    if (invalidDouble && Logger != null)
                    {
                        LogWarning($"Invalid number value for \\{Utils.GetString(AssTagRegistry.GetNameBytes(tag))}: '{Utils.GetString(trimmedSpan)}', treated as 0.");
                    }
                    return AssTagValue.FromDouble(dv);
                }
                return AssTagValue.Empty;
            case AssTagValueKind.Bool:
                // Semantics: only 0/1 are explicit; any other number (including -1) => reset.
                if (Utils.TryParseIntLoose(trimmedSpan, out int bv, out var invalidBool))
                {
                    if (invalidBool && Logger != null)
                    {
                        LogWarning($"Invalid bool value for \\{Utils.GetString(AssTagRegistry.GetNameBytes(tag))}: '{Utils.GetString(trimmedSpan)}', treated as 0.");
                    }
                    return bv switch
                    {
                        0 => AssTagValue.FromBool(false),
                        1 => AssTagValue.FromBool(true),
                        _ => AssTagValue.Empty,
                    };
                }
                return AssTagValue.Empty;
            case AssTagValueKind.Byte:
                if (Utils.TryParseIntLoose(trimmedSpan, out int byv, out var invalidByte))
                {
                    if (byv < 0 || byv > 255)
                    {
                        byv = 0;
                        invalidByte = true;
                    }
                    if (invalidByte && Logger != null)
                    {
                        LogWarning($"Invalid byte value for \\{Utils.GetString(AssTagRegistry.GetNameBytes(tag))}: '{Utils.GetString(trimmedSpan)}', treated as 0.");
                    }
                    return AssTagValue.FromByte((byte)byv);
                }
                return AssTagValue.Empty;
            case AssTagValueKind.Color:
                if (AssColor32.TryParseTagColor(trimmedSpan, out var color, out var ignoredHighByte, out var invalidColor))
                {
                    if (invalidColor && Logger != null)
                    {
                        LogWarning($"Invalid color value for \\{Utils.GetString(AssTagRegistry.GetNameBytes(tag))}: '{Utils.GetString(trimmedSpan)}', treated as 0.");
                    }
                    if (ignoredHighByte && Logger != null)
                    {
                        LogWarning($"ASS color tag \\{Utils.GetString(AssTagRegistry.GetNameBytes(tag))} has more than 6 hex digits; high byte ignored.");
                    }
                    return AssTagValue.FromColor(color);
                }
                return AssTagValue.Empty;
            case AssTagValueKind.Bytes:
                return AssTagValue.FromBytes(trimmedMemory.IsEmpty ? trimmedSpan.ToArray() : trimmedMemory);
            case AssTagValueKind.Function:
            case AssTagValueKind.None:
            default:
                return AssTagValue.Empty;
        }
    }

    private static bool TryParseFunctionTag(AssTagFunctionKind functionKind, ReadOnlySpan<byte> param, ReadOnlyMemory<byte> paramMemory, out AssTagFunctionValue value, in AssTextOptions options)
    {
        value = default;
        switch (functionKind)
        {
            case AssTagFunctionKind.Pos:
                if (AssFunctionTagParsers.TryParsePos(param, out var x, out var y) ||
                    (options.ModMode && AssFunctionTagParsers.TryParsePos3(param, out x, out y, out _)))
                {
                    value = new AssTagFunctionValue { Kind = AssTagFunctionKind.Pos, X1 = x, Y1 = y };
                    return true;
                }
                return false;
            case AssTagFunctionKind.Org:
                if (AssFunctionTagParsers.TryParseOrg(param, out var ox, out var oy))
                {
                    value = new AssTagFunctionValue { Kind = AssTagFunctionKind.Org, X1 = ox, Y1 = oy };
                    return true;
                }
                return false;
            case AssTagFunctionKind.Move:
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
            case AssTagFunctionKind.Fade:
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
            case AssTagFunctionKind.Fad:
                if (AssFunctionTagParsers.TryParseFad(param, out var fadT1, out var fadT2))
                {
                    value = new AssTagFunctionValue { Kind = AssTagFunctionKind.Fad, T1 = fadT1, T2 = fadT2 };
                    return true;
                }
                return false;
            case AssTagFunctionKind.ClipRect:
            case AssTagFunctionKind.ClipDrawing:
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
            case AssTagFunctionKind.Transform:
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
}

