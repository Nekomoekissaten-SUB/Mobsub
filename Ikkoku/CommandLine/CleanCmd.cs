using Mobsub.SubtitleParse.AssTypes;
using System.Diagnostics;
using Mobsub.Ikkoku.SubtileProcess;

namespace Mobsub.Ikkoku.CommandLine;

internal class CleanCmd
{
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
                break;
            default:
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
        var data = new AssData();
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
