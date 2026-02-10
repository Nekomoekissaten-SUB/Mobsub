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
            close = open + 1 + close;

            var inner = textField.Slice(open + 1, close - open - 1);
            AnalyzeTagBlock(line, baseCharInLine + open + 1, inner, diagnostics, context);

            i = close + 1;
        }
    }


    private static void AnalyzeTagBlock(int line, int blockBaseChar, ReadOnlySpan<char> block, List<AssDiagnostic> diagnostics, AssOverrideTextAnalyzerContext? context)
    {
        AnalyzeTagBlock(line, blockBaseChar, block, diagnostics, context, depth: 0);
    }

    private static void AnalyzeTagBlock(int line, int blockBaseChar, ReadOnlySpan<char> block, List<AssDiagnostic> diagnostics, AssOverrideTextAnalyzerContext? context, int depth)
    {
        int i = 0;
        while (i < block.Length)
        {
            int slash = block.Slice(i).IndexOf('\\');
            if (slash < 0)
                break;
            slash += i;
            int nameStart = slash + 1;
            if (nameStart >= block.Length)
                break;

            int nameEnd = nameStart;
            while (nameEnd < block.Length && IsAsciiLetterOrDigit(block[nameEnd]))
                nameEnd++;

            if (nameEnd == nameStart)
            {
                i = nameStart;
                continue;
            }

            var nameAndMaybePayload = block.Slice(nameStart, nameEnd - nameStart);
            if (!TryMatchTag(nameAndMaybePayload, out var tag, out var desc, out int matchedLength))
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, blockBaseChar + slash), new AssPosition(line, blockBaseChar + nameEnd)),
                    AssSeverity.Error,
                    $"Unknown override tag: \\{nameAndMaybePayload.ToString()}",
                    Code: "ass.override.unknownTag"));
                i = nameEnd;
                continue;
            }

            int paramStart = nameStart + matchedLength;
            int paramEnd;

            int parenStart = paramStart;
            bool isFunction = AssTagRegistry.TryGetFunctionKind(tag, out var functionKind);
            if (isFunction)
            {
                while (parenStart < block.Length && block[parenStart] == ' ')
                    parenStart++;
            }

            if (isFunction && parenStart < block.Length && block[parenStart] == '(')
            {
                int j = parenStart + 1;
                int parenDepth = 1;
                while (j < block.Length && parenDepth > 0)
                {
                    char c = block[j];
                    if (c == '(') parenDepth++;
                    else if (c == ')') parenDepth--;
                    j++;
                }
                paramEnd = j;
            }
            else
            {
                int nextSlash = block.Slice(paramStart).IndexOf('\\');
                paramEnd = nextSlash < 0 ? block.Length : paramStart + nextSlash;
            }

            var param = block.Slice(paramStart, Math.Max(0, paramEnd - paramStart));

            if (AssTagRegistry.TryGetObsoleteReplacement(tag, out var obsoleteReplacement))
            {
                string? mapping = null;
                if (tag == AssTag.AlignmentLegacy)
                {
                    var p = param.Trim();
                    if (int.TryParse(p, out int legacy) && AssTagRegistry.TryMapLegacyAlignmentToAn(legacy, out int an))
                        mapping = $" (\\\\a{legacy} -> \\\\an{an})";
                }

                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, blockBaseChar + slash), new AssPosition(line, blockBaseChar + nameEnd)),
                    AssSeverity.Warning,
                    $"Override tag \\\\{nameAndMaybePayload.ToString()} is obsolete; use \\\\{Encoding.ASCII.GetString(obsoleteReplacement)} instead.{mapping}",
                    Code: "ass.override.obsoleteTag"));
            }

            ValidateTagValue(line, blockBaseChar + paramStart, param, tag, desc, isFunction ? functionKind : AssTagFunctionKind.None, diagnostics, context, depth);

            i = paramEnd;
        }
    }

    private static void ValidateTagValue(int line, int paramBaseChar, ReadOnlySpan<char> param, AssTag tag, AssTagDescriptor desc, AssTagFunctionKind functionKind, List<AssDiagnostic> diagnostics, AssOverrideTextAnalyzerContext? context, int depth)
    {
        var trimmed = param.Trim();
        if (trimmed.IsEmpty)
            return;

        int leadingSpaces = 0;
        while (leadingSpaces < param.Length && char.IsWhiteSpace(param[leadingSpaces]))
            leadingSpaces++;

        Span<byte> bytes = trimmed.Length <= 256 ? stackalloc byte[trimmed.Length] : new byte[trimmed.Length];
        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            bytes[i] = c <= 0x7F ? (byte)c : (byte)'?';
        }

        if (functionKind != AssTagFunctionKind.None)
        {
            if (!ValidateFunctionTagValueBytes(line, paramBaseChar + leadingSpaces, bytes, functionKind, tag, desc, diagnostics, context, depth))
            {
                var sig = AssTagRegistry.GetFunctionSignature(functionKind);
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Warning,
                    sig == null
                        ? $"Invalid function payload for \\{Encoding.ASCII.GetString(desc.Name.Span)}"
                        : $"Invalid function payload for \\{Encoding.ASCII.GetString(desc.Name.Span)} (expected {sig})",
                    Code: "ass.override.functionInvalid"));
            }
            return;
        }

        if (IsAlphaTag(tag))
        {
            if (!AssColor32.TryParseAlphaByte(bytes, out _, out var invalidAlpha) || invalidAlpha)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Warning,
                    $"Invalid alpha value for \\{Encoding.ASCII.GetString(desc.Name.Span)}",
                    Code: "ass.override.alphaInvalid"));
            }
            return;
        }

        if (desc.ValueType == typeof(AssColor32))
        {
            if (!AssColor32.TryParseTagColor(bytes, out var color, out var ignoredHighByte, out var invalidColor) || invalidColor)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Warning,
                    $"Invalid color value for \\{Encoding.ASCII.GetString(desc.Name.Span)}",
                    Code: "ass.override.colorInvalid"));
            }
            else if (TryGetColorNormalizationSuggestion(bytes, color, ignoredHighByte, out var normalized))
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Info,
                    $"Normalize to \\{Encoding.ASCII.GetString(desc.Name.Span)}{normalized}",
                    Code: "ass.override.colorNormalize"));
            }
            return;
        }

        if (desc.ValueType == typeof(int))
        {
            if (!Utils.TryParseIntLoose(bytes, out var v, out var invalid))
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Warning,
                    $"Invalid integer value for \\{Encoding.ASCII.GetString(desc.Name.Span)}",
                    Code: "ass.override.intInvalid"));
                return;
            }
            if (invalid)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Info,
                    $"Non-standard integer value for \\{Encoding.ASCII.GetString(desc.Name.Span)} (treated as 0).",
                    Code: "ass.override.intLoose"));
            }

            if (tag is AssTag.Alignment or AssTag.AlignmentLegacy && v is < 1 or > 9)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Warning,
                    "Alignment should be in [1..9].",
                    Code: "ass.override.alignRange"));
            }
            if (tag is AssTag.WrapStyle && v is < 0 or > 3)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Warning,
                    "WrapStyle (\\q) should be in [0..3].",
                    Code: "ass.override.wrapStyleRange"));
            }
            return;
        }

        if (desc.ValueType == typeof(double))
        {
            if (!Utils.TryParseDoubleLoose(bytes, out _, out var invalid))
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Warning,
                    $"Invalid number value for \\{Encoding.ASCII.GetString(desc.Name.Span)}",
                    Code: "ass.override.doubleInvalid"));
                return;
            }
            if (invalid)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Info,
                    $"Non-standard numeric value for \\{Encoding.ASCII.GetString(desc.Name.Span)} (treated as 0).",
                    Code: "ass.override.doubleLoose"));
            }
            return;
        }
    }

    private static bool ValidateFunctionTagValueBytes(
        int line,
        int trimmedParamBaseChar,
        ReadOnlySpan<byte> bytes,
        AssTagFunctionKind functionKind,
        AssTag tag,
        AssTagDescriptor desc,
        List<AssDiagnostic> diagnostics,
        AssOverrideTextAnalyzerContext? context,
        int depth)
    {
        switch (functionKind)
        {
            case AssTagFunctionKind.Pos:
                if (!AssFunctionTagParsers.TryParsePos(bytes, out var x, out var y))
                    return false;
                WarnCoordinateOutOfRange(line, trimmedParamBaseChar, bytes.Length, context, x, y, diagnostics);
                return true;
            case AssTagFunctionKind.Org:
                if (!AssFunctionTagParsers.TryParseOrg(bytes, out var ox, out var oy))
                    return false;
                WarnCoordinateOutOfRange(line, trimmedParamBaseChar, bytes.Length, context, ox, oy, diagnostics);
                return true;
            case AssTagFunctionKind.Move:
                if (!AssFunctionTagParsers.TryParseMove(bytes, out var x1, out var y1, out var x2, out var y2, out var t1, out var t2, out var hasTimes))
                    return false;
                WarnCoordinateOutOfRange(line, trimmedParamBaseChar, bytes.Length, context, x1, y1, diagnostics);
                WarnCoordinateOutOfRange(line, trimmedParamBaseChar, bytes.Length, context, x2, y2, diagnostics);
                if (hasTimes)
                    WarnRelativeTimeRange(line, trimmedParamBaseChar, bytes.Length, context, t1, t2, diagnostics);
                return true;
            case AssTagFunctionKind.Fad:
                if (!AssFunctionTagParsers.TryParseFad(bytes, out var fi, out var fo))
                    return false;
                WarnRelativeTimeScalar(line, trimmedParamBaseChar, bytes.Length, context, fi, "fad.t1", diagnostics);
                WarnRelativeTimeScalar(line, trimmedParamBaseChar, bytes.Length, context, fo, "fad.t2", diagnostics);
                return true;
            case AssTagFunctionKind.Fade:
                if (!AssFunctionTagParsers.TryParseFade(bytes, out _, out _, out _, out var ft1, out var ft2, out var ft3, out var ft4))
                    return false;
                WarnRelativeTimeScalar(line, trimmedParamBaseChar, bytes.Length, context, ft1, "fade.t1", diagnostics);
                WarnRelativeTimeScalar(line, trimmedParamBaseChar, bytes.Length, context, ft2, "fade.t2", diagnostics);
                WarnRelativeTimeScalar(line, trimmedParamBaseChar, bytes.Length, context, ft3, "fade.t3", diagnostics);
                WarnRelativeTimeScalar(line, trimmedParamBaseChar, bytes.Length, context, ft4, "fade.t4", diagnostics);
                return true;
            case AssTagFunctionKind.ClipRect:
            case AssTagFunctionKind.ClipDrawing:
                if (!AssFunctionTagParsers.TryParseClip(bytes, out var clipKind, out int cx1, out int cy1, out int cx2, out int cy2, out _, out _))
                    return false;
                if (clipKind == AssFunctionTagParsers.AssClipKind.Rect && context != null && context.TryGetCoordinateBounds(out _, out _))
                {
                    WarnRectOutOfRange(line, trimmedParamBaseChar, bytes.Length, context, cx1, cy1, cx2, cy2, diagnostics);
                }
                return true;
            case AssTagFunctionKind.Transform:
                return ValidateTransform(bytes, line, trimmedParamBaseChar, diagnostics, context, depth);
            default:
                // Unknown function signature: accept if it at least looks like "(...)".
                return Utils.TryGetParenContent(bytes, out _);
        }
    }

    private static bool ValidateTransform(
        ReadOnlySpan<byte> bytes,
        int line,
        int trimmedParamBaseChar,
        List<AssDiagnostic> diagnostics,
        AssOverrideTextAnalyzerContext? context,
        int depth)
    {
        if (!AssFunctionTagParsers.TryParseTransform(bytes, out var t1, out var t2, out var hasTimes, out _, out _, out var tagPayload))
            return false;

        if (tagPayload.IsEmpty || tagPayload[0] != (byte)'\\')
            return false;

        if (hasTimes)
            WarnRelativeTimeRange(line, trimmedParamBaseChar, bytes.Length, context, t1, t2, diagnostics);

        if (depth >= MaxNestedTransformDepth)
            return true;

        int payloadOffsetInTrimmed = bytes.Length - tagPayload.Length;
        AnalyzeTagPayloadBytes(line, trimmedParamBaseChar + payloadOffsetInTrimmed, tagPayload, diagnostics, context, depth + 1);
        return true;
    }

    private static void AnalyzeTagPayloadBytes(
        int line,
        int baseChar,
        ReadOnlySpan<byte> payload,
        List<AssDiagnostic> diagnostics,
        AssOverrideTextAnalyzerContext? context,
        int depth)
    {
        int i = 0;
        while (i < payload.Length)
        {
            int slash = payload.Slice(i).IndexOf((byte)'\\');
            if (slash < 0)
                break;
            slash += i;

            int nameStart = slash + 1;
            if (nameStart >= payload.Length)
                break;

            int nameEnd = nameStart;
            while (nameEnd < payload.Length && IsAsciiLetterOrDigit((char)payload[nameEnd]))
                nameEnd++;

            if (nameEnd == nameStart)
            {
                i = nameStart;
                continue;
            }

            var nameAndMaybePayload = payload.Slice(nameStart, nameEnd - nameStart);
            if (!AssTagRegistry.TryMatch(nameAndMaybePayload, out var tag, out var desc, out int matchedLength))
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, baseChar + slash), new AssPosition(line, baseChar + nameEnd)),
                    AssSeverity.Error,
                    $"Unknown override tag: \\{Encoding.ASCII.GetString(nameAndMaybePayload)}",
                    Code: "ass.override.unknownTag"));
                i = nameEnd;
                continue;
            }

            int paramStart = nameStart + matchedLength;
            int paramEnd;

            int parenStart = paramStart;
            bool isFunction = AssTagRegistry.TryGetFunctionKind(tag, out var functionKind);
            if (isFunction)
            {
                while (parenStart < payload.Length && payload[parenStart] == (byte)' ')
                    parenStart++;
            }

            if (isFunction && parenStart < payload.Length && payload[parenStart] == (byte)'(')
            {
                int j = parenStart + 1;
                int depthParens = 1;
                while (j < payload.Length && depthParens > 0)
                {
                    byte c = payload[j];
                    if (c == (byte)'(') depthParens++;
                    else if (c == (byte)')') depthParens--;
                    j++;
                }
                paramEnd = j;
            }
            else
            {
                int nextSlash = payload.Slice(paramStart).IndexOf((byte)'\\');
                paramEnd = nextSlash < 0 ? payload.Length : paramStart + nextSlash;
            }

            var param = payload.Slice(paramStart, Math.Max(0, paramEnd - paramStart));

            if (AssTagRegistry.TryGetObsoleteReplacement(tag, out var obsoleteReplacement))
            {
                string? mapping = null;
                if (tag == AssTag.AlignmentLegacy)
                {
                    Utils.TrimSpaces(param, out int s, out int len);
                    var p = param.Slice(s, len);
                    if (Utils.TryParseIntLoose(p, out int legacy, out _) && AssTagRegistry.TryMapLegacyAlignmentToAn(legacy, out int an))
                        mapping = $" (\\\\a{legacy} -> \\\\an{an})";
                }

                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, baseChar + slash), new AssPosition(line, baseChar + nameEnd)),
                    AssSeverity.Warning,
                    $"Override tag \\\\{Encoding.ASCII.GetString(nameAndMaybePayload)} is obsolete; use \\\\{Encoding.ASCII.GetString(obsoleteReplacement)} instead.{mapping}",
                    Code: "ass.override.obsoleteTag"));
            }

            ValidateTagValueBytes(line, baseChar + paramStart, param, tag, desc, isFunction ? functionKind : AssTagFunctionKind.None, diagnostics, context, depth);

            i = paramEnd;
        }
    }

    private static void ValidateTagValueBytes(
        int line,
        int paramBaseChar,
        ReadOnlySpan<byte> param,
        AssTag tag,
        AssTagDescriptor desc,
        AssTagFunctionKind functionKind,
        List<AssDiagnostic> diagnostics,
        AssOverrideTextAnalyzerContext? context,
        int depth)
    {
        Utils.TrimSpaces(param, out int trimStart, out int trimLen);
        var trimmed = param.Slice(trimStart, trimLen);
        if (trimmed.IsEmpty)
            return;

        int trimmedBase = paramBaseChar + trimStart;

        if (functionKind != AssTagFunctionKind.None)
        {
            if (!ValidateFunctionTagValueBytes(line, trimmedBase, trimmed, functionKind, tag, desc, diagnostics, context, depth))
            {
                var sig = AssTagRegistry.GetFunctionSignature(functionKind);
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Warning,
                    sig == null
                        ? $"Invalid function payload for \\{Encoding.ASCII.GetString(desc.Name.Span)}"
                        : $"Invalid function payload for \\{Encoding.ASCII.GetString(desc.Name.Span)} (expected {sig})",
                    Code: "ass.override.functionInvalid"));
            }
            return;
        }

        if (IsAlphaTag(tag))
        {
            if (!AssColor32.TryParseAlphaByte(trimmed, out _, out var invalidAlpha) || invalidAlpha)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Warning,
                    $"Invalid alpha value for \\{Encoding.ASCII.GetString(desc.Name.Span)}",
                    Code: "ass.override.alphaInvalid"));
            }
            return;
        }

        if (desc.ValueType == typeof(AssColor32))
        {
            if (!AssColor32.TryParseTagColor(trimmed, out var color, out var ignoredHighByte, out var invalidColor) || invalidColor)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Warning,
                    $"Invalid color value for \\{Encoding.ASCII.GetString(desc.Name.Span)}",
                    Code: "ass.override.colorInvalid"));
            }
            else if (TryGetColorNormalizationSuggestion(trimmed, color, ignoredHighByte, out var normalized))
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Info,
                    $"Normalize to \\{Encoding.ASCII.GetString(desc.Name.Span)}{normalized}",
                    Code: "ass.override.colorNormalize"));
            }
            return;
        }

        if (desc.ValueType == typeof(int))
        {
            if (!Utils.TryParseIntLoose(trimmed, out var v, out var invalid))
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Warning,
                    $"Invalid integer value for \\{Encoding.ASCII.GetString(desc.Name.Span)}",
                    Code: "ass.override.intInvalid"));
                return;
            }
            if (invalid)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Info,
                    $"Non-standard integer value for \\{Encoding.ASCII.GetString(desc.Name.Span)} (treated as 0).",
                    Code: "ass.override.intLoose"));
            }

            if (tag is AssTag.Alignment or AssTag.AlignmentLegacy && v is < 1 or > 9)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Warning,
                    "Alignment should be in [1..9].",
                    Code: "ass.override.alignRange"));
            }
            if (tag is AssTag.WrapStyle && v is < 0 or > 3)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Warning,
                    "WrapStyle (\\q) should be in [0..3].",
                    Code: "ass.override.wrapStyleRange"));
            }
            return;
        }

        if (desc.ValueType == typeof(double))
        {
            if (!Utils.TryParseDoubleLoose(trimmed, out _, out var invalid))
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Warning,
                    $"Invalid number value for \\{Encoding.ASCII.GetString(desc.Name.Span)}",
                    Code: "ass.override.doubleInvalid"));
                return;
            }
            if (invalid)
            {
                diagnostics.Add(new AssDiagnostic(
                    new AssRange(new AssPosition(line, paramBaseChar), new AssPosition(line, paramBaseChar + param.Length)),
                    AssSeverity.Info,
                    $"Non-standard numeric value for \\{Encoding.ASCII.GetString(desc.Name.Span)} (treated as 0).",
                    Code: "ass.override.doubleLoose"));
            }
            return;
        }
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

    private static bool TryMatchTag(ReadOnlySpan<char> ascii, out AssTag tag, out AssTagDescriptor desc, out int matchedLength)
    {
        Span<byte> bytes = ascii.Length <= 64 ? stackalloc byte[ascii.Length] : new byte[ascii.Length];
        for (int i = 0; i < ascii.Length; i++)
        {
            char c = ascii[i];
            bytes[i] = c <= 0x7F ? (byte)c : (byte)'?';
        }
        return AssTagRegistry.TryMatch(bytes, out tag, out desc, out matchedLength);
    }

    private static bool IsAsciiLetterOrDigit(char c)
        => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static bool IsAlphaTag(AssTag tag)
        => tag is AssTag.Alpha or AssTag.AlphaPrimary or AssTag.AlphaSecondary or AssTag.AlphaBorder or AssTag.AlphaShadow;
}
