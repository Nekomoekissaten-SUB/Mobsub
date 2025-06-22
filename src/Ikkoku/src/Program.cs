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
        var path = new Argument<FileSystemInfo>("path")
        {
            Description = "The file path to read (support file or directory)."
        };

        path.Validators.Add((result) =>
            {
                var p = result.GetValue(path);
                
                if (p is not null && p.Exists)
                {
                    switch (p)
                    {
                        case FileInfo f:
                            if (!(f.Name.EndsWith(".ass") || f.Name.EndsWith(".txt") || f.Name.EndsWith(".sup")))
                            {
                                result.AddError("You should input .ass, .txt or .sup file or a directory.");
                            }
                            break;
                    }
                }
            }
        );
        
        var optPath = new Option<FileSystemInfo>("--output", "-o")
        {
            Description = "The output file path (support file or directory)."
        };

        var verbose = new Option<bool>("--verbose") { Description = "More Output Info." };
        
        var fps = new Option<string>("--fps")
        {
            Description = "Specify video fps.",
            DefaultValueFactory = _ => "23.976",
        };
        fps.Validators.Add((result) =>
            {
                var s = result.GetValue(fps);
                string[] valid = ["23.976", "23.98", "29.970", "29.97", "59.940", "59.94"];
                if (s is null)
                {
                }
                else if (!(valid.Contains(s) || decimal.TryParse(s, out _) || s.Contains('/')))
                {
                    result.AddError("You should check --fps format");
                }
            }
        );

        var conf = new Option<FileInfo>("--config", "-c") { Description = "Configuration file." };

        // clean
        rootCommand.Add(CleanCmd.Build(path, optPath, verbose));
        // check
        // rootCommand.Add(CheckCmd.Build(path, verbose));
        // tpp
        rootCommand.Add(TppCmd.Build(path, optPath, fps));
        // merge
        rootCommand.Add(MergeCmd.Build(path, optPath, conf));
        // cjkpp (zhconvert)
        // sub: build-dict
        rootCommand.Add(CJKppCmd.Build(path, optPath, conf));
        // convert
        rootCommand.Add(ConvertCmd.Build(path, optPath));

        return await rootCommand.Parse(args).InvokeAsync();
    }
}
