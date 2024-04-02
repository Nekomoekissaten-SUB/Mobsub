using Mobsub.SubtitleParse.AssTypes;
using Mobsub.Ikkoku.SubtileProcess;
using Mobsub.Ikkoku.FormatData;

namespace Mobsub.Ikkoku.CommandLine;

// TimingPostProcessor
internal class TppCmd
{
    internal static void Execute(FileSystemInfo path, FileSystemInfo? optPath, string? shiftSpan, string[] styles, string fps, FileInfo? tcfile)
    {
        // select mode
        var tsp = (shiftSpan is null) ? TimeSpan.Zero : Utils.GetTimespan(shiftSpan, fps);
        var toCfr = tcfile is not null;

        // tpp
        FileInfo opt;
        var ext = ".ass";

        switch (path)
        {
            case FileInfo iptFile:

                optPath ??= iptFile;
                opt = optPath switch
                {
                    FileInfo optFile => optFile,
                    DirectoryInfo d => new FileInfo(Path.Combine(d.FullName, iptFile.Name)),
                    _ => throw new IOException(),
                };
                if (tsp != TimeSpan.Zero)
                {
                    ShiftOneAss(iptFile, opt, tsp, styles);
                }
                if (toCfr && tcfile is not null)
                {
                    ToCfrOneAss(iptFile, opt, tcfile, fps);
                }
                break;

            case DirectoryInfo iptDir:

                var subfiles = Utils.Traversal(iptDir, ext);
                optPath ??= iptDir;

                switch (optPath)
                {
                    case DirectoryInfo d:
                        if (!d.Exists)
                        {
                            d.Create();
                        }
                        if (tsp != TimeSpan.Zero)
                        {
                            foreach (var f in subfiles)
                            {
                                opt = new FileInfo(Path.Combine(d.FullName, f.Name));
                                ShiftOneAss(f, opt, tsp, styles);
                            }
                        }
                        if (toCfr && tcfile is not null)
                        {
                            foreach (var f in subfiles)
                            {
                                opt = new FileInfo(Path.Combine(d.FullName, f.Name));
                                ToCfrOneAss(f, opt, tcfile, fps);
                            }
                        }
                        break;
                }
                break;
        }
    }

    private static void ShiftOneAss(FileInfo f, FileInfo opt, TimeSpan tsp, string[] styles)
    {
        Console.WriteLine(f);
        var data = new AssData();
        data.ReadAssFile(f.FullName);

        if (styles.Length > 0)
        {
            var negation = styles[0] == "!";
            Tpp.ShiftAss(data.Events.Collection, tsp, negation ? styles[1..] : styles, negation);
        }
        else
        {
            Tpp.ShiftAss(data.Events.Collection, tsp);
        }

        data.WriteAssFile(opt.FullName);
        Console.WriteLine("fine.");
    }
    private static void ToCfrOneAss(FileInfo f, FileInfo opt, FileInfo tcfile, string fps)
    {
        Console.WriteLine(f);
        var data = new AssData();
        data.ReadAssFile(f.FullName);
        var tcdata = ParseTcfile(tcfile.FullName);
        Tpp.AssumeFPS(data.Events.Collection, tcdata, fps);
        data.WriteAssFile(opt.FullName);
    }

    internal static AVTimestamp ParseTcfile(string f)
    {
        using var fs = new FileStream(f, FileMode.Open, FileAccess.Read);
        var sr = new StreamReader(fs);
        var version = AVTimestamp.CheckVersion(sr);
        if (version != 2)
        {
            throw new Exception("tcfile must be version 2");
        }
        var data = new AVTimestamp();
        data.ParseVersion2(sr);
        return data;
    }
}
