namespace Mobsub.SubtitleParse.Language;

public readonly record struct AssPosition(int Line, int Character);

public readonly record struct AssRange(AssPosition Start, AssPosition End);

public enum AssSeverity : byte
{
    Error = 1,
    Warning = 2,
    Info = 3,
}

public sealed record AssDiagnostic(AssRange Range, AssSeverity Severity, string Message, string? Code = null);

public enum AssCompletionKind : byte
{
    Text = 0,
    Keyword,
    Property,
    Value,
}

public sealed record AssCompletionItem(string Label, string InsertText, AssCompletionKind Kind, string? Detail = null);

public sealed class AssCompletionResult(IReadOnlyList<AssCompletionItem> Items, AssRange ReplaceRange)
{
    public IReadOnlyList<AssCompletionItem> Items { get; } = Items;
    public AssRange ReplaceRange { get; } = ReplaceRange;
}

public sealed class AssOverrideTextAnalysisResult(TextLineMap LineMap, IReadOnlyList<AssDiagnostic> Diagnostics)
{
    public TextLineMap LineMap { get; } = LineMap;
    public IReadOnlyList<AssDiagnostic> Diagnostics { get; } = Diagnostics;
}
