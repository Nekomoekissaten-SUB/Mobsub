using Mobsub.AssFormat;
using Mobsub.SubtitleProcess;
using Mobsub.SubtitleProcess2;
using System.CommandLine;
using Mobsub.Utils;

namespace Mobsub.SubtitleCLI;


internal class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Process Your ASS by CLI!");
        var cleanCommand = new Command("CleanASS", "Clean Your ASS! Remove unused script info and sections, check undefined styles.");
        var mergeCommand = new Command("MergeASS", "Merge sections from source ass files to path ass files, don’t support many-to-many.");
        var shiftCommand = new Command("ShiftASS", "Shift ASS time by shift from path. Output can’t use file.");
        var shimgCommand = new Command("ShiftMergeASS", "A private function. Read config file to shift and merge ass files, both path and output are dir.");
        var fontsCommand = new Command("ASSFontTools", "Some useful tools about fonts with ass.");
        /// var convtCommand = new Command("Convert", "Convert subtitles to other types.");  srt, vtt

        /// Public Option
        var pathOption = new Option<FileSystemInfo[]>(
            name: "--path",
            description: "The .ass file path to read (support multi file and dir).")
            { IsRequired = true, AllowMultipleArgumentsPerToken = true };
        rootCommand.AddGlobalOption(pathOption);

        var outputOption = new Option<FileSystemInfo>(
            name: "--output",
            description: "The output file path (ONLY support single file and dir).");
        outputOption.AddAlias("-o");

        var fps = new Option<string>(
            name: "--fps",
            description: "format like 24000/1001");

        var confOption = new Option<FileInfo>(
            name: "--config",
            description: "support yml config file");
        confOption.AddAlias("-c");

        /// CleanASS
        /// Feature
        /// 1. Remove spaces at both ends of the line (when parse)
        /// 2. Remove unused script info, such like value is none, begin with `; / Comment / By`, `keep_comment` will keep `Comment`
        /// 3. Check undefined styles, except comment lines
        /// 4. Remove some sections, like `Fonts`, `Graphics`, `Aegisub Project Garbage`, `Aegisub Extradata`
        /// 5. `drop_unused` will drop unused styles which appears in `V4+ Styles` but not in `Events`
        cleanCommand.AddAlias("clean");
        /// cleanCommand.AddOption(pathOption);

        var keepCmtOption = new Option<bool>(
            name: "--keep-comment", 
            description: "Don’t remove Comment key-value lines in Script Info.");
        cleanCommand.AddOption(keepCmtOption);
        
        var dropStylesOption = new Option<bool>(
            name: "--drop-unused",
            description: "Drop unused styles.");
        cleanCommand.AddOption(dropStylesOption);

        var dropSelectionStylesOption = new Option<string[]>(
            name: "--drop-selection",
            description: "Drop selection styles.")
            { AllowMultipleArgumentsPerToken = true };
        cleanCommand.AddOption(dropSelectionStylesOption);

        /// Decode bin from ass file (TO DO)

        cleanCommand.SetHandler(
            (path, keepCmt, dropUnusedStyles, dropSelectionStyles) =>
            {
                CleanMulti(path, keepCmt, dropUnusedStyles, dropSelectionStyles);
            },
            pathOption, keepCmtOption, dropStylesOption, dropSelectionStylesOption);

        /// MergeASS
        /// TODO: resample? Assume resolution?
        mergeCommand.AddAlias("merge");
        mergeCommand.AddOption(outputOption);
        /// mergeCommand.AddOption(pathOption); /// to

        var mergeFromOption = new Option<FileSystemInfo[]>(
            name: "--source",
            description: "The source .ass file path (support multi file and dir).")
        { IsRequired = true, AllowMultipleArgumentsPerToken = true };
        mergeCommand.AddOption(mergeFromOption); /// from

        var mergeSectionOption = new Option<string>(
            name: "--section",
            description: "Sections which need merge. All means styles and events.")
            .FromAmong("all", "styles", "events");
        mergeCommand.AddOption(mergeSectionOption);

        mergeCommand.SetHandler(
            (to, from, output, section) =>
            {
                MergeMulti(to, from, output, section);
            },
            pathOption, mergeFromOption, outputOption, mergeSectionOption);

        /// ShiftASS
        /// multiplier?
        shiftCommand.AddAlias("shift");
        shiftCommand.AddOption(outputOption);
        shiftCommand.AddOption(fps);

        var shiftSpan = new Option<string>(
            name: "--by",
            description: "support mls (millisecond), cts (centisecond), sec (second), min (minute), frm (frame).");
        shiftCommand.AddOption(shiftSpan);

        shiftCommand.SetHandler(
            (path, opt, by, fps) =>
            {
                ShiftMulti(path, opt, by, fps);
            },
            pathOption, outputOption, shiftSpan, fps);

        /// ShiftMergeASS
        shimgCommand.AddAlias("merge2");

        var shimgOptOption = new Option<DirectoryInfo>(
            name: "--output",
            description: "The output directory path.");
        shimgOptOption.AddAlias("-o");
        shimgCommand.AddOption(shimgOptOption);

        shimgCommand.AddOption(confOption);

        var shimgVarOption = new Option<string[]>(
            name: "--var",
            description: "Value passed into YAML config file.")
            { AllowMultipleArgumentsPerToken = true };
        shimgCommand.AddOption(shimgVarOption);

        shimgCommand.SetHandler(
            (path, opt, conf, var) =>
            {
                Merge2(path, opt, conf, var);
            },
            pathOption, shimgOptOption, confOption, shimgVarOption);

        /// AssFontTools
        fontsCommand.AddAlias("aft");
        var fontsListCommand = new Command("list", "List Ass Fonts");
        var fontsExtractCommand = new Command("extract", "Extract Ass Fonts"); /// decode_bin
        var fontsCheckCommand = new Command("check", "Check Ass Fonts");
        var fontsSubsetCommand = new Command("subset", "Subset Ass Fonts");

        fontsCommand.AddOption(outputOption);


        fontsCommand.Add(fontsListCommand);
        fontsCommand.Add(fontsExtractCommand);
        fontsCommand.Add(fontsCheckCommand);
        fontsCommand.Add(fontsSubsetCommand);

        /// Add SubCommands
        rootCommand.Add(cleanCommand);
        rootCommand.Add(mergeCommand);
        rootCommand.Add(shiftCommand);
        rootCommand.Add(shimgCommand);
        /// rootCommand.Add(fontsCommand);

        return await rootCommand.InvokeAsync(args);
    }

    internal static void Clean(FileSystemInfo path, bool keepCmt, bool dropUnusedStyles, string[]? dropSelectionStyles)
    {
        switch (path)
        {
            case FileInfo file:
                AssProcess.Clean(file, keepCmt, dropUnusedStyles, dropSelectionStyles);
                break;
            case DirectoryInfo dir:
                foreach (FileInfo file in Files.Traversal(dir, ".ass"))
                {
                    if (file is not null)
                    {
                        AssProcess.Clean(file, keepCmt, dropUnusedStyles, dropSelectionStyles);
                    }
                }
                break;
            default:
                throw new IOException();
        }
    }
    internal static void CleanMulti(FileSystemInfo[] path, bool keepCmt, bool dropUnusedStyles, string[]? dropSelectionStyles)
    {
        DateTime timeBegin = DateTime.Now;

        if (path.Length == 1)
        {
            Clean(path[0], keepCmt, dropUnusedStyles, dropSelectionStyles);
        }
        else
        {
            foreach (var p in path)
            {
                Clean(p, keepCmt, dropUnusedStyles, dropSelectionStyles);
            }
        }

        Console.WriteLine($"{Environment.NewLine}CleanASS used {(DateTime.Now - timeBegin).TotalMilliseconds} ms");
    }
    internal static void MergeMulti(FileSystemInfo[] to, FileSystemInfo[] from, FileSystemInfo output, string section)
    {
        DateTime timeBegin = DateTime.Now;

        section ??= "all";
        var toAssData = new Dictionary<FileInfo, Dictionary<string, AssData>>();
        var fromAssData = new Dictionary<FileInfo, Dictionary<string, AssData>>();
        
        foreach (var t in to)
        {
            toAssData = toAssData.Concat(AssParse.ParseMulti(t)).ToDictionary(k => k.Key, v => v.Value);
        }
        
        foreach (var f in from)
        {
            fromAssData = fromAssData.Concat(AssParse.ParseMulti(f)).ToDictionary(k => k.Key, v => v.Value);
        }

        var optAssData = AssProcess.Merge(toAssData.Values.ToArray(), fromAssData.Values.ToArray(), section);

        switch (output)
        {
            case null:
                /// Default `output` file is `to` file
                for (int i=0; i< optAssData.Length; i++)
                {
                    Files.Write(toAssData.Keys.ToArray()[i], AssParse.JoinSections(optAssData[i]));
                }
                break;
            case FileInfo file:
                if (to.Length == 1)
                {
                    Files.Write(file, AssParse.JoinSections(optAssData[0]));
                    break;
                }
                else
                {
                    throw new Exception("Merge: output must be a single file or dir.");
                }
            case DirectoryInfo dir:
                _ = Files.CheckDir(dir);
                for (int i = 0; i < optAssData.Length; i++)
                {
                    var optFileName = new FileInfo(Path.Combine(dir.FullName, toAssData.Keys.ToArray()[i].Name));
                    Files.Write(optFileName, AssParse.JoinSections(optAssData[i]));
                }
                break;
            default:
                throw new IOException();
        }

        Console.WriteLine($"{Environment.NewLine}MergeASS used {(DateTime.Now - timeBegin).TotalMilliseconds} ms");
    }
    internal static void ShiftMulti(FileSystemInfo[] path, FileSystemInfo output, string span, string fps)
    {
        DateTime timeBegin = DateTime.Now;

        var AssData = new Dictionary<FileInfo, Dictionary<string, AssData>>();
        fps ??= "24000/1001";
        var spanTime = TimecodesConvert.ConvertToSpan(span, fps);

        foreach (var p in path)
        {
            AssData = AssData.Concat(AssParse.ParseMulti(p)).ToDictionary(k => k.Key, v => v.Value);
        }

        foreach (var k in AssData.Keys)
        {
            var v = AssData[k];
            v["Events"].Table = AssProcess.Shift(v["Events"].Table, spanTime);
        }

        switch (output)
        {
            case null:
                foreach (var f in AssData.Keys)
                {
                    Files.Write(f, AssParse.JoinSections(AssData[f]));
                }
                break;
            case FileInfo file:
                {
                    throw new Exception("Shift: output must be a dir.");
                }
            case DirectoryInfo dir:
                _ = Files.CheckDir(dir);
                foreach (var f in AssData.Keys)
                {
                    var optFileName = new FileInfo(Path.Combine(dir.FullName, f.Name));
                    Files.Write(optFileName, AssParse.JoinSections(AssData[f]));
                }
                break;
            default:
                throw new IOException();
        }

        Console.WriteLine($"{Environment.NewLine}ShiftASS used {(DateTime.Now - timeBegin).TotalMilliseconds} ms");
    }
    internal static void Merge2(FileSystemInfo[] path, DirectoryInfo output, FileInfo confFile, string[] var)
    {
        if (path.Length == 1)
        {
            switch (path[0])
            {
                case FileInfo:
                    throw new Exception("ShiftMerge: path only support directory.");
                case DirectoryInfo dir:

                    if (var[0].Contains('-'))
                    {
                        var newVarList = new List<string>() { };
                        var numStrArr = var[0].Split("-");
                        var numArr = numStrArr.Select(short.Parse).ToArray();
                        for (short i= numArr[0]; i<= numArr[1]; i++)
                        {
                            newVarList.Add(i.ToString().PadLeft(numStrArr[1].Length, '0'));
                            newVarList.Add(var[1]);
                            AssProcess2.Merge2(dir, output, confFile, newVarList.ToArray());
                            newVarList.Clear();
                        }
                    }
                    else
                    {
                        AssProcess2.Merge2(dir, output, confFile, var);
                    }
                    break;
            }
        }
        else
        {
            throw new Exception("ShiftMerge: path only support single directory.");
        }
    }

}