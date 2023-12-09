using System.CommandLine;

namespace Mobsub.Ikkoku.CommandLine;

partial class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Process Your Subtitles by Ikkoku! (Now only support ass)");

        var path = new Argument<FileSystemInfo>(
            name: "path",
            description: "The file path to read (support file or directory)."
        );
        path.AddValidator((result) =>
            {
                var p = result.GetValueForArgument(path);
                
                if (p.Exists)
                {
                    switch (p)
                    {
                        case FileInfo f:
                            if (!f.Name.EndsWith(".ass"))
                            {
                                result.ErrorMessage = "You should input .ass file or a directory.";
                            }
                            break;
                    }
                }
                else
                {
                    result.ErrorMessage = result.LocalizationResources.FileOrDirectoryDoesNotExist(p.FullName);
                }
            }
        );
        
        var optPath = new Option<FileSystemInfo>(
            name: "--output",
            description: "The output file path (support file or directory)."
        );
        optPath.AddAlias("-o");

        var verbose = new Option<bool>(
            name: "--verbose",
            description: "More Output Info."
        );

        var fps = new Option<string>(
            name: "--fps",
            description: "Specify video fps.",
            getDefaultValue: () => "23.976"
        );
        fps.AddValidator((result) =>
            {
                var s = result.GetValueForOption(fps);
                string[] valid = ["23.976", "23.98", "29.970", "29.97", "59.940", "59.94"];
                if (s is null)
                {
                }
                else if (!(valid.Contains(s) || decimal.TryParse(s, out _) || s.Contains('/')))
                {
                    result.ErrorMessage = result.LocalizationResources.ArgumentConversionCannotParseForOption(s, "fps", typeof(ArgumentException));
                }
            }
        );

        // clean
        var extractBinaries = new Option<bool>(name: "--extract-binaries",
        description: "Extract binaries, such as section Fonts and Graphics.");
        var keepCommentLines = new Option<bool>(name: "--keep-comments",
        description: "Keep comment lines (start with ;) in section Script Info.");
        var notAddLayoutRes = new Option<bool>(name: "--no-layoutres",
        description: "Not add LayoutResX/Y in Script Info, default value will same as PlayResX/Y");
        var dropUnusedStyles = new Option<bool>(name: "--drop-unused-styles",
        description: "Remove unused styles not used in Events");
        
        var cleanCommand = new Command("clean", "Clean Your ASS! Remove unused script info and sections, check undefined styles.")
        {
            path, optPath, extractBinaries, keepCommentLines, verbose, notAddLayoutRes, dropUnusedStyles
        };
        cleanCommand.SetHandler(Clean, path, optPath, extractBinaries, keepCommentLines, verbose, notAddLayoutRes, dropUnusedStyles);
        rootCommand.Add(cleanCommand);
        
        // check
        var tagMode = new Option<string>(
            name: "--tag",
            description: "Check tags mode: mod, weird, both."
        ).FromAmong("mod", "weird", "both");

        var styleCheck = new Option<bool>(
            name: "--style",
            description: "Check undefined styles."
        );
        // fonts glyphs
        var checkCommand = new Command("check", "Check Your ASS!")
        {
            path, tagMode, styleCheck, verbose
        };
        checkCommand.SetHandler(Check, path, tagMode, styleCheck, verbose);
        rootCommand.Add(checkCommand);
        
        // tpp
        var shiftSpan = new Option<string>(
            name: "--shift-by",
            description: "Shift subtitle time. Support int ends with mls (millisecond), cts (centisecond), sec (second), min (minute), frm (frame); pure int use second; if use frm but not specify fps, fps will be 24000/1001."
        );

        var tcfile = new Option<FileInfo>(
            name: "--tcfile",
            description: "You should specify timecode file (v2) if you want convert vfr subtitles to cfr subtitles."
        );

        var tppCommand = new Command("tpp", "Subtitle timing post-processor.")
        {
            path, optPath, shiftSpan, fps, tcfile
        };
        tppCommand.SetHandler(TimingPostProcessor, path, optPath, shiftSpan, fps, tcfile);
        tppCommand.AddValidator((result) =>
            {
                switch (result.GetValueForOption(optPath))
                {
                    case FileInfo:
                        switch (result.GetValueForArgument(path))
                        {
                            case DirectoryInfo:
                                result.ErrorMessage = "Output path must be directory when input path is a dir!";
                                break;
                        }
                        break;
                }

                var shift = result.GetValueForOption(shiftSpan);
                var tcf = result.GetValueForOption(tcfile);
                if (shift is null)
                {
                    if (tcf is null)
                    {
                        result.ErrorMessage = "--shift-by and --tcfile can’t both null,";
                    }
                    else if (!tcf.Exists)
                    {
                        result.ErrorMessage = result.LocalizationResources.FileDoesNotExist(tcf.FullName);
                    }
                }
                else
                {
                    if (tcf is not null)
                    {
                        result.ErrorMessage = "You can’t specify --shift-by and --tcfile both.";
                    }
                    else
                    {
                        string[] a = ["mls", "cts", "sec", "min", "frm"];
                        if (!(int.TryParse(shift, out _) || a.Contains(shift[^3..]) || shift.AsSpan().IndexOf('-') <= 0))
                        {
                            result.ErrorMessage = result.LocalizationResources.ArgumentConversionCannotParseForOption(shift, "--shift-by", typeof(ArgumentException));
                        }
                    }
                }
            }
        );
        rootCommand.Add(tppCommand);

        // merge
        var baseFile = new Option<FileInfo>(
            name: "--base",
            description: "Basic files in the merge. (Merge mode 1, required)."
        );
        var mergeFile = new Option<FileInfo[]>(
            name: "--merge",
            description: "Files will be merged into the base file by order. (Merge mode 1, required)."
        ) { AllowMultipleArgumentsPerToken = true };
        var mergeConf = new Option<FileInfo>(
            name: "--config",
            description: "Shift and Merge Config file. (Merge mode 2 base file, required)."
        );
        mergeConf.AddAlias("-c");
        var confVar = new Option<string[]>(
            name: "--config-var",
            description: "Values into configuration file. (Merge mode 2, required)."
        ) { AllowMultipleArgumentsPerToken = true };
        confVar.AddAlias("-var");
        var mergeSection = new Option<string>(
            name: "--section",
            description: "Sections to be merged. Can be style, event, all (default).",
            getDefaultValue: () => "all"
        ).FromAmong("style", "event", "all");
        
        var mergeCommand = new Command("merge", "Merge subtitles. Output is required.")
        {
            baseFile, mergeFile, optPath, mergeConf, confVar, mergeSection
        };
        mergeCommand.SetHandler(Merge, baseFile, mergeFile, optPath, mergeConf, confVar, mergeSection);
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

        rootCommand.Add(mergeCommand);

        // cjkpp / zhconvert
        var convConf = new Option<FileInfo>(
            name: "--config",
            description: "Zhconvert Config file."
        ) { IsRequired = true };
        convConf.AddAlias("-c");

        var cjkppCommand = new Command("cjkpp", "CJK post-processor, such as simplified and traditional conversion of Hanzi.")
        {
            path, optPath, convConf
        };
        cjkppCommand.SetHandler(CJKPostProcessor, path, optPath, convConf);
        
        // cjkpp / zhconvert build-dict
        var convDictCommand = new Command("build-dict", "Build tris dictinaries from txt files (txt file same as OpenCC).")
        {
            path, optPath
        };
        convDictCommand.SetHandler(BuildOpenccsharpDict, path, optPath);
        convDictCommand.AddValidator((result) =>
            {
                switch (result.GetValueForOption(optPath))
                {
                    case FileInfo:
                        switch (result.GetValueForArgument(path))
                        {
                            case DirectoryInfo:
                                result.ErrorMessage = "Output path must be directory when input path is a dir!";
                                break;
                        }
                        break;
                }
            }
        );
        cjkppCommand.Add(convDictCommand);
        
        cjkppCommand.AddValidator((result) =>
            {
                switch (result.GetValueForOption(optPath))
                {
                    case FileInfo:
                        switch (result.GetValueForArgument(path))
                        {
                            case DirectoryInfo:
                                result.ErrorMessage = "Output path must be directory when input path is a dir!";
                                break;
                        }
                        break;
                }
            }
        );
        rootCommand.Add(cjkppCommand);


        return await rootCommand.InvokeAsync(args);
    }

}
