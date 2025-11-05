using Microsoft.Extensions.Logging;
using System.Text;
using ZLogger;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public sealed class AssStyleView
{
    internal readonly ILogger? logger;
    public readonly ReadOnlyMemory<byte> LineRaw;
    public readonly bool IsCommentLine = false;

    public Range Name { get; private set; }
    public Range Fontname { get; private set; } // GDI max 32, last is null
    public double Fontsize { get; set; }  // ushort; Is negative and float really correct?
    public AssRGB8 PrimaryColour { get; set; }
    public AssRGB8 SecondaryColour { get; set; }
    public AssRGB8 OutlineColour { get; set; }
    public AssRGB8 BackColour { get; set; }
    public bool Bold { get; set; }     // ? 0 / 400, 1 / 700
    public bool Italic { get; set; }
    public bool Underline { get; set; }  // 0 = false, -1 = true
    public bool StrikeOut { get; set; }
    public double ScaleX { get; set; }
    public double ScaleY { get; set; }
    public double Spacing { get; set; }
    public double Angle { get; set; }
    public short BorderStyle { get; set; }  // 1, 3?
    public double Outline { get; set; }
    public double Shadow { get; set; }
    public short Alignment { get; set; }  // 1-9
    public int MarginL { get; set; }
    public int MarginR { get; set; }
    public int MarginV { get; set; }
    public int MarginT { get; set; }
    public int MarginB { get; set; }
    public int Encoding { get; set; }
    public int AlphaLevel { get; set; }
    public int RelativeTo { get; set; }

    public string GetName() => Utils.GetString(LineRaw, Name);
    public string GetFontname() => Utils.GetString(LineRaw, Fontname);

    public void Write(StringBuilder sb)
    {
        sb.AppendLine(Utils.GetString(LineRaw));
    }

    public AssStyleView(ReadOnlyMemory<byte> line, ReadOnlySpan<byte> header, string[] formats, ILogger? logger = null)
    {
        this.logger = logger;
        var sepIndex = header.Length;

        var sp = line.Span[sepIndex..];
        LineRaw = line;

        if (header.SequenceEqual("/"u8))
        {
            IsCommentLine = true;
            return;
        }
        else if (header.SequenceEqual("Style"u8))
        {

        }
        else
        {
            throw new Exception($"Unknown Styles line '{Utils.GetString(line)}'");
        }

        var segCount = 0;
        foreach (var range in sp.Split((byte)','))
        {
            var value = sp[range];
            switch (formats[segCount])
            {
                case "Name": Name = range; break;
                case "Fontname": Fontname = range; break;
                case "Fontsize": Fontsize = double.Parse(value); break;
                case "PrimaryColour": PrimaryColour = AssRGB8.Parse(value); break;
                case "SecondaryColour": SecondaryColour = AssRGB8.Parse(value); break;
                case "OutlineColour": OutlineColour = AssRGB8.Parse(value); break;
                case "BackColour": BackColour = AssRGB8.Parse(value); break;
                case "Bold": Bold = sbyte.Parse(value) == -1; break;
                case "Italic": Italic = sbyte.Parse(value) == -1; break;
                case "Underline": Underline = sbyte.Parse(value) == -1; break;
                case "StrikeOut": StrikeOut = sbyte.Parse(value) == -1; break;
                case "ScaleX": ScaleX = double.Parse(value); break;
                case "ScaleY": ScaleY = double.Parse(value); break;
                case "Spacing": Spacing = double.Parse(value); break;
                case "Angle": Angle = double.Parse(value); break;
                case "BorderStyle": BorderStyle = short.Parse(value); break;
                case "Outline": Outline = double.Parse(value); break;
                case "Shadow": Shadow = double.Parse(value); break;
                case "Alignment": Alignment = short.Parse(value); break;
                case "MarginL": MarginL = int.Parse(value); break;
                case "MarginR": MarginR = int.Parse(value); break;
                case "MarginV": MarginV = int.Parse(value); break;
                case "MarginT": MarginT = int.Parse(value); break;
                case "MarginB": MarginB = int.Parse(value); break;
                case "Encoding": Encoding = int.Parse(value); break;
                case "AlphaLevel": AlphaLevel = int.Parse(value); break;
                case "RelativeTo": RelativeTo = int.Parse(value); break;
            }

            segCount++;
        }
    }
}
