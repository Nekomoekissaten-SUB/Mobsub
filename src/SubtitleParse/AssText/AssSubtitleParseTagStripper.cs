using System.Buffers;
using System.Text;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

public static class AssSubtitleParseTagStripper
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    public static string RemoveTagsInAllOverrideBlocks(string lineText, Func<AssTag, bool> shouldRemove)
    {
        if (string.IsNullOrEmpty(lineText))
            return lineText;

        byte[] lineUtf8 = Utf8.GetBytes(lineText);
        using var buffer = AssEventTextParser.ParseLinePooled(lineUtf8);
        var segments = buffer.Span;

        if (segments.Length == 0)
            return lineText;

        var intervals = new List<(int Start, int End)>(capacity: 16);

        for (int s = 0; s < segments.Length; s++)
        {
            var seg = segments[s];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock)
                continue;

            ReadOnlySpan<AssTagSpan> tags = seg.Tags.HasValue ? seg.Tags.Value.Span : default;
            for (int i = 0; i < tags.Length; i++)
            {
                if (!shouldRemove(tags[i].Tag))
                    continue;

                var (ts, te) = GetRangeOffsets(tags[i].LineRange, lineUtf8.Length);
                if (te > ts)
                    intervals.Add((ts, te));
            }
        }

        if (intervals.Count == 0)
            return lineText;

        intervals.Sort((a, b) => a.Start.CompareTo(b.Start));
        MergeIntervalsInPlace(intervals);

        var writer = new ArrayBufferWriter<byte>(lineUtf8.Length);

        int pos = 0;
        for (int i = 0; i < intervals.Count; i++)
        {
            var it = intervals[i];
            if (it.Start > pos)
                writer.Write(lineUtf8.AsSpan(pos, it.Start - pos));
            pos = Math.Max(pos, it.End);
        }

        if (pos < lineUtf8.Length)
            writer.Write(lineUtf8.AsSpan(pos));

        return Utf8.GetString(writer.WrittenSpan);
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
