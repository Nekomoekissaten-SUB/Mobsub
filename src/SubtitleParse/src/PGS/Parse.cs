using System.Diagnostics;
using CommunityToolkit.Diagnostics;
using Mobsub.Helper;
using Mobsub.SubtitleParse.PGS.DataTypes;
using static Mobsub.Helper.ColorConv;

namespace Mobsub.SubtitleParse.PGS;

[Flags]
public enum ParseFlag
{
    None = 0b_0000_0000,
    OnlyRead = 0b_0000_0001,
    DecodeImages = 0b_0000_0010,
    WithoutSaveFile = 0b_0000_0100,
    //DecodeTimestamps = 2,
}

public class Parse(BigEndianBinaryReader reader, ParseFlag flag = ParseFlag.OnlyRead)
{
    private ParseFlag parseFlag = flag;

    private int row = 0;
    private int col = 0;
    private PresentationCompositionSegment curPCS;
    private Dictionary<byte, PaletteDefinitionSegment> curPalettes = [];
    private uint previousObjectDataLength = 0;
    private int imageIndex = 0;
    private SimpleBitmap? image;
    internal string? saveDir;
    
    public bool Standalone = true;
    public byte ImageBinarizeThreshold = 0;

    internal SegmentHeader ParseHeader()
    {
        var start = reader.ReadInt16();
        if (start != 0x5047)
        {
            throw new Exception("Segment header must be 0x5047 ('PG')");
        }
        return new SegmentHeader
        {
            PTS = reader.ReadInt32(),
            DTS = reader.ReadInt32(),
            Type = (SegmentType)reader.ReadByte(),
            Size = reader.ReadUInt16(),
        };
    }

    public void ParsePCS(SegmentHeader header, out PresentationCompositionSegment pcs)
    {
        pcs = new PresentationCompositionSegment
        {
            //Header = header,
            Width = reader.ReadUInt16(),
            Height = reader.ReadUInt16(),
            FrameRate = reader.ReadByte(),
            CompositionNumber = reader.ReadUInt16(),
            CompositionState = (CompositionType)reader.ReadByte(),
            PaletteUpdateFlag = (PaletteUpdateFlag)reader.ReadByte(),
            PaletteID = reader.ReadByte(),
            NumberOfCompositionObjects = reader.ReadByte()
        };
        
        if (pcs.NumberOfCompositionObjects == 0) { return; }
        if (pcs.NumberOfCompositionObjects > 2) { throw new InvalidDataException(); }
        pcs.compositionObjects = new CompositionObject[pcs.NumberOfCompositionObjects];
        for (var i = 0; i < pcs.NumberOfCompositionObjects; i++)
        {
            var obj = new CompositionObject
            {
                ObjectID = reader.ReadInt16(),
                WindowID = reader.ReadByte(),
                ObjectCroppedFlag = reader.ReadByte(),
                ObjectHorizontalPosition = reader.ReadUInt16(),
                ObjectVerticalPosition = reader.ReadUInt16(),
            };
            if (obj.ObjectCroppedFlag == 0x40)
            {
                obj.ObjectCroppingHorizontalPosition = reader.ReadUInt16();
                obj.ObjectCroppingVerticalPosition = reader.ReadUInt16();
                obj.ObjectCroppingWidth = reader.ReadUInt16();
                obj.ObjectCroppingHeight = reader.ReadUInt16();
            }
            pcs.compositionObjects[i] = obj;
        }

        if (parseFlag.HasFlag(ParseFlag.DecodeImages))
        {
            switch (pcs.CompositionState)
            {
                case CompositionType.EpochStart:
                case CompositionType.AcquisitionPoint:
                    curPCS = pcs;
                    break;
                case CompositionType.Normal:
                    curPCS.PaletteID = pcs.PaletteID;
                    if (pcs.PaletteUpdateFlag == PaletteUpdateFlag.False)
                    {
                        curPCS.NumberOfCompositionObjects = pcs.NumberOfCompositionObjects;
                        curPCS.compositionObjects = pcs.compositionObjects;
                    }
                    break;
            }
        }

        return;
    }
    public void ParseWDS(SegmentHeader header, out WindowDefinitionSegment wds)
    {
        wds = new WindowDefinitionSegment
        {
            //Header = header,
            NumberOfWindows = reader.ReadByte(),
        };
        if (wds.NumberOfWindows == 0) { return; }
        wds.Windows = new Window[wds.NumberOfWindows];
        for (var i = 0; i < wds.NumberOfWindows; i++)
        {
            wds.Windows[i] = new Window
            {
                WindowID = reader.ReadByte(),
                WindowHorizontalPosition = reader.ReadInt16(),
                WindowVerticalPosition = reader.ReadInt16(),
                WindowWidth = reader.ReadUInt16(),
                WindowHeight = reader.ReadUInt16()
            };
        }

        return;
    }
    public void ParsePDS(SegmentHeader header, out PaletteDefinitionSegment pds)
    {
        pds = new PaletteDefinitionSegment
        {
            //Header = header,
            PaletteID = reader.ReadByte(),
            PaletteVersionNumber = reader.ReadByte(),
        };
        var count = (header.Size - 2) / 5;

        if (count > 0)
        {
            pds.Palettes = new Palette[count];
            for (var i = 0; i < count; i++)
            {
                pds.Palettes[i] = new Palette
                {
                    PaletteEntryID = reader.ReadByte(),
                    LuminanceY = reader.ReadByte(),
                    ColorDifferenceRedCr = reader.ReadByte(),
                    ColorDifferenceBlueCb = reader.ReadByte(),
                    TransparencyAlpha = reader.ReadByte(),
                };
            }

            if (parseFlag.HasFlag(ParseFlag.DecodeImages))
            {
                if (!curPalettes.TryAdd(pds.PaletteID, pds))
                {
                    if (pds.PaletteVersionNumber > curPalettes[pds.PaletteID].PaletteVersionNumber)
                    {
                        curPalettes[pds.PaletteID] = pds;
                    }
                }
            }
        }
        return;
    }
    public void ParseODS(SegmentHeader header, out ObjectDefinitionSegment ods)
    {
        ods = new ObjectDefinitionSegment
        {
            //Header = header,
            ObjectID = reader.ReadUInt16(),
            ObjectVersionNumber = reader.ReadByte(),
            LastInSequenceFlag = (SequenceFlag)reader.ReadByte(),
            ObjectDataLength = reader.ReadUInt24(),
            Width = reader.ReadUInt16(),
            Height = reader.ReadUInt16()
        };

        var start = reader.BaseStream.Position;
        if (parseFlag.HasFlag(ParseFlag.OnlyRead))
        {
            ods.ObjectData = reader.ReadBytes((int)ods.ObjectDataLength - 4);
        }
        
        if (parseFlag.HasFlag(ParseFlag.DecodeImages))
        {
            if (!parseFlag.HasFlag(ParseFlag.WithoutSaveFile) && saveDir is null) { ThrowHelper.ThrowArgumentNullException(nameof(saveDir)); }
            reader.BaseStream.Position = start;
            row = col = 0;
            
            if ((uint)ods.ObjectDataLength != previousObjectDataLength)
            {
                Standalone = true;
                previousObjectDataLength = (uint)ods.ObjectDataLength;

                image = new SimpleBitmap(ods.Width, ods.Height);
                DecodeImage((uint)ods.ObjectDataLength - 4);
                if (ImageBinarizeThreshold > 0) image.Binarize(ImageBinarizeThreshold);
                
                if (parseFlag.HasFlag(ParseFlag.WithoutSaveFile)) return;
                var imgPath = Path.Combine(saveDir!, $"{imageIndex}.bmp");
                image.Save(imgPath);
                imageIndex++;
            }
            else
            {
                reader.ReadBytes((int)ods.ObjectDataLength - 4);
            }
        }
        
        return;
    }

    /// <summary>
    /// Decode four-stage run-length encoding
    /// </summary>
    /// <param name="length"></param>
    private void DecodeImage(uint length)
    {
        byte b;
        var pallettes = curPalettes[curPCS.PaletteID].Palettes.ToDictionary(entry => entry.PaletteEntryID, entry => YCbCr2ARGB2(entry));
        var start = reader.BaseStream.Position;

        while (reader.BaseStream.Position < start + length)
        {
            b = reader.ReadByte();
            if (b != 0)
            {
                image!.SetPixel(col, row, pallettes[b]);
                col++;
                continue;
            }

            b = reader.ReadByte();
            var flagA = (b & (1 << 7)) != 0;
            var flagB = (b & (1 << 6)) != 0;


            if (b == 0)
            {
                col = 0;
                row += 1;
            }
            else
            {
                var count = b & 0b_11_1111;

                ARGB8b color;
                switch (flagA)
                {
                    case false:
                        switch (flagB)
                        {
                            case true:
                                b = reader.ReadByte();
                                count = (count << 8) | b;
                                goto case false;
                            case false:
                                color = pallettes[0];
                                break;
                        }
                        break;
                    default:
                        switch (flagB)
                        {
                            case true:
                                b = reader.ReadByte();
                                count = (count << 8) | b;
                                goto case false;
                            case false:
                                color = pallettes[reader.ReadByte()];
                                break;
                        }
                        break;
                }

                image!.DrawHorizontalLine(col, row, col + count - 1, color);
                col += count;
            }
        }
    }

    private static ARGB8b YCbCr2ARGB2(Palette palette)
    {
        var yuv = new YCbCr8b(palette.LuminanceY, palette.ColorDifferenceBlueCb, palette.ColorDifferenceRedCr);
        return YCbCr2RGB(yuv, Matrix.bt709, ColorRange.limited, palette.TransparencyAlpha);
    }

    internal static void CheckSize(ushort headerSize, ushort segSize)
    {
        if (headerSize != segSize) { throw new InvalidDataException(); }
    }

    public SimpleBitmap? GetBitmap() => image;
}