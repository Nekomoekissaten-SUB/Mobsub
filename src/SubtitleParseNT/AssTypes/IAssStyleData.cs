namespace Mobsub.SubtitleParseNT2.AssTypes;

public interface IAssStyleData
{
    public string Name { get; }
    public string Fontname { get; } // GDI max 32, last is null
    public double Fontsize { get; }  // ushort; Is negative and float really correct?
    public AssRGB8 PrimaryColour { get; }
    public AssRGB8 SecondaryColour { get; }
    public AssRGB8 OutlineColour { get; }
    public AssRGB8 BackColour { get; }
    public bool Bold { get; }     // ? 0 / 400, 1 / 700
    public bool Italic { get; }
    public bool Underline { get; }  // 0 = false, -1 = true
    public bool StrikeOut { get; }
    public double ScaleX { get; }
    public double ScaleY { get; }
    public double Spacing { get; }
    public double Angle { get; }
    public short BorderStyle { get; }  // 1, 3?
    public double Outline { get; }
    public double Shadow { get; }
    public short Alignment { get; }  // 1-9
    public int MarginL { get; }
    public int MarginR { get; }
    public int MarginV { get; }
    public int MarginT { get; }
    public int MarginB { get; }
    public int Encoding { get; }
    public int AlphaLevel { get; }
    public int RelativeTo { get; }
}
