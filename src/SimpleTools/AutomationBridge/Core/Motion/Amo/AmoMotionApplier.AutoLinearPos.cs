using Mobsub.SubtitleParse.AssText;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal static partial class AmoMotionApplier
{
    private static partial bool TryGetPosSeries(
        AmoPreparedLine line,
        AmoApplyContext ctx,
        int relStart,
        int relEnd,
        AmoPosErrorMode errorMode,
        out double[] t,
        out double[] x,
        out double[] y)
    {
        t = Array.Empty<double>();
        x = Array.Empty<double>();
        y = Array.Empty<double>();

        if (relStart < 1 || relEnd < relStart)
            return false;

        if (ctx.MainData is not AmoTsrData tsr || tsr.Length <= 0)
            return false;

        bool absPos = ctx.Options.Main.AbsPos;

        double baseX = 0;
        double baseY = 0;
        if (!absPos)
        {
            using var read = AssEventTextRead.Parse(line.Tokenized.Utf8);

            if (!AssTagValueParser.TryParsePosOrMoveBase(read, out baseX, out baseY))
            {
                int align = AssTagValueParser.TryParseAlign(read) ?? (line.Source.Align ?? line.Style.Align);

                int marginL = line.Source.MarginL is not null && line.Source.MarginL.Value != 0 ? line.Source.MarginL.Value : line.Style.MarginL;
                int marginR = line.Source.MarginR is not null && line.Source.MarginR.Value != 0 ? line.Source.MarginR.Value : line.Style.MarginR;
                int marginT = line.Source.MarginT is not null && line.Source.MarginT.Value != 0 ? line.Source.MarginT.Value : line.Style.MarginT;

                (baseX, baseY) = MotionTsrMath.GetDefaultPosition(ctx.ScriptResX, ctx.ScriptResY, align, marginL, marginR, marginT);
            }
        }

        int n = relEnd - relStart + 1;
        t = new double[n];
        x = new double[n];
        y = new double[n];

        bool ignoreScaleRot = errorMode == AmoPosErrorMode.IgnoreScaleRot;

        for (int i = 0; i < n; i++)
        {
            int frame = relStart + i;

            int startMsAbs = GetFrameStartMs(ctx, frame);
            int endMsAbs = GetFrameStartMs(ctx, Math.Min(frame + 1, ctx.TotalFrames + 1));
            t[i] = 0.5 * (startMsAbs + endMsAbs);

            var st = ctx.MainData.GetTsrState(
                frame,
                applyX: ctx.Options.Main.XPosition,
                applyY: ctx.Options.Main.YPosition,
                applyScale: !ignoreScaleRot && ctx.Options.Main.XScale,
                applyRotation: !ignoreScaleRot && ctx.Options.Main.ZRotation);

            if (absPos)
            {
                x[i] = st.XCur;
                y[i] = st.YCur;
            }
            else
            {
                (double nx, double ny) = MotionTsrMath.PositionMath(baseX, baseY, tsr.XStartPos, tsr.YStartPos, st);
                x[i] = nx;
                y[i] = ny;
            }
        }

        return true;
    }

    private static partial bool IsPosLinearWithinEps(double[] t, double[] x, double[] y, double eps)
    {
        if (t.Length != x.Length || x.Length != y.Length)
            return false;

        int n = t.Length;
        if (n <= 2)
            return true;

        double t0 = t[0];
        double t1 = t[n - 1];
        double dt = t1 - t0;
        if (Math.Abs(dt) <= 1e-9)
            return true;

        double x0 = x[0];
        double y0 = y[0];
        double x1 = x[n - 1];
        double y1 = y[n - 1];

        double eps2 = eps * eps;
        for (int i = 1; i < n - 1; i++)
        {
            double u = (t[i] - t0) / dt;
            double px = x0 + (x1 - x0) * u;
            double py = y0 + (y1 - y0) * u;

            double dx = x[i] - px;
            double dy = y[i] - py;
            if (dx * dx + dy * dy > eps2)
                return false;
        }

        return true;
    }
}
