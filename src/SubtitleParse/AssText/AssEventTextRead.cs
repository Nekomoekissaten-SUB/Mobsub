using System.Buffers;
using System.Text;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

public sealed class AssEventTextRead : IDisposable
{
    private AssEventTextParser.AssEventSegmentBuffer _buffer;
    private ReadOnlyMemory<byte> _utf8;
    private byte[]? _utf8PoolArray;
    private bool _disposed;

    public ReadOnlyMemory<byte> Utf8
    {
        get
        {
            ThrowIfDisposed();
            return _utf8;
        }
    }

    public ReadOnlySpan<AssEventSegment> Segments
    {
        get
        {
            ThrowIfDisposed();
            return _buffer.Span;
        }
    }

    private AssEventTextRead(ReadOnlyMemory<byte> utf8, AssEventTextParser.AssEventSegmentBuffer buffer, byte[]? utf8PoolArray)
    {
        _utf8 = utf8;
        _buffer = buffer;
        _utf8PoolArray = utf8PoolArray;
    }

    public static AssEventTextRead Parse(ReadOnlyMemory<byte> utf8)
        => new(utf8, AssEventTextParser.ParseLinePooled(utf8), utf8PoolArray: null);

    public static AssEventTextRead Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty)
            return Parse(ReadOnlyMemory<byte>.Empty);

        byte[] rented = ArrayPool<byte>.Shared.Rent(utf8.Length);
        utf8.CopyTo(rented);
        var mem = rented.AsMemory(0, utf8.Length);
        return new(mem, AssEventTextParser.ParseLinePooled(mem), rented);
    }

    public static AssEventTextRead Parse(string? text, Encoding? encoding = null)
    {
        return Parse((text ?? string.Empty).AsSpan(), encoding);
    }

    public static AssEventTextRead Parse(ReadOnlySpan<char> text, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        int byteCount = encoding.GetByteCount(text);
        if (byteCount == 0)
            return Parse(ReadOnlyMemory<byte>.Empty);

        byte[] rented = ArrayPool<byte>.Shared.Rent(byteCount);
        int written = encoding.GetBytes(text, rented);
        var mem = rented.AsMemory(0, written);
        return new(mem, AssEventTextParser.ParseLinePooled(mem), rented);
    }

    public static AssEventTextRead ParseTextSpan(in AssEvent ev)
    {
        int len = ev.LineRaw.Length;
        int start = ev.TextReadOnly.Start.GetOffset(len);
        int end = ev.TextReadOnly.End.GetOffset(len);
        if (end < start) end = start;
        return Parse(ev.LineRaw.Slice(start, end - start));
    }

    public bool TryGetFirstOverrideBlock(out Range lineRange, out ReadOnlySpan<AssTagSpan> tags)
    {
        ThrowIfDisposed();

        tags = default;
        lineRange = default;

        var segs = _buffer.Span;
        if (segs.Length == 0)
            return false;

        ref readonly var seg = ref segs[0];
        if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
            return false;

        var (start, _) = GetRangeOffsets(seg.LineRange, Utf8.Length);
        if (start != 0)
            return false;

        lineRange = seg.LineRange;
        tags = seg.Tags.Value.Span;
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _buffer.Dispose();
        _buffer = default;

        if (_utf8PoolArray != null)
        {
            ArrayPool<byte>.Shared.Return(_utf8PoolArray);
            _utf8PoolArray = null;
        }

        _utf8 = default;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AssEventTextRead));
    }

    private static (int Start, int End) GetRangeOffsets(Range range, int length)
        => (range.Start.GetOffset(length), range.End.GetOffset(length));
}
