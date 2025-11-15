using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;
using System.Text;

namespace Mobsub.SubtitleParseNT2.AssTypes;

// Copy from https://neue.cc/2024/08/29_AlternateLookup.html
public sealed class Utf8StringEqualityComparer : IEqualityComparer<byte[]>, IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>
{
    public static IEqualityComparer<byte[]> Default { get; } = new Utf8StringEqualityComparer();

    // IEqualityComparer

    public bool Equals(byte[]? x, byte[]? y)
    {
        if (x == null && y == null) return true;
        if (x == null || y == null) return false;

        return x.AsSpan().SequenceEqual(y);
    }

    public int GetHashCode([DisallowNull] byte[] obj)
    {
        return GetHashCode(obj.AsSpan());
    }

    // IAlternateEqualityComparer

    public byte[] Create(ReadOnlySpan<byte> alternate)
    {
        return alternate.ToArray();
    }

    public bool Equals(ReadOnlySpan<byte> alternate, byte[] other)
    {
        return other.AsSpan().SequenceEqual(alternate);
    }

    public int GetHashCode(ReadOnlySpan<byte> alternate)
    {
        // System.IO.Hashing package, cast to int is safe for hashing
        return unchecked((int)XxHash3.HashToUInt64(alternate));
    }
}
