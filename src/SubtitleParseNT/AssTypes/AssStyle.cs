using System.Buffers;
using System.Text;
using Mobsub.SubtitleParseNT2.AssUtils;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public struct AssStyle
{
    public ReadOnlyMemory<byte> LineRaw { get; set; }
    public bool IsCommentLine { get; set; } = false;

    public Range NameReadOnly { get; set; }
    public Range FontnameReadOnly { get; set; }
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
    public byte BorderStyle { get; set; }
    public double Outline { get; set; }
    public double Shadow { get; set; }
    public byte Alignment { get; set; }
    public int MarginL { get; set; }
    public int MarginR { get; set; }
    public int MarginV { get; set; }
    public int MarginT { get; set; }
    public int MarginB { get; set; }
    public int Encoding { get; set; }
    public int AlphaLevel { get; set; }
    public int RelativeTo { get; set; }

    private string? _name;
    private string? _fontname;

    public string Name
    {
        get => _name ?? Utils.GetString(LineRaw, NameReadOnly);
        set => _name = value;
    }
    public string Fontname
    {
        get => _fontname ?? Utils.GetString(LineRaw, FontnameReadOnly);
        set => _fontname = value;
    }

    public ReadOnlySpan<byte> NameSpan => LineRaw.Span[NameReadOnly];
    public ReadOnlySpan<byte> FontnameSpan => LineRaw.Span[FontnameReadOnly];

    public AssStyle(ReadOnlyMemory<byte> line, ReadOnlySpan<byte> header, string[] formats)
    {
        var sepIndex = header.Length;
        var sp = line.Span;

        LineRaw = line;

        // Initialize other fields to default
        NameReadOnly = default;
        FontnameReadOnly = default;
        Fontsize = 0;
        PrimaryColour = default;
        SecondaryColour = default;
        OutlineColour = default;
        BackColour = default;
        Bold = false;
        Italic = false;
        Underline = false;
        StrikeOut = false;
        ScaleX = 100;
        ScaleY = 100;
        Spacing = 0;
        Angle = 0;
        BorderStyle = 1;
        Outline = 0;
        Shadow = 0;
        Alignment = 2;
        MarginL = 0;
        MarginR = 0;
        MarginV = 0;
        MarginT = 0;
        MarginB = 0;
        Encoding = 0;
        AlphaLevel = 0;
        RelativeTo = 0;

        _name = null;
        _fontname = null;

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

        sepIndex += (sp[sepIndex + 1] == 0x20) ? 2 : 1;
        sp = sp[sepIndex..];
        var segCount = 0;
        foreach (var range in sp.Split((byte)','))
        {
            var value = sp[range];
            switch (formats[segCount])
            {
                case "Name": NameReadOnly = new Range(range.Start.Value + sepIndex, range.End.Value + sepIndex); break;
                case "Fontname": FontnameReadOnly = new Range(range.Start.Value + sepIndex, range.End.Value + sepIndex); break;
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
                case "BorderStyle": BorderStyle = byte.Parse(value); break;
                case "Outline": Outline = double.Parse(value); break;
                case "Shadow": Shadow = double.Parse(value); break;
                case "Alignment": Alignment = byte.Parse(value); break;
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

    public void Write(TextWriter writer, string[] formats)
    {
        Helper.Write(writer, this, formats);
    }
}
