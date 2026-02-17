using Mobsub.AutomationBridge.Core.Models;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal static partial class AmoMotionApplier
{
    private const string ClassDialogue = "dialogue";

    public static AmoOutputLine[]? ApplyLine(AmoPreparedLine line, AmoApplyContext ctx, List<string> logs)
    {
        if (!string.Equals(line.Source.Class, ClassDialogue, StringComparison.OrdinalIgnoreCase))
            return null;

        if (line.Source.Comment is true)
            return null;

        if (line.Source.StartFrame is null || line.Source.EndFrame is null)
            return null;

        if (line.Source.StartTime is null || line.Source.EndTime is null)
            return null;

        if (line.Source.TextUtf8 is null)
            return null;

        int startAbs = line.Source.StartFrame.Value;
        int endAbs = line.Source.EndFrame.Value;

        int relStart = startAbs - ctx.SelectionStartFrame + 1;
        int relEnd = endAbs - ctx.SelectionStartFrame;

        if (relStart < 1) relStart = 1;
        if (relEnd < 1) relEnd = 1;
        if (relStart > ctx.TotalFrames) relStart = ctx.TotalFrames;
        if (relEnd > ctx.TotalFrames) relEnd = ctx.TotalFrames;
        if (relEnd < relStart) relEnd = relStart;

        bool supportsLinear = !ctx.Options.Main.ClipOnly
            && !((ctx.Options.Main.Origin && line.HasOrg)
                || ((ctx.RectClipData is not null || ctx.VectClipData is not null) && line.HasClip));

        var linearMode = ctx.Options.Main.LinearMode;

        if (supportsLinear && linearMode != AmoLinearMode.ForceNonlinear)
        {
            if (linearMode == AmoLinearMode.ForceLinear ||
                (linearMode == AmoLinearMode.AutoLinearPos && CanAutoLinearPos(line, ctx, relStart, relEnd)) ||
                (linearMode == AmoLinearMode.AutoSegmentPos && CanAutoLinearPos(line, ctx, relStart, relEnd)))
            {
                byte[] outText = ApplyLinear(line, ctx, relStart, relEnd);
                if (line.Source.TextUtf8.Value.Span.SequenceEqual(outText))
                    return null;

                return
                [
                    new AmoOutputLine(line.Source.StartTime.Value, line.Source.EndTime.Value, outText),
                ];
            }

            if (linearMode == AmoLinearMode.AutoSegmentPos)
            {
                var outLinesSegmented = ApplySegmentedLinear(line, ctx, relStart, relEnd, logs);
                if (outLinesSegmented.Length == 0)
                    return null;

                if (outLinesSegmented.Length == 1 &&
                    outLinesSegmented[0].StartTime == line.Source.StartTime.Value &&
                    outLinesSegmented[0].EndTime == line.Source.EndTime.Value &&
                    line.Source.TextUtf8.Value.Span.SequenceEqual(outLinesSegmented[0].TextUtf8.Span))
                {
                    return null;
                }

                return outLinesSegmented;
            }
        }

        var outLines = ApplyNonlinear(line, ctx, relStart, relEnd, logs);
        if (outLines.Length == 0)
            return null;

        if (outLines.Length == 1 &&
            outLines[0].StartTime == line.Source.StartTime.Value &&
            outLines[0].EndTime == line.Source.EndTime.Value &&
            line.Source.TextUtf8.Value.Span.SequenceEqual(outLines[0].TextUtf8.Span))
        {
            return null;
        }

        return outLines;
    }

    private static int GetFrameStartMs(AmoApplyContext ctx, int relFrame)
    {
        // Lua passes frame_ms for frames [selection_start_frame .. selection_start_frame + total_frames].
        // We use 1-based relFrame mapping:
        // relFrame=1 => frame_ms[0]
        // relFrame=total_frames+1 => frame_ms[total_frames]
        int i = Math.Clamp(relFrame - 1, 0, ctx.FrameMs.Length - 1);
        return ctx.FrameMs[i];
    }

    private static partial byte[] ApplyLinear(AmoPreparedLine line, AmoApplyContext ctx, int relStart, int relEnd);
    private static partial AmoOutputLine[] ApplyNonlinear(AmoPreparedLine line, AmoApplyContext ctx, int relStart, int relEnd, List<string> logs);
    private static bool CanAutoLinearPos(AmoPreparedLine line, AmoApplyContext ctx, int relStart, int relEnd)
    {
        if (ctx.MainData is not AmoTsrData)
            return false;

        double eps = GetSegmentPosEps(ctx.Options.Main.SegmentPosEps, ctx.ScriptResX, ctx.ScriptResY);
        if (eps <= 0)
            return false;

        if (!TryGetPosSeries(line, ctx, relStart, relEnd, ctx.Options.Main.PosErrorMode, out var t, out var x, out var y))
            return false;

        return IsPosLinearWithinEps(t, x, y, eps);
    }

    private static partial AmoOutputLine[] ApplySegmentedLinear(AmoPreparedLine line, AmoApplyContext ctx, int relStart, int relEnd, List<string> logs);
    private static double GetSegmentPosEps(double configured, int resX, int resY)
    {
        if (configured > 0)
            return configured;
        int minRes = Math.Min(resX, resY);
        return minRes > 0 ? minRes / 1000.0 : 0;
    }

    private static partial bool TryGetPosSeries(
        AmoPreparedLine line,
        AmoApplyContext ctx,
        int relStart,
        int relEnd,
        AmoPosErrorMode errorMode,
        out double[] t,
        out double[] x,
        out double[] y);

    private static partial bool IsPosLinearWithinEps(double[] t, double[] x, double[] y, double eps);
}
