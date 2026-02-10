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
    {
        if (typeof(T) == typeof(int) && Value.Kind == AssTagValueKind.Int)
        {
            int v = Value.IntValue;
            result = Unsafe.As<int, T>(ref v);
            return true;
        }
        if (typeof(T) == typeof(double) && Value.Kind == AssTagValueKind.Double)
        {
            double v = Value.DoubleValue;
            result = Unsafe.As<double, T>(ref v);
            return true;
        }
        if (typeof(T) == typeof(bool) && Value.Kind == AssTagValueKind.Bool)
        {
            bool v = Value.BoolValue;
            result = Unsafe.As<bool, T>(ref v);
            return true;
        }
        if (typeof(T) == typeof(byte) && Value.Kind == AssTagValueKind.Byte)
        {
            byte v = Value.ByteValue;
            result = Unsafe.As<byte, T>(ref v);
            return true;
        }
        if (typeof(T) == typeof(AssColor32) && Value.Kind == AssTagValueKind.Color)
        {
            AssColor32 v = Value.ColorValue;
            result = Unsafe.As<AssColor32, T>(ref v);
            return true;
        }
        if (typeof(T) == typeof(ReadOnlyMemory<byte>) && Value.Kind == AssTagValueKind.Bytes)
        {
            ReadOnlyMemory<byte> v = Value.BytesValue;
            result = Unsafe.As<ReadOnlyMemory<byte>, T>(ref v);
            return true;
        }
        if (typeof(T) == typeof(AssTagFunctionValue) && Value.Kind == AssTagValueKind.Function)
        {
            AssTagFunctionValue v = Value.FunctionValue;
            result = Unsafe.As<AssTagFunctionValue, T>(ref v);
            return true;
        }

        result = default!;
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
