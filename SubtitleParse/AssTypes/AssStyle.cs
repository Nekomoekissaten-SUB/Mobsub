using System.Text;

namespace Mobsub.AssTypes;

public class AssStyles
{
    private readonly string format = "Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding";
    private string[]? formats;
    // public string Version;
    public string[] Formats
    {
        get => formats ?? format.Split(',').Select(s => s.Trim()).ToArray();
        set => formats = value;
    }
    public List<AssStyle> Collection = [];
    public HashSet<string> Names = [];

    public void Write(StreamWriter sw, char[] newline, string scriptType)
    {
        switch (scriptType)
        {
            case "v4.00":
                sw.Write("[V4 Styles]");
                break;
            case "v4.00+":
                sw.Write("[V4+ Styles]");
                break;
            case "v4.00++":
                sw.Write("[V4++ Styles]");
            break;
        }
        sw.Write(newline);
        sw.Write($"Format: {string.Join(", ", Formats)}");
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
    public string Name = string.Empty;
    public string Fontname = string.Empty;
    public float Fontsize;  // ushort; Is negative and float really correct?
    public AssRGB8? PrimaryColour;
    public AssRGB8? SecondaryColour;
    public AssRGB8? OutlineColour;
    public AssRGB8? BackColour;
    public bool Bold;     // ? 0 / 400, 1 / 700
    public bool Italic;
    public bool Underline;  // 0 = false, -1 = true
    public bool StrikeOut;
    public float ScaleX;
    public float ScaleY;
    public float Spacing;
    public float Angle;
    public short BorderStyle;  // 1, 3?
    public float Outline;
    public float Shadow;
    public short Alignment;  // 1-9
    public int MarginL;
    public int MarginR;
    public int MarginV;
    public int Encoding;

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
                case "Encoding":
                    sw.Write(Encoding);
                    break;
            }

            if (i < formats.Length - 1)
            {
                sw.Write(',');
            }
        }
    }

}
