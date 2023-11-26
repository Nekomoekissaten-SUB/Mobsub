using System.Diagnostics;
using Mobsub.SubtitleParse;
using Mobsub.AssTypes;
using Mobsub.Ikkoku.FormatData;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace Mobsub.Ikkoku.CommandLine;

partial class Program
{
    internal static void Clean(FileSystemInfo path, FileSystemInfo? optPath, bool extractBinaries, bool keepCommentLines, bool verbose, bool notAddLayoutRes, bool dropUnusedStyles)
    {
        var sw = new Stopwatch();
        sw.Start();

        DirectoryInfo? binDir = null;
        FileInfo opt;
        var ext = ".ass";
        
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

                CleanOneAss(iptFile, opt, binDir, keepCommentLines, verbose, notAddLayoutRes, dropUnusedStyles);
                break;
            
            case DirectoryInfo iptDir:
                
                var subfiles = Traversal(iptDir, ext);
                binDir = extractBinaries ? iptDir : binDir;
                optPath ??= iptDir;

                switch (optPath)
                {
                    case FileInfo:
                        throw new ArgumentException("Output path must be directory when input path is a dir!");
                    case DirectoryInfo d:
                        if (!d.Exists)
                        {
                            d.Create();
                        }
                        foreach (var f in subfiles)
                        {
                            opt = new FileInfo(Path.Combine(d.FullName, f.Name));
                            CleanOneAss(f, opt, binDir, keepCommentLines, verbose, notAddLayoutRes, dropUnusedStyles);
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

    private static void CleanOneAss(FileInfo f, FileInfo opt, DirectoryInfo? binDir, bool keepCommentLines, bool verbose, bool notAddLayoutRes, bool dropUnusedStyles)
    {
        Console.WriteLine(f);
        var fs = new FileStream(f.FullName, FileMode.Open, FileAccess.ReadWrite);
        var fileNoSuffix = f.Name.AsSpan()[..(f.Name.Length - 4)];
        var data = AssParse.ReadAssFile(fs);
        if (binDir is not null)
        {
            ExtractBinaries(data, binDir, verbose);
        }

        SubtileProcess.CleanAss(data, keepCommentLines, fileNoSuffix, !notAddLayoutRes, dropUnusedStyles, out string msg, out bool untouched);
        
        if (!untouched)
        {
            AssParse.WriteAssFile(data, opt.FullName);
        }
        
        if (verbose)
        {
            Console.WriteLine(msg);
        }
    }

    private static void ExtractBinaries(AssData data, DirectoryInfo binDir, bool verbose)
    {
        if (!binDir.Exists)
        {
            binDir.Create();
        }

        if (data.Sections.Contains(AssSection.Fonts))
        {
            foreach (var font in data.Fonts)
            {
                var filePath = Path.Combine(binDir.FullName, $"{font.OriginalName}{font.Suffix}");

                if (!new FileInfo(filePath).Exists)
                {
                    AssEmbededParse.WriteFontFile(font, filePath, verbose);
                }
            }
        }

        if (data.Sections.Contains(AssSection.Graphics))
        {
            foreach (var g in data.Graphics)
            {
                var filePath = Path.Combine(binDir.FullName, $"{g.Name}");

                if (!new FileInfo(filePath).Exists)
                {
                    AssEmbededParse.WriteGraphicFile(g, filePath, verbose);
                }
            }
        }
    }

    internal static void Check(FileSystemInfo path, string? tagMode, bool styleCheck, bool verbose)
    {
        switch (path)
        {
            case FileInfo file:
                CheckOneAss(file, tagMode, styleCheck, verbose);
                break;
            case DirectoryInfo dir:
                foreach (var file in Traversal(dir, ".ass"))
                {
                    CheckOneAss(file, tagMode, styleCheck, verbose);
                }
                break;
            default:
                throw new IOException();
        }
    }

    private static void CheckOneAss(FileInfo f, string? tagMode, bool styleCheck, bool verbose)
    {
        Console.WriteLine(f);
        var data = AssParse.ReadAssFile(f.FullName);
        
        if (tagMode is not null)
        {
            SubtileProcess.PrintUnnormalAssTags(data.Events.Collection, verbose, tagMode);
        }
        
        if (styleCheck)
        {
            var usedStyles = SubtileProcess.GetUsedStyles(data.Events.Collection);
            var undefinedStyles = new HashSet<string>(usedStyles);
            undefinedStyles.ExceptWith(data.Styles.Names);
            
            if (undefinedStyles.Count > 0)
            {
                Console.WriteLine($"Undefined styles {string.Join(", ", undefinedStyles)}");
            }
        }
        
        Console.WriteLine("Check completed.");
    }

    internal static void TimingPostProcessor(FileSystemInfo path, FileSystemInfo? optPath, string? shiftSpan, string fps, FileInfo? tcfile)
    {
        // select mode
        var tsp = (shiftSpan is null) ? TimeSpan.Zero : GetTimespan(shiftSpan, fps);
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
                    ShiftOneAss(iptFile, opt, tsp);
                }
                if (toCfr && tcfile is not null)
                {
                    ToCfrOneAss(iptFile, opt, tcfile, fps);
                }
                break;
            
            case DirectoryInfo iptDir:
                
                var subfiles = Traversal(iptDir, ext);
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
                                ShiftOneAss(f, opt, tsp);
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

    private static void ShiftOneAss(FileInfo f, FileInfo opt, TimeSpan tsp)
    {
        Console.WriteLine(f);
        var data = AssParse.ReadAssFile(f.FullName);
        SubtileProcess.ShiftAss( data.Events.Collection, tsp);
        AssParse.WriteAssFile(data, opt.FullName);
    }

    private static TimeSpan GetTimespan(string shiftSpan, string fps)
    {
        var ss = shiftSpan.AsSpan();
        TimeSpan tsp;

        if (ss.Length > 3 && ss[^4] >= '0' && ss[^4] <= '9')
        {
            int v = 0;
            var negative = false;
            for (var i = 0; i < ss.Length - 3; i++)
            {
                var c = ss[i];
                if (c == '-')
                {
                    if (i == 0)
                    {
                        negative = true;
                    }
                    else
                    {
                        throw new ArgumentException("Invaild --by value");
                    }
                    
                }
                else
                {
                    v = c - '0' + (v * 10);
                }
            }
            v = negative ? 0 - v : v;

            tsp = ss[^3..] switch
            {
                "mls" => TimeSpan.FromMicroseconds(v),
                "cts" => TimeSpan.FromMicroseconds(v * 10),
                "sec" => TimeSpan.FromSeconds(v),
                "min" => TimeSpan.FromMinutes(v),
                "frm" => TimeSpan.FromMilliseconds(FrameToMillisecond(v, UnifiedFps(fps))),
                _ => throw new ArgumentException("Invaild suffixes"),
            };
        }
        else
        {
            tsp = TimeSpan.FromSeconds(Convert.ToInt32(shiftSpan));
        }
        return tsp;
    }

    private static void ToCfrOneAss(FileInfo f, FileInfo opt, FileInfo tcfile, string fps)
    {
        Console.WriteLine(f);
        var data = AssParse.ReadAssFile(f.FullName);
        var tcdata = ParseTcfile(tcfile.FullName);
        SubtileProcess.AssumeFPS(data.Events.Collection, tcdata, fps);
        AssParse.WriteAssFile(data, opt.FullName);
    }

    public static AVTimestamp ParseTcfile(string f)
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

    internal static void Merge(FileInfo? baseFile, FileInfo[]? mergeFile, FileSystemInfo optPath, FileInfo? mergeConf, string[]? confVar, string mergeSection)
    {
        if (mergeConf is null)
        {
            MergeBase(baseFile!, mergeFile!, optPath, mergeSection);
        }
        else
        {
            DirectoryInfo baseDir = mergeConf.Directory!;
            var tmpValues = new ShiftMergeYamlValue(confVar![0], confVar![1]);
            var ymlString = File.ReadAllText(mergeConf.FullName);
            if (ymlString.Length == 0)
            {
                throw new FormatException("Configuration file is empty.");
            }
            var _deserializer = new StaticDeserializerBuilder(new YamlStaticContext()).WithNodeDeserializer(tmpValues).WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
            var ymlData = ymlString.AsSpan().StartsWith("version") ? _deserializer.Deserialize<ShiftMergeYamlV2>(ymlString) : _deserializer.Deserialize<ShiftMergeYaml>(ymlString).CovertToV2();
            Debug.Assert(ymlData is not null);

            var baseFileName = $"{ymlData.Namef["master"]}.ass";
            var mergeDataList = new List<AssData>();

            string optFileName;
            switch (optPath)
            {
                case DirectoryInfo d:
                    if (!d.Exists)
                    {
                        d.Create();
                    }
                    optFileName = Path.Combine(optPath.FullName, baseFileName);
                    break;
                default:
                    throw new ArgumentException("Output must be a directory.");
            }

            foreach (var kvp in ymlData.ShiftFr.Where(kvp => kvp.Key == confVar[0]))
            {
                TimeSpan tsp;
                foreach (var kvpEp in kvp.Value)
                {
                    if (kvpEp.Value.Length > 1 && (kvpEp.Value[1] - ymlData.TplFr[kvpEp.Key] != kvpEp.Value[0]))
                    {
                        throw new Exception("Merge: Please check your configuration file shift_fr, it may be wrong.");
                    }
                    else
                    {
                        var dataFrom = AssParse.ReadAssFile(Path.Combine(baseDir.FullName, $"{ymlData.Namef[kvpEp.Key]}.ass"));
                        tsp = GetTimespan($"{kvpEp.Value[0]}frm", ymlData.Fps);
                        SubtileProcess.ShiftAss(dataFrom.Events.Collection, tsp);
                        mergeDataList.Add(dataFrom);
                    }
                }
            }

            var bf = Path.Combine(baseDir.FullName, baseFileName);
            var baseData = AssParse.ReadAssFile(bf);
            
            if (mergeDataList is null)
            {
                File.Copy(bf, optFileName);
            }
            else
            {
                SubtileProcess.MergeAss(baseData, [.. mergeDataList], mergeSection);
                AssParse.WriteAssFile(baseData, optFileName);
            }
            
        }
    }

    private static void MergeBase(FileInfo baseFile, FileInfo[] mergeFile, FileSystemInfo optPath, string mergeSection)
    {
        var mergeData = mergeFile.Select(mf => AssParse.ReadAssFile(mf.FullName)).ToArray();
        var baseData = AssParse.ReadAssFile(baseFile.FullName);

        SubtileProcess.MergeAss(baseData, mergeData, mergeSection);
        
        WriteAssFile(baseData, baseFile, optPath);
    }

    private static void WriteAssFile(AssData data, FileInfo baseFile, FileSystemInfo optPath)
    {
        switch (optPath)
        {
            case FileInfo f:
                AssParse.WriteAssFile(data, f.FullName);
                break;
            case DirectoryInfo dir:
                if (!dir.Exists)
                {
                    dir.Create();
                }
                AssParse.WriteAssFile(data, Path.Combine(dir.FullName, baseFile.Name));
                break;
        }
    }

    internal static FileInfo[] Traversal(DirectoryInfo path, string ext) => path.GetFiles().Where(fi => fi.Extension == ext).ToArray();

    internal static double FrameToMillisecond(int frame, decimal fps) => (double)(frame / fps) * 1000;

    internal static decimal UnifiedFps(string fpsString)
    {
        string[] Arr24 = ["23.976", "23.98"];
        string[] Arr30 = ["29.970", "29.97"];
        string[] Arr60 = ["59.940", "59.94"];

        if (fpsString.Contains('/'))
        {
            var fpsA = fpsString.Split('/').Select(s => Convert.ToDecimal(s.Trim())).ToArray();
            return fpsA[0] / fpsA[1];
        }
        else if (Arr24.Contains(fpsString))
        {
            return 24000 / 1001M;
        }
        else if (Arr30.Contains(fpsString))
        {
            return 30000 / 1001M;
        }
        else if (Arr60.Contains(fpsString))
        {
            return 60000 / 1001M;
        }
        else
        {
            return Convert.ToDecimal(fpsString);
        }
    }

}