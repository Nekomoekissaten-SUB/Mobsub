using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mobsub.SubtitleParse.AssTypes;

public struct AssFontInfo
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
    
    public readonly override bool Equals(object? obj)
    {
        if ((obj is null) || (obj is not AssFontInfo))
            return false;

        var other = (AssFontInfo)obj;

        return Name == other.Name &&
               Weight == other.Weight &&
               Italic == other.Italic &&
               Encoding == other.Encoding;
    }

    public readonly override int GetHashCode() => HashCode.Combine(Name, Weight, Italic, Encoding);
    public static bool operator ==(AssFontInfo lhs, AssFontInfo rhs) => lhs.Equals(rhs);
    public static bool operator !=(AssFontInfo lhs, AssFontInfo rhs) => !lhs.Equals(rhs);
    public readonly override string ToString() => $"{Name},{Weight},{(Italic ? 1 : 0)},{Encoding}";
}
