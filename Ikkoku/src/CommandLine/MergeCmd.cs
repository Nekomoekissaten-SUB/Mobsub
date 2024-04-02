using Mobsub.Ikkoku.FormatData;
using Mobsub.SubtitleParse.AssTypes;
using System.Diagnostics;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using Mobsub.Ikkoku.SubtileProcess;
using System.CommandLine;

namespace Mobsub.Ikkoku.CommandLine;

internal class MergeCmd
{
    internal static Command Build(Option<FileSystemInfo> optPath, Option<FileInfo> mergeConf)
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
            baseFile, mergeFile, optPath, mergeConf, confVar, mergeSection
        };
        mergeCommand.SetHandler(Execute, baseFile, mergeFile, optPath, mergeConf, confVar, mergeSection);
        mergeCommand.AddValidator((result) =>
        {
            var bf = result.GetValueForOption(baseFile);
            var mf = result.GetValueForOption(mergeFile);
            var opt = result.GetValueForOption(optPath);

            var conf = result.GetValueForOption(mergeConf);
            var confv = result.GetValueForOption(confVar);

            if (opt is null)
            {
                result.ErrorMessage = "Must specify output";
            }

            if (conf is null)
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
                else if (!conf.Exists)
                {
                    result.ErrorMessage = result.LocalizationResources.FileDoesNotExist(conf.FullName);
                }
            }
        }
        );
        return mergeCommand;
    }

    internal static void Execute(FileInfo? baseFile, FileInfo[]? mergeFile, FileSystemInfo optPath, FileInfo? mergeConf, string[]? confVar, string mergeSection)
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
}
