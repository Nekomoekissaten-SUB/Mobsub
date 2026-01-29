using System.Buffers;
using System.Text;
using Mobsub.SubtitleParse.AssUtils;

namespace Mobsub.SubtitleParse.AssTypes;

public struct AssStyle
{
    public ReadOnlyMemory<byte> LineRaw { get; set; }
    public bool IsCommentLine { get; set; } = false;

    public Range NameReadOnly { get; set; }
    public Range FontnameReadOnly { get; set; }
    public double Fontsize { get; set; }
    public AssColor32 PrimaryColour { get; set; }
    public AssColor32 SecondaryColour { get; set; }
    public AssColor32 OutlineColour { get; set; }
    public AssColor32 BackColour { get; set; }
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

    public bool TryGetNameOverride(out ReadOnlySpan<char> name)
    {
        if (_name is null)
        {
            name = default;
            return false;
        }

        name = _name.AsSpan();
        return true;
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

        if (header.SequenceEqual(AssConstants.StylesLineHeaders.CommentSlash))
        {
            IsCommentLine = true;
            return;
        }
        else if (header.SequenceEqual(AssConstants.StylesLineHeaders.Style))
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
                case AssConstants.StyleFields.Name: NameReadOnly = new Range(range.Start.Value + sepIndex, range.End.Value + sepIndex); break;
                case AssConstants.StyleFields.Fontname: FontnameReadOnly = new Range(range.Start.Value + sepIndex, range.End.Value + sepIndex); break;
                case AssConstants.StyleFields.Fontsize: Fontsize = Utils.ParseDouble(value); break;
                case AssConstants.StyleFields.PrimaryColour: PrimaryColour = AssColor32.Parse(value); break;
                case AssConstants.StyleFields.SecondaryColour: SecondaryColour = AssColor32.Parse(value); break;
                case AssConstants.StyleFields.OutlineColour: OutlineColour = AssColor32.Parse(value); break;
                case AssConstants.StyleFields.BackColour: BackColour = AssColor32.Parse(value); break;
                case AssConstants.StyleFields.Bold: Bold = Utils.ParseSByte(value) == -1; break;
                case AssConstants.StyleFields.Italic: Italic = Utils.ParseSByte(value) == -1; break;
                case AssConstants.StyleFields.Underline: Underline = Utils.ParseSByte(value) == -1; break;
                case AssConstants.StyleFields.StrikeOut: StrikeOut = Utils.ParseSByte(value) == -1; break;
                case AssConstants.StyleFields.ScaleX: ScaleX = Utils.ParseDouble(value); break;
                case AssConstants.StyleFields.ScaleY: ScaleY = Utils.ParseDouble(value); break;
                case AssConstants.StyleFields.Spacing: Spacing = Utils.ParseDouble(value); break;
                case AssConstants.StyleFields.Angle: Angle = Utils.ParseDouble(value); break;
                case AssConstants.StyleFields.BorderStyle: BorderStyle = Utils.ParseByte(value); break;
                case AssConstants.StyleFields.Outline: Outline = Utils.ParseDouble(value); break;
                case AssConstants.StyleFields.Shadow: Shadow = Utils.ParseDouble(value); break;
                case AssConstants.StyleFields.Alignment: Alignment = Utils.ParseByte(value); break;
                case AssConstants.StyleFields.MarginL: MarginL = Utils.ParseInt(value); break;
                case AssConstants.StyleFields.MarginR: MarginR = Utils.ParseInt(value); break;
                case AssConstants.StyleFields.MarginV: MarginV = Utils.ParseInt(value); break;
                case AssConstants.StyleFields.MarginT: MarginT = Utils.ParseInt(value); break;
                case AssConstants.StyleFields.MarginB: MarginB = Utils.ParseInt(value); break;
                case AssConstants.StyleFields.Encoding: Encoding = Utils.ParseInt(value); break;
                case AssConstants.StyleFields.AlphaLevel: AlphaLevel = Utils.ParseInt(value); break;
                case AssConstants.StyleFields.RelativeTo: RelativeTo = Utils.ParseInt(value); break;
            }

            segCount++;
        }
    }

    public void Write(TextWriter writer, string[] formats)
    {
        Helper.Write(writer, this, formats);
    }
}
