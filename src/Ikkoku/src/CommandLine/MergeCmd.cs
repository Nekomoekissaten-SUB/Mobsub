using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleProcess;
using Mobsub.SubtitleProcessNotAot;
using System.CommandLine;
using System.Reflection.Metadata.Ecma335;

namespace Mobsub.Ikkoku.CommandLine;

internal partial class MergeCmd
{
    internal static Command Build(Argument<FileSystemInfo> path, Option<FileSystemInfo> optPath, Option<FileInfo> conf)
    {
        var baseFile = new Option<FileInfo>("--base") { Description = "Basic files in the merge. (Merge mode 1, required)." };
        var mergeFile = new Option<FileInfo[]>("--merge")
        {
            Description = "Files will be merged into the base file by order. (Merge mode 1, required).",
            AllowMultipleArgumentsPerToken = true
        };

        // Shift and Merge Config file. (Merge mode 2 base file, required).

        var confVar = new Option<string[]>("--config-var", "-var")
        {
            Description = "Values into configuration file. (Merge mode 2, required).",
            AllowMultipleArgumentsPerToken = true
        };
        var mergeSection = new Option<string>("--section")
        {
            Description = "Sections to be merged. Can be style, event, all (default).",
            DefaultValueFactory = _ => "all"
        }.AcceptOnlyFromAmong("style", "event", "all");

        var mergeCommand = new Command("merge", "Merge subtitles. Output is required.")
        {
            baseFile, mergeFile, optPath, conf, confVar, mergeSection
        };
        
        mergeCommand.Validators.Add((result) =>
        {
            var bf = result.GetValue(baseFile);
            var mf = result.GetValue(mergeFile);
            var opt = result.GetValue(optPath);

            var confF = result.GetValue(conf);
            var confv = result.GetValue(confVar);

            if (opt is null)
            {
                result.AddError("Must specify output");
            }

            if (confF is null)
            {
                if (bf is null || mf is null)
                {
                    result.AddError("You should specify base and merge files if no configure file.");
                }
                else if (!bf.Exists)
                {
                    result.AddError($"{bf.FullName} is not exist");
                }
                else
                {
                    foreach (var f in mf)
                    {
                        if (!f.Exists)
                        {
                            result.AddError($"{f.FullName} is not exist");
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
                        result.AddError("--output must be a directory when specify configure file.");
                        break;
                }

                if (confv is null)
                {
                    result.AddError("You should specify --config-var when specify configure file.");
                }
                else if (confv.Length != 2)
                {
                    result.AddError("You only can input --config-var 2 values, these will replace ep and lang in configure file.");
                }
                else if (!confF.Exists)
                {
                    result.AddError($"{confF.FullName} is not exist");
                }
            }
        }
        );

        mergeCommand.SetAction(result =>
        {
            var baseFileValue = result.GetValue(baseFile);
            var mergeFileValues = result.GetValue(mergeFile);
            var outputPath = result.GetValue(optPath);
            var confFile = result.GetValue(conf);
            var confVarValues = result.GetValue(confVar);
            var mergeSectionValue = result.GetValue(mergeSection);
            Execute(baseFileValue, mergeFileValues, outputPath!, confFile, confVarValues, mergeSectionValue!);
        });

# if NotAot
        // subcommand base-diff
        mergeCommand.Add(BuildSubGitDiff(path, conf));
# endif

        // subcommand nijigasaki
        mergeCommand.Add(BuildNijigasaki(path, optPath, confVar));

        return mergeCommand;
    }

    private static Command BuildSubGitDiff(Argument<FileSystemInfo> path, Option<FileInfo> conf)
    {
        var startCommitId = new Option<string>("--start")
        {
            Description = "The commit before the first commit in subtitle modification history.",
            Required = true
        };
        var endCommitId = new Option<string>("--end")
        {
            Description = "End git commit id of subtitle modification history.",
            Required = true
        };
        var subtitleSuffixes = new Option<string[]>("--suffixes")
        { 
            Description = "Choose chs and cht subtitles suffix (usually not used unless the preset cannot recognize it).",
            AllowMultipleArgumentsPerToken = true
        };

        var cmd = new Command("base-diff", "Merge chs diff to cht ass file, zhconvert like cjkpp")
        {
            path, startCommitId, endCommitId, conf, subtitleSuffixes
        };

        cmd.SetAction(result =>
        {
            ExecuteBaseDiff(
                result.GetValue(path)!,
                result.GetValue(startCommitId)!,
                result.GetValue(endCommitId)!,
                result.GetValue(conf)!,
                result.GetValue(subtitleSuffixes)!
                );
        });

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
            
            if (SplitFirstConfigVariable(confVar![0], out var _epStart, out var _epEnd, out var _length))
            {
                for (var i = _epStart; i <= _epEnd; i++)
                {
                    mergeConf.Merge(i.ToString($"D{_length}"), confVar[1], baseDir.FullName, optPath.FullName, mergeSection);
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

    private static bool SplitFirstConfigVariable(string value, out int epStart, out int epEnd, out int length)
    {
        var span = value.AsSpan();
        var _sepIndex = span.IndexOf(':');

        if (_sepIndex <= -1)
        {
            epStart = epEnd = length = -1;
            return false;
        }

        if (int.TryParse(span[.._sepIndex], out epStart) && int.TryParse(span[(_sepIndex + 1)..], out epEnd))
        {
            length = Math.Max(span[(_sepIndex + 1)..].Length, 2);
            return true;
        }
        else
        {
            throw new Exception("Please check first confVal");
        }
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
