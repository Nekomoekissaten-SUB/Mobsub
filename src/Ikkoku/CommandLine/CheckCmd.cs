using System.CommandLine;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleProcess;

namespace Mobsub.Ikkoku.CommandLine;

internal class CheckCmd
{
    internal static Command Build(Argument<FileSystemInfo> path, Option<bool> verbose)
    {
        var styleCheck = new Option<bool>("--style") { Description = "Check undefined styles." };
        var dialectOpt = new Option<string>("--dialect")
        {
            Description = "Override tag dialect: ass|vsfilter|vsfiltermod.",
            DefaultValueFactory = _ => "vsfilter",
        };
        var profileOpt = new Option<string>("--profile")
        {
            Description = "Renderer profile: vsfilter|libass_0_17_4.",
            DefaultValueFactory = _ => "vsfilter",
        };
        var strictnessOpt = new Option<string>("--strictness")
        {
            Description = "Validation strictness: compat|normal|strict.",
            DefaultValueFactory = _ => "normal",
        };

        var checkCommand = new Command("check", "Check Your ASS!")
        {
            path, styleCheck, dialectOpt, profileOpt, strictnessOpt, verbose
        };
        checkCommand.SetAction(result =>
        {
            var options = CreateTextOptions(
                result.GetValue(dialectOpt),
                result.GetValue(profileOpt),
                result.GetValue(strictnessOpt));

            Execute(result.GetValue(path)!, result.GetValue(styleCheck), result.GetValue(verbose), options);
        });

        // fonts glyphs subcommand

        return checkCommand;
    }

    internal static void Execute(FileSystemInfo path, bool styleCheck, bool verbose, in AssTextOptions options)
    {
        switch (path)
        {
            case FileInfo file:
                CheckOneAss(file, styleCheck, verbose, options);
                break;
            case DirectoryInfo dir:
                foreach (var file in Utils.Traversal(dir, ".ass"))
                {
                    CheckOneAss(file, styleCheck, verbose, options);
                }
                break;
            default:
                throw new IOException();
        }
    }

    private static void CheckOneAss(FileInfo f, bool styleCheck, bool verbose, in AssTextOptions options)
    {
        if (!f.Name.EndsWith(".ass", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Skip non-.ass file: {f.FullName}");
            return;
        }

        if (verbose)
            Console.WriteLine(f.FullName);

        var data = new AssData();
        data.ReadAssFile(f.FullName);

        var events = data.Events?.Collection;
        if (events == null || events.Count == 0)
        {
            if (verbose)
                Console.WriteLine("No events.");
            return;
        }

        int totalErrors = 0;
        int totalWarnings = 0;
        int totalInfos = 0;

        int boundX = data.ScriptInfo.LayoutResX;
        int boundY = data.ScriptInfo.LayoutResY;

        foreach (var evt in events)
        {
            if (evt.StartSemicolon)
                continue;

            using var read = AssEventTextRead.ParseTextSpan(in evt, options);

            var ctx = new AssOverrideValidationContext(
                eventDurationMs: TryGetDurationMs(evt.Start, evt.End),
                coordinateBoundX: boundX,
                coordinateBoundY: boundY);

            var sink = new ConsoleSink(f.FullName, evt.LineNumber, verbose);
            AssOverrideTagValidator.ValidateOverrideBlocks(read.Utf8, read.Segments, ref sink, ctx, options);

            totalErrors += sink.ErrorCount;
            totalWarnings += sink.WarningCount;
            totalInfos += sink.InfoCount;
        }

        if (styleCheck)
        {
            var undefined = GetUndefinedStyles(data, events);
            if (undefined.Count > 0)
                Console.WriteLine($"Undefined styles: {string.Join(", ", undefined)}");
        }

        if (totalErrors + totalWarnings + totalInfos > 0)
        {
            Console.WriteLine($"Override tag issues: {totalErrors} error(s), {totalWarnings} warning(s), {totalInfos} info(s).");
        }
        else if (verbose)
        {
            Console.WriteLine("No override tag issues.");
        }
    }

    private static AssTextOptions CreateTextOptions(string? dialect, string? profile, string? strictness)
    {
        var d = (dialect ?? "vsfilter").Trim().ToLowerInvariant() switch
        {
            "ass" => AssTextDialect.Ass,
            "vsfiltermod" => AssTextDialect.VsFilterMod,
            _ => AssTextDialect.VsFilter,
        };

        var p = (profile ?? "vsfilter").Trim().ToLowerInvariant() switch
        {
            "libass_0_17_4" => AssRendererProfile.LibAss_0_17_4,
            _ => AssRendererProfile.VsFilter,
        };

        var s = (strictness ?? "normal").Trim().ToLowerInvariant() switch
        {
            "compat" => AssValidationStrictness.Compat,
            "strict" => AssValidationStrictness.Strict,
            _ => AssValidationStrictness.Normal,
        };

        return new AssTextOptions(d, p, s);
    }

    private static int? TryGetDurationMs(AssTime start, AssTime end)
    {
        long deltaTicks = end.Ticks - start.Ticks;
        if (deltaTicks <= 0)
            return null;

        long ms = deltaTicks / 10_000;
        if (ms > int.MaxValue)
            return int.MaxValue;
        return (int)ms;
    }

    private static HashSet<string> GetUndefinedStyles(AssData data, List<AssEvent> events)
    {
        var defined = new HashSet<string>(StringComparer.Ordinal);
        foreach (var style in data.Styles.Collection)
        {
            if (style.IsCommentLine)
                continue;

            var name = NormalizeStyleName(style.Name);
            if (name.Length > 0)
                defined.Add(name);
        }

        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var evt in events)
        {
            if (evt.StartSemicolon)
                continue;

            var name = NormalizeStyleName(evt.Style);
            if (name.Length > 0)
                used.Add(name);
        }

        used.ExceptWith(defined);
        return used;
    }

    private static string NormalizeStyleName(string name)
    {
        if (name.Length == 0)
            return string.Empty;

        name = name.TrimStart('*').Trim();
        if (name.Length == 0)
            return string.Empty;

        if (string.Equals(name, "default", StringComparison.OrdinalIgnoreCase))
            return "Default";

        return name;
    }

    private struct ConsoleSink : IAssOverrideValidationSink
    {
        private readonly string _filePath;
        private readonly int _eventLineNumber;
        private readonly bool _verbose;

        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public int InfoCount { get; private set; }

        public ConsoleSink(string filePath, int eventLineNumber, bool verbose)
        {
            _filePath = filePath;
            _eventLineNumber = eventLineNumber;
            _verbose = verbose;

            ErrorCount = 0;
            WarningCount = 0;
            InfoCount = 0;
        }

        public void Report(in AssOverrideValidationIssue issue)
        {
            switch (issue.Severity)
            {
                case AssOverrideValidationSeverity.Error:
                    ErrorCount++;
                    break;
                case AssOverrideValidationSeverity.Warning:
                    WarningCount++;
                    break;
                case AssOverrideValidationSeverity.Info:
                    InfoCount++;
                    break;
            }

            if (!_verbose && issue.Severity == AssOverrideValidationSeverity.Info)
                return;

            Console.WriteLine($"{_filePath}:{_eventLineNumber}: {issue.Severity} {issue.Code}: {issue.Message}");
        }
    }
}
