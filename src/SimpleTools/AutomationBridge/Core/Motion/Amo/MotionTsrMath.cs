using System.Globalization;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal static class MotionTsrMath
{
    internal readonly record struct TsrState(double XCur, double YCur, double RatioX, double RatioY, double RotDiffDeg);

    public static void GetState(
        int frame,
        double[] xPos,
        double[] yPos,
        double[] xScale,
        double[] yScale,
        double[] zRotDeg,
        double xStartPos,
        double yStartPos,
        double startScaleX,
        double startScaleY,
        double startRotDeg,
        out TsrState state)
    {
        double xCur = xPos[frame];
        double yCur = yPos[frame];
        double ratioX = xScale[frame] / startScaleX;
        double ratioY = yScale[frame] / startScaleY;
        double rotDiffDeg = zRotDeg[frame] - startRotDeg;
        state = new TsrState(xCur, yCur, ratioX, ratioY, rotDiffDeg);
    }

    public static (double X, double Y) PositionMath(double x, double y, double xStartPos, double yStartPos, TsrState state)
    {
        double dx = (x - xStartPos) * state.RatioX;
        double dy = (y - yStartPos) * state.RatioY;
        double rot = state.RotDiffDeg * (Math.PI / 180.0);
        double cos = Math.Cos(rot);
        double sin = Math.Sin(rot);

        // Rotate by -rot (matches legacy alpha-rot behavior).
        double rx = dx * cos + dy * sin;
        double ry = -dx * sin + dy * cos;

        return (state.XCur + rx, state.YCur + ry);
    }

    public static (double X, double Y) GetDefaultPosition(int resX, int resY, int align, int marginL, int marginR, int marginT)
    {
        double x = align % 3 switch
        {
            0 => resX - marginR, // 3,6,9
            1 => marginL,        // 1,4,7
            _ => 0.5 * resX      // 2,5,8
        };

        double y = ((align + 2) / 3) switch
        {
            1 => resY - marginT, // 1,2,3
            2 => 0.5 * resY,     // 4,5,6
            _ => marginT         // 7,8,9
        };

        return (x, y);
    }

    public static int FrameToTimeMs(int frame, int frameStart, int frameEnd, int duration)
    {
        if (duration <= 0)
            return 0;
        if (frame <= frameStart)
            return 0;
        if (frame >= frameEnd)
            return duration;
        int span = frameEnd - frameStart;
        if (span <= 0)
            return 0;
        double t = (double)(frame - frameStart) * duration / span;
        return (int)Math.Round(t, MidpointRounding.AwayFromZero);
    }

    public static double Round2(double v)
        => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static string FormatCompact(double v)
        => v.ToString("0.##", CultureInfo.InvariantCulture);
}
