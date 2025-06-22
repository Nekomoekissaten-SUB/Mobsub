using Mobsub.SubtitleParse.AssTypes;
using System.Diagnostics;
using Mobsub.Ikkoku.SubtileProcess;
using System.CommandLine;
using Mobsub.SubtitleProcess;

namespace Mobsub.Ikkoku.CommandLine;

internal class CleanCmd
{
    internal static Command Build(Argument<FileSystemInfo> path, Option<FileSystemInfo> optPath, Option<bool> verbose)
    {
        var extractBinaries = new Option<bool>("--extract-binaries") { Description = "Extract binaries, such as section Fonts and Graphics." };
        var keepCommentLines = new Option<bool>("--keep-comments") { Description = "Keep comment lines (start with ;) in section Script Info." };
        var addLayoutRes = new Option<bool>("--add-layoutres") { Description = "Add LayoutResX/Y in Script Info, default value will same as PlayResX/Y" };
        var dropUnusedStyles = new Option<bool>("--drop-unused-styles") { Description = "Remove unused styles not used in Events" };
        var cleanPreset = new Option<CleanPreset>("--preset")
        {
            Description = "CleanAss preset, default is Basic",
            DefaultValueFactory = _ => CleanPreset.Basic,
        };

        var cleanCommand = new Command("clean", "Clean Your ASS! Remove unused script info and sections, check undefined styles.")
        {
            path, optPath, extractBinaries, keepCommentLines, verbose, addLayoutRes, dropUnusedStyles, cleanPreset
        };

        cleanCommand.SetAction(result =>
        {
            Execute(
                result.GetValue(path)!,
                result.GetValue(optPath),
                result.GetValue(extractBinaries),
                result.GetValue(keepCommentLines),
                result.GetValue(verbose),
                result.GetValue(addLayoutRes),
                result.GetValue(dropUnusedStyles),
                result.GetValue(cleanPreset)
                );
        });

        return cleanCommand;
    }

    internal enum CleanPreset
    {
        Basic,  // for vcb-s
        More,   // for nekomoe
    }

    internal static void Execute(FileSystemInfo path, FileSystemInfo? optPath, bool extractBinaries, bool keepCommentLines, bool verbose, bool addLayoutRes, bool dropUnusedStyles, CleanPreset preset)
    {
        var sw = new Stopwatch();
        sw.Start();

        DirectoryInfo? binDir = null;
        FileInfo opt;
        var ext = ".ass";

        var args = new Clean.CleanAssArgs();
        switch (preset)
        {
            case CleanPreset.Basic:
                args.keepComment = true;
                break;
            case CleanPreset.More:
                args.keepComment = false;
                args.renameTitle = true;
                args.addLayoutRes = true;
                args.dropUnusedStyles = false;
                args.processEvents = true;
                args.rmMotionGarbage = true;
                args.deleteFanhuaji = true;
                args.dropDuplicateStyles = true;
                args.fixStyleName = true;
                break;
        }

        // override
        if (keepCommentLines)
            args.keepComment = true;
        if (addLayoutRes)
            args.addLayoutRes = true;
        if (dropUnusedStyles)
            args.dropUnusedStyles = true;

        switch (path)
        {
            case FileInfo iptFile:
                optPath ??= iptFile;
                switch (optPath)
                {
                    case FileInfo optFile:
                        binDir = extractBinaries ? optFile.Directory : binDir;
                        opt = optFile;
                        break;
                    case DirectoryInfo d:
                        if (!d.Exists)
                        {
                            d.Create();
                        }
                        binDir = extractBinaries ? d : binDir;
                        opt = new FileInfo(Path.Combine(d.FullName, iptFile.Name));
                        break;
                    default:
                        throw new IOException();
                }

                CleanOneAss(iptFile, opt, binDir, verbose, args);
                break;

            case DirectoryInfo iptDir:

                var subfiles = Utils.Traversal(iptDir, ext);
                binDir = extractBinaries ? iptDir : binDir;
                optPath ??= iptDir;

                switch (optPath)
                {
                    case FileInfo:
                        Console.WriteLine("Output path must be directory when input path is a dir!");
                        break;
                    case DirectoryInfo d:
                        if (!d.Exists)
                        {
                            d.Create();
                        }
                        foreach (var f in subfiles)
                        {
                            opt = new FileInfo(Path.Combine(d.FullName, f.Name));
                            CleanOneAss(f, opt, binDir, verbose, args);
                        }
                        break;
                    default:
                        throw new IOException();
                }
                break;
        }

        sw.Stop();
        if (verbose)
        {
            Console.WriteLine($"Total Time: {sw.Elapsed}");
        }
    }

    private static void CleanOneAss(FileInfo f, FileInfo opt, DirectoryInfo? binDir, bool verbose, Clean.CleanAssArgs args)
    {
        Console.WriteLine(f);
        var fs = new FileStream(f.FullName, FileMode.Open, FileAccess.ReadWrite);
        var fileNoSuffix = f.Name.AsSpan()[..(f.Name.Length - 4)];

        AssParseOption assOption = AssParseOption.None;
        if (args.dropDuplicateStyles)
        {
            assOption |= AssParseOption.DropDuplicateStyle;
        }
        else if (args.fixStyleName)
        {
            assOption |= AssParseOption.FixStyleName;
        }
        var data = new AssData() { ParseOptions = assOption };
        data.ReadAssFile(fs);

        if (binDir is not null)
        {
            Clean.ExtractBinaries(data, binDir);
        }

        Clean.CleanAss(data, fileNoSuffix, args, out string msg, out bool untouched);

        if (!untouched)
        {
            data.WriteAssFile(opt.FullName);
        }

        if (verbose)
        {
            Console.WriteLine(msg);
        }
    }
}
