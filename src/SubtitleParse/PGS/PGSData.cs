using Mobsub.Helper;
using Mobsub.SubtitleParse.PGS.DataTypes;

namespace Mobsub.SubtitleParse.PGS;

public class PGSData
{
    public static List<DisplaySet> GetDisplaySets(string filename)
    {
        return Decode(filename, ParseFlag.OnlyRead).ToList();
    }

    public static void DecodeImages(string filename, string saveDir, byte imageBinarizeThreshold)
    {
        foreach (var _ in Decode(filename, ParseFlag.DecodeImages, saveDir, imageBinarizeThreshold)) { }
    }

    public static Task DecodeImagesAsync(string filename, string saveDir, byte imageBinarizeThreshold)
    {
        foreach (var _ in Decode(filename, ParseFlag.DecodeImages, saveDir, imageBinarizeThreshold)) { }
        return Task.CompletedTask;
    }

    private static IEnumerable<DisplaySet> Decode(string filename, ParseFlag flag, string? saveDir = null, byte imageBinarizeThreshold = 0)
    {
        if (flag.HasFlag(ParseFlag.DecodeImages))
        {
            saveDir ??= Path.GetDirectoryName(filename);
        }

        using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
        using var reader = new BigEndianBinaryReader(fs);
        var pgs = new Parse(reader, flag) { saveDir = saveDir, ImageBinarizeThreshold = imageBinarizeThreshold };

        var ds = new DisplaySet();
        while (reader.PeekChar() != -1)
        {
            var header = pgs.ParseHeader();
            switch (header.Type)
            {
                case SegmentType.PresentationCompositionSegment:
                    pgs.ParsePCS(header, out ds.PCS);
                    break;
                case SegmentType.WindowDefinitionSegment:
                    pgs.ParseWDS(header, out var wds);
                    ds.WDS = wds;
                    break;
                case SegmentType.PaletteDefinitionSegment:
                    pgs.ParsePDS(header, out var pds);
                    ds.PDS = pds;
                    break;
                case SegmentType.ObjectDefinitionSegment:
                    pgs.ParseODS(header, out var ods);
                    ds.ODS = ods;
                    break;
                case SegmentType.EndOfDisplaySetSegment:
                    Parse.CheckSize(header.Size, 0);
                    ds.EndHeader = header;
                    yield return ds;
                    break;
                default:
                    throw new InvalidDataException();
            }
        }
    }
    
    public static IEnumerable<SimpleBitmap?> DecodeBitmapData(string filename, byte imageBinarizeThreshold = 128)
    {
        using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
        using var reader = new BigEndianBinaryReader(fs);
        var pgs = new Parse(reader, ParseFlag.DecodeImages | ParseFlag.WithoutSaveFile) { ImageBinarizeThreshold = imageBinarizeThreshold};

        while (reader.PeekChar() != -1)
        {
            var header = pgs.ParseHeader();
            switch (header.Type)
            {
                case SegmentType.PresentationCompositionSegment:
                    pgs.ParsePCS(header, out _);
                    break;
                case SegmentType.WindowDefinitionSegment:
                    pgs.ParseWDS(header, out _);
                    break;
                case SegmentType.PaletteDefinitionSegment:
                    pgs.ParsePDS(header, out _);
                    break;
                case SegmentType.ObjectDefinitionSegment:
                    pgs.ParseODS(header, out _);
                    if (pgs.Standalone)
                    {
                        yield return pgs.GetBitmap();
                        pgs.Standalone = false;
                    }
                    break;
                case SegmentType.EndOfDisplaySetSegment:
                    Parse.CheckSize(header.Size, 0);
                    break;
                default:
                    throw new InvalidDataException();
            }
        }
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
