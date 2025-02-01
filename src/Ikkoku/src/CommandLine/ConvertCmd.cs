using Mobsub.Ikkoku.SubtileProcess;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleParse.PGS;
using System.CommandLine;
using Mobsub.SubtitleProcess;

namespace Mobsub.Ikkoku.CommandLine;

internal class ConvertCmd
{
    internal static Command Build(Argument<FileSystemInfo> path, Option<FileSystemInfo> optPath)
    {
        var inputSuffix = new Option<string>(
            name: "--from-format",
            description: "Format which will convert from"
        );
        var convertSuffix = new Option<string>(
            name: "--to-format",
            description: "Format which will convert to"
        )
        { IsRequired = true };
        var convSubtitleCommand = new Command("convert", "Convert subtitle format")
        {
            path, optPath, convertSuffix, inputSuffix
        };
        convSubtitleCommand.SetHandler(Execute, path, optPath, convertSuffix, inputSuffix);
        return convSubtitleCommand;
    }

    internal static void Execute(FileSystemInfo path, FileSystemInfo? optPath, string convertSuffix, string inputSuffix)
    {
        switch (path)
        {
            case FileInfo f:
                ConvertSubtitle(f, optPath, convertSuffix);
                break;
            case DirectoryInfo d:
                var files = Utils.Traversal(d, inputSuffix);
                foreach (var f in files)
                {
                    ConvertSubtitle(f, optPath, convertSuffix);
                }
                break;
        }
    }

    internal static void ConvertSubtitle(FileInfo fromFile, FileSystemInfo? optPath, string convertSuffix)
    {
        if (fromFile.Extension == convertSuffix)
        {
            throw new Exception($"{convertSuffix} can’t same as {fromFile.Extension}");
        }

        DirectoryInfo optDir = fromFile.Directory!;
        switch (optPath)
        {
            case DirectoryInfo d:
                optDir = d;
                break;
            case FileInfo f:
                optDir = f.Directory!;
                break;
            default:
                break;
        }

        switch (fromFile.Extension)
        {
            case ".ass":
                var ass = new AssData();
                ass.ReadAssFile(fromFile.FullName);

                switch (convertSuffix)
                {
                    case ".txt":
                        var optFile = Utils.ChangeSuffix(fromFile, optDir, convertSuffix);
                        var fs = new FileStream(optFile.FullName, FileMode.Create, FileAccess.Write);
                        using (var memStream = new MemoryStream())
                        {
                            using var sw = new StreamWriter(memStream, SubtitleParse.Utils.EncodingRefOS());
                            ConvertSub.ConvertAssToTxt(sw, ass);
                            sw.Flush();
                            memStream.Seek(0, SeekOrigin.Begin);
                            memStream.CopyTo(fs);
                        }
                        break;
                    default:
                        throw new NotImplementedException($"Unsupported: {fromFile.Extension} convert to {convertSuffix}.");
                }
                break;
            case ".sup":
                switch (convertSuffix)
                {
                    case ".bmp":
                        PGSData.DecodeImages(fromFile.FullName, optDir.FullName);
                        break;
                    case ".txt":
                        var optFile = Utils.ChangeSuffix(fromFile, optDir, convertSuffix);
                        ConvertImageSubtitle.OcrPgsSup(fromFile.FullName, optFile.FullName);
                        break;
                    default:
                        throw new NotImplementedException($"Unsupported: {fromFile.Extension} convert to {convertSuffix}.");
                }
                break;
            default:
                throw new NotImplementedException($"Unsupported: {fromFile.Extension}.");
        }
    }
}
