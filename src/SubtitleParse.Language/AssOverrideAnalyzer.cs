using System.Text;
using Mobsub.SubtitleParse;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.Language;

internal static class AssOverrideAnalyzer
{
    private const int MaxNestedTransformDepth = 4;

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

        var utf8 = read.Utf8;
        var segments = read.Segments;
        for (int i = 0; i < segments.Length; i++)
        {
            ref readonly var seg = ref segments[i];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock)
                continue;

            var (start, end) = GetRangeOffsets(seg.LineRange, utf8.Length);
            if (end - start < 2)
                continue;

            int innerStart = start + 1;
            int innerEnd = end - 1;
            if (innerEnd <= innerStart)
                continue;

            AnalyzeTagPayloadBytes(
                line,
                baseCharInLine,
                map,
                utf8,
                utf8.Span.Slice(innerStart, innerEnd - innerStart),
                payloadAbsoluteStartByte: innerStart,
                diagnostics,
                context,
                depth: 0);
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
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, baseCharInLine + open), new AssPosition(line, baseCharInLine + textField.Length)),
                    AssSeverity.Warning,
                    "Unclosed override block ('{...}').",
                    Code: "ass.override.unclosed"));
                break;
            }

            i = open + 1 + close + 1;
        }
    }

    private static void AnalyzeTagPayloadBytes(
        int line,
        int baseCharInLine,
        Utf8IndexMap map,
        ReadOnlyMemory<byte> lineBytes,
        ReadOnlySpan<byte> payload,
        int payloadAbsoluteStartByte,
        List<AssDiagnostic> diagnostics,
        AssOverrideTextAnalyzerContext? context,
        int depth)
    {
        var scanner = new AssTagBlockScanner(payload, payloadAbsoluteStartByte, lineBytes);
        while (scanner.MoveNext(out var token))
        {
            int slashChar = baseCharInLine + map.ByteToCharIndex(token.TagStart);
            int nameEndChar = baseCharInLine + map.ByteToCharIndex(token.NameEnd);

            if (!token.IsKnown)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, slashChar), new AssPosition(line, nameEndChar)),
                    AssSeverity.Error,
                    $"Unknown override tag: \\{Encoding.ASCII.GetString(token.NameAndMaybePayload)}",
                    Code: "ass.override.unknownTag"));
                continue;
            }

            if (AssTagRegistry.TryGetObsoleteReplacement(token.Tag, out var obsoleteReplacement))
            {
                string? mapping = null;
                if (token.Tag == AssTag.AlignmentLegacy)
                {
                    var p = Utils.TrimSpaces(token.Param);
                    if (Utils.TryParseIntLoose(p, out int legacy, out var invalid) && !invalid &&
                        AssTagRegistry.TryMapLegacyAlignmentToAn(legacy, out int an))
                    {
                        mapping = $" (\\\\a{legacy} -> \\\\an{an})";
                    }
                }

                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, slashChar), new AssPosition(line, nameEndChar)),
                    AssSeverity.Warning,
                    $"Override tag \\\\{Encoding.ASCII.GetString(token.NameAndMaybePayload)} is obsolete; use \\\\{Encoding.ASCII.GetString(obsoleteReplacement)} instead.{mapping}",
                    Code: "ass.override.obsoleteTag"));
            }

            ValidateTagValueBytes(
                line,
                baseCharInLine,
                map,
                lineBytes,
                token,
                diagnostics,
                context,
                depth);
        }
    }

    private static void ValidateTagValueBytes(
        int line,
        int baseCharInLine,
        Utf8IndexMap map,
        ReadOnlyMemory<byte> lineBytes,
        in AssTagBlockToken token,
        List<AssDiagnostic> diagnostics,
        AssOverrideTextAnalyzerContext? context,
        int depth)
    {
        var param = token.Param;
        Utils.TrimSpaces(param, out int trimStart, out int trimLength);
        if (trimLength == 0)
            return;

        var trimmed = param.Slice(trimStart, trimLength);

        int paramStartChar = baseCharInLine + map.ByteToCharIndex(token.ParamStart);
        int paramEndChar = baseCharInLine + map.ByteToCharIndex(token.ParamEnd);

        int trimmedStartByte = token.ParamStart + trimStart;
        int trimmedEndByte = trimmedStartByte + trimLength;
        int trimmedParamBaseChar = baseCharInLine + map.ByteToCharIndex(trimmedStartByte);
        int trimmedParamEndChar = baseCharInLine + map.ByteToCharIndex(trimmedEndByte);
        int trimmedParamLength = Math.Max(0, trimmedParamEndChar - trimmedParamBaseChar);

        if (AssTagRegistry.TryGetFunctionKind(token.Tag, out var functionKind))
        {
            if (!ValidateFunctionTagValueBytes(
                line,
                baseCharInLine,
                map,
                lineBytes,
                trimmedStartByte,
                trimmedParamBaseChar,
                trimmedParamLength,
                trimmed,
                functionKind,
                diagnostics,
                context,
                depth))
            {
                var sig = AssTagRegistry.GetFunctionSignature(functionKind);
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramStartChar), new AssPosition(line, paramEndChar)),
                    AssSeverity.Warning,
                    sig == null
                        ? $"Invalid function payload for \\{Encoding.ASCII.GetString(token.Desc.Name.Span)}"
                        : $"Invalid function payload for \\{Encoding.ASCII.GetString(token.Desc.Name.Span)} (expected {sig})",
                    Code: "ass.override.functionInvalid"));
            }
            return;
        }

        if (IsAlphaTag(token.Tag))
        {
            if (!AssColor32.TryParseAlphaByte(trimmed, out _, out var invalidAlpha) || invalidAlpha)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramStartChar), new AssPosition(line, paramEndChar)),
                    AssSeverity.Warning,
                    $"Invalid alpha value for \\{Encoding.ASCII.GetString(token.Desc.Name.Span)}",
                    Code: "ass.override.alphaInvalid"));
            }
            return;
        }

        if (token.Desc.ValueType == typeof(AssColor32))
        {
            if (!AssColor32.TryParseTagColor(trimmed, out var color, out var ignoredHighByte, out var invalidColor) || invalidColor)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramStartChar), new AssPosition(line, paramEndChar)),
                    AssSeverity.Warning,
                    $"Invalid color value for \\{Encoding.ASCII.GetString(token.Desc.Name.Span)}",
                    Code: "ass.override.colorInvalid"));
            }
            else if (TryGetColorNormalizationSuggestion(trimmed, color, ignoredHighByte, out var normalized))
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramStartChar), new AssPosition(line, paramEndChar)),
                    AssSeverity.Info,
                    $"Normalize to \\{Encoding.ASCII.GetString(token.Desc.Name.Span)}{normalized}",
                    Code: "ass.override.colorNormalize"));
            }
            return;
        }

        if (token.Desc.ValueType == typeof(int) || token.Tag is AssTag.Alignment or AssTag.AlignmentLegacy or AssTag.WrapStyle)
        {
            if (!Utils.TryParseIntLoose(trimmed, out var v, out var invalid))
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramStartChar), new AssPosition(line, paramEndChar)),
                    AssSeverity.Warning,
                    $"Invalid integer value for \\{Encoding.ASCII.GetString(token.Desc.Name.Span)}",
                    Code: "ass.override.intInvalid"));
                return;
            }
            if (invalid)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramStartChar), new AssPosition(line, paramEndChar)),
                    AssSeverity.Info,
                    $"Non-standard integer value for \\{Encoding.ASCII.GetString(token.Desc.Name.Span)} (treated as 0).",
                    Code: "ass.override.intLoose"));
            }

            if (token.Tag is AssTag.Alignment or AssTag.AlignmentLegacy && v is < 1 or > 9)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramStartChar), new AssPosition(line, paramEndChar)),
                    AssSeverity.Warning,
                    "Alignment should be in [1..9].",
                    Code: "ass.override.alignRange"));
            }
            if (token.Tag is AssTag.WrapStyle && v is < 0 or > 3)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramStartChar), new AssPosition(line, paramEndChar)),
                    AssSeverity.Warning,
                    "WrapStyle (\\q) should be in [0..3].",
                    Code: "ass.override.wrapStyleRange"));
            }
            return;
        }

        if (token.Desc.ValueType == typeof(double))
        {
            if (!Utils.TryParseDoubleLoose(trimmed, out _, out var invalid))
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramStartChar), new AssPosition(line, paramEndChar)),
                    AssSeverity.Warning,
                    $"Invalid number value for \\{Encoding.ASCII.GetString(token.Desc.Name.Span)}",
                    Code: "ass.override.doubleInvalid"));
                return;
            }
            if (invalid)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramStartChar), new AssPosition(line, paramEndChar)),
                    AssSeverity.Info,
                    $"Non-standard numeric value for \\{Encoding.ASCII.GetString(token.Desc.Name.Span)} (treated as 0).",
                    Code: "ass.override.doubleLoose"));
            }
            return;
        }
    }

    private static bool ValidateFunctionTagValueBytes(
        int line,
        int baseCharInLine,
        Utf8IndexMap map,
        ReadOnlyMemory<byte> lineBytes,
        int trimmedParamStartByte,
        int trimmedParamBaseChar,
        int trimmedParamLength,
        ReadOnlySpan<byte> bytes,
        AssTagFunctionKind functionKind,
        List<AssDiagnostic> diagnostics,
        AssOverrideTextAnalyzerContext? context,
        int depth)
    {
        switch (functionKind)
        {
            case AssTagFunctionKind.Pos:
                if (!AssFunctionTagParsers.TryParsePos(bytes, out var x, out var y))
                    return false;
                WarnCoordinateOutOfRange(line, trimmedParamBaseChar, trimmedParamLength, context, x, y, diagnostics);
                return true;
            case AssTagFunctionKind.Org:
                if (!AssFunctionTagParsers.TryParseOrg(bytes, out var ox, out var oy))
                    return false;
                WarnCoordinateOutOfRange(line, trimmedParamBaseChar, trimmedParamLength, context, ox, oy, diagnostics);
                return true;
            case AssTagFunctionKind.Move:
                if (!AssFunctionTagParsers.TryParseMove(bytes, out var x1, out var y1, out var x2, out var y2, out var t1, out var t2, out var hasTimes))
                    return false;
                WarnCoordinateOutOfRange(line, trimmedParamBaseChar, trimmedParamLength, context, x1, y1, diagnostics);
                WarnCoordinateOutOfRange(line, trimmedParamBaseChar, trimmedParamLength, context, x2, y2, diagnostics);
                if (hasTimes)
                    WarnRelativeTimeRange(line, trimmedParamBaseChar, trimmedParamLength, context, t1, t2, diagnostics);
                return true;
            case AssTagFunctionKind.Fad:
                if (!AssFunctionTagParsers.TryParseFad(bytes, out var fi, out var fo))
                    return false;
                WarnRelativeTimeScalar(line, trimmedParamBaseChar, trimmedParamLength, context, fi, "fad.t1", diagnostics);
                WarnRelativeTimeScalar(line, trimmedParamBaseChar, trimmedParamLength, context, fo, "fad.t2", diagnostics);
                return true;
            case AssTagFunctionKind.Fade:
                if (!AssFunctionTagParsers.TryParseFade(bytes, out _, out _, out _, out var ft1, out var ft2, out var ft3, out var ft4))
                    return false;
                WarnRelativeTimeScalar(line, trimmedParamBaseChar, trimmedParamLength, context, ft1, "fade.t1", diagnostics);
                WarnRelativeTimeScalar(line, trimmedParamBaseChar, trimmedParamLength, context, ft2, "fade.t2", diagnostics);
                WarnRelativeTimeScalar(line, trimmedParamBaseChar, trimmedParamLength, context, ft3, "fade.t3", diagnostics);
                WarnRelativeTimeScalar(line, trimmedParamBaseChar, trimmedParamLength, context, ft4, "fade.t4", diagnostics);
                return true;
            case AssTagFunctionKind.ClipRect:
            case AssTagFunctionKind.ClipDrawing:
                if (!AssFunctionTagParsers.TryParseClip(bytes, out var clipKind, out int cx1, out int cy1, out int cx2, out int cy2, out _, out _))
                    return false;
                if (clipKind == AssFunctionTagParsers.AssClipKind.Rect && context != null && context.TryGetCoordinateBounds(out _, out _))
                {
                    WarnRectOutOfRange(line, trimmedParamBaseChar, trimmedParamLength, context, cx1, cy1, cx2, cy2, diagnostics);
                }
                return true;
            case AssTagFunctionKind.Transform:
                return ValidateTransform(bytes, line, baseCharInLine, map, lineBytes, trimmedParamStartByte, trimmedParamBaseChar, trimmedParamLength, diagnostics, context, depth);
            default:
                // Unknown function signature: accept if it at least looks like "(...)". (Might still be user-defined / future extension.)
                return Utils.TryGetParenContent(bytes, out _);
        }
    }

    private static bool ValidateTransform(
        ReadOnlySpan<byte> bytes,
        int line,
        int baseCharInLine,
        Utf8IndexMap map,
        ReadOnlyMemory<byte> lineBytes,
        int trimmedParamStartByte,
        int trimmedParamBaseChar,
        int trimmedParamLength,
        List<AssDiagnostic> diagnostics,
        AssOverrideTextAnalyzerContext? context,
        int depth)
    {
        if (!AssFunctionTagParsers.TryParseTransform(bytes, out var t1, out var t2, out var hasTimes, out _, out _, out var tagPayload))
            return false;

        if (tagPayload.IsEmpty || tagPayload[0] != (byte)'\\')
            return false;

        if (hasTimes)
            WarnRelativeTimeRange(line, trimmedParamBaseChar, trimmedParamLength, context, t1, t2, diagnostics);

        if (depth >= MaxNestedTransformDepth)
            return true;

        int slashIndex = bytes.IndexOf((byte)'\\');
        if (slashIndex < 0)
            return false;

        AnalyzeTagPayloadBytes(
            line,
            baseCharInLine,
            map,
            lineBytes,
            tagPayload,
            payloadAbsoluteStartByte: trimmedParamStartByte + slashIndex,
            diagnostics,
            context,
            depth: depth + 1);
        return true;
    }

    private static void WarnCoordinateOutOfRange(
        int line,
        int paramBaseChar,
        int paramLength,
        AssOverrideTextAnalyzerContext? context,
        double x,
        double y,
        List<AssDiagnostic> diagnostics)
    {
        if (context == null || !context.TryGetCoordinateBounds(out int rx, out int ry))
            return;
        const double eps = 1e-6;

        bool outOfRange = x < -eps || y < -eps || x > rx + eps || y > ry + eps;
        if (!outOfRange)
            return;

        diagnostics.Add(new AssDiagnostic(
            new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + paramLength)),
            AssSeverity.Warning,
            $"Coordinate is outside LayoutRes/PlayRes bounds: ({x:0.###}, {y:0.###}) vs [{rx}x{ry}]",
            Code: "ass.override.coordOutOfRange"));
    }

    private static void WarnRectOutOfRange(
        int line,
        int paramBaseChar,
        int paramLength,
        AssOverrideTextAnalyzerContext? context,
        int x1,
        int y1,
        int x2,
        int y2,
        List<AssDiagnostic> diagnostics)
    {
        if (context == null || !context.TryGetCoordinateBounds(out int rx, out int ry))
            return;

        int left = Math.Min(x1, x2);
        int right = Math.Max(x1, x2);
        int top = Math.Min(y1, y2);
        int bottom = Math.Max(y1, y2);

        bool outOfRange = left < 0 || top < 0 || right > rx || bottom > ry;
        if (!outOfRange)
            return;

        diagnostics.Add(new AssDiagnostic(
            new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + paramLength)),
            AssSeverity.Warning,
            $"Clip rect is outside LayoutRes/PlayRes bounds: ({x1},{y1},{x2},{y2}) vs [{rx}x{ry}]",
            Code: "ass.override.coordOutOfRange"));
    }

    private static void WarnRelativeTimeRange(
        int line,
        int paramBaseChar,
        int paramLength,
        AssOverrideTextAnalyzerContext? context,
        int t1,
        int t2,
        List<AssDiagnostic> diagnostics)
    {
        if (t1 < 0 || t2 < 0 || t2 < t1)
        {
            diagnostics.Add(new AssDiagnostic(
                new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + paramLength)),
                AssSeverity.Warning,
                $"Relative time range is invalid: [{t1}, {t2}] ms",
                Code: "ass.override.timeOutOfRange"));
            return;
        }

        if (context?.EventDurationMs is not int duration)
            return;

        if (t1 > duration || t2 > duration)
        {
            diagnostics.Add(new AssDiagnostic(
                new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + paramLength)),
                AssSeverity.Warning,
                $"Relative time range exceeds event duration: [{t1}, {t2}] ms vs {duration} ms",
                Code: "ass.override.timeOutOfRange"));
        }
    }

    private static void WarnRelativeTimeScalar(
        int line,
        int paramBaseChar,
        int paramLength,
        AssOverrideTextAnalyzerContext? context,
        int t,
        string name,
        List<AssDiagnostic> diagnostics)
    {
        if (t < 0)
        {
            diagnostics.Add(new AssDiagnostic(
                new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + paramLength)),
                AssSeverity.Warning,
                $"Relative time {name} is negative: {t} ms",
                Code: "ass.override.timeOutOfRange"));
            return;
        }

        if (context?.EventDurationMs is not int duration)
            return;

        if (t > duration)
        {
            diagnostics.Add(new AssDiagnostic(
                new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + paramLength)),
                AssSeverity.Warning,
                $"Relative time {name} exceeds event duration: {t} ms vs {duration} ms",
                Code: "ass.override.timeOutOfRange"));
        }
    }

    private static bool TryGetColorNormalizationSuggestion(ReadOnlySpan<byte> trimmedValue, AssColor32 color, bool ignoredHighByte, out string normalized)
    {
        // Canonical form: &HBBGGRR& (6 hex digits, prefix "&H", trailing "&").
        // We keep it simple: if input isn't in canonical form (or had ignored high byte), suggest a normalized value.
        normalized = default!;

        var sp = Utils.TrimSpaces(trimmedValue);
        if (sp.IsEmpty)
            return false;

        bool hasTrailingAmp = sp[^1] == (byte)'&';
        if (hasTrailingAmp)
            sp = sp[..^1];

        bool hasLeadingAmp = !sp.IsEmpty && sp[0] == (byte)'&';
        if (hasLeadingAmp)
            sp = sp[1..];

        bool hasH = !sp.IsEmpty && (sp[0] == (byte)'H' || sp[0] == (byte)'h');
        if (hasH)
            sp = sp[1..];

        int digits = sp.Length;
        bool canonical = hasLeadingAmp && hasH && hasTrailingAmp && digits == 6 && (digits & 1) == 0 && !ignoredHighByte;
        if (canonical)
            return false;

        // Always normalize to 6 digits (BBGGRR), alpha (if any) ignored for \c/\1c.. tags.
        normalized = "&H" + color.ConvertToString(withAlpha: false) + "&";
        return true;
    }

    private static bool IsAlphaTag(AssTag tag)
        => tag is AssTag.Alpha or AssTag.AlphaPrimary or AssTag.AlphaSecondary or AssTag.AlphaBorder or AssTag.AlphaShadow;

    private static (int Start, int End) GetRangeOffsets(Range range, int length)
        => (range.Start.GetOffset(length), range.End.GetOffset(length));
}
