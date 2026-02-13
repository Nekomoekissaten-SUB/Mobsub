using System.Buffers;
using System.Text;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

/// <summary>
/// Batch editor for ASS event text (the <c>Text</c> field inside an Event line).
/// Works on the UTF-8 bytes produced/held by <see cref="AssEventTextRead"/> and applies
/// all queued range edits in a single pass.
/// </summary>
public sealed class AssEventTextEdit : IDisposable
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    private AssEventTextRead _read;
    private readonly List<Edit> _edits = new(capacity: 8);
    private int _nextOrder;
    private bool _disposed;

    private readonly record struct Edit(int Start, int EndExclusive, byte[]? ReplacementUtf8, int Order);

    private AssEventTextEdit(AssEventTextRead read)
    {
        _read = read;
    }

    public ReadOnlyMemory<byte> Utf8Bytes
    {
        get
        {
            ThrowIfDisposed();
            return _read.Utf8;
        }
    }

    public ReadOnlySpan<AssEventSegment> Segments
    {
        get
        {
            ThrowIfDisposed();
            return _read.Segments;
        }
    }

    public bool HasEdits
    {
        get
        {
            ThrowIfDisposed();
            return _edits.Count != 0;
        }
    }

    public static AssEventTextEdit Parse(ReadOnlyMemory<byte> utf8)
        => new(AssEventTextRead.Parse(utf8));

    public static AssEventTextEdit Parse(ReadOnlySpan<byte> utf8)
        => new(AssEventTextRead.Parse(utf8));

    public static AssEventTextEdit Parse(string? text, Encoding? encoding = null)
        => new(AssEventTextRead.Parse(text, encoding));

    public static AssEventTextEdit Parse(ReadOnlySpan<char> text, Encoding? encoding = null)
        => new(AssEventTextRead.Parse(text, encoding));

    public static AssEventTextEdit ParseTextSpan(in AssEvent ev)
        => new(AssEventTextRead.ParseTextSpan(in ev));

    public void Delete(Range range)
        => Replace(range, ReadOnlySpan<byte>.Empty);

    public void Delete(int start, int endExclusive)
        => Replace(start, endExclusive, ReadOnlySpan<byte>.Empty);

    public void Insert(int index, ReadOnlySpan<byte> insertUtf8)
        => Replace(index, index, insertUtf8);

    public void Insert(int index, byte[] insertUtf8)
        => Replace(index, index, insertUtf8);

    public void Replace(Range range, ReadOnlySpan<byte> replacementUtf8)
    {
        ThrowIfDisposed();

        int len = _read.Utf8.Length;
        int start = range.Start.GetOffset(len);
        int end = range.End.GetOffset(len);
        Replace(start, end, replacementUtf8);
    }

    public void Replace(Range range, byte[] replacementUtf8)
    {
        ThrowIfDisposed();
        if (replacementUtf8 == null)
            throw new ArgumentNullException(nameof(replacementUtf8));

        int len = _read.Utf8.Length;
        int start = range.Start.GetOffset(len);
        int end = range.End.GetOffset(len);
        Replace(start, end, replacementUtf8);
    }

    public void Replace(int start, int endExclusive, ReadOnlySpan<byte> replacementUtf8)
    {
        ThrowIfDisposed();

        int len = _read.Utf8.Length;
        start = Math.Clamp(start, 0, len);
        endExclusive = Math.Clamp(endExclusive, start, len);

        if (start == endExclusive && replacementUtf8.IsEmpty)
            return;

        byte[]? repl = replacementUtf8.IsEmpty ? null : replacementUtf8.ToArray();
        _edits.Add(new Edit(start, endExclusive, repl, _nextOrder++));
    }

    public void Replace(int start, int endExclusive, byte[] replacementUtf8)
    {
        ThrowIfDisposed();
        if (replacementUtf8 == null)
            throw new ArgumentNullException(nameof(replacementUtf8));

        int len = _read.Utf8.Length;
        start = Math.Clamp(start, 0, len);
        endExclusive = Math.Clamp(endExclusive, start, len);

        if (start == endExclusive && replacementUtf8.Length == 0)
            return;

        _edits.Add(new Edit(start, endExclusive, replacementUtf8.Length == 0 ? null : replacementUtf8, _nextOrder++));
    }

    /// <summary>
    /// Queues deletions for all tags in all override blocks matching <paramref name="shouldRemove"/>.
    /// Returns the number of matching tag spans found.
    /// </summary>
    public int DeleteTagsInAllOverrideBlocks(Func<AssTag, bool> shouldRemove)
    {
        ThrowIfDisposed();
        if (shouldRemove == null)
            throw new ArgumentNullException(nameof(shouldRemove));

        int matches = 0;
        var segments = _read.Segments;

        for (int s = 0; s < segments.Length; s++)
        {
            ref readonly var seg = ref segments[s];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tags = seg.Tags.Value.Span;
            for (int i = 0; i < tags.Length; i++)
            {
                ref readonly var t = ref tags[i];
                if (!shouldRemove(t.Tag))
                    continue;
                Delete(t.LineRange);
                matches++;
            }
        }

        return matches;
    }

    public string ApplyToString(Encoding? encoding = null)
    {
        ThrowIfDisposed();

        if (_edits.Count == 0)
        {
            encoding ??= Utf8;
            return encoding.GetString(_read.Utf8.Span);
        }

        encoding ??= Utf8;

        var edits = _edits;
        edits.Sort(static (a, b) =>
        {
            int c = a.Start.CompareTo(b.Start);
            if (c != 0) return c;
            c = a.EndExclusive.CompareTo(b.EndExclusive);
            if (c != 0) return c;
            return a.Order.CompareTo(b.Order);
        });

        ReadOnlySpan<byte> src = _read.Utf8.Span;
        int srcLen = src.Length;

        int extra = 0;
        for (int i = 0; i < edits.Count; i++)
        {
            var e = edits[i];
            int replLen = e.ReplacementUtf8?.Length ?? 0;
            int removedLen = e.EndExclusive - e.Start;
            extra += replLen - removedLen;
        }

        var writer = new ArrayBufferWriter<byte>(Math.Max(0, srcLen + extra));

        int pos = 0;
        for (int i = 0; i < edits.Count; i++)
        {
            var e = edits[i];
            if (e.Start < pos)
                throw new InvalidOperationException("Overlapping edits are not supported.");

            if (e.Start > pos)
                writer.Write(src.Slice(pos, e.Start - pos));

            if (e.ReplacementUtf8 is { Length: > 0 } repl)
                writer.Write(repl);

            pos = e.EndExclusive;
        }

        if (pos < srcLen)
            writer.Write(src[pos..]);

        return encoding.GetString(writer.WrittenSpan);
    }

    public byte[] ApplyToUtf8Bytes()
    {
        ThrowIfDisposed();

        if (_edits.Count == 0)
            return _read.Utf8.ToArray();

        var edits = _edits;
        edits.Sort(static (a, b) =>
        {
            int c = a.Start.CompareTo(b.Start);
            if (c != 0) return c;
            c = a.EndExclusive.CompareTo(b.EndExclusive);
            if (c != 0) return c;
            return a.Order.CompareTo(b.Order);
        });

        ReadOnlySpan<byte> src = _read.Utf8.Span;
        int srcLen = src.Length;

        int extra = 0;
        for (int i = 0; i < edits.Count; i++)
        {
            var e = edits[i];
            int replLen = e.ReplacementUtf8?.Length ?? 0;
            int removedLen = e.EndExclusive - e.Start;
            extra += replLen - removedLen;
        }

        var writer = new ArrayBufferWriter<byte>(Math.Max(0, srcLen + extra));

        int pos = 0;
        for (int i = 0; i < edits.Count; i++)
        {
            var e = edits[i];
            if (e.Start < pos)
                throw new InvalidOperationException("Overlapping edits are not supported.");

            if (e.Start > pos)
                writer.Write(src.Slice(pos, e.Start - pos));

            if (e.ReplacementUtf8 is { Length: > 0 } repl)
                writer.Write(repl);

            pos = e.EndExclusive;
        }

        if (pos < srcLen)
            writer.Write(src[pos..]);

        return writer.WrittenSpan.ToArray();
    }

    public void ClearEdits()
    {
        ThrowIfDisposed();
        _edits.Clear();
        _nextOrder = 0;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _read.Dispose();
        _read = null!;
        _edits.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AssEventTextEdit));
    }
}
