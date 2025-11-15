namespace Mobsub.SubtitleParse.PGS.DataTypes;

public struct SegmentHeader
{
    //public short MagicNumber;
    public int PTS;
    public int DTS;
    public SegmentType Type;
    public ushort Size;
}

[Flags]
public enum SegmentType : byte
{
    PaletteDefinitionSegment = 0x14,
    ObjectDefinitionSegment = 0x15,
    PresentationCompositionSegment = 0x16,
    WindowDefinitionSegment = 0x17,
    EndOfDisplaySetSegment = 0x80,
}