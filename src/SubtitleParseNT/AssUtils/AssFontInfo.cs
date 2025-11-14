using Mobsub.SubtitleParseNT2.AssTypes;

namespace Mobsub.SubtitleParseNT2.AssUtils;

public record struct AssFontInfo : IEquatable<AssFontInfo>
{
    public string Name;
    public int Weight;
    public bool Italic;
    public int Encoding;

    public AssFontInfo(ReadOnlySpan<char> span)
    {
        var index = span.IndexOf(',');
        Name = span[..index].ToString();

        span = span[(index + 1)..];
        index = span.IndexOf(',');
        Weight = int.Parse(span[..index]);

        span = span[(index + 1)..];
        index = span.IndexOf(',');
        Italic = int.Parse(span[..index]) != 0;

        span = span[(index + 1)..];
        Encoding = int.Parse(span);
    }

    public AssFontInfo(AssStyleView syl)
    {
        Name = syl.Fontname;
        Weight = syl.Bold ? 1 : 0;
        Italic = syl.Italic;
        Encoding = syl.Encoding;
    }

    public readonly bool Equals(AssFontInfo other) =>
        Name == other.Name &&
        Weight == other.Weight &&
        Italic == other.Italic &&
        Encoding == other.Encoding;

    public readonly override int GetHashCode() => HashCode.Combine(Name, Weight, Italic, Encoding);
    public readonly override string ToString() => $"{Name},{Weight},{(Italic ? 1 : 0)},{Encoding}";
}