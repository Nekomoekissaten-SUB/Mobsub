using System.CommandLine;
using Mobsub.SubtitleProcess;

namespace Mobsub.Ikkoku.CommandLine;

internal class CheckCmd
{
    internal static Command Build(Argument<FileSystemInfo> path, Option<bool> verbose)
    {
        var tagMode = new Option<string>("--tag")
        {
            Description = "Check tags mode: mod, weird, both."
        }.AcceptOnlyFromAmong("mod", "weird", "both");
        var styleCheck = new Option<bool>("--style") { Description = "Check undefined styles." };

        var checkCommand = new Command("check", "Check Your ASS!")
        {
            path, tagMode, styleCheck, verbose
        };
        checkCommand.SetAction(result =>
        {
            Execute(result.GetValue(path)!, result.GetValue(tagMode), result.GetValue(styleCheck), result.GetValue(verbose));
        });

        // fonts glyphs subcommand

        return checkCommand;
    }

    internal static void Execute(FileSystemInfo path, string? tagMode, bool styleCheck, bool verbose)
    {
        switch (path)
        {
            case FileInfo file:
                CheckOneAss(file, tagMode, styleCheck, verbose);
                break;
            case DirectoryInfo dir:
                foreach (var file in Utils.Traversal(dir, ".ass"))
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
        // Console.WriteLine(f);
        // var data = new AssData();
        // data.ReadAssFile(f.FullName);
        //
        // if (tagMode is not null)
        // {
        //     Check.PrintUnnormalAssTags(data.Events.Collection, verbose, tagMode);
        // }
        //
        // if (styleCheck)
        // {
        //     var usedStyles = AssCheck.GetUsedStyles(data.Events.Collection);
        //     var undefinedStyles = new HashSet<string>(usedStyles);
        //     undefinedStyles.ExceptWith(data.Styles.Names);
        //
        //     if (undefinedStyles.Count > 0)
        //     {
        //         Console.WriteLine($"Undefined styles: {string.Join(", ", undefinedStyles)}");
        //     }
        // }
        //
        // // var evtStartLine = data.Events.Collection.First().lineNumber;
        // List<int> weirdTimeLines = [];
        // List<int> unusedCharLines = [];
        // List<int> weirdSpaceLines = [];
        // foreach (var evt in data.Events.Collection)
        // {
        //     if (Check.WeirdTimeOneLine(evt))
        //     {
        //         weirdTimeLines.Add(evt.lineNumber);
        //     }
        //
        //     Check.CheckWeirdChars(evt.Text, out bool hadUnusedChar, out bool hadWeirdSpace);
        //     if (hadUnusedChar)
        //     {
        //         unusedCharLines.Add(evt.lineNumber);
        //     }
        //     if (hadWeirdSpace)
        //     {
        //         weirdSpaceLines.Add(evt.lineNumber);
        //     }
        // }
        //
        // if (weirdTimeLines.Count > 0)
        // {
        //     Console.WriteLine($"Dialogue end time less than start time: {string.Join(", ", weirdTimeLines)}");
        // }
        //
        // if (unusedCharLines.Count > 0)
        // {
        //     Console.WriteLine($"Maybe use unused chars: {string.Join(", ", unusedCharLines)}");
        // }
        //
        // if (weirdSpaceLines.Count > 0)
        // {
        //     Console.WriteLine($"Maybe use wrong space char: {string.Join(", ", weirdSpaceLines)}");
        // }
        //
        // Console.WriteLine("Check completed.");
    }
}
