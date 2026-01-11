﻿using Mobsub.Helper;
using Mobsub.SubtitleParse.PGS.DataTypes;

namespace Mobsub.SubtitleParse.PGS;

public class PGSData
{
    public sealed class SegmentItem
    {
        public SegmentHeader Header;
        public SegmentType Type;
        public PresentationCompositionSegment? PCS;
        public WindowDefinitionSegment? WDS;
        public PaletteDefinitionSegment? PDS;
        public ObjectDefinitionSegment? ODS;
        public SimpleBitmap? Bitmap;
        public bool IsEndOfDisplaySet => Type == SegmentType.EndOfDisplaySetSegment;
    }

    public static List<DisplaySet> GetDisplaySets(string filename)
    {
        var list = new List<DisplaySet>();
        var ds = new DisplaySet();

        foreach (var item in EnumerateSegments(filename, ParseFlag.OnlyRead))
        {
            switch (item.Type)
            {
                case SegmentType.PresentationCompositionSegment: ds.PCS = item.PCS!.Value; break;
                case SegmentType.WindowDefinitionSegment: ds.WDS = item.WDS; break;
                case SegmentType.PaletteDefinitionSegment: ds.PDS = item.PDS; break;
                case SegmentType.ObjectDefinitionSegment: ds.ODS = item.ODS; break;
                case SegmentType.EndOfDisplaySetSegment:
                    ds.EndHeader = item.Header;
                    list.Add(ds);
                    ds = new DisplaySet();
                    break;
            }
        }
        return list;
    }

    public static void DecodeImages(string filename, string saveDir, byte threshold)
    {
        foreach (var _ in EnumerateSegments(filename, ParseFlag.DecodeImages, saveDir, threshold)) { }
    }

    public static IEnumerable<SimpleBitmap?> DecodeBitmapData(string filename, byte threshold = 128)
    {
        foreach (var item in EnumerateSegments(filename, ParseFlag.DecodeImages | ParseFlag.WithoutSaveFile, null, threshold))
        {
            if (item.Type == SegmentType.ObjectDefinitionSegment && item.Bitmap is not null)
                yield return item.Bitmap;
        }
    }

    public static async Task DecodeImagesAsync(string filename, string saveDir, byte threshold)
    {
        await foreach (var _ in EnumerateSegmentsAsync(filename, ParseFlag.DecodeImages, saveDir, threshold)) { }
    }

    public static async Task<List<DisplaySet>> GetDisplaySetsAsync(string filename)
    {
        var list = new List<DisplaySet>();
        var ds = new DisplaySet();

        await foreach (var item in EnumerateSegmentsAsync(filename, ParseFlag.OnlyRead))
        {
            switch (item.Type)
            {
                case SegmentType.PresentationCompositionSegment: ds.PCS = item.PCS!.Value; break;
                case SegmentType.WindowDefinitionSegment: ds.WDS = item.WDS; break;
                case SegmentType.PaletteDefinitionSegment: ds.PDS = item.PDS; break;
                case SegmentType.ObjectDefinitionSegment: ds.ODS = item.ODS; break;
                case SegmentType.EndOfDisplaySetSegment:
                    ds.EndHeader = item.Header;
                    list.Add(ds);
                    ds = new DisplaySet();
                    break;
            }
        }
        return list;
    }

    private static IEnumerable<SegmentItem> EnumerateSegments(string filename, ParseFlag flag, string? saveDir = null, byte threshold = 0)
    {
        if (flag.HasFlag(ParseFlag.DecodeImages)) saveDir ??= Path.GetDirectoryName(filename);

        using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
        using var reader = new BigEndianBinaryReader(fs);
        var pgs = new Parse(reader, flag) { saveDir = saveDir, ImageBinarizeThreshold = threshold };

        while (reader.PeekChar() != -1)
        {
            yield return ParseSegmentCore(pgs, reader, flag);
        }
    }

    private static async IAsyncEnumerable<SegmentItem> EnumerateSegmentsAsync(string filename, ParseFlag flag, string? saveDir = null, byte threshold = 0)
    {
        if (flag.HasFlag(ParseFlag.DecodeImages)) saveDir ??= Path.GetDirectoryName(filename);

        await using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new BigEndianBinaryReader(fs);
        var pgs = new Parse(reader, flag) { saveDir = saveDir, ImageBinarizeThreshold = threshold };

        while (reader.PeekChar() != -1)
        {
            yield return ParseSegmentCore(pgs, reader, flag);
            await Task.Yield();
        }
    }

    private static SegmentItem ParseSegmentCore(Parse pgs, BigEndianBinaryReader reader, ParseFlag flag)
    {
        var header = pgs.ParseHeader();
        var item = new SegmentItem { Header = header, Type = header.Type };

        switch (header.Type)
        {
            case SegmentType.PresentationCompositionSegment:
                pgs.ParsePCS(header, out var pcs);
                item.PCS = pcs;
                break;
            case SegmentType.WindowDefinitionSegment:
                pgs.ParseWDS(header, out var wds);
                item.WDS = wds;
                break;
            case SegmentType.PaletteDefinitionSegment:
                pgs.ParsePDS(header, out var pds);
                item.PDS = pds;
                break;
            case SegmentType.ObjectDefinitionSegment:
                pgs.ParseODS(header, out var ods);
                item.ODS = ods;
                if (pgs.Standalone && (flag & ParseFlag.WithoutSaveFile) != 0)
                {
                    item.Bitmap = pgs.GetBitmap();
                    pgs.Standalone = false;
                }
                break;
            case SegmentType.EndOfDisplaySetSegment:
                Parse.CheckSize(header.Size, 0);
                break;
            default:
                throw new InvalidDataException();
        }
        return item;
    }

    public struct DisplaySet
    {
        public PresentationCompositionSegment PCS;
        public WindowDefinitionSegment? WDS;
        public PaletteDefinitionSegment? PDS;
        public ObjectDefinitionSegment? ODS;
        public SegmentHeader EndHeader;
    }
    private readonly SegmentType[] displaySetSegOrder = [
        SegmentType.PresentationCompositionSegment,
        SegmentType.WindowDefinitionSegment,
        SegmentType.PaletteDefinitionSegment,
        SegmentType.ObjectDefinitionSegment,
        SegmentType.EndOfDisplaySetSegment,
    ];

}
