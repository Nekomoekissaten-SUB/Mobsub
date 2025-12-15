using Mobsub.SubtitleParseNT2.AssTypes;

namespace Mobsub.SubtitleParseNT2.AssUtils;

public readonly struct AssEventSegment(Range range, AssEventSegmentKind kind, ReadOnlyMemory<AssTagSpan>? tags = null)
{
    public AssEventSegmentKind SegmentKind { get; } = kind;
    public Range LineRange { get; } = range;
    public ReadOnlyMemory<AssTagSpan>? Tags { get; } = tags;
}

public readonly struct AssTagSpan(AssTag tag, Range range, AssTagValue value)
{
    public AssTag Tag { get; } = tag;
    public Range LineRange { get; } = range;
    public AssTagValue Value { get; } = value;

    public bool TryGet<T>(out T result)
    {
        result = default!;
        if (typeof(T) == typeof(int) && Value.Kind == AssTagValueKind.Int)
        {
            object o = Value.IntValue;
            result = (T)o;
            return true;
        }
        if (typeof(T) == typeof(double) && Value.Kind == AssTagValueKind.Double)
        {
            object o = Value.DoubleValue;
            result = (T)o;
            return true;
        }
        if (typeof(T) == typeof(bool) && Value.Kind == AssTagValueKind.Bool)
        {
            object o = Value.BoolValue;
            result = (T)o;
            return true;
        }
        if (typeof(T) == typeof(byte) && Value.Kind == AssTagValueKind.Byte)
        {
            object o = Value.ByteValue;
            result = (T)o;
            return true;
        }
        if (typeof(T) == typeof(AssRGB8) && Value.Kind == AssTagValueKind.Color)
        {
            object o = Value.ColorValue;
            result = (T)o;
            return true;
        }
        if (typeof(T) == typeof(ReadOnlyMemory<byte>) && Value.Kind == AssTagValueKind.Bytes)
        {
            object o = Value.BytesValue;
            result = (T)o;
            return true;
        }
        return false;
    }
}

public enum AssEventSegmentKind
{
    TagBlock,
    Text,
    HardLineBreaker,   // \N
    SoftLineBreaker,   // \n
    NonBreakingSpace // \h
}