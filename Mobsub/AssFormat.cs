using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using Mobsub.Utils;
/// Advanced SubStation Alpha (Sub Station Alpha v4.00+) Script
namespace Mobsub.AssFormat;

public class SectionValue
{
    public bool IsTableau { get; set; }
    public int LineNumber { get; set; }
}

public class AssData
{
    public GernalSection Gernal { get; set; }
    public DataTable Table { get; set; }

    public class GernalSection : Dictionary<string, string> { }
}

public class AssConst
{
    public static readonly string[] binSectionArr = { "Fonts", "Graphics" };

    public static readonly string assStyleVer = "V4+ Styles";
    public static readonly string assStyle =
        @"Name, Fontname, Fontsize, PrimaryColour, SecondaryColour,
        OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut,
        ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow,
        Alignment, MarginL, MarginR, MarginV, Encoding";
    public static readonly string assEvent = "Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text";
}

public class AssColor
{

}
