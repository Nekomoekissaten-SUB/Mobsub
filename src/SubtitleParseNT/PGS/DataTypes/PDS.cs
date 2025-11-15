namespace Mobsub.SubtitleParse.PGS.DataTypes;

public struct PaletteDefinitionSegment
{
    //public SegmentHeader Header;

    /// <summary>
    /// ID of the palette
    /// </summary>
    public byte PaletteID;

    /// <summary>
    /// Version of this palette within the Epoch
    /// </summary>
    public byte PaletteVersionNumber;

    public Palette[] Palettes;
}

public struct Palette
{
    /// <summary>
    /// Entry number of the palette
    /// </summary>
    public byte PaletteEntryID;

    /// <summary>
    /// Luminance (Y value)
    /// </summary>
    public byte LuminanceY;

    /// <summary>
    /// Color Difference Red (Cr value)
    /// </summary>
    public byte ColorDifferenceRedCr;

    /// <summary>
    /// Color Difference Blue (Cb value)
    /// </summary>
    public byte ColorDifferenceBlueCb;

    /// <summary>
    /// Transparency (Alpha value)
    /// </summary>
    public byte TransparencyAlpha;
}