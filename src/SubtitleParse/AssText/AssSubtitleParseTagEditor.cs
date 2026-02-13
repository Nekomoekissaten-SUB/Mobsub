using System.Buffers;
using System.Text;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

public static class AssSubtitleParseTagEditor
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    public static string InsertOrReplaceMoveInFirstOverrideBlock(string lineText, string moveTag, ReadOnlySpan<byte> moveTagUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            lineText,
            insertTags: moveTag,
            insertTagsUtf8: moveTagUtf8,
            shouldRemove: static t => t is AssTag.Movement or AssTag.Position);

    public static string InsertOrReplaceMoveInFirstOverrideBlock(AssEventTextRead read, ReadOnlySpan<byte> moveTagUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            read,
            insertTagsUtf8: moveTagUtf8,
            shouldRemove: static t => t is AssTag.Movement or AssTag.Position);

    public static string InsertOrReplaceClipInFirstOverrideBlock(string lineText, string clipTag, ReadOnlySpan<byte> clipTagUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            lineText,
            insertTags: clipTag,
            insertTagsUtf8: clipTagUtf8,
            shouldRemove: static t => t is AssTag.Clip or AssTag.InverseClip);

    public static string InsertOrReplaceClipInFirstOverrideBlock(AssEventTextRead read, ReadOnlySpan<byte> clipTagUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            read,
            insertTagsUtf8: clipTagUtf8,
            shouldRemove: static t => t is AssTag.Clip or AssTag.InverseClip);

    public static string InsertOrReplacePerspectiveTagsInFirstOverrideBlock(string lineText, string tagBlock, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            lineText,
            insertTags: tagBlock,
            insertTagsUtf8: tagBlockUtf8,
            shouldRemove: static t => t is AssTag.Alignment or AssTag.AlignmentLegacy
                or AssTag.OriginRotation or AssTag.Position or AssTag.Movement
                or AssTag.FontRotationX or AssTag.FontRotationY or AssTag.FontRotationZ or AssTag.FontRotationZSimple
                or AssTag.FontScaleX or AssTag.FontScaleY or AssTag.FontScale
                or AssTag.FontShiftX or AssTag.FontShiftY);

    public static string InsertOrReplacePerspectiveTagsInFirstOverrideBlock(AssEventTextRead read, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            read,
            insertTagsUtf8: tagBlockUtf8,
            shouldRemove: static t => t is AssTag.Alignment or AssTag.AlignmentLegacy
                or AssTag.OriginRotation or AssTag.Position or AssTag.Movement
                or AssTag.FontRotationX or AssTag.FontRotationY or AssTag.FontRotationZ or AssTag.FontRotationZSimple
                or AssTag.FontScaleX or AssTag.FontScaleY or AssTag.FontScale
                or AssTag.FontShiftX or AssTag.FontShiftY);

    public static string InsertOrReplaceTsrLinearTagsInFirstOverrideBlock(string lineText, string tagBlock, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            lineText,
            insertTags: tagBlock,
            insertTagsUtf8: tagBlockUtf8,
            shouldRemove: static t => t is AssTag.FontScaleX or AssTag.FontScaleY or AssTag.FontScale
                or AssTag.FontRotationX or AssTag.FontRotationY or AssTag.FontRotationZ or AssTag.FontRotationZSimple);

    public static string InsertOrReplaceTsrLinearTagsInFirstOverrideBlock(AssEventTextRead read, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            read,
            insertTagsUtf8: tagBlockUtf8,
            shouldRemove: static t => t is AssTag.FontScaleX or AssTag.FontScaleY or AssTag.FontScale
                or AssTag.FontRotationX or AssTag.FontRotationY or AssTag.FontRotationZ or AssTag.FontRotationZSimple);

    public static string InsertOrReplaceTsrLinearExtendedTagsInFirstOverrideBlock(string lineText, string tagBlock, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            lineText,
            insertTags: tagBlock,
            insertTagsUtf8: tagBlockUtf8,
            shouldRemove: static t => t is AssTag.FontScaleX or AssTag.FontScaleY or AssTag.FontScale
                or AssTag.FontRotationX or AssTag.FontRotationY or AssTag.FontRotationZ or AssTag.FontRotationZSimple
                or AssTag.Border or AssTag.BorderX or AssTag.BorderY
                or AssTag.Shadow or AssTag.ShadowX or AssTag.ShadowY
                or AssTag.BlurEdgesGaussian);

    public static string InsertOrReplaceTsrLinearExtendedTagsInFirstOverrideBlock(AssEventTextRead read, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            read,
            insertTagsUtf8: tagBlockUtf8,
            shouldRemove: static t => t is AssTag.FontScaleX or AssTag.FontScaleY or AssTag.FontScale
                or AssTag.FontRotationX or AssTag.FontRotationY or AssTag.FontRotationZ or AssTag.FontRotationZSimple
                or AssTag.Border or AssTag.BorderX or AssTag.BorderY
                or AssTag.Shadow or AssTag.ShadowX or AssTag.ShadowY
                or AssTag.BlurEdgesGaussian);

    public static string InsertOrReplaceScaleTagsInFirstOverrideBlock(string lineText, string tagBlock, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            lineText,
            insertTags: tagBlock,
            insertTagsUtf8: tagBlockUtf8,
            shouldRemove: static t => t is AssTag.FontScaleX or AssTag.FontScaleY or AssTag.FontScale);

    public static string InsertOrReplaceScaleTagsInFirstOverrideBlock(AssEventTextRead read, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            read,
            insertTagsUtf8: tagBlockUtf8,
            shouldRemove: static t => t is AssTag.FontScaleX or AssTag.FontScaleY or AssTag.FontScale);

    public static string InsertOrReplaceScaleExtendedTagsInFirstOverrideBlock(string lineText, string tagBlock, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            lineText,
            insertTags: tagBlock,
            insertTagsUtf8: tagBlockUtf8,
            shouldRemove: static t => t is AssTag.FontScaleX or AssTag.FontScaleY or AssTag.FontScale
                or AssTag.Border or AssTag.BorderX or AssTag.BorderY
                or AssTag.Shadow or AssTag.ShadowX or AssTag.ShadowY
                or AssTag.BlurEdgesGaussian);

    public static string InsertOrReplaceScaleExtendedTagsInFirstOverrideBlock(AssEventTextRead read, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            read,
            insertTagsUtf8: tagBlockUtf8,
            shouldRemove: static t => t is AssTag.FontScaleX or AssTag.FontScaleY or AssTag.FontScale
                or AssTag.Border or AssTag.BorderX or AssTag.BorderY
                or AssTag.Shadow or AssTag.ShadowX or AssTag.ShadowY
                or AssTag.BlurEdgesGaussian);

    public static string InsertOrReplaceRotationTagsInFirstOverrideBlock(string lineText, string tagBlock, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            lineText,
            insertTags: tagBlock,
            insertTagsUtf8: tagBlockUtf8,
            shouldRemove: static t => t is AssTag.FontRotationX or AssTag.FontRotationY or AssTag.FontRotationZ or AssTag.FontRotationZSimple);

    public static string InsertOrReplaceRotationTagsInFirstOverrideBlock(AssEventTextRead read, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            read,
            insertTagsUtf8: tagBlockUtf8,
            shouldRemove: static t => t is AssTag.FontRotationX or AssTag.FontRotationY or AssTag.FontRotationZ or AssTag.FontRotationZSimple);

    public static string InsertOrReplaceOriginTagsInFirstOverrideBlock(string lineText, string tagBlock, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            lineText,
            insertTags: tagBlock,
            insertTagsUtf8: tagBlockUtf8,
            shouldRemove: static t => t is AssTag.OriginRotation);

    public static string InsertOrReplaceOriginTagsInFirstOverrideBlock(AssEventTextRead read, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            read,
            insertTagsUtf8: tagBlockUtf8,
            shouldRemove: static t => t is AssTag.OriginRotation);

    public static string InsertOrReplaceTagsInFirstOverrideBlock(
        string lineText,
        string insertTags,
        ReadOnlySpan<byte> insertTagsUtf8,
        Func<AssTag, bool> shouldRemove)
    {
        if (string.IsNullOrEmpty(lineText))
            return lineText;

        // Fast path: the legacy behavior prefixes a new first override block
        // only when the line does not start with '{'.
        if (lineText[0] != '{')
            return "{" + insertTags + "}" + lineText;

        using var read = AssEventTextRead.Parse(lineText, Utf8);
        return InsertOrReplaceTagsInFirstOverrideBlock(read, insertTagsUtf8, shouldRemove);
    }

    public static string InsertOrReplaceTagsInFirstOverrideBlock(
        AssEventTextRead read,
        ReadOnlySpan<byte> insertTagsUtf8,
        Func<AssTag, bool> shouldRemove)
    {
        if (insertTagsUtf8.IsEmpty)
            return Utf8.GetString(read.Utf8.Span);

        ReadOnlySpan<byte> lineUtf8 = read.Utf8.Span;

        if (!read.TryGetFirstOverrideBlock(out var firstBlockRange, out var tags))
            return PrefixNewOverrideBlock(lineUtf8, insertTagsUtf8);

        var (segStart, segEnd) = GetRangeOffsets(firstBlockRange, lineUtf8.Length);

        if (segStart != 0 || segEnd <= 1 || segEnd > lineUtf8.Length)
            return PrefixNewOverrideBlock(lineUtf8, insertTagsUtf8);

        int contentStart = segStart + 1;
        int contentEnd = segEnd - 1; // before '}'

        if (contentEnd < contentStart)
            return PrefixNewOverrideBlock(lineUtf8, insertTagsUtf8);

        var writer = new ArrayBufferWriter<byte>(lineUtf8.Length + insertTagsUtf8.Length + 16);

        // New block
        writer.Write(stackalloc byte[1] { (byte)'{' });
        writer.Write(insertTagsUtf8);

        int pos = contentStart;
        for (int i = 0; i < tags.Length; i++)
        {
            ref readonly var t = ref tags[i];
            if (!shouldRemove(t.Tag))
                continue;

            var (ts, te) = GetRangeOffsets(t.LineRange, lineUtf8.Length);
            if (ts < contentStart || te > contentEnd || te <= ts)
                continue;

            if (ts > pos)
                writer.Write(lineUtf8.Slice(pos, ts - pos));
            pos = Math.Max(pos, te);
        }

        if (contentEnd > pos)
            writer.Write(lineUtf8.Slice(pos, contentEnd - pos));

        writer.Write(stackalloc byte[1] { (byte)'}' });

        // Suffix after original block
        if (segEnd < lineUtf8.Length)
            writer.Write(lineUtf8.Slice(segEnd));

        return Utf8.GetString(writer.WrittenSpan);
    }

    public static byte[] InsertOrReplaceTagsInFirstOverrideBlockUtf8(
        AssEventTextRead read,
        ReadOnlySpan<byte> insertTagsUtf8,
        Func<AssTag, bool> shouldRemove)
    {
        if (insertTagsUtf8.IsEmpty)
            return read.Utf8.ToArray();

        ReadOnlySpan<byte> lineUtf8 = read.Utf8.Span;

        if (!read.TryGetFirstOverrideBlock(out var firstBlockRange, out var tags))
            return PrefixNewOverrideBlockUtf8(lineUtf8, insertTagsUtf8);

        var (segStart, segEnd) = GetRangeOffsets(firstBlockRange, lineUtf8.Length);

        if (segStart != 0 || segEnd <= 1 || segEnd > lineUtf8.Length)
            return PrefixNewOverrideBlockUtf8(lineUtf8, insertTagsUtf8);

        int contentStart = segStart + 1;
        int contentEnd = segEnd - 1; // before '}'

        if (contentEnd < contentStart)
            return PrefixNewOverrideBlockUtf8(lineUtf8, insertTagsUtf8);

        var writer = new ArrayBufferWriter<byte>(lineUtf8.Length + insertTagsUtf8.Length + 16);

        // New block
        writer.Write(stackalloc byte[1] { (byte)'{' });
        writer.Write(insertTagsUtf8);

        int pos = contentStart;
        for (int i = 0; i < tags.Length; i++)
        {
            ref readonly var t = ref tags[i];
            if (!shouldRemove(t.Tag))
                continue;

            var (ts, te) = GetRangeOffsets(t.LineRange, lineUtf8.Length);
            if (ts < contentStart || te > contentEnd || te <= ts)
                continue;

            if (ts > pos)
                writer.Write(lineUtf8.Slice(pos, ts - pos));
            pos = Math.Max(pos, te);
        }

        if (contentEnd > pos)
            writer.Write(lineUtf8.Slice(pos, contentEnd - pos));

        writer.Write(stackalloc byte[1] { (byte)'}' });

        // Suffix after original block
        if (segEnd < lineUtf8.Length)
            writer.Write(lineUtf8.Slice(segEnd));

        return writer.WrittenSpan.ToArray();
    }

    public static bool TryGetPolygonMode(string lineText, out int p, out int firstTagBlockEndByteIndex, out byte[] lineUtf8)
    {
        p = 0;
        firstTagBlockEndByteIndex = 0;
        lineUtf8 = Array.Empty<byte>();

        if (string.IsNullOrEmpty(lineText))
            return false;

        using var read = AssEventTextRead.Parse(lineText, Utf8);
        if (!TryGetPolygonMode(read, out p, out firstTagBlockEndByteIndex))
            return false;

        lineUtf8 = read.Utf8.ToArray();
        return true;
    }

    public static bool TryGetPolygonMode(AssEventTextRead read, out int p, out int firstTagBlockEndByteIndex)
    {
        p = 0;
        firstTagBlockEndByteIndex = 0;

        ReadOnlySpan<byte> lineUtf8 = read.Utf8.Span;
        if (!read.TryGetFirstOverrideBlock(out var firstBlockRange, out var tags))
            return false;

        var (start, end) = GetRangeOffsets(firstBlockRange, lineUtf8.Length);
        if (start != 0 || end <= 1 || end > lineUtf8.Length)
            return false;

        for (int i = 0; i < tags.Length; i++)
        {
            if (tags[i].Tag != AssTag.Polygon)
                continue;
            if (tags[i].TryGet<int>(out var value))
            {
                p = value;
                firstTagBlockEndByteIndex = end;
                return true;
            }
        }

        return false;
    }

    private static string PrefixNewOverrideBlock(ReadOnlySpan<byte> lineUtf8, ReadOnlySpan<byte> insertTagsUtf8)
    {
        var writer = new ArrayBufferWriter<byte>(lineUtf8.Length + insertTagsUtf8.Length + 2);
        writer.Write(stackalloc byte[1] { (byte)'{' });
        writer.Write(insertTagsUtf8);
        writer.Write(stackalloc byte[1] { (byte)'}' });
        writer.Write(lineUtf8);
        return Utf8.GetString(writer.WrittenSpan);
    }

    private static byte[] PrefixNewOverrideBlockUtf8(ReadOnlySpan<byte> lineUtf8, ReadOnlySpan<byte> insertTagsUtf8)
    {
        var writer = new ArrayBufferWriter<byte>(lineUtf8.Length + insertTagsUtf8.Length + 2);
        writer.Write(stackalloc byte[1] { (byte)'{' });
        writer.Write(insertTagsUtf8);
        writer.Write(stackalloc byte[1] { (byte)'}' });
        writer.Write(lineUtf8);
        return writer.WrittenSpan.ToArray();
    }

    private static (int Start, int End) GetRangeOffsets(Range range, int length)
        => (range.Start.GetOffset(length), range.End.GetOffset(length));

}
