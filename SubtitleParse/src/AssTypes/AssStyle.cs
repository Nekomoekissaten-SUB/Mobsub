using Microsoft.Extensions.Logging;
using ZLogger;

namespace Mobsub.SubtitleParse.AssTypes;

public class AssStyles(ILogger? logger = null)
{
    private readonly string formatV4 = "Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, TertiaryColour, BackColour, Bold, Italic, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, AlphaLevel, Encoding";
    private readonly string formatV4P = "Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding";
    private readonly string formatV4PP = "Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginT, MarginB, Encoding, RelativeTo";
    internal const string sectionNameV4 = "[V4 Styles]";
    internal const string sectionNameV4P = "[V4+ Styles]";
    internal const string sectionNameV4PP = "[V4++ Styles]";

    private string[]? formats;
    // public string Version;
    public string[] Formats
    {
        get => formats ?? formatV4P.Split(',').Select(s => s.Trim()).ToArray();
        set => formats = value;
    }
    public List<AssStyle> Collection = [];
    public HashSet<string> Names = [];

    private readonly ILogger? _logger = logger;

    public void Read(ReadOnlySpan<char> sp, int lineNumber)
    {
        if (sp[0] == '/')
        {
            _logger?.ZLogDebug($"Line {lineNumber} is comment, will not parse");
            return;
        }

        var sepIndex = sp.IndexOf(':');

        if (sp[..sepIndex].SequenceEqual("Format".AsSpan()))
        {
            Formats = sp[(sepIndex + 1)..].ToString().Split(',').Select(s => s.Trim()).ToArray();
            _logger?.ZLogDebug($"Line {lineNumber} is a format line, parse completed");
        }
        else if (sp[..sepIndex].SequenceEqual("Style".AsSpan()))
        {
            var syl = new AssStyle(_logger);
            var va = sp[(sepIndex + 1)..].ToString().Split(',').Select(s => s.Trim()).ToArray();

            if (va.Length != Formats.Length)
            {
                throw new Exception($"Please check style line: {sp.ToString()}");
            }

            for (var i = 0; i < va.Length; i++)
            {
                Utils.SetProperty(syl, typeof(AssStyle), Formats[i], va[i]);
            }
            Collection.Add(syl);
            if (syl.Fontname.Length > 31)
            {
                _logger?.ZLogWarning($"Length ({syl.Fontname.Length}) of style {syl.Name}’s fontname “{syl.Fontname}” exceeds 31 characters, may affect the correct rendering of VSFilter");
            }
            if (!Names.Add(syl.Name))
            {
                throw new Exception($"Styles: duplicate style {syl.Name}");
            }
            _logger?.ZLogDebug($"Line {lineNumber} is a style line, parse completed, style name is {syl.Name}");
        }
        else
        {
            throw new Exception($"Styles: invalid format {sp.ToString()}");
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
                _logger?.ZLogInformation($"Start write section {sectionNameV4}");
                sw.Write(sectionNameV4);
                break;
            case "v4.00+":
                if (fmtStr != formatV4P)
                {
                    throw new Exception("Invalid style format for v4.00 script. Expected: " + formatV4P + ", got: " + fmtStr);
                }
                _logger?.ZLogInformation($"Start write section {sectionNameV4P}");
                sw.Write(sectionNameV4P);
                break;
            case "v4.00++":
                if (fmtStr != formatV4PP)
                {
                    Formats = formatV4PP.Split(',').Select(s => s.Trim()).ToArray();
                }
                _logger?.ZLogInformation($"Start write section {sectionNameV4PP}");
                sw.Write(sectionNameV4PP);
            break;
        }
        sw.Write(newline);
        sw.Write($"Format: {fmtStr}");
        sw.Write(newline);
        _logger?.ZLogDebug($"Write format line fine");

        for (var i = 0; i < Collection.Count; i++)
        {
            Collection[i].Write(sw, Formats);
            sw.Write(newline);
        }
        _logger?.ZLogDebug($"Write style lines fine");
        //sw.Write(newline);
        _logger?.ZLogDebug($"Section write completed");
    }

}

public class AssStyle(ILogger? logger = null)
{
    private string? name;
    private string? fontname;
    private readonly ILogger? _logger = logger;
    public string Name
    {
        get => name ?? "Default";
        set => name = value;
    }
    public string Fontname
    {
        get => fontname ?? "Arial";  // GDI max 32, last is null
        set => fontname = value;
    }
    public float Fontsize { get; set; }  // ushort; Is negative and float really correct?
    public AssRGB8 PrimaryColour { get; set; }
    public AssRGB8 SecondaryColour { get; set; }
    public AssRGB8 OutlineColour { get; set; }
    public AssRGB8 BackColour { get; set; }
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

    public AssStyle GetDefault()
    {
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
        return this;
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
        _logger?.ZLogDebug($"Write {Name} style line fine");
    }

    public override bool Equals(object? obj)
    {
        return obj is AssStyle style &&
               Name == style.Name &&
               Fontname == style.Fontname &&
               Fontsize == style.Fontsize &&
               EqualityComparer<AssRGB8>.Default.Equals(PrimaryColour, style.PrimaryColour) &&
               EqualityComparer<AssRGB8>.Default.Equals(SecondaryColour, style.SecondaryColour) &&
               EqualityComparer<AssRGB8>.Default.Equals(OutlineColour, style.OutlineColour) &&
               EqualityComparer<AssRGB8>.Default.Equals(BackColour, style.BackColour) &&
               Bold == style.Bold &&
               Italic == style.Italic &&
               Underline == style.Underline &&
               StrikeOut == style.StrikeOut &&
               ScaleX == style.ScaleX &&
               ScaleY == style.ScaleY &&
               Spacing == style.Spacing &&
               Angle == style.Angle &&
               BorderStyle == style.BorderStyle &&
               Outline == style.Outline &&
               Shadow == style.Shadow &&
               Alignment == style.Alignment &&
               MarginL == style.MarginL &&
               MarginR == style.MarginR &&
               MarginV == style.MarginV &&
               MarginT == style.MarginT &&
               MarginB == style.MarginB &&
               Encoding == style.Encoding &&
               AlphaLevel == style.AlphaLevel &&
               RelativeTo == style.RelativeTo;
    }

    public override int GetHashCode()
    {
        HashCode hash = new HashCode();
        hash.Add(Name);
        hash.Add(Fontname);
        hash.Add(Fontsize);
        hash.Add(PrimaryColour);
        hash.Add(SecondaryColour);
        hash.Add(OutlineColour);
        hash.Add(BackColour);
        hash.Add(Bold);
        hash.Add(Italic);
        hash.Add(Underline);
        hash.Add(StrikeOut);
        hash.Add(ScaleX);
        hash.Add(ScaleY);
        hash.Add(Spacing);
        hash.Add(Angle);
        hash.Add(BorderStyle);
        hash.Add(Outline);
        hash.Add(Shadow);
        hash.Add(Alignment);
        hash.Add(MarginL);
        hash.Add(MarginR);
        hash.Add(MarginV);
        hash.Add(MarginT);
        hash.Add(MarginB);
        hash.Add(Encoding);
        hash.Add(AlphaLevel);
        hash.Add(RelativeTo);
        return hash.ToHashCode();
    }
}
