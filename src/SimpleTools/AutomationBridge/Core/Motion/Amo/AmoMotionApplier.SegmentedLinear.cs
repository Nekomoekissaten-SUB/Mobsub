using System.Buffers.Text;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal static partial class AmoMotionApplier
{
    private static partial AmoOutputLine[] ApplySegmentedLinear(AmoPreparedLine line, AmoApplyContext ctx, int relStart, int relEnd, List<string> logs)
    {
        var src = line.Source;
        if (src.StartTime is null || src.EndTime is null)
            return Array.Empty<AmoOutputLine>();

        if (ctx.MainData is not AmoTsrData tsr || tsr.Length <= 0)
            return ApplyNonlinear(line, ctx, relStart, relEnd, logs);

        double eps = GetSegmentPosEps(ctx.Options.Main.SegmentPosEps, ctx.ScriptResX, ctx.ScriptResY);
        if (eps <= 0)
            return ApplyNonlinear(line, ctx, relStart, relEnd, logs);

        if (!TryGetPosSeries(line, ctx, relStart, relEnd, ctx.Options.Main.PosErrorMode, out var t, out var x, out var y))
            return ApplyNonlinear(line, ctx, relStart, relEnd, logs);

        int frameCount = relEnd - relStart + 1;
        if (frameCount <= 2)
            return ApplyNonlinear(line, ctx, relStart, relEnd, logs);

        var segments = ComputeGreedySegments(t, x, y, eps);

        // No meaningful reduction: fall back to exact per-frame logic.
        if (segments.Count <= 1 || segments.Count >= frameCount)
            return ApplyNonlinear(line, ctx, relStart, relEnd, logs);

        int lineStartTimeAbs = src.StartTime.Value;
        int baseStartMsAbs = GetFrameStartMs(ctx, relStart);

        bool doNormal = ctx.MainData.Kind != AmoDataKind.Srs && !ctx.Options.Main.ClipOnly;
        bool doPosMath = doNormal && (ctx.Options.Main.XPosition || ctx.Options.Main.YPosition || ctx.Options.Main.XScale || ctx.Options.Main.ZRotation);
        bool doOrigin = doNormal && ctx.Options.Main.Origin;
        bool doScale = doNormal && ctx.Options.Main.XScale;
        bool doRot = doNormal && ctx.Options.Main.ZRotation;

        var outLines = new List<AmoOutputLine>(capacity: segments.Count);

        for (int s = 0; s < segments.Count; s++)
        {
            var seg = segments[s];
            int segStart = relStart + seg.Start;
            int segEnd = relStart + seg.End;

            int newStartMsAbs = GetFrameStartMs(ctx, segStart);
            int newEndMsAbs = GetFrameStartMs(ctx, Math.Min(segEnd + 1, ctx.TotalFrames + 1));

            int timeDeltaMs = newStartMsAbs - baseStartMsAbs;
            int relativeTimeMs = newStartMsAbs - lineStartTimeAbs;
            if (relativeTimeMs < 0) relativeTimeMs = 0;

            int durationMs = Math.Max(0, newEndMsAbs - newStartMsAbs);

            int startFrameTime = newStartMsAbs;
            int frameAfterStartTime = GetFrameStartMs(ctx, Math.Min(segStart + 1, ctx.TotalFrames + 1));
            int frameBeforeEndTime = GetFrameStartMs(ctx, Math.Max(segEnd, 1));
            int endFrameTime = GetFrameStartMs(ctx, Math.Min(segEnd + 1, ctx.TotalFrames + 1));

            int beginTime = (int)Math.Floor(0.5 * (startFrameTime + frameAfterStartTime) - newStartMsAbs);
            int endTime = (int)Math.Floor(0.5 * (frameBeforeEndTime + endFrameTime) - newStartMsAbs);
            if (beginTime < 0) beginTime = 0;
            if (endTime < beginTime) endTime = beginTime;

            // Evaluate TSR states at segment endpoints.
            var stS = ctx.MainData.GetTsrState(segStart, ctx.Options.Main.XPosition, ctx.Options.Main.YPosition, ctx.Options.Main.XScale, ctx.Options.Main.ZRotation);
            var stE = ctx.MainData.GetTsrState(segEnd, ctx.Options.Main.XPosition, ctx.Options.Main.YPosition, ctx.Options.Main.XScale, ctx.Options.Main.ZRotation);

            bool scaleNeutral = Math.Abs(stS.RatioX - 1.0) <= Eps && Math.Abs(stE.RatioX - 1.0) <= Eps;
            bool rotNeutral = Math.Abs(stS.RotDiffDeg) <= Eps && Math.Abs(stE.RotDiffDeg) <= Eps;

            bool posNeutral = false;
            if (!ctx.Options.Main.AbsPos)
                posNeutral = IsNeutralForPosMath(stS, tsr) && IsNeutralForPosMath(stE, tsr);

            bool needPosRewrite = doPosMath && (!posNeutral || ctx.Options.Main.AbsPos);
            bool needOrgRewrite = doOrigin && (!posNeutral || ctx.Options.Main.AbsPos);

            bool doScaleNow = doScale && !scaleNeutral;
            bool doRotNow = doRot && !rotNeutral;

            byte[] rewrittenTokenized = RewriteSegmentLinearOnce(
                line.Tokenized.Utf8,
                tsr,
                stS,
                stE,
                ctx.Options.Main,
                absPos: ctx.Options.Main.AbsPos,
                begin: beginTime,
                end: endTime,
                needPosRewrite: needPosRewrite,
                needOrgRewrite: needOrgRewrite,
                doScale: doScaleNow,
                doRot: doRotNow,
                relativeTimeMs: relativeTimeMs,
                timeDeltaMs: timeDeltaMs,
                logs: logs);

            byte[] detokShifted = new AssTokenizedText(rewrittenTokenized, line.Tokenized.Transforms, line.Tokenized.LineDurationMs).DetokenizeShifted(timeDeltaMs);
            detokShifted = AssOverrideTagRewriter.RemoveEmptyOverrideBlocks(detokShifted);

            if (outLines.Count > 0)
            {
                var last = outLines[outLines.Count - 1];
                if (last.EndTime == newStartMsAbs && last.TextUtf8.Span.SequenceEqual(detokShifted))
                {
                    outLines[outLines.Count - 1] = last with { EndTime = newEndMsAbs };
                    continue;
                }
            }

            outLines.Add(new AmoOutputLine(newStartMsAbs, newEndMsAbs, detokShifted));
        }

        return outLines.ToArray();
    }

    private readonly record struct Segment(int Start, int End);

    private static List<Segment> ComputeGreedySegments(double[] t, double[] x, double[] y, double eps)
    {
        int n = t.Length;
        var segments = new List<Segment>(capacity: Math.Min(64, n));

        if (n <= 0)
            return segments;

        double eps2 = eps * eps;

        int start = 0;
        while (start < n)
        {
            int end = start;
            int next = end + 1;

            while (next < n && IsSegmentWithinEps(t, x, y, start, next, eps2))
            {
                end = next;
                next = end + 1;
            }

            segments.Add(new Segment(start, end));
            start = end + 1;
        }

        return segments;
    }

    private static bool IsSegmentWithinEps(double[] t, double[] x, double[] y, int start, int end, double eps2)
    {
        if (end - start <= 1)
            return true;

        double tA = t[start];
        double tB = t[end];
        double dt = tB - tA;

        double xA = x[start];
        double yA = y[start];
        double xB = x[end];
        double yB = y[end];

        for (int i = start + 1; i < end; i++)
        {
            double u = Math.Abs(dt) <= 1e-9 ? 0 : (t[i] - tA) / dt;
            double px = xA + (xB - xA) * u;
            double py = yA + (yB - yA) * u;

            double dx = x[i] - px;
            double dy = y[i] - py;
            double err = dx * dx + dy * dy;
            if (err > eps2)
                return false;
        }

        return true;
    }

    private static byte[] RewriteSegmentLinearOnce(
        byte[] textUtf8,
        AmoTsrData data,
        MotionTsrMath.TsrState s,
        MotionTsrMath.TsrState e,
        AmoMainOptions opt,
        bool absPos,
        int begin,
        int end,
        bool needPosRewrite,
        bool needOrgRewrite,
        bool doScale,
        bool doRot,
        int relativeTimeMs,
        int timeDeltaMs,
        List<string> logs)
    {
        if (textUtf8.Length == 0)
            return textUtf8;

        if (!needPosRewrite && !needOrgRewrite && !doScale && !doRot && timeDeltaMs == 0)
            return textUtf8;

        // No override blocks => nothing to rewrite for motion tags.
        if (textUtf8.AsSpan().IndexOf((byte)'{') < 0)
            return textUtf8;

        int shiftCs = (int)Math.Round(timeDeltaMs * 0.1, MidpointRounding.AwayFromZero);
        int shiftRemainingCs = shiftCs;

        using var edit = AssEventTextEdit.Parse(textUtf8);
        ReadOnlySpan<byte> utf8 = edit.Utf8Bytes.Span;
        ReadOnlySpan<AssEventSegment> segments = edit.Segments;

        Span<byte> karaokeNumBuf = stackalloc byte[16];

        for (int si = 0; si < segments.Length; si++)
        {
            ref readonly var seg = ref segments[si];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tags = seg.Tags.Value.Span;
            for (int i = 0; i < tags.Length; i++)
            {
                ref readonly var t = ref tags[i];

                int start = t.LineRange.Start.GetOffset(utf8.Length);
                int endExclusive = t.LineRange.End.GetOffset(utf8.Length);
                if (endExclusive <= start)
                    continue;

                ReadOnlySpan<byte> tagBytes = utf8.Slice(start, endExclusive - start);

                if (t.Tag == AssTag.Fade && timeDeltaMs != 0)
                {
                    if (!tagBytes.StartsWith(RawTagStartFade))
                        continue;
                    if (!t.TryGet<AssTagFunctionValue>(out var func) || func.Kind != AssTagFunctionKind.Fade)
                    {
                        logs.Add("Malformed \\fade(...) encountered; leaving unchanged.");
                        continue;
                    }

                    int t1 = func.T1 - timeDeltaMs;
                    int t2 = func.T2 - timeDeltaMs;
                    int t3 = func.T3 - timeDeltaMs;
                    int t4 = func.T4 - timeDeltaMs;
                    edit.Replace(t.LineRange, BuildFadeTag(func.A1, func.A2, func.A3, t1, t2, t3, t4));
                    continue;
                }

                if ((t.Tag == AssTag.Movement || t.Tag == AssTag.Position) && (needPosRewrite || needOrgRewrite))
                {
                    if (t.Tag == AssTag.Movement)
                    {
                        if (!tagBytes.StartsWith(RawTagStartMove))
                            continue;
                        if (!t.TryGet<AssTagFunctionValue>(out var func) || func.Kind != AssTagFunctionKind.Move)
                            continue;

                        (double ox, double oy) = GetMovePosAtTime(func, relativeTimeMs);

                        if (needPosRewrite)
                        {
                            (double xs, double ys) = absPos
                                ? (s.XCur, s.YCur)
                                : MotionTsrMath.PositionMath(ox, oy, data.XStartPos, data.YStartPos, s);
                            (double xe, double ye) = absPos
                                ? (e.XCur, e.YCur)
                                : MotionTsrMath.PositionMath(ox, oy, data.XStartPos, data.YStartPos, e);

                            xs = Round2(xs);
                            ys = Round2(ys);
                            xe = Round2(xe);
                            ye = Round2(ye);

                            if ((Math.Abs(xs - xe) <= Eps && Math.Abs(ys - ye) <= Eps) || begin >= end)
                                edit.Replace(t.LineRange, BuildPosTag(xs, ys));
                            else
                                edit.Replace(t.LineRange, BuildMoveTag(xs, ys, xe, ye, begin, end));
                        }
                        else
                        {
                            edit.Replace(t.LineRange, BuildPosTag(ox, oy));
                        }

                        continue;
                    }

                    if (t.Tag == AssTag.Position && needPosRewrite)
                    {
                        if (!tagBytes.StartsWith(RawTagStartPos))
                            continue;
                        if (!t.TryGet<AssTagFunctionValue>(out var func) || func.Kind != AssTagFunctionKind.Pos)
                            continue;

                        (double xs, double ys) = absPos
                            ? (s.XCur, s.YCur)
                            : MotionTsrMath.PositionMath(func.X1, func.Y1, data.XStartPos, data.YStartPos, s);
                        (double xe, double ye) = absPos
                            ? (e.XCur, e.YCur)
                            : MotionTsrMath.PositionMath(func.X1, func.Y1, data.XStartPos, data.YStartPos, e);

                        xs = Round2(xs);
                        ys = Round2(ys);
                        xe = Round2(xe);
                        ye = Round2(ye);

                        if ((Math.Abs(xs - xe) <= Eps && Math.Abs(ys - ye) <= Eps) || begin >= end)
                        {
                            if (Math.Abs(xs - func.X1) > Eps || Math.Abs(ys - func.Y1) > Eps)
                                edit.Replace(t.LineRange, BuildPosTag(xs, ys));
                        }
                        else
                        {
                            edit.Replace(t.LineRange, BuildMoveTag(xs, ys, xe, ye, begin, end));
                        }

                        continue;
                    }
                }

                if (t.Tag == AssTag.OriginRotation && needOrgRewrite)
                {
                    if (!tagBytes.StartsWith(RawTagStartOrg))
                        continue;
                    if (!t.TryGet<AssTagFunctionValue>(out var func) || func.Kind != AssTagFunctionKind.Org)
                        continue;

                    var (xs, ys) = MotionTsrMath.PositionMath(func.X1, func.Y1, data.XStartPos, data.YStartPos, s);
                    var (xe, ye) = MotionTsrMath.PositionMath(func.X1, func.Y1, data.XStartPos, data.YStartPos, e);
                    xs = Round2(xs);
                    ys = Round2(ys);
                    xe = Round2(xe);
                    ye = Round2(ye);

                    if ((Math.Abs(xs - xe) <= Eps && Math.Abs(ys - ye) <= Eps) || begin >= end)
                    {
                        if (Math.Abs(xs - func.X1) > Eps || Math.Abs(ys - func.Y1) > Eps)
                            edit.Replace(t.LineRange, BuildOrgTag(xs, ys));
                    }
                    else
                    {
                        byte[] startTag = BuildOrgTag(xs, ys);
                        byte[] finishTag = BuildOrgTag(xe, ye);
                        edit.Replace(t.LineRange, BuildTagWithTransform(startTag, begin, end, finishTag));
                    }

                    continue;
                }

                if (doRot && (t.Tag == AssTag.FontRotationZ || t.Tag == AssTag.FontRotationZSimple) && t.TryGet<double>(out double r))
                {
                    ReadOnlySpan<byte> tagPrefixUtf8 = t.Tag == AssTag.FontRotationZ ? "\\frz"u8 : "\\fr"u8;

                    double sv = Round2(r + s.RotDiffDeg);
                    double ev = Round2(r + e.RotDiffDeg);

                    if (Math.Abs(sv - ev) <= Eps || begin >= end)
                    {
                        if (Math.Abs(sv - r) > Eps)
                            edit.Replace(t.LineRange, BuildValueTag(tagPrefixUtf8, sv));
                    }
                    else
                    {
                        byte[] startTag = BuildValueTag(tagPrefixUtf8, sv);
                        byte[] finishTag = BuildValueTag(tagPrefixUtf8, ev);
                        edit.Replace(t.LineRange, BuildTagWithTransform(startTag, begin, end, finishTag));
                    }

                    continue;
                }

                if (doScale)
                {
                    ReadOnlySpan<byte> tagPrefixUtf8;
                    bool enabled;
                    switch (t.Tag)
                    {
                        case AssTag.FontScaleX:
                            tagPrefixUtf8 = "\\fscx"u8;
                            enabled = true;
                            break;
                        case AssTag.FontScaleY:
                            tagPrefixUtf8 = "\\fscy"u8;
                            enabled = true;
                            break;
                        case AssTag.BorderX:
                            tagPrefixUtf8 = "\\xbord"u8;
                            enabled = opt.Border;
                            break;
                        case AssTag.BorderY:
                            tagPrefixUtf8 = "\\ybord"u8;
                            enabled = opt.Border;
                            break;
                        case AssTag.Border:
                            tagPrefixUtf8 = "\\bord"u8;
                            enabled = opt.Border;
                            break;
                        case AssTag.ShadowX:
                            tagPrefixUtf8 = "\\xshad"u8;
                            enabled = opt.Shadow;
                            break;
                        case AssTag.ShadowY:
                            tagPrefixUtf8 = "\\yshad"u8;
                            enabled = opt.Shadow;
                            break;
                        case AssTag.Shadow:
                            tagPrefixUtf8 = "\\shad"u8;
                            enabled = opt.Shadow;
                            break;
                        case AssTag.BlurEdgesGaussian:
                            tagPrefixUtf8 = "\\blur"u8;
                            enabled = opt.Blur;
                            break;
                        default:
                            tagPrefixUtf8 = default;
                            enabled = false;
                            break;
                    }

                    if (enabled && !tagPrefixUtf8.IsEmpty && t.TryGet<double>(out double v))
                    {
                        double sRatio = s.RatioX;
                        double eRatio = e.RatioX;

                        double sv = v;
                        double ev = v;

                        if (t.Tag == AssTag.BlurEdgesGaussian)
                        {
                            double rs = 1 - (1 - sRatio) * opt.BlurScale;
                            double re = 1 - (1 - eRatio) * opt.BlurScale;
                            sv = sv * rs;
                            ev = ev * re;
                        }
                        else
                        {
                            sv = sv * sRatio;
                            ev = ev * eRatio;
                        }

                        sv = Round2(sv);
                        ev = Round2(ev);

                        if (Math.Abs(sv - ev) <= Eps || begin >= end)
                        {
                            if (Math.Abs(sv - v) > Eps)
                                edit.Replace(t.LineRange, BuildValueTag(tagPrefixUtf8, sv));
                        }
                        else
                        {
                            byte[] startTag = BuildValueTag(tagPrefixUtf8, sv);
                            byte[] finishTag = BuildValueTag(tagPrefixUtf8, ev);
                            edit.Replace(t.LineRange, BuildTagWithTransform(startTag, begin, end, finishTag));
                        }

                        continue;
                    }
                }

                if (shiftRemainingCs > 0 && IsKaraokeTag(t.Tag) && t.TryGet<int>(out int tCs))
                {
                    int digitIndex = IndexOfFirstAsciiDigit(tagBytes);
                    if (digitIndex < 0)
                        continue;

                    int expectedDigitIndex = (t.Tag == AssTag.KaraokeF || t.Tag == AssTag.KaraokeO) ? 3 : 2;
                    if (digitIndex != expectedDigitIndex)
                        continue;
                    int digitEnd = digitIndex;
                    while ((uint)digitEnd < (uint)tagBytes.Length && (uint)(tagBytes[digitEnd] - (byte)'0') <= 9)
                        digitEnd++;

                    int newT = tCs - shiftRemainingCs;
                    shiftRemainingCs -= tCs;

                    var head = tagBytes[..digitIndex];
                    var tail = tagBytes[digitEnd..];

                    if (newT > 0)
                    {
                        if (!Utf8Formatter.TryFormat(newT, karaokeNumBuf, out int written))
                            continue;

                        byte[] repl = new byte[head.Length + written + tail.Length];
                        head.CopyTo(repl);
                        karaokeNumBuf[..written].CopyTo(repl.AsSpan(head.Length));
                        tail.CopyTo(repl.AsSpan(head.Length + written));
                        edit.Replace(t.LineRange, repl);
                    }
                    else
                    {
                        edit.Replace(t.LineRange, tail.ToArray());
                    }
                }
            }
        }

        return edit.HasEdits ? edit.ApplyToUtf8Bytes() : textUtf8;
    }
}
