using static Mobsub.Utils.ParseHelper;

namespace Mobsub.AssTypes;

public class AssStyles
{
    private readonly string formatV4 = "Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, TertiaryColour, BackColour, Bold, Italic, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, AlphaLevel, Encoding";
    private readonly string formatV4P = "Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding";
    private readonly string formatV4PP = "Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginT, MarginB, Encoding, RelativeTo";
    
    private string[]? formats;
    // public string Version;
    public string[] Formats
    {
        get => formats ?? formatV4P.Split(',').Select(s => s.Trim()).ToArray();
        set => formats = value;
    }
    public List<AssStyle> Collection = [];
    public HashSet<string> Names = [];

    public void Read(ReadOnlySpan<char> sp)
    {
        if (sp[0] == '/')
        {
            return;
        }

        var sepIndex = sp.IndexOf(':');

        if (sp[..sepIndex].SequenceEqual("Format".AsSpan()))
        {
            Formats = sp[(sepIndex + 1)..].ToString().Split(',').Select(s => s.Trim()).ToArray();
        }
        else if (sp[..sepIndex].SequenceEqual("Style".AsSpan()))
        {
            var syl = new AssStyle();
            var va = sp[(sepIndex + 1)..].ToString().Split(',').Select(s => s.Trim()).ToArray();

            if (va.Length != Formats.Length)
            {
                throw new Exception($"Please check style line: {sp.ToString()}");
            }

            for (var i = 0; i < va.Length; i++)
            {
                SetProperty(syl, Formats[i], va[i]);
            }
            Collection.Add(syl);
            if (!Names.Add(syl.Name))
            {
                throw new Exception($"Styles: duplicate style {syl.Name}");
            }
        }
        else
        {
            throw new Exception($"Styles: invaild format {sp.ToString()}");
        }
    }

    public void Write(StreamWriter sw, char[] newline, string scriptType)
    {
        var fmtStr = string.Join(", ", Formats);
        switch (scriptType)
        {
            case "v4.00":
                if (fmtStr != formatV4)
                {
                    throw new Exception("Invalid style format for v4.00 script. Expected: " + formatV4 + ", got: " + fmtStr);
                }
                sw.Write("[V4 Styles]");
                break;
            case "v4.00+":
                if (fmtStr != formatV4P)
                {
                    throw new Exception("Invalid style format for v4.00 script. Expected: " + formatV4P + ", got: " + fmtStr);
                }
                sw.Write("[V4+ Styles]");
                break;
            case "v4.00++":
                if (fmtStr != formatV4PP)
                {
                    Formats = formatV4PP.Split(',').Select(s => s.Trim()).ToArray();
                }
                sw.Write("[V4++ Styles]");
            break;
        }
        sw.Write(newline);
        sw.Write($"Format: {fmtStr}");
        sw.Write(newline);

        for (var i = 0; i < Collection.Count; i++)
        {
            Collection[i].Write(sw, Formats);
            sw.Write(newline);
        }
        sw.Write(newline);
    }

}

public class AssStyle
{
    private string? name;
    private string? fontname;
    public string Name
    {
        get => name is null ? "Default" : name;
        set => name = value;
    }
    public string Fontname
    {
        get => fontname is null ? "Arial" : fontname.Length > 31 ? fontname[..31] : fontname;  // GDI max 32, last is null
        set => fontname = value;
    }
    public float Fontsize { get; set; }  // ushort; Is negative and float really correct?
    public AssRGB8? PrimaryColour { get; set; }
    public AssRGB8? SecondaryColour { get; set; }
    public AssRGB8? OutlineColour { get; set; }
    public AssRGB8? BackColour { get; set; }
    public bool Bold { get; set; }     // ? 0 / 400, 1 / 700
    public bool Italic { get; set; }
    public bool Underline { get; set; }  // 0 = false, -1 = true
    public bool StrikeOut { get; set; }
    public float ScaleX { get; set; }
    public float ScaleY { get; set; }
    public float Spacing { get; set; }
    public float Angle { get; set; }
    public short BorderStyle { get; set; }  // 1, 3?
    public float Outline { get; set; }
    public float Shadow { get; set; }
    public short Alignment { get; set; }  // 1-9
    public int MarginL { get; set; }
    public int MarginR { get; set; }
    public int MarginV { get; set; }
    public int MarginT { get; set; }
    public int MarginB { get; set; }
    public int Encoding { get; set; }
    public int AlphaLevel { get; set; }
    public int RelativeTo { get; set; }

    // from libass, wait…
    // public int TreatFontNameAsPattern { get; set; }
    // public double Blur { get; set; }
    // public int Justify { get; set; }

    public static AssStyle Fallback()
    {
        return new AssStyle
        {
            Name = "Default",
            Fontname = "Arial",
            Bold = false,
            Italic = false,
            Encoding = 0,
        };
    }

    public void Write(StreamWriter sw, string[] formats)
    {
        sw.Write("Style: ");
        for (var i = 0; i < formats.Length; i++)
        {
            switch (formats[i])
            {
                case "Name":
                    sw.Write(Name);
                    break;
                case "Fontname":
                    sw.Write(Fontname);
                    break;
                case "Fontsize":
                    sw.Write(Fontsize);
                    break;
                case "PrimaryColour":
                    sw.Write("&H");
                    sw.Write(PrimaryColour?.ConvetToString(true));
                    break;
                case "SecondaryColour":
                    sw.Write("&H");
                    sw.Write(SecondaryColour?.ConvetToString(true));
                    break;
                case "OutlineColour":
                    sw.Write("&H");
                    sw.Write(OutlineColour?.ConvetToString(true));
                    break;
                case "BackColour":
                    sw.Write("&H");
                    sw.Write(BackColour?.ConvetToString(true));
                    break;
                case "Bold":
                    sw.Write(Bold ? -1 : 0);
                    break;
                case "Italic":
                    sw.Write(Italic ? -1 : 0);
                    break;
                case "Underline":
                    sw.Write(Underline ? -1 : 0);
                    break;
                case "StrikeOut":
                    sw.Write(StrikeOut ? -1 : 0);
                    break;
                case "ScaleX":
                    sw.Write(ScaleX);
                    break;
                case "ScaleY":
                    sw.Write(ScaleY);
                    break;
                case "Spacing":
                    sw.Write(Spacing);
                    break;
                case "Angle":
                    sw.Write(Angle);
                    break;
                case "BorderStyle":
                    sw.Write(BorderStyle);
                    break;
                case "Outline":
                    sw.Write(Outline);
                    break;
                case "Shadow":
                    sw.Write(Shadow);
                    break;
                case "Alignment":
                    sw.Write(Alignment);
                    break;
                case "MarginL":
                    sw.Write(MarginL);
                    break;
                case "MarginR":
                    sw.Write(MarginR);
                    break;
                case "MarginV":
                    sw.Write(MarginV);
                    break;
                case "MarginT":
                    sw.Write(MarginT);
                    break;
                case "MarginB":
                    sw.Write(MarginB);
                    break;
                case "Encoding":
                    sw.Write(Encoding);
                    break;
                case "AlphaLevel":
                    sw.Write(AlphaLevel);
                    break;
                case "RelativeTo":
                    sw.Write(RelativeTo);
                    break;
            }

            if (i < formats.Length - 1)
            {
                sw.Write(',');
            }
        }
    }

}
