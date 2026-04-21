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

        var scanner = new AssOverrideTagScanner(block, absoluteStart, lineMemory, options);
        while (scanner.MoveNext(out var token))
        {
            if (!token.IsKnown)
                continue;

            var value = AssOverrideTagValueParser.ParseValue(token.Tag, token.Param, token.ParamMemory, options);
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
}

