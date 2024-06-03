using Mobsub.Ikkoku.CommandLine;
using Mobsub.Ikkoku.SubtileProcess;
using System.CommandLine;

namespace Mobsub.Ikkoku;

partial class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Process Your Subtitles by Ikkoku! (Now only support ass)");

        // shared argument or option
        var path = new Argument<FileSystemInfo>(name: "path", description: "The file path to read (support file or directory).");
        path.AddValidator((result) =>
            {
                var p = result.GetValueForArgument(path);
                
                if (p.Exists)
                {
                    switch (p)
                    {
                        case FileInfo f:
                            if (!(f.Name.EndsWith(".ass") || f.Name.EndsWith(".txt") || f.Name.EndsWith(".sup")))
                            {
                                result.ErrorMessage = "You should input .ass or txt file or a directory.";
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
        
        var optPath = new Option<FileSystemInfo>(name: "--output", description: "The output file path (support file or directory).");
        optPath.AddAlias("-o");

        var verbose = new Option<bool>(name: "--verbose", description: "More Output Info.");
        
        var fps = new Option<string>(name: "--fps", description: "Specify video fps.",  getDefaultValue: () => "23.976");
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

        var conf = new Option<FileInfo>(name: "--config", description: "Configuration file.");  // { IsRequired = true }
        conf.AddAlias("-c");

        // clean
        rootCommand.Add(CleanCmd.Build(path, optPath, verbose));
        // check
        rootCommand.Add(CheckCmd.Build(path, verbose));
        // tpp
        rootCommand.Add(TppCmd.Build(path, optPath, fps));
        // merge
        rootCommand.Add(MergeCmd.Build(path, optPath, conf));
        // cjkpp (zhconvert)
        // sub: build-dict
        rootCommand.Add(CJKppCmd.Build(path, optPath, conf));
        // convert
        rootCommand.Add(ConvertCmd.Build(path, optPath));

        return await rootCommand.InvokeAsync(args);
    }
}
