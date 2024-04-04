using Mobsub.Ikkoku.FormatData;
using Mobsub.SubtitleParse.AssTypes;
using System.Diagnostics;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using Mobsub.Ikkoku.SubtileProcess;
using System.CommandLine;
using System.Text;

namespace Mobsub.Ikkoku.CommandLine;

internal class MergeCmd
{
    internal static Command Build(Argument<FileSystemInfo> path, Option<FileSystemInfo> optPath, Option<FileInfo> conf)
    {
        var baseFile = new Option<FileInfo>(
            name: "--base",
            description: "Basic files in the merge. (Merge mode 1, required).");
        var mergeFile = new Option<FileInfo[]>(
            name: "--merge",
            description: "Files will be merged into the base file by order. (Merge mode 1, required).")
        { AllowMultipleArgumentsPerToken = true };

        // Shift and Merge Config file. (Merge mode 2 base file, required).

        var confVar = new Option<string[]>(name: "--config-var", description: "Values into configuration file. (Merge mode 2, required).")
        { AllowMultipleArgumentsPerToken = true };
        confVar.AddAlias("-var");
        var mergeSection = new Option<string>(name: "--section", description: "Sections to be merged. Can be style, event, all (default).", getDefaultValue: () => "all").FromAmong("style", "event", "all");

        var mergeCommand = new Command("merge", "Merge subtitles. Output is required.")
        {
            baseFile, mergeFile, optPath, conf, confVar, mergeSection
        };
        mergeCommand.SetHandler(Execute, baseFile, mergeFile, optPath, conf, confVar, mergeSection);
        mergeCommand.AddValidator((result) =>
        {
            var bf = result.GetValueForOption(baseFile);
            var mf = result.GetValueForOption(mergeFile);
            var opt = result.GetValueForOption(optPath);

            var confF = result.GetValueForOption(conf);
            var confv = result.GetValueForOption(confVar);

            if (opt is null)
            {
                result.ErrorMessage = "Must specify output";
            }

            if (confF is null)
            {
                if (bf is null || mf is null)
                {
                    result.ErrorMessage = "You should specify base and merge files if no configure file.";
                }
                else if (!bf.Exists)
                {
                    result.ErrorMessage = result.LocalizationResources.FileDoesNotExist(bf.FullName);
                }
                else
                {
                    foreach (var f in mf)
                    {
                        if (!f.Exists)
                        {
                            result.ErrorMessage = result.LocalizationResources.FileDoesNotExist(f.FullName);
                        }
                    }
                }
            }
            else
            {
                switch (opt)
                {
                    case DirectoryInfo:
                        break;
                    default:
                        result.ErrorMessage = "--output must be a directory when specify configure file.";
                        break;
                }

                if (confv is null)
                {
                    result.ErrorMessage = "You should specify --config-var when specify configure file.";
                }
                else if (confv.Length != 2)
                {
                    result.ErrorMessage = "You only can input --config-var 2 values, these will replace ep and lang in configure file.";
                }
                else if (!confF.Exists)
                {
                    result.ErrorMessage = result.LocalizationResources.FileDoesNotExist(confF.FullName);
                }
            }
        }
        );

# if NotAot
        // subcommand base-diff
        mergeCommand.Add(BuildSubGitDiff(path, conf));
# endif
        return mergeCommand;
    }

    private static Command BuildSubGitDiff(Argument<FileSystemInfo> path, Option<FileInfo> conf)
    {
        var startCommitId = new Option<string>(name: "--start", description: "Start git commit id of subtitle modification history.") { IsRequired = true };
        var endCommitId = new Option<string>(name: "--end", description: "End git commit id of subtitle modification history.") { IsRequired = true};
        var subtitleSuffixes = new Option<string[]>(name: "--suffixes", description: "Choose chs and cht subtitles suffix (usually not used unless the preset cannot recognize it).")
        { AllowMultipleArgumentsPerToken = true };

        var cmd = new Command("base-diff", "Merge chs diff to cht ass file, zhconvert like cjkpp")
        {
            path, startCommitId, endCommitId, conf, subtitleSuffixes
        };
        cmd.SetHandler(ExecuteBaseDiff, path, startCommitId, endCommitId, conf, subtitleSuffixes);
        return cmd;
    }

    internal static void Execute(FileInfo? baseFile, FileInfo[]? mergeFile, FileSystemInfo optPath, FileInfo? conf, string[]? confVar, string mergeSection)
    {
        if (conf is null)
        {
            MergeBase(baseFile!, mergeFile!, optPath, mergeSection);
        }
        else
        {
            DirectoryInfo baseDir = conf.Directory!;

            var _sepIndex = confVar![0].AsSpan().IndexOf(':');

            if (_sepIndex > -1)
            {
                if (int.TryParse(confVar[0].AsSpan()[.._sepIndex], out int _epStart) && int.TryParse(confVar[0].AsSpan()[(_sepIndex + 1)..], out int _epEnd))
                {
                    for (var i = _epStart; i <= _epEnd; i++)
                    {
                        MergeByConfigBase(i.ToString().PadLeft(CountDigits(_epEnd), '0'), confVar[1], conf, baseDir, optPath, mergeSection);
                    }
                }
                else
                {
                    throw new Exception("Please check first confVal");
                }
            }
            else
            {
                MergeByConfigBase(confVar[0], confVar[1], conf, baseDir, optPath, mergeSection);
            }
        }
    }

    private static void MergeBase(FileInfo baseFile, FileInfo[] mergeFile, FileSystemInfo optPath, string mergeSection)
    {
        var mergeData = mergeFile.Select(file =>
        {
            var data = new AssData();
            data.ReadAssFile(file.FullName);
            return data;
        }).ToArray();
        var baseData = new AssData();
        baseData.ReadAssFile(baseFile.FullName);
        Merge.MergeAss(baseData, mergeData, mergeSection);
        Utils.WriteAssToPath(baseData, baseFile, optPath);
    }

    private static void MergeByConfigBase(string episode, string lang, FileInfo conf, DirectoryInfo baseDir, FileSystemInfo optPath, string mergeSection)
    {

        var tmpValues = new ShiftMergeYamlValue(episode, lang);
        var ymlString = File.ReadAllText(conf.FullName);
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

                var dataFrom = new AssData();
                dataFrom.ReadAssFile(Path.Combine(baseDir.FullName, $"{ymlData.Namef[kvpEp.Key]}.ass"));
                tsp = Utils.GetTimespan($"{kvpEp.Value[0]}frm", ymlData.Fps);

                if (kvpEp.Value.Length == 3 && kvpEp.Value[2] != -1)
                {
                    var start = new AssTime((int)Utils.FrameToMillisecond(kvpEp.Value[2], Utils.UnifiedFps(ymlData.Fps)));
                    Tpp.ShiftAss(dataFrom.Events.Collection, tsp, start);
                }
                else
                {
                    Tpp.ShiftAss(dataFrom.Events.Collection, tsp);
                }

                mergeDataList.Add(dataFrom);
            }
        }

        var baseData = new AssData();
        var bf = Path.Combine(baseDir.FullName, baseFileName);
        baseData.ReadAssFile(bf);

        if (mergeDataList.Count == 0)
        {
            File.Copy(bf, optFileName);
        }
        else
        {
            Merge.MergeAss(baseData, [.. mergeDataList], mergeSection);
            baseData.WriteAssFile(optFileName);
        }
    }

    private static void ExecuteBaseDiff(FileSystemInfo path, string startCommitId, string endCommitId, FileInfo convConf, string[] subtitleSuffixes)
    {
        switch (path)
        {
            case DirectoryInfo d:
                MergeDiffByDirectory(d, subtitleSuffixes, startCommitId, endCommitId, convConf.FullName);
                break;
            case FileInfo f:
                MergeDiffByDirectory(new DirectoryInfo(f.DirectoryName!), subtitleSuffixes, startCommitId, endCommitId, convConf.FullName);
                break;
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

    private static bool FindGitRootDirectory(string startPath, out string rootPath, out string relativePath)
    {
        var currentPath = startPath;
        rootPath = relativePath = string.Empty;

        while (!string.IsNullOrEmpty(currentPath))
        {
            var gitPath = Path.Combine(currentPath, ".git");
            if (Directory.Exists(gitPath))
            {
                rootPath = currentPath;
                break;
            }
            var parentDir = Directory.GetParent(currentPath);
            if (parentDir == null)
            {
                break;
            }
            currentPath = parentDir.FullName;
        }

        if (!string.IsNullOrEmpty(rootPath))
        {
            relativePath = Path.GetRelativePath(rootPath, startPath);
            return true;
        }
        return false;
    }

    private static string[] GetHadSuffixes(DirectoryInfo path)
    {
        var suffixes = new[] { "*.jpsc.ass", "*.sc.ass", "*.chs.ass", "*.gb.ass" };
        var options = new EnumerationOptions
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            RecurseSubdirectories = false
        };
        string? ssuffix = null;

        foreach (var suffix in suffixes)
        {
            var files = path.GetFiles(suffix, options);
            if (files.Length > 0)
            {
                ssuffix = files[0].Name[^(suffix.Length - 1)..];
                break;
            }
        }

        return ssuffix is null ? [] : [ssuffix, GetChtSuffix(ssuffix)];
    }

    private static string GetChtSuffix(string suffix)
    {
        var mappings = new[]
        {
            new { From = "sc", To = "tc" },
            new { From = "chs", To = "cht" },
            new { From = "gb", To = "big5" }
        };

        var sb = new StringBuilder(suffix);

        foreach (var mapping in mappings)
        {
            var index = sb.ToString().IndexOf(mapping.From, StringComparison.OrdinalIgnoreCase);
            if (index != -1)
            {
                var actualFrom = suffix.Substring(index, mapping.From.Length);
                var replacement = actualFrom.Select((c, i) =>
                    char.IsUpper(c) ? mapping.To[i].ToString().ToUpper() : mapping.To[i].ToString().ToLower()
                    ).Aggregate((a, b) => a + b);
                sb.Replace(actualFrom, replacement, index, mapping.From.Length);
                break;
            }
        }
        return sb.ToString();
    }

    private static void MergeDiffByDirectory(DirectoryInfo dir, string[] subtitleSuffixes, string startCommitId, string endCommitId, string confName)
    {
        try
        {
            if (FindGitRootDirectory(dir.FullName, out var repoLocalPath, out var relativePath))
            {
                subtitleSuffixes = subtitleSuffixes.Length == 0 ? GetHadSuffixes(dir) : subtitleSuffixes;
                if (subtitleSuffixes.Length == 0)
                {
                    throw new ArgumentException();
                }
                else
                {
                    var baseSuffix = subtitleSuffixes[0];
                    var targetSuffix = subtitleSuffixes[1];
                    Merge.MergeGitDiffToCht(repoLocalPath, startCommitId, endCommitId, relativePath, baseSuffix, targetSuffix, confName);
                }
            }
            else
            {
                throw new DirectoryNotFoundException();
            }
        }
        catch (ArgumentException)
        {
            Console.WriteLine("Please specify --suffixes.");
        }
        catch (DirectoryNotFoundException)
        {
            Console.WriteLine("Please check path, can’t find git in parent directories.");
        }
    }
}
