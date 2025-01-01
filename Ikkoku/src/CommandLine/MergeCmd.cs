using Mobsub.SubtitleParse.AssTypes;
using System.CommandLine;
using Mobsub.SubtitleProcessNotAot;
using Mobsub.SubtitleProcess;

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
        var startCommitId = new Option<string>(name: "--start", description: "The commit before the first commit in subtitle modification history.") { IsRequired = true };
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
            var baseDir = conf.Directory!;
            var mergeConf = new MergeByConfig(conf.FullName);

            switch (optPath)
            {
                case DirectoryInfo d:
                    if (!d.Exists)
                    {
                        d.Create();
                    }
                    break;
                default:
                    throw new ArgumentException("Output must be a directory.");
            }
            
            var _sepIndex = confVar![0].AsSpan().IndexOf(':');

            if (_sepIndex > -1)
            {
                if (int.TryParse(confVar[0].AsSpan()[.._sepIndex], out int _epStart) && int.TryParse(confVar[0].AsSpan()[(_sepIndex + 1)..], out int _epEnd))
                {
                    var minLength = confVar[0].Length - _sepIndex - 1;
                    for (var i = _epStart; i <= _epEnd; i++)
                    {
                        mergeConf.Merge(i.ToString().PadLeft(CountDigits(_epEnd, minLength)), confVar[1], baseDir.FullName, optPath.FullName, mergeSection);
                    }
                }
                else
                {
                    throw new Exception("Please check first confVal");
                }
            }
            else
            {
                mergeConf.Merge(confVar[0], confVar[1], baseDir.FullName, optPath.FullName, mergeSection);
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
        MergeCommon.MergeAss(baseData, mergeData, mergeSection);
        Utils.WriteAssToPath(baseData, baseFile, optPath);
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

    private static int CountDigits(int number, int minLength)
    {
        if (number == 0)
        {
            return Math.Max(1, minLength);
        }

        int digitCount = 0;
        while (number != 0)
        {
            number /= 10;
            digitCount++;
        }

        return Math.Max(digitCount, minLength);
    }

    
    private static void MergeDiffByDirectory(DirectoryInfo dir, string[] subtitleSuffixes, string startCommitId, string endCommitId, string confName)
    {
        try
        {
            MergeSimplifiedChineseGitDiff.MergeDiffByDirectory(dir, subtitleSuffixes, startCommitId, endCommitId, confName);
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
