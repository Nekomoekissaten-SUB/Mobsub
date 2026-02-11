using System.Text;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

public enum AssOverrideValidationSeverity : byte
{
    Error = 1,
    Warning = 2,
    Info = 3,
}

public readonly record struct AssOverrideValidationIssue(
    int StartByte,
    int EndByte,
    AssOverrideValidationSeverity Severity,
    string Message,
    string Code);

public readonly struct AssOverrideValidationContext
{
    public AssOverrideValidationContext(int? eventDurationMs = null, int? coordinateBoundX = null, int? coordinateBoundY = null)
    {
        EventDurationMs = eventDurationMs;
        CoordinateBoundX = coordinateBoundX;
        CoordinateBoundY = coordinateBoundY;
    }

    public int? EventDurationMs { get; }
    public int? CoordinateBoundX { get; }
    public int? CoordinateBoundY { get; }

    internal bool TryGetCoordinateBounds(out int x, out int y)
    {
        if (CoordinateBoundX is int bx && CoordinateBoundY is int by)
        {
            x = bx;
            y = by;
            return true;
        }

        x = y = 0;
        return false;
    }
}

public interface IAssOverrideValidationSink
{
    void Report(in AssOverrideValidationIssue issue);
}

public static class AssOverrideTagValidator
{
    private const int MaxNestedTransformDepth = 4;

    public static void ValidateOverrideBlocks(
        ReadOnlyMemory<byte> lineBytes,
        ReadOnlySpan<AssEventSegment> segments,
        List<AssOverrideValidationIssue> issues,
        in AssOverrideValidationContext context = default)
    {
        var sink = new ListSink(issues);
        ValidateOverrideBlocks(lineBytes, segments, ref sink, context);
    }

    public static void ValidateOverrideBlocks<TSink>(
        ReadOnlyMemory<byte> lineBytes,
        ReadOnlySpan<AssEventSegment> segments,
        ref TSink sink,
        in AssOverrideValidationContext context = default)
        where TSink : struct, IAssOverrideValidationSink
    {
        for (int i = 0; i < segments.Length; i++)
        {
            ref readonly var seg = ref segments[i];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock)
                continue;

            var (start, end) = GetRangeOffsets(seg.LineRange, lineBytes.Length);
            if (end - start < 2)
                continue;

            int innerStart = start + 1;
            int innerEnd = end - 1;
            if (innerEnd <= innerStart)
                continue;

            ValidateTagPayload(
                lineBytes,
                payload: lineBytes.Span.Slice(innerStart, innerEnd - innerStart),
                payloadAbsoluteStartByte: innerStart,
                sink: ref sink,
                context: context,
                depth: 0);
        }
    }

    public static void ValidateTagPayload(
        ReadOnlyMemory<byte> lineBytes,
        ReadOnlySpan<byte> payload,
        int payloadAbsoluteStartByte,
        List<AssOverrideValidationIssue> issues,
        in AssOverrideValidationContext context = default)
    {
        var sink = new ListSink(issues);
        ValidateTagPayload(lineBytes, payload, payloadAbsoluteStartByte, ref sink, context, depth: 0);
    }

    public static void ValidateTagPayload<TSink>(
        ReadOnlyMemory<byte> lineBytes,
        ReadOnlySpan<byte> payload,
        int payloadAbsoluteStartByte,
        ref TSink sink,
        in AssOverrideValidationContext context,
        int depth)
        where TSink : struct, IAssOverrideValidationSink
    {
        var scanner = new AssTagBlockScanner(payload, payloadAbsoluteStartByte, lineBytes);
        while (scanner.MoveNext(out var token))
        {
            int tagStartByte = token.TagStart;
            int nameEndByte = token.NameEnd;
            var tagNameBytes = token.NameAndMaybePayload[..token.MatchedLength];

            if (!token.IsKnown)
            {
                sink.Report(new AssOverrideValidationIssue(
                    StartByte: tagStartByte,
                    EndByte: nameEndByte,
                    Severity: AssOverrideValidationSeverity.Error,
                    Message: $"Unknown override tag: \\{Encoding.ASCII.GetString(token.NameAndMaybePayload)}",
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

                sink.Report(new AssOverrideValidationIssue(
                    StartByte: tagStartByte,
                    EndByte: nameEndByte,
                    Severity: AssOverrideValidationSeverity.Warning,
                    Message: $"Override tag \\\\{Encoding.ASCII.GetString(tagNameBytes)} is obsolete; use \\\\{Encoding.ASCII.GetString(obsoleteReplacement)} instead.{mapping}",
                    Code: "ass.override.obsoleteTag"));
            }

            ValidateTagValue(
                lineBytes,
                token,
                tagNameBytes,
                ref sink,
                context,
                depth);
        }
    }

    private static void ValidateTagValue<TSink>(
        ReadOnlyMemory<byte> lineBytes,
        in AssTagBlockToken token,
        ReadOnlySpan<byte> tagNameBytes,
        ref TSink sink,
        in AssOverrideValidationContext context,
        int depth)
        where TSink : struct, IAssOverrideValidationSink
    {
        int paramStartByte = token.ParamStart;
        int paramEndByte = token.ParamEnd;

        Utils.TrimSpaces(token.Param, out int trimStart, out int trimLength);
        int trimmedStartByte = paramStartByte + trimStart;
        int trimmedEndByte = trimmedStartByte + trimLength;

        var trimmed = trimLength == 0 ? ReadOnlySpan<byte>.Empty : token.Param.Slice(trimStart, trimLength);

        if (!AssTagRegistry.TryGetValueKind(token.Tag, out var valueKind))
            return;

        if (valueKind == AssTagValueKind.Function && AssTagRegistry.TryGetFunctionKind(token.Tag, out var functionKind))
        {
            if (!ValidateFunctionValueBytes(
                lineBytes,
                token.Tag,
                tagNameBytes,
                trimmed,
                trimmedStartByte,
                trimmedEndByte,
                functionKind,
                ref sink,
                context,
                depth))
            {
                var sig = AssTagRegistry.GetFunctionSignature(functionKind);
                sink.Report(new AssOverrideValidationIssue(
                    StartByte: paramStartByte,
                    EndByte: paramEndByte,
                    Severity: AssOverrideValidationSeverity.Warning,
                    Message: sig == null
                        ? $"Invalid function payload for \\{Encoding.ASCII.GetString(tagNameBytes)}"
                        : $"Invalid function payload for \\{Encoding.ASCII.GetString(tagNameBytes)} (expected {sig})",
                    Code: "ass.override.functionInvalid"));
            }
            return;
        }

        if (valueKind == AssTagValueKind.Byte && AssTagRegistry.IsAlphaTag(token.Tag))
        {
            if (!AssColor32.TryParseAlphaByte(trimmed, out _, out var invalidAlpha) || invalidAlpha)
            {
                sink.Report(new AssOverrideValidationIssue(
                    StartByte: paramStartByte,
                    EndByte: paramEndByte,
                    Severity: AssOverrideValidationSeverity.Warning,
                    Message: $"Invalid alpha value for \\{Encoding.ASCII.GetString(tagNameBytes)}",
                    Code: "ass.override.alphaInvalid"));
            }
            return;
        }

        if (valueKind == AssTagValueKind.Color)
        {
            if (!AssColor32.TryParseTagColor(trimmed, out var color, out var ignoredHighByte, out var invalidColor) || invalidColor)
            {
                sink.Report(new AssOverrideValidationIssue(
                    StartByte: paramStartByte,
                    EndByte: paramEndByte,
                    Severity: AssOverrideValidationSeverity.Warning,
                    Message: $"Invalid color value for \\{Encoding.ASCII.GetString(tagNameBytes)}",
                    Code: "ass.override.colorInvalid"));
            }
            else if (TryGetColorNormalizationSuggestion(trimmed, color, ignoredHighByte, out var normalized))
            {
                sink.Report(new AssOverrideValidationIssue(
                    StartByte: paramStartByte,
                    EndByte: paramEndByte,
                    Severity: AssOverrideValidationSeverity.Info,
                    Message: $"Normalize to \\{Encoding.ASCII.GetString(tagNameBytes)}{normalized}",
                    Code: "ass.override.colorNormalize"));
            }
            return;
        }

        if (valueKind == AssTagValueKind.Int || valueKind == AssTagValueKind.Byte || valueKind == AssTagValueKind.Bool)
        {
            if (!Utils.TryParseIntLoose(trimmed, out var v, out var invalid))
                return;

            if (invalid)
            {
                var kind = valueKind == AssTagValueKind.Bool ? "bool" : "integer";
                sink.Report(new AssOverrideValidationIssue(
                    StartByte: paramStartByte,
                    EndByte: paramEndByte,
                    Severity: AssOverrideValidationSeverity.Info,
                    Message: $"Non-standard {kind} value for \\{Encoding.ASCII.GetString(tagNameBytes)} (treated as 0).",
                    Code: "ass.override.intLoose"));
            }

            if (AssTagRegistry.TryGetIntAllowedMask(token.Tag, out var mask, out var allowedCode, out var allowedMessage))
            {
                bool ok = v is >= 0 and < 64 && ((mask >> v) & 1UL) != 0;
                if (!ok)
                {
                    sink.Report(new AssOverrideValidationIssue(
                        StartByte: paramStartByte,
                        EndByte: paramEndByte,
                        Severity: AssOverrideValidationSeverity.Warning,
                        Message: allowedMessage ?? "Value is not allowed.",
                        Code: allowedCode ?? "ass.override.intAllowedMask"));
                }
                return;
            }

            if (AssTagRegistry.TryGetIntRange(token.Tag, out int min, out int max, out var code, out var message) && (v < min || v > max))
            {
                sink.Report(new AssOverrideValidationIssue(
                    StartByte: paramStartByte,
                    EndByte: paramEndByte,
                    Severity: AssOverrideValidationSeverity.Warning,
                    Message: message ?? "Value is out of range.",
                    Code: code ?? "ass.override.intRange"));
            }
            return;
        }

        if (valueKind == AssTagValueKind.Double)
        {
            if (!Utils.TryParseDoubleLoose(trimmed, out double v, out var invalid))
                return;

            if (invalid)
            {
                sink.Report(new AssOverrideValidationIssue(
                    StartByte: paramStartByte,
                    EndByte: paramEndByte,
                    Severity: AssOverrideValidationSeverity.Info,
                    Message: $"Non-standard numeric value for \\{Encoding.ASCII.GetString(tagNameBytes)} (treated as 0).",
                    Code: "ass.override.doubleLoose"));
            }

            if (AssTagRegistry.TryGetDoubleRange(token.Tag, out var min, out var max, out var code, out var message) && (v < min || v > max))
            {
                sink.Report(new AssOverrideValidationIssue(
                    StartByte: paramStartByte,
                    EndByte: paramEndByte,
                    Severity: AssOverrideValidationSeverity.Warning,
                    Message: message ?? "Value is out of range.",
                    Code: code ?? "ass.override.doubleRange"));
            }
            return;
        }

        _ = trimmedStartByte;
        _ = trimmedEndByte;
    }

    private static bool ValidateFunctionValueBytes<TSink>(
        ReadOnlyMemory<byte> lineBytes,
        AssTag tag,
        ReadOnlySpan<byte> tagNameBytes,
        ReadOnlySpan<byte> bytes,
        int trimmedParamStartByte,
        int trimmedParamEndByte,
        AssTagFunctionKind functionKind,
        ref TSink sink,
        in AssOverrideValidationContext context,
        int depth)
        where TSink : struct, IAssOverrideValidationSink
    {
        switch (functionKind)
        {
            case AssTagFunctionKind.Pos:
                if (!AssFunctionTagParsers.TryParsePos(bytes, out var x, out var y))
                    return false;
                WarnCoordinateOutOfRange(trimmedParamStartByte, trimmedParamEndByte, context, x, y, ref sink);
                return true;
            case AssTagFunctionKind.Org:
                if (!AssFunctionTagParsers.TryParseOrg(bytes, out var ox, out var oy))
                    return false;
                WarnCoordinateOutOfRange(trimmedParamStartByte, trimmedParamEndByte, context, ox, oy, ref sink);
                return true;
            case AssTagFunctionKind.Move:
                if (!AssFunctionTagParsers.TryParseMove(bytes, out var x1, out var y1, out var x2, out var y2, out var t1, out var t2, out var hasTimes))
                    return false;
                WarnCoordinateOutOfRange(trimmedParamStartByte, trimmedParamEndByte, context, x1, y1, ref sink);
                WarnCoordinateOutOfRange(trimmedParamStartByte, trimmedParamEndByte, context, x2, y2, ref sink);
                if (hasTimes)
                    WarnRelativeTimeRange(trimmedParamStartByte, trimmedParamEndByte, context, t1, t2, ref sink);
                return true;
            case AssTagFunctionKind.Fad:
                if (!AssFunctionTagParsers.TryParseFad(bytes, out var fi, out var fo))
                    return false;
                WarnRelativeTimeScalar(trimmedParamStartByte, trimmedParamEndByte, context, fi, "fad.t1", ref sink);
                WarnRelativeTimeScalar(trimmedParamStartByte, trimmedParamEndByte, context, fo, "fad.t2", ref sink);
                return true;
            case AssTagFunctionKind.Fade:
                if (!AssFunctionTagParsers.TryParseFade(bytes, out var a1, out var a2, out var a3, out var ft1, out var ft2, out var ft3, out var ft4))
                    return false;
                if (!IsValidAlpha(a1) || !IsValidAlpha(a2) || !IsValidAlpha(a3))
                {
                    sink.Report(new AssOverrideValidationIssue(
                        StartByte: trimmedParamStartByte,
                        EndByte: trimmedParamEndByte,
                        Severity: AssOverrideValidationSeverity.Warning,
                        Message: $"Fade alpha values should be in [0..255], got [{a1}, {a2}, {a3}].",
                        Code: "ass.override.fadeAlphaRange"));
                }
                if (ft2 < ft1 || ft3 < ft2 || ft4 < ft3)
                {
                    sink.Report(new AssOverrideValidationIssue(
                        StartByte: trimmedParamStartByte,
                        EndByte: trimmedParamEndByte,
                        Severity: AssOverrideValidationSeverity.Warning,
                        Message: $"Fade time order should be t1<=t2<=t3<=t4, got [{ft1}, {ft2}, {ft3}, {ft4}] ms.",
                        Code: "ass.override.fadeTimeOrder"));
                }
                WarnRelativeTimeScalar(trimmedParamStartByte, trimmedParamEndByte, context, ft1, "fade.t1", ref sink);
                WarnRelativeTimeScalar(trimmedParamStartByte, trimmedParamEndByte, context, ft2, "fade.t2", ref sink);
                WarnRelativeTimeScalar(trimmedParamStartByte, trimmedParamEndByte, context, ft3, "fade.t3", ref sink);
                WarnRelativeTimeScalar(trimmedParamStartByte, trimmedParamEndByte, context, ft4, "fade.t4", ref sink);
                return true;
            case AssTagFunctionKind.ClipRect:
            case AssTagFunctionKind.ClipDrawing:
                if (!AssFunctionTagParsers.TryParseClip(bytes, out var clipKind, out int cx1, out int cy1, out int cx2, out int cy2, out _, out _))
                    return false;
                if (clipKind == AssFunctionTagParsers.AssClipKind.Rect && context.TryGetCoordinateBounds(out _, out _))
                    WarnRectOutOfRange(trimmedParamStartByte, trimmedParamEndByte, context, cx1, cy1, cx2, cy2, ref sink);
                return true;
            case AssTagFunctionKind.Transform:
                return ValidateTransform(lineBytes, tag, tagNameBytes, bytes, trimmedParamStartByte, trimmedParamEndByte, ref sink, context, depth);
            default:
                return Utils.TryGetParenContent(bytes, out _);
        }
    }

    private static bool ValidateTransform<TSink>(
        ReadOnlyMemory<byte> lineBytes,
        AssTag tag,
        ReadOnlySpan<byte> tagNameBytes,
        ReadOnlySpan<byte> bytes,
        int trimmedParamStartByte,
        int trimmedParamEndByte,
        ref TSink sink,
        in AssOverrideValidationContext context,
        int depth)
        where TSink : struct, IAssOverrideValidationSink
    {
        if (!AssFunctionTagParsers.TryParseTransform(bytes, out var t1, out var t2, out var hasTimes, out var accel, out var hasAccel, out var tagPayload))
            return false;

        if (tagPayload.IsEmpty || tagPayload[0] != (byte)'\\')
            return false;

        if (hasAccel && accel <= 0)
        {
            sink.Report(new AssOverrideValidationIssue(
                StartByte: trimmedParamStartByte,
                EndByte: trimmedParamEndByte,
                Severity: AssOverrideValidationSeverity.Warning,
                Message: $"Transform accel should be > 0, got {accel:0.###}.",
                Code: "ass.override.accelOutOfRange"));
        }

        if (hasTimes)
            WarnRelativeTimeRange(trimmedParamStartByte, trimmedParamEndByte, context, t1, t2, ref sink);

        if (depth >= MaxNestedTransformDepth)
            return true;

        int slashIndex = bytes.IndexOf((byte)'\\');
        if (slashIndex < 0)
            return false;

        int payloadAbsoluteStartByte = trimmedParamStartByte + slashIndex;
        ValidateTagPayload(lineBytes: lineBytes, payload: tagPayload, payloadAbsoluteStartByte: payloadAbsoluteStartByte, sink: ref sink, context: context, depth: depth + 1);
        return true;
    }

    private static bool IsValidAlpha(int v) => (uint)v <= 255;

    private static void WarnCoordinateOutOfRange<TSink>(
        int paramStartByte,
        int paramEndByte,
        in AssOverrideValidationContext context,
        double x,
        double y,
        ref TSink sink)
        where TSink : struct, IAssOverrideValidationSink
    {
        if (!context.TryGetCoordinateBounds(out int rx, out int ry))
            return;
        const double eps = 1e-6;

        bool outOfRange = x < -eps || y < -eps || x > rx + eps || y > ry + eps;
        if (!outOfRange)
            return;

        sink.Report(new AssOverrideValidationIssue(
            StartByte: paramStartByte,
            EndByte: paramEndByte,
            Severity: AssOverrideValidationSeverity.Warning,
            Message: $"Coordinate is outside LayoutRes/PlayRes bounds: ({x:0.###}, {y:0.###}) vs [{rx}x{ry}]",
            Code: "ass.override.coordOutOfRange"));
    }

    private static void WarnRectOutOfRange<TSink>(
        int paramStartByte,
        int paramEndByte,
        in AssOverrideValidationContext context,
        int x1,
        int y1,
        int x2,
        int y2,
        ref TSink sink)
        where TSink : struct, IAssOverrideValidationSink
    {
        if (!context.TryGetCoordinateBounds(out int rx, out int ry))
            return;

        int left = Math.Min(x1, x2);
        int right = Math.Max(x1, x2);
        int top = Math.Min(y1, y2);
        int bottom = Math.Max(y1, y2);

        bool outOfRange = left < 0 || top < 0 || right > rx || bottom > ry;
        if (!outOfRange)
            return;

        sink.Report(new AssOverrideValidationIssue(
            StartByte: paramStartByte,
            EndByte: paramEndByte,
            Severity: AssOverrideValidationSeverity.Warning,
            Message: $"Clip rect is outside LayoutRes/PlayRes bounds: ({x1},{y1},{x2},{y2}) vs [{rx}x{ry}]",
            Code: "ass.override.coordOutOfRange"));
    }

    private static void WarnRelativeTimeRange<TSink>(
        int paramStartByte,
        int paramEndByte,
        in AssOverrideValidationContext context,
        int t1,
        int t2,
        ref TSink sink)
        where TSink : struct, IAssOverrideValidationSink
    {
        if (t1 < 0 || t2 < 0 || t2 < t1)
        {
            sink.Report(new AssOverrideValidationIssue(
                StartByte: paramStartByte,
                EndByte: paramEndByte,
                Severity: AssOverrideValidationSeverity.Warning,
                Message: $"Relative time range is invalid: [{t1}, {t2}] ms",
                Code: "ass.override.timeOutOfRange"));
            return;
        }

        if (context.EventDurationMs is not int duration)
            return;

        if (t1 > duration || t2 > duration)
        {
            sink.Report(new AssOverrideValidationIssue(
                StartByte: paramStartByte,
                EndByte: paramEndByte,
                Severity: AssOverrideValidationSeverity.Warning,
                Message: $"Relative time range exceeds event duration: [{t1}, {t2}] ms vs {duration} ms",
                Code: "ass.override.timeOutOfRange"));
        }
    }

    private static void WarnRelativeTimeScalar<TSink>(
        int paramStartByte,
        int paramEndByte,
        in AssOverrideValidationContext context,
        int t,
        string name,
        ref TSink sink)
        where TSink : struct, IAssOverrideValidationSink
    {
        if (t < 0)
        {
            sink.Report(new AssOverrideValidationIssue(
                StartByte: paramStartByte,
                EndByte: paramEndByte,
                Severity: AssOverrideValidationSeverity.Warning,
                Message: $"Relative time {name} is negative: {t} ms",
                Code: "ass.override.timeOutOfRange"));
            return;
        }

        if (context.EventDurationMs is not int duration)
            return;

        if (t > duration)
        {
            sink.Report(new AssOverrideValidationIssue(
                StartByte: paramStartByte,
                EndByte: paramEndByte,
                Severity: AssOverrideValidationSeverity.Warning,
                Message: $"Relative time {name} exceeds event duration: {t} ms vs {duration} ms",
                Code: "ass.override.timeOutOfRange"));
        }
    }

    private static bool TryGetColorNormalizationSuggestion(ReadOnlySpan<byte> trimmedValue, AssColor32 color, bool ignoredHighByte, out string normalized)
    {
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

        normalized = "&H" + color.ConvertToString(withAlpha: false) + "&";
        return true;
    }

    private readonly struct ListSink(List<AssOverrideValidationIssue> issues) : IAssOverrideValidationSink
    {
        public void Report(in AssOverrideValidationIssue issue) => issues.Add(issue);
    }

    private static (int Start, int End) GetRangeOffsets(Range range, int length)
        => (range.Start.GetOffset(length), range.End.GetOffset(length));
}
