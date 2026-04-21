using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

public static class AssEventTextQuery
{
    public static T FindLastTag<T>(ReadOnlySpan<AssEventSegment> segments, AssTag target, T defaultValue, out bool found)
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
        => FindLastTag(segments, AssTag.WrapStyle, infoValue, out _);

    public static bool HasPolygon(ReadOnlySpan<AssEventSegment> segments)
    {
        FindLastTag<int>(segments, AssTag.Polygon, default, out bool found);
        return found;
    }
}
