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

    public static string InsertOrReplaceClipInFirstOverrideBlock(string lineText, string clipTag, ReadOnlySpan<byte> clipTagUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            lineText,
            insertTags: clipTag,
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

    public static string InsertOrReplaceTsrLinearTagsInFirstOverrideBlock(string lineText, string tagBlock, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            lineText,
            insertTags: tagBlock,
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

    public static string InsertOrReplaceScaleTagsInFirstOverrideBlock(string lineText, string tagBlock, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            lineText,
            insertTags: tagBlock,
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

    public static string InsertOrReplaceRotationTagsInFirstOverrideBlock(string lineText, string tagBlock, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            lineText,
            insertTags: tagBlock,
            insertTagsUtf8: tagBlockUtf8,
            shouldRemove: static t => t is AssTag.FontRotationX or AssTag.FontRotationY or AssTag.FontRotationZ or AssTag.FontRotationZSimple);

    public static string InsertOrReplaceOriginTagsInFirstOverrideBlock(string lineText, string tagBlock, ReadOnlySpan<byte> tagBlockUtf8)
        => InsertOrReplaceTagsInFirstOverrideBlock(
            lineText,
            insertTags: tagBlock,
            insertTagsUtf8: tagBlockUtf8,
            shouldRemove: static t => t is AssTag.OriginRotation);

    private static string InsertOrReplaceTagsInFirstOverrideBlock(
        string lineText,
        string insertTags,
        ReadOnlySpan<byte> insertTagsUtf8,
        Func<AssTag, bool> shouldRemove)
    {
        if (string.IsNullOrEmpty(lineText))
            return lineText;

        byte[] lineUtf8 = Utf8.GetBytes(lineText);

        // Match the legacy behavior: only treat it as a "first override block"
        // if the line starts with '{'.
        if (lineUtf8.Length == 0 || lineUtf8[0] != (byte)'{')
            return "{" + insertTags + "}" + lineText;

        using var buffer = AssEventTextParser.ParseLinePooled(lineUtf8);
        var segments = buffer.Span;

        if (segments.Length == 0 || segments[0].SegmentKind != AssEventSegmentKind.TagBlock)
            return "{" + insertTags + "}" + lineText;

        var seg = segments[0];
        var (segStart, segEnd) = GetRangeOffsets(seg.LineRange, lineUtf8.Length);

        if (segStart != 0 || segEnd <= 1 || segEnd > lineUtf8.Length)
            return "{" + insertTags + "}" + lineText;

        int contentStart = segStart + 1;
        int contentEnd = segEnd - 1; // before '}'

        if (contentEnd < contentStart)
            return "{" + insertTags + "}" + lineText;

        ReadOnlySpan<AssTagSpan> tags = seg.Tags.HasValue ? seg.Tags.Value.Span : default;
        var intervals = new List<(int Start, int End)>(capacity: 8);

        for (int i = 0; i < tags.Length; i++)
        {
            if (!shouldRemove(tags[i].Tag))
                continue;

            var (ts, te) = GetRangeOffsets(tags[i].LineRange, lineUtf8.Length);
            if (ts >= contentStart && te <= contentEnd && te > ts)
                intervals.Add((ts, te));
        }

        intervals.Sort((a, b) => a.Start.CompareTo(b.Start));
        MergeIntervalsInPlace(intervals);

        var writer = new ArrayBufferWriter<byte>(lineUtf8.Length + insertTagsUtf8.Length + 16);

        // New block
        writer.Write(stackalloc byte[1] { (byte)'{' });
        writer.Write(insertTagsUtf8);

        int pos = contentStart;
        for (int i = 0; i < intervals.Count; i++)
        {
            var it = intervals[i];
            if (it.Start > pos)
                writer.Write(lineUtf8.AsSpan(pos, it.Start - pos));
            pos = Math.Max(pos, it.End);
        }

        if (contentEnd > pos)
            writer.Write(lineUtf8.AsSpan(pos, contentEnd - pos));

        writer.Write(stackalloc byte[1] { (byte)'}' });

        // Suffix after original block
        if (segEnd < lineUtf8.Length)
            writer.Write(lineUtf8.AsSpan(segEnd));

        return Utf8.GetString(writer.WrittenSpan);
    }

    public static bool TryGetPolygonMode(string lineText, out int p, out int firstTagBlockEndByteIndex, out byte[] lineUtf8)
    {
        p = 0;
        firstTagBlockEndByteIndex = 0;
        lineUtf8 = Array.Empty<byte>();

        if (string.IsNullOrEmpty(lineText))
            return false;

        lineUtf8 = Utf8.GetBytes(lineText);
        if (lineUtf8.Length == 0 || lineUtf8[0] != (byte)'{')
            return false;

        using var buffer = AssEventTextParser.ParseLinePooled(lineUtf8);
        var segments = buffer.Span;

        if (segments.Length == 0 || segments[0].SegmentKind != AssEventSegmentKind.TagBlock)
            return false;

        var seg = segments[0];
        var (start, end) = GetRangeOffsets(seg.LineRange, lineUtf8.Length);
        if (start != 0 || end <= 1 || end > lineUtf8.Length)
            return false;

        ReadOnlySpan<AssTagSpan> tags = seg.Tags.HasValue ? seg.Tags.Value.Span : default;
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

    private static (int Start, int End) GetRangeOffsets(Range range, int length)
        => (range.Start.GetOffset(length), range.End.GetOffset(length));

    private static void MergeIntervalsInPlace(List<(int Start, int End)> intervals)
    {
        if (intervals.Count <= 1)
            return;

        int write = 0;
        for (int read = 1; read < intervals.Count; read++)
        {
            var current = intervals[write];
            var next = intervals[read];

            if (next.Start <= current.End)
            {
                intervals[write] = (current.Start, Math.Max(current.End, next.End));
                continue;
            }

            write++;
            if (write != read)
                intervals[write] = next;
        }

        int newCount = write + 1;
        if (newCount < intervals.Count)
            intervals.RemoveRange(newCount, intervals.Count - newCount);
    }
}
