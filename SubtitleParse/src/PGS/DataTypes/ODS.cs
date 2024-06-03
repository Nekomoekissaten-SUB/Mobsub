using Mobsub.Helper;

namespace Mobsub.SubtitleParse.PGS.DataTypes;

public struct ObjectDefinitionSegment
{
    //public SegmentHeader Header;

    /// <summary>
    /// ID of this object
    /// </summary>
    public ushort ObjectID;

    /// <summary>
    /// Version of this object
    /// </summary>
    public byte ObjectVersionNumber;

    /// <summary>
    /// If the image is split into a series of consecutive fragments, the last fragment has this flag set.
    /// Possible values:
    /// 0x40: Last in sequence
    /// 0x80: First in sequence
    /// 0xC0: First and last in sequence (0x40 | 0x80)
    /// </summary>
    public SequenceFlag LastInSequenceFlag;

    /// <summary>
    /// The length of the Run-length Encoding (RLE) data buffer with the compressed image data.
    /// </summary>
    public UInt24 ObjectDataLength;

    /// <summary>
    /// Width of the image
    /// </summary>
    public ushort Width;

    /// <summary>
    /// Height of the image
    /// </summary>
    public ushort Height;

    /// <summary>
    /// This is the image data compressed using Run-length Encoding (RLE).
    /// </summary>
    public byte[] ObjectData;
}

[Flags]
public enum SequenceFlag : byte
{
    Last = 0x40,
    First = 0x80,
    LastAndFirst = 0xC0,
}