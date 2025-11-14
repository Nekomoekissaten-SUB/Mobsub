using Mobsub.SubtitleParseNT2.AssTypes;

namespace Mobsub.SubtitleParseNT2.AssUtils;

public readonly struct AssEventSegment(Range range, AssEventSegmentKind kind, ReadOnlyMemory<AssTagSpan>? tags = null)
{
    public AssEventSegmentKind SegmentKind { get; } = kind;
    public Range LineRange { get; } = range;
    public ReadOnlyMemory<AssTagSpan>? Tags { get; } = tags;
}

public readonly struct AssTagSpan(AssTag tag, Range range, object? value)
{
    public AssTag Tag { get; } = tag;
    public Range LineRange { get; } = range;
    public object? Value { get; } = value;
}

public enum AssEventSegmentKind
{
    TagBlock,
    Text,
    HardLineBreaker,   // \N
    SoftLineBreaker,   // \n
    NonBreakingSpace // \h
}