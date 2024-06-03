namespace Mobsub.SubtitleParse.PGS.DataTypes;

public struct WindowDefinitionSegment
{
    //public SegmentHeader Header;

    /// <summary>
    /// Number of windows defined in this segment
    /// </summary>
    public byte NumberOfWindows;

    public Window[] Windows;
}
public struct Window
{
    /// <summary>
    /// ID of this window
    /// </summary>
    public byte WindowID;

    /// <summary>
    /// X offset from the top left pixel of the window in the screen.
    /// </summary>
    public short WindowHorizontalPosition;

    /// <summary>
    /// Y offset from the top left pixel of the window in the screen.
    /// </summary>
    public short WindowVerticalPosition;

    /// <summary>
    /// Width of the window
    /// </summary>
    public ushort WindowWidth;

    /// <summary>
    /// Height of the window
    /// </summary>
    public ushort WindowHeight;
}

