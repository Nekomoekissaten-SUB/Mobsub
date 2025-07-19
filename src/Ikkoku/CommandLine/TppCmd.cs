﻿using Mobsub.SubtitleParse.AssTypes;
using System.CommandLine;
using Mobsub.SubtitleProcess;
using Mobsub.SubtitleProcess.FormatData;

namespace Mobsub.Ikkoku.CommandLine;

// TimingPostProcessor
internal class TppCmd
{
    internal static Command Build(Argument<FileSystemInfo> path, Option<FileSystemInfo> optPath, Option<string> fps)
    {
        var shiftSpan = new Option<string>("--shift-by")
        {
            Description = "Shift subtitle time. Support int ends with mls (millisecond), cts (centi-second), sec (second), min (minute), frm (frame); pure int use second; if use frm but not specify fps, fps will be 24000/1001."
        };
        var shiftStyles = new Option<string[]>("--shift-styles")
        { 
            Description = "Experimental. Shift styles. Default is shift all styles or you select styles, first is ! means you will not shift styles.",
            AllowMultipleArgumentsPerToken = true
        };
        var tcfile = new Option<FileInfo>("--tcfile")
        {
            Description = "You should specify timecodes file (v2) if you want convert vfr subtitles to cfr subtitles."
        };

        var tppCommand = new Command("tpp", "Subtitle timing post-processor.")
        {
            path, optPath, shiftSpan, shiftStyles, fps, tcfile
        };
        
        tppCommand.Validators.Add((result) =>
        {
            switch (result.GetValue(optPath))
            {
                case FileInfo:
                    switch (result.GetValue(path))
                    {
                        case DirectoryInfo:
                            result.AddError("Output path must be directory when input path is a dir!");
                            break;
                    }
                    break;
            }

            var shift = result.GetValue(shiftSpan);
            var tcf = result.GetValue(tcfile);
            if (shift is null)
            {
                if (tcf is null)
                {
                    result.AddError("--shift-by and --tcfile can’t both null,");
                }
                else if (!tcf.Exists)
                {
                    result.AddError($"{tcf.FullName} is not exist");
                }
            }
            else
            {
                if (tcf is not null)
                {
                    result.AddError("You can’t specify --shift-by and --tcfile both.");
                }
                else
                {
                    string[] a = ["mls", "cts", "sec", "min", "frm"];
                    if (!(int.TryParse(shift, out _) || a.Contains(shift[^3..]) || shift.AsSpan().IndexOf('-') <= 0))
                    {
                        result.AddError($"\"--shift-by\" value must use correct format");
                    }
                }
            }
        }
        );

        tppCommand.SetAction((result) =>
        {
            var pathValue = result.GetValue(path);
            var optPathValue = result.GetValue(optPath);
            var shiftSpanValue = result.GetValue(shiftSpan);
            var styles = result.GetValue(shiftStyles);
            var fpsValue = result.GetValue(fps);
            var tcfileValue = result.GetValue(tcfile);
            Execute(pathValue!, optPathValue, shiftSpanValue, styles!, fpsValue!, tcfileValue);
        });

        return tppCommand;
    }

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
