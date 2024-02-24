using System.Diagnostics;
using Mobsub.SubtitleParse;
using Mobsub.AssTypes;
using Mobsub.Ikkoku.FormatData;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using OpenCCSharp.Conversion;
using System.Text;
using Mobsub.ZhConvert;
using Mobsub.Utils;

namespace Mobsub.Ikkoku.CommandLine;

partial class Program
{
    internal static void Clean(FileSystemInfo path, FileSystemInfo? optPath, bool extractBinaries, bool keepCommentLines, bool verbose, bool addLayoutRes, bool dropUnusedStyles, CleanPreset preset)
    {
        var sw = new Stopwatch();
        sw.Start();

        DirectoryInfo? binDir = null;
        FileInfo opt;
        var ext = ".ass";

        var args = new SubtileProcess.CleanAssArgs();
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

    internal enum CleanPreset
    {
        Basic,  // for vcb-s
        More,   // for nekomoe
    }

    private static void CleanOneAss(FileInfo f, FileInfo opt, DirectoryInfo? binDir, bool verbose, SubtileProcess.CleanAssArgs args)
    {
        Console.WriteLine(f);
        var fs = new FileStream(f.FullName, FileMode.Open, FileAccess.ReadWrite);
        var fileNoSuffix = f.Name.AsSpan()[..(f.Name.Length - 4)];
        var data = AssParse.ReadAssFile(fs);
        if (binDir is not null)
        {
            ExtractBinaries(data, binDir, verbose);
        }

        SubtileProcess.CleanAss(data, fileNoSuffix, args, out string msg, out bool untouched);
        
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

        // var evtStartLine = data.Events.Collection.First().lineNumber;
        List<int> weridTimeLines = [];
        List<int> unusedCharLines = [];
        List<int> weridSpaceLines = [];
        foreach (var evt in data.Events.Collection)
        {
            if (SubtileProcess.WeridTimeOneLine(evt))
            {
                weridTimeLines.Add(evt.lineNumber);
            }

            SubtileProcess.CheckWeridChars(evt.Text, out bool hadUnusedChar, out bool hadWeridSpace);
            if (hadUnusedChar)
            {
                unusedCharLines.Add(evt.lineNumber);
            }
            if (hadWeridSpace)
            {
                weridSpaceLines.Add(evt.lineNumber);
            }
        }

        if (weridTimeLines.Count > 0)
        {
            Console.WriteLine($"Dialogue end time less than start time: {string.Join(", ", weridTimeLines)}");
        }

        if (unusedCharLines.Count > 0)
        {
            Console.WriteLine($"Maybe use unused chars: {string.Join(", ", unusedCharLines)}");
        }

        if (weridSpaceLines.Count > 0)
        {
            Console.WriteLine($"Maybe use wrong space char: {string.Join(", ", weridSpaceLines)}");
        }
        
        Console.WriteLine("Check completed.");
    }

    internal static void TimingPostProcessor(FileSystemInfo path, FileSystemInfo? optPath, string? shiftSpan, string[] styles, string fps, FileInfo? tcfile)
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
                    ShiftOneAss(iptFile, opt, tsp, styles);
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
        var data = AssParse.ReadAssFile(f.FullName);

        if (styles.Length > 0)
        {
            var negation = styles[0] == "!";
            SubtileProcess.ShiftAss(data.Events.Collection, tsp, negation ? styles[1..] : styles, negation);
        }
        else
        {
            SubtileProcess.ShiftAss(data.Events.Collection, tsp);
        }

        AssParse.WriteAssFile(data, opt.FullName);
        Console.WriteLine("fine.");
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

            var _sepIndex = confVar![0].AsSpan().IndexOf(':');

            if (_sepIndex > -1)
            {                
                if (int.TryParse(confVar[0].AsSpan()[.._sepIndex], out int _epStart) && int.TryParse(confVar[0].AsSpan()[(_sepIndex + 1)..], out int _epEnd))
                {
                    for (var i = _epStart; i <= _epEnd; i++)
                    {
                        MergeByConfigBase(i.ToString().PadLeft(CountDigits(_epEnd), '0'), confVar[1], mergeConf, baseDir, optPath, mergeSection);
                    }
                }
                else
                {
                    throw new Exception("Please check first confVal");
                }
            }
            else
            {
                MergeByConfigBase(confVar[0], confVar[1], mergeConf, baseDir, optPath, mergeSection);
            }
        }
    }

    internal static void ConvertSubtitles(FileSystemInfo path, FileSystemInfo? optPath, string convertSuffix, string inputSuffix)
    {
        switch (path)
        {
            case FileInfo f:
                ConvertSubtitle(f, optPath, convertSuffix);
                break;
            case DirectoryInfo d:
                var files = Traversal(d, inputSuffix);
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
        
        var optFile = ChangeSuffix(fromFile, optDir, convertSuffix);
        var fs = new FileStream(optFile.FullName, FileMode.Create, FileAccess.Write);
        using var memStream = new MemoryStream();
        using var sw = new StreamWriter(memStream, DetectEncoding.EncodingRefOS());
        
        switch (fromFile.Extension)
        {
            case ".ass":
                var ass = AssParse.ReadAssFile(fromFile.FullName);

                switch (convertSuffix)
                {
                    case ".txt":
                        SubtileProcess.ConvertAssToTxt(sw, ass);
                        break;
                    default:
                        // fs.Close();
                        throw new NotImplementedException($"Unsupport: {fromFile.Extension} convert to {convertSuffix}.");
                }

                break;
            default:
                // fs.Close();
                throw new NotImplementedException($"Unsupport: {fromFile.Extension}.");
        }

        sw.Flush();

        memStream.Seek(0, SeekOrigin.Begin);
        memStream.CopyTo(fs);
        // fs.Close();
    }

    private static void MergeBase(FileInfo baseFile, FileInfo[] mergeFile, FileSystemInfo optPath, string mergeSection)
    {
        var mergeData = mergeFile.Select(mf => AssParse.ReadAssFile(mf.FullName)).ToArray();
        var baseData = AssParse.ReadAssFile(baseFile.FullName);

        SubtileProcess.MergeAss(baseData, mergeData, mergeSection);
        
        WriteAssFile(baseData, baseFile, optPath);
    }

    private static void MergeByConfigBase(string episode, string lang, FileInfo mergeConf, DirectoryInfo baseDir, FileSystemInfo optPath, string mergeSection)
    {

        var tmpValues = new ShiftMergeYamlValue(episode, lang);
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

        foreach (var kvp in ymlData.ShiftFr.Where(kvp => kvp.Key == episode))
        {
            TimeSpan tsp;
            foreach (var kvpEp in kvp.Value)
            {
                if (kvpEp.Value.Length > 3 || (kvpEp.Value.Length > 1 && kvpEp.Value[1] != -1 && (kvpEp.Value[1] - ymlData.TplFr[kvpEp.Key] != kvpEp.Value[0])))
                {
                    throw new Exception("Merge: Please check your configuration file shift_fr, it may be wrong.");
                }

                var dataFrom = AssParse.ReadAssFile(Path.Combine(baseDir.FullName, $"{ymlData.Namef[kvpEp.Key]}.ass"));
                tsp = GetTimespan($"{kvpEp.Value[0]}frm", ymlData.Fps);

                if (kvpEp.Value.Length == 3 && kvpEp.Value[2] != -1)
                {
                    var start = new AssTime((int)FrameToMillisecond(kvpEp.Value[2], UnifiedFps(ymlData.Fps)));
                    SubtileProcess.ShiftAss(dataFrom.Events.Collection, tsp, start);
                }
                else
                {
                    SubtileProcess.ShiftAss(dataFrom.Events.Collection, tsp);
                }

                mergeDataList.Add(dataFrom);
            }
        }
        
        var bf = Path.Combine(baseDir.FullName, baseFileName);
        var baseData = AssParse.ReadAssFile(bf);
        
        if (mergeDataList.Count == 0)
        {
            File.Copy(bf, optFileName);
        }
        else
        {
            SubtileProcess.MergeAss(baseData, [.. mergeDataList], mergeSection);
            AssParse.WriteAssFile(baseData, optFileName);
        }
    }

    private static int CountDigits(int number)
    {
        if (number == 0)
        {
            return 1;
        }

        int digitCount = 0;
        while (number != 0)
        {
            number /= 10;
            digitCount++;
        }

        return digitCount;
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

    private static void ConvertAssByOpencc(FileInfo f, FileInfo opt, ChainedScriptConverter converter)
    {
        if (f.FullName.Equals(opt.FullName))
        {
            throw new ArgumentException("Output file path can’t same as input file!");
        }

        Console.WriteLine($"Input: {f}");
        Console.WriteLine($"Output: {opt}");
        var data = AssParse.ReadAssFile(f.FullName);
        var sb = new StringBuilder();
        Dictionary<int, string[]> changesRecord = [];

        foreach (var et in data.Events.Collection)
        {
            if (!et.Style.AsSpan().Contains("JP".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                SubtileProcess.ZhConvertEventLineByOpenccsharp(et.Text, sb, converter, out string[]? countChanges);

                if (countChanges is not null)
                {
                    changesRecord.Add(et.lineNumber, countChanges);
                }
            }
        }
        
        AssParse.WriteAssFile(data, opt.FullName);

        if (changesRecord.Keys.Count > 0)
        {
            Console.WriteLine("Please pay attention:");
            foreach (var pair in changesRecord)
            {
                Console.WriteLine($"Linenumber: {pair.Key}");
                Console.WriteLine(pair.Value[0]);
                Console.WriteLine(pair.Value[1]);
                Console.WriteLine();
            }
        }

        Console.WriteLine("fine");
        Console.WriteLine();
    }

    internal static void CJKPostProcessor(FileSystemInfo path, FileSystemInfo opt, FileInfo config)
    {
        var dicts = OpenCCSharpUtils.LoadJson(config);
        var converter = OpenCCSharpUtils.GetConverter(dicts);

        switch (path)
        {
            case FileInfo f:
                switch (opt)
                {
                    case FileInfo fo:
                        ConvertAssByOpencc(f, fo, converter);
                        break;
                    case DirectoryInfo diro:
                        if (!diro.Exists)
                        {
                            diro.Create();
                        }
                        ConvertAssByOpencc(f, new FileInfo(Path.Combine(diro.FullName, f.Name)), converter);
                        break;
                }
                break;
            
            case DirectoryInfo dir:
                
                var subfiles = Traversal(dir, ".ass");
                switch (opt)
                {
                    case DirectoryInfo diro:
                        
                        if (!diro.Exists)
                        {
                            diro.Create();
                        }
                        foreach (var f in subfiles)
                        {
                            ConvertAssByOpencc(f, new FileInfo(Path.Combine(diro.FullName, f.Name)), converter);
                        }
                        
                        break;
                }
                break;
        }
    }

    internal static async Task BuildOpenccsharpDict(FileSystemInfo path, FileSystemInfo opt)
    {
        var targetSuffix = ".tris";
        
        switch (path)
        {
            case FileInfo f:
                switch (opt)
                {
                    case FileInfo fo:
                        await OpenCCSharpUtils.BuildTriesDictionary(f, fo);
                        break;
                    case DirectoryInfo diro:
                        if (!diro.Exists)
                        {
                            diro.Create();
                        }
                        await OpenCCSharpUtils.BuildTriesDictionary(f, ChangeSuffix(f, targetSuffix));
                        break;
                }
                break;
            
            case DirectoryInfo dir:
                
                var subfiles = Traversal(dir, ".txt");
                switch (opt)
                {
                    case DirectoryInfo diro:
                        
                        if (!diro.Exists)
                        {
                            diro.Create();
                        }
                        foreach (var f in subfiles)
                        {
                            await OpenCCSharpUtils.BuildTriesDictionary(f, ChangeSuffix(f, diro, targetSuffix));
                        }
                        
                        break;
                }
                break;
        }
    }

    private static FileInfo ChangeSuffix(FileInfo f, string suffix) => ChangeSuffix(f, f.Directory!, suffix);

    private static FileInfo ChangeSuffix(FileInfo f, DirectoryInfo dir, string suffix)
    {
        return new FileInfo(Path.Combine(dir.FullName, Path.GetFileNameWithoutExtension(f.FullName) + suffix));

    }

}