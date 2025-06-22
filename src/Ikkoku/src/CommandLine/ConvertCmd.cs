﻿using Mobsub.Ikkoku.SubtileProcess;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleParse.PGS;
using System.CommandLine;
using Mobsub.SubtitleProcess;

namespace Mobsub.Ikkoku.CommandLine;

internal class ConvertCmd
{
    internal static Command Build(Argument<FileSystemInfo> path, Option<FileSystemInfo> optPath)
    {
        var inputSuffix = new Option<string>("--from-format") { Description = "Format which will convert from" };
        var convertSuffix = new Option<string>("--to-format") { Description = "Format which will convert to", Required = true };
        var imageBinarizeThreshold = new Option<byte?>("--image-binarize-threshold")
        {
            Description = "Image Binarize Threshold when input is .sup, range is 0-255, 0 is disabled. Default: 0 (convert to .bmp) / 128 (convert to .txt)",
            DefaultValueFactory = _ => null
        };

        inputSuffix.Validators.Add(result =>
        {
            var p = result.GetValue(path);
            var s = result.GetValue(inputSuffix);
            if (p is DirectoryInfo && s is null)
            {
                result.AddError("You should specify --from-format when input is a directory.");
            }
        });

        var convSubtitleCommand = new Command("convert", "Convert subtitle format")
        {
            path, optPath, convertSuffix, inputSuffix, imageBinarizeThreshold
        };

        convSubtitleCommand.SetAction(result =>
        {
            Execute(
                result.GetValue(path)!,
                result.GetValue(optPath),
                result.GetValue(convertSuffix)!,
                result.GetValue(inputSuffix)!,
                result.GetValue(imageBinarizeThreshold)
                );
        });

        return convSubtitleCommand;
    }

    internal static void Execute(FileSystemInfo path, FileSystemInfo? optPath, string convertSuffix, string inputSuffix, byte? imageBinarizeThreshold)
    {
        switch (path)
        {
            case FileInfo f:
                ConvertSubtitle(f, optPath, convertSuffix, imageBinarizeThreshold);
                break;
            case DirectoryInfo d:
                var files = Utils.Traversal(d, inputSuffix);
                foreach (var f in files)
                {
                    ConvertSubtitle(f, optPath, convertSuffix, imageBinarizeThreshold);
                }
                break;
        }
    }

    internal static void ConvertSubtitle(FileInfo fromFile, FileSystemInfo? optPath, string convertSuffix, byte? imageBinarizeThreshold)
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
                        imageBinarizeThreshold ??= 0;
                        PGSData.DecodeImages(fromFile.FullName, optDir.FullName, (byte)imageBinarizeThreshold);
                        break;
                    case ".txt":
                        imageBinarizeThreshold ??= 128;
                        var optFile = Utils.ChangeSuffix(fromFile, optDir, convertSuffix);
                        ConvertImageSubtitle.OcrPgsSup(fromFile.FullName, optFile.FullName, (byte)imageBinarizeThreshold);
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
