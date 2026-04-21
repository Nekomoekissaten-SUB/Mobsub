﻿using Mobsub.SubtitleParse.AssTypes;

using System.Runtime.CompilerServices;

namespace Mobsub.SubtitleParse.AssText;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet<T>(out T result)
        => Value.TryGet(out result);
}

public enum AssEventSegmentKind
{
    TagBlock,
    Text,
    HardLineBreaker,   // \N
    SoftLineBreaker,   // \n
    NonBreakingSpace // \h
}
