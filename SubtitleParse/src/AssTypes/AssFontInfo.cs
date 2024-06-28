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

    public override readonly bool Equals(object? obj)
    {
        if ((obj is null) || (obj is not AssFontInfo))
            return false;

        var other = (AssFontInfo)obj;

        return Name == other.Name &&
               Weight == other.Weight &&
               Italic == other.Italic;
    }

    public override readonly int GetHashCode() => HashCode.Combine(Name, Weight, Italic);
    public static bool operator ==(AssFontInfo lhs, AssFontInfo rhs) => lhs.Equals(rhs);
    public static bool operator !=(AssFontInfo lhs, AssFontInfo rhs) => !lhs.Equals(rhs);
    public override readonly string ToString() => $"{Name},{Weight},{(Italic ? 1 : 0)}";
}
