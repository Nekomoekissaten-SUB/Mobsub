using System;
using System.Collections.Generic;
using Mobsub.SubtitleParse.AssText;

namespace Mobsub.SubtitleParse.Language;

internal static class AssOverrideAnalyzer
{
    public static void AnalyzeOverrideBlocks(
        int line,
        int baseCharInLine,
        ReadOnlySpan<char> textField,
        List<AssDiagnostic> diagnostics,
        AssOverrideTextAnalyzerContext? context = null)
    {
        // Fast path: no override blocks.
        if (textField.IndexOf('{') < 0)
            return;

        using var map = Utf8IndexMap.Create(textField);
        using var read = AssEventTextRead.Parse(textField);

        ScanForUnclosedOverrideBlock(line, baseCharInLine, textField, diagnostics);

        var validationContext = CreateValidationContext(context);

        var utf8 = read.Utf8;
        var segments = read.Segments;

        var sink = new DiagnosticSink(line, baseCharInLine, map, diagnostics);
        AssOverrideTagValidator.ValidateOverrideBlocks(utf8, segments, ref sink, validationContext);
    }

    private static AssOverrideValidationContext CreateValidationContext(AssOverrideTextAnalyzerContext? context)
    {
        if (context == null)
            return default;

        int? boundX = null;
        int? boundY = null;

        if (context.LayoutResX is int lx && context.LayoutResY is int ly)
        {
            boundX = lx;
            boundY = ly;
        }
        else if (context.PlayResX is int px && context.PlayResY is int py)
        {
            boundX = px;
            boundY = py;
        }

        return new AssOverrideValidationContext(context.EventDurationMs, boundX, boundY);
    }

    private struct DiagnosticSink : IAssOverrideValidationSink
    {
        private readonly int _line;
        private readonly int _baseCharInLine;
        private readonly Utf8IndexMap _map;
        private readonly List<AssDiagnostic> _diagnostics;

        public DiagnosticSink(int line, int baseCharInLine, Utf8IndexMap map, List<AssDiagnostic> diagnostics)
        {
            _line = line;
            _baseCharInLine = baseCharInLine;
            _map = map;
            _diagnostics = diagnostics;
        }

        public void Report(in AssOverrideValidationIssue issue)
        {
            int startChar = _baseCharInLine + _map.ByteToCharIndex(issue.StartByte);
            int endChar = _baseCharInLine + _map.ByteToCharIndex(issue.EndByte);

            var severity = issue.Severity switch
            {
                AssOverrideValidationSeverity.Error => AssSeverity.Error,
                AssOverrideValidationSeverity.Warning => AssSeverity.Warning,
                _ => AssSeverity.Info
            };

            AssOverrideAnalyzer.AddDiagnostic(_diagnostics, _line, startChar, endChar, severity, issue.Message, issue.Code);
        }
    }

    private static void ScanForUnclosedOverrideBlock(
        int line,
        int baseCharInLine,
        ReadOnlySpan<char> textField,
        List<AssDiagnostic> diagnostics)
    {
        int i = 0;
        while (i < textField.Length)
        {
            int open = textField.Slice(i).IndexOf('{');
            if (open < 0)
                break;
            open += i;

            int close = textField.Slice(open + 1).IndexOf('}');
            if (close < 0)
            {
                AddDiagnostic(diagnostics, line, baseCharInLine + open, baseCharInLine + textField.Length, AssSeverity.Warning,
                    "Unclosed override block ('{...}').",
                    "ass.override.unclosed");
                break;
            }

            i = open + 1 + close + 1;
        }
    }

    private static void AddDiagnostic(
        List<AssDiagnostic> diagnostics,
        int line,
        int startChar,
        int endChar,
        AssSeverity severity,
        string message,
        string code)
    {
        diagnostics.Add(new AssDiagnostic(
            new AssRange(new AssPosition(line, startChar), new AssPosition(line, endChar)),
            severity,
            message,
            Code: code));
    }

}
