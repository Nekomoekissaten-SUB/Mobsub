using CommunityToolkit.Diagnostics;
using Mobsub.Helper;
using Mobsub.SubtitleParse.PGS.DataTypes;
using Mobsub.SubtitleParseNT2.PGS.DataTypes;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Mobsub.Helper.ColorConv;

namespace Mobsub.SubtitleParseNT2.PGS;

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

    private PresentationCompositionSegment curPCS;
    private Dictionary<byte, PaletteDefinitionSegment> curPalettes = [];
    private readonly uint[] paletteLutPacked = new uint[256];
    private byte currentPaletteId;

    private sealed class ObjectAssembly
    {
        public ushort ObjectId;
        public byte Version;
        public int Width;
        public int Height;
        // Buffer to accumulate RLE fragments (pooled or growable)
        public ArrayBufferWriter<byte> RleBuffer = new(1024);
    }
    private readonly Dictionary<ushort, ObjectAssembly> assemblies = [];

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

        if ((parseFlag & ParseFlag.DecodeImages) == 0) return;
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
        if (count <= 0) return;

        var entries = new Palette[count];
        for (int i = 0; i < entries.Length; i++)
        {
            var e = new Palette
            {
                PaletteEntryID = reader.ReadByte(),
                LuminanceY = reader.ReadByte(),
                ColorDifferenceRedCr = reader.ReadByte(),
                ColorDifferenceBlueCb = reader.ReadByte(),
                TransparencyAlpha = reader.ReadByte(),
            };
            entries[i] = e;

            if ((parseFlag & ParseFlag.DecodeImages) != 0)
            {
                var argb = YCbCr2ARGB2(e);
                paletteLutPacked[e.PaletteEntryID] = (uint)(argb.Blue | (argb.Green << 8) | (argb.Red << 16) | (argb.Alpha << 24));
            }
        }

        if ((parseFlag & ParseFlag.DecodeImages) == 0) return;
        if (!curPalettes.TryAdd(pds.PaletteID, pds))
        {
            if (pds.PaletteVersionNumber > curPalettes[pds.PaletteID].PaletteVersionNumber)
            {
                curPalettes[pds.PaletteID] = pds;
            }
        }
        currentPaletteId = pds.PaletteID;
    }
    public void ParseODS(SegmentHeader header, out ObjectDefinitionSegment ods)
    {
        ods = new ObjectDefinitionSegment
        {
            ObjectID = reader.ReadUInt16(),
            ObjectVersionNumber = reader.ReadByte(),
            LastInSequenceFlag = (SequenceFlag)reader.ReadByte(),
            ObjectDataLength = reader.ReadUInt24(),
            Width = reader.ReadUInt16(),
            Height = reader.ReadUInt16()
        };

        int payloadLen = (int)ods.ObjectDataLength - 4;
        var payload = reader.ReadBytes(payloadLen);

        Standalone = (uint)ods.ObjectDataLength != previousObjectDataLength;
        previousObjectDataLength = (uint)ods.ObjectDataLength;

        var flag = ods.LastInSequenceFlag;
        bool isFirst = (flag & SequenceFlag.First) != 0;
        bool isLast = (flag & SequenceFlag.Last) != 0;

        if (isFirst || isLast)
        {
            if (!assemblies.TryGetValue(ods.ObjectID, out var asm) || isFirst)
            {
                asm = new ObjectAssembly
                {
                    ObjectId = ods.ObjectID,
                    Version = ods.ObjectVersionNumber,
                    Width = ods.Width,
                    Height = ods.Height,
                    RleBuffer = new ArrayBufferWriter<byte>(payloadLen)
                };
                assemblies[ods.ObjectID] = asm;
            }

            asm.RleBuffer.Write(payload);

            if (isLast)
            {
                var rle = asm.RleBuffer.WrittenSpan;
                assemblies.Remove(ods.ObjectID);

                if (Standalone)
                    DecodeAndSave(rle, asm.Width, asm.Height);
            }
        }
        else if (Standalone)
        {
            DecodeAndSave(payload, ods.Width, ods.Height);
        }
    }
    private void DecodeAndSave(ReadOnlySpan<byte> rle, int width, int height)
    {
        image = new SimpleBitmap(width, height);
        DecodeImageSpan(rle, image, paletteLutPacked);

        if (ImageBinarizeThreshold > 0)
            image.BinarizeVector2(ImageBinarizeThreshold);

        if ((parseFlag & ParseFlag.WithoutSaveFile) == 0)
        {
            var imgPath = Path.Combine(saveDir!, $"{imageIndex}.bmp");
            image.Save2(imgPath);
            imageIndex++;
        }
    }

    private static ARGB8b YCbCr2ARGB2(Palette palette)
    {
        var yuv = new YCbCr8b(palette.LuminanceY, palette.ColorDifferenceBlueCb, palette.ColorDifferenceRedCr);
        return YCbCr2RGB_Int(yuv, Matrix.bt709, ColorRange.limited, palette.TransparencyAlpha);
    }

    internal static void CheckSize(ushort headerSize, ushort segSize)
    {
        if (headerSize != segSize) { throw new InvalidDataException(); }
    }

    public SimpleBitmap? GetBitmap() => image;

    private static void DecodeImageSpan(ReadOnlySpan<byte> data, SimpleBitmap dst, uint[] paletteLutPacked)
    {
        Span<uint> dstSpan = dst.GetPixelSpanUInt();
        int width = dst.GetWidth();
        int height = dst.GetHeight();

        int i = 0;
        int x = 0;
        int y = 0;

        while (i < data.Length && y < height)
        {
            byte b = data[i++];

            if (b != 0)
            {
                dstSpan[y * width + x] = paletteLutPacked[b];
                x++;
                continue;
            }

            if (i >= data.Length) break;
            byte b2 = data[i++];

            if (b2 == 0)
            {
                x = 0;
                y++;
                continue;
            }

            int count = b2 & 0x3F;
            if ((b2 & 0x40) != 0)
            {
                if (i >= data.Length) break;
                count = (count << 8) | data[i++];
            }

            uint color;
            if ((b2 & 0x80) == 0)
            {
                color = paletteLutPacked[0];
            }
            else
            {
                if (i >= data.Length) break;
                color = paletteLutPacked[data[i++]];
            }

            int dstIndex = y * width + x;

            if (dstIndex + count > dstSpan.Length)
            {
                count = dstSpan.Length - dstIndex;
            }

            FillRunVector(dstSpan, dstIndex, count, color);
            x += count;
        }
    }

    private static void FillRunVector(Span<uint> span, int start, int count, uint color)
    {
        var vecColor = new Vector<uint>(color);
        int step = Vector<uint>.Count;
        int i = 0;

        for (; i + step <= count; i += step)
        {
            vecColor.CopyTo(span.Slice(start + i, step));
        }

        for (; i < count; i++)
        {
            span[start + i] = color;
        }
    }
}