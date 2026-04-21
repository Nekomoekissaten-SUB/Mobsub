namespace Mobsub.SubtitleParse.Language;

using Mobsub.SubtitleParse.AssTypes;

public static class AssOverrideTextAnalyzer
{
    /// <summary>
    /// Analyzes a plain Event "Text" field (i.e. the part containing override tags like "{\...}").
    /// The input may contain newlines (editor representation); diagnostics are reported per line.
    /// </summary>
    public static AssOverrideTextAnalysisResult Analyze(string text, AssOverrideTextAnalyzerContext? context = null)
        => Analyze(text, context, options: default);

    public static AssOverrideTextAnalysisResult Analyze(string text, AssOverrideTextAnalyzerContext? context, in AssTextOptions options)
    {
        text ??= string.Empty;

        var lineMap = new TextLineMap(text);
        var diagnostics = new List<AssDiagnostic>(capacity: 64);

        for (int line = 0; line < lineMap.LineCount; line++)
        {
            var span = lineMap.GetLineSpan(text, line);
            AssOverrideAnalyzer.AnalyzeOverrideBlocks(line, baseCharInLine: 0, span, diagnostics, context, options);
        }

        return new AssOverrideTextAnalysisResult(lineMap, diagnostics);
    }
}
