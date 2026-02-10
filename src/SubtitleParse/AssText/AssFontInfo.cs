﻿using Mobsub.SubtitleParse.AssTypes;
using System.IO.Hashing;

namespace Mobsub.SubtitleParse.AssText;

public record struct AssFontInfo : IEquatable<AssFontInfo>
{
    public ReadOnlyMemory<byte> NameBytes;
    private string? _name;
    public string Name => _name ??= Utils.GetString(NameBytes.Span);
    public int Weight;
    public bool Italic;
    public int Encoding;

    public AssFontInfo(ReadOnlySpan<char> span)
    {
        var index = span.IndexOf(',');
        var nameSpan = span[..index];
        var byteCount = System.Text.Encoding.UTF8.GetByteCount(nameSpan);
        var bytes = new byte[byteCount];
        System.Text.Encoding.UTF8.GetBytes(nameSpan, bytes);
        NameBytes = bytes;

        span = span[(index + 1)..];
        index = span.IndexOf(',');
        Weight = int.Parse(span[..index]);

        span = span[(index + 1)..];
        index = span.IndexOf(',');
        Italic = int.Parse(span[..index]) != 0;

        span = span[(index + 1)..];
        Encoding = int.Parse(span);
    }

    public AssFontInfo(AssStyle syl)
    {
        var (offset, length) = syl.FontnameReadOnly.GetOffsetAndLength(syl.LineRaw.Length);
        NameBytes = syl.LineRaw.Slice(offset, length);
        Weight = syl.Bold ? 1 : 0;
        Italic = syl.Italic;
        Encoding = syl.Encoding;
    }

    public readonly bool Equals(AssFontInfo other) =>
        NameBytes.Span.SequenceEqual(other.NameBytes.Span) &&
        Weight == other.Weight &&
        Italic == other.Italic &&
        Encoding == other.Encoding;

    public readonly override int GetHashCode() => HashCode.Combine(unchecked((int)XxHash3.HashToUInt64(NameBytes.Span)), Weight, Italic, Encoding);
    public override string ToString() => $"{Name},{Weight},{(Italic ? 1 : 0)},{Encoding}";
}
