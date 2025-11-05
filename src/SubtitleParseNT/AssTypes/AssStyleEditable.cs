using Microsoft.Extensions.Logging;
using ZLogger;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public sealed class AssStyleEditable
{
    private readonly ILogger? logger;

    public string Name { get; set; }
    public string Fontname { get; set; }
    public double Fontsize { get; set; }
    public AssRGB8 PrimaryColour { get; set; }
    public AssRGB8 SecondaryColour { get; set; }
    public AssRGB8 OutlineColour { get; set; }
    public AssRGB8 BackColour { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public bool StrikeOut { get; set; }
    public double ScaleX { get; set; }
    public double ScaleY { get; set; }
    public double Spacing { get; set; }
    public double Angle { get; set; }
    public short BorderStyle { get; set; }
    public double Outline { get; set; }
    public double Shadow { get; set; }
    public short Alignment { get; set; }
    public int MarginL { get; set; }
    public int MarginR { get; set; }
    public int MarginV { get; set; }
    public int Encoding { get; set; }

    public AssStyleEditable(ILogger? logger = null)
    {
        this.logger = logger;
        Name = "Default";
        Fontname = "Arial";
        Fontsize = 18;
        PrimaryColour = new AssRGB8(255, 255, 255, 0);
        SecondaryColour = new AssRGB8(255, 0, 0, 0);
        OutlineColour = new AssRGB8(0, 0, 0, 0);
        BackColour = new AssRGB8(0, 0, 0, 0);
        Bold = Italic = Underline = StrikeOut = false;
        ScaleX = ScaleY = 100;
        Spacing = 0;
        Angle = 0;
        BorderStyle = 1;
        Outline = 2;
        Shadow = 3;
        Alignment = 2;
        MarginL = MarginR = MarginV = 20;
        Encoding = 1;
    }

    public AssStyleEditable(AssStyleView view)
    {
        logger = view.logger;
        Name = view.GetName();
        Fontname = view.GetFontname();
        Fontsize = view.Fontsize;
        PrimaryColour = view.PrimaryColour;
        SecondaryColour = view.SecondaryColour;
        OutlineColour = view.OutlineColour;
        BackColour = view.BackColour;
        Bold = view.Bold;
        Italic = view.Italic;
        Underline = view.Underline;
        StrikeOut = view.StrikeOut;
        ScaleX = view.ScaleX;
        ScaleY = view.ScaleY;
        Spacing = view.Spacing;
        Angle = view.Angle;
        BorderStyle = view.BorderStyle;
        Outline = view.Outline;
        Shadow = view.Shadow;
        Alignment = view.Alignment;
        MarginL = view.MarginL;
        MarginR = view.MarginR;
        MarginV = view.MarginV;
        Encoding = view.Encoding;
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
                    sw.Write(PrimaryColour.ConvertToString(true));
                    break;
                case "SecondaryColour":
                    sw.Write("&H");
                    sw.Write(SecondaryColour.ConvertToString(true));
                    break;
                case "OutlineColour":
                    sw.Write("&H");
                    sw.Write(OutlineColour.ConvertToString(true));
                    break;
                case "BackColour":
                    sw.Write("&H");
                    sw.Write(BackColour.ConvertToString(true));
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
        logger?.ZLogDebug($"Write {Name} style line fine");
    }
}
