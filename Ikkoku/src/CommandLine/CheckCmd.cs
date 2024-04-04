using Mobsub.SubtitleParse.AssTypes;
using Mobsub.Ikkoku.SubtileProcess;
using System.CommandLine;
using System.IO;

namespace Mobsub.Ikkoku.CommandLine;

internal class CheckCmd
{
    internal static Command Build(Argument<FileSystemInfo> path, Option<bool> verbose)
    {
        var tagMode = new Option<string>(
            name: "--tag",
            description: "Check tags mode: mod, weird, both.").FromAmong("mod", "weird", "both");
        var styleCheck = new Option<bool>(
            name: "--style",
            description: "Check undefined styles.");

        var checkCommand = new Command("check", "Check Your ASS!")
        {
            path, tagMode, styleCheck, verbose
        };
        checkCommand.SetHandler(Execute, path, tagMode, styleCheck, verbose);

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
        Console.WriteLine(f);
        var data = new AssData();
        data.ReadAssFile(f.FullName);

        if (tagMode is not null)
        {
            Check.PrintUnnormalAssTags(data.Events.Collection, verbose, tagMode);
        }

        if (styleCheck)
        {
            var usedStyles = Check.GetUsedStyles(data.Events.Collection);
            var undefinedStyles = new HashSet<string>(usedStyles);
            undefinedStyles.ExceptWith(data.Styles.Names);

            if (undefinedStyles.Count > 0)
            {
                Console.WriteLine($"Undefined styles: {string.Join(", ", undefinedStyles)}");
            }
        }

        // var evtStartLine = data.Events.Collection.First().lineNumber;
        List<int> weridTimeLines = [];
        List<int> unusedCharLines = [];
        List<int> weridSpaceLines = [];
        foreach (var evt in data.Events.Collection)
        {
            if (Check.WeridTimeOneLine(evt))
            {
                weridTimeLines.Add(evt.lineNumber);
            }

            Check.CheckWeridChars(evt.Text, out bool hadUnusedChar, out bool hadWeridSpace);
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
}
