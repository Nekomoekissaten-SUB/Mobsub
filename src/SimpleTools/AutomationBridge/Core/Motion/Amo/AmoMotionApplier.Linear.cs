using System.Buffers.Text;
using Mobsub.AutomationBridge.Core.Models;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal static partial class AmoMotionApplier
{
    private const double Eps = 1e-9;

    private static partial byte[] ApplyLinear(AmoPreparedLine line, AmoApplyContext ctx, int relStart, int relEnd)
    {
        var src = line.Source;
        int durationMs = (src.StartTime is not null && src.EndTime is not null) ? Math.Max(0, src.EndTime.Value - src.StartTime.Value) : 0;

        int beginTime = 0;
        int endTime = durationMs;
        if (src.StartTime is not null)
        {
            int lineStartTime = src.StartTime.Value;
            int startFrameTime = GetFrameStartMs(ctx, relStart);
            int frameAfterStartTime = GetFrameStartMs(ctx, Math.Min(relStart + 1, ctx.TotalFrames + 1));
            int frameBeforeEndTime = GetFrameStartMs(ctx, Math.Max(relEnd, 1));
            int endFrameTime = GetFrameStartMs(ctx, Math.Min(relEnd + 1, ctx.TotalFrames + 1));

            beginTime = (int)Math.Floor(0.5 * (startFrameTime + frameAfterStartTime) - lineStartTime);
            endTime = (int)Math.Floor(0.5 * (frameBeforeEndTime + endFrameTime) - lineStartTime);
            if (beginTime < 0) beginTime = 0;
            if (endTime < beginTime) endTime = beginTime;
        }

        byte[] textUtf8 = line.Tokenized.Utf8;

        bool doNormal = ctx.MainData.Kind != AmoDataKind.Srs && !ctx.Options.Main.ClipOnly;
        bool doPosMath = doNormal && (ctx.Options.Main.XPosition || ctx.Options.Main.YPosition || ctx.Options.Main.XScale || ctx.Options.Main.ZRotation);
        bool doOrigin = doNormal && ctx.Options.Main.Origin;
        bool doScale = doNormal && ctx.Options.Main.XScale;
        bool doRot = doNormal && ctx.Options.Main.ZRotation;

        if (doPosMath || doOrigin || doScale || doRot)
        {
            var s = ctx.MainData.GetTsrState(relStart, ctx.Options.Main.XPosition, ctx.Options.Main.YPosition, ctx.Options.Main.XScale, ctx.Options.Main.ZRotation);
            var e = ctx.MainData.GetTsrState(relEnd, ctx.Options.Main.XPosition, ctx.Options.Main.YPosition, ctx.Options.Main.XScale, ctx.Options.Main.ZRotation);

            bool scaleNeutral = Math.Abs(s.RatioX - 1.0) <= Eps && Math.Abs(e.RatioX - 1.0) <= Eps;
            bool rotNeutral = Math.Abs(s.RotDiffDeg) <= Eps && Math.Abs(e.RotDiffDeg) <= Eps;

            bool posNeutral = false;
            if (ctx.MainData is AmoTsrData tsrNeutral && !ctx.Options.Main.AbsPos)
                posNeutral = IsNeutralForPosMath(s, tsrNeutral) && IsNeutralForPosMath(e, tsrNeutral);

            bool needPosRewrite = doPosMath && ctx.MainData is AmoTsrData && (!posNeutral || ctx.Options.Main.AbsPos);
            bool needOrgRewrite = doOrigin && ctx.MainData is AmoTsrData && (!posNeutral || ctx.Options.Main.AbsPos);

            bool doScaleNow = doScale && !scaleNeutral;
            bool doRotNow = doRot && !rotNeutral;

            if ((needPosRewrite || needOrgRewrite || doScaleNow || doRotNow) && ctx.MainData is AmoTsrData tsr)
            {
                textUtf8 = RewriteLinearOnce(
                    textUtf8,
                    tsr,
                    s,
                    e,
                    ctx.Options.Main,
                    ctx.Options.Main.AbsPos,
                    beginTime,
                    endTime,
                    needPosRewrite,
                    needOrgRewrite,
                    doScaleNow,
                    doRotNow);
            }
        }

        // Restore transforms raw (dontTouchTransforms) after modifications.
        var restored = new AssTokenizedText(textUtf8, line.Tokenized.Transforms, line.Tokenized.LineDurationMs).DontTouchTransforms();
        return restored;
    }

    private static double Round2(double v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    private static bool IsNeutralForPosMath(MotionTsrMath.TsrState st, AmoTsrData tsr)
    {
        return Math.Abs(st.XCur - tsr.XStartPos) <= Eps
            && Math.Abs(st.YCur - tsr.YStartPos) <= Eps
            && Math.Abs(st.RatioX - 1.0) <= Eps
            && Math.Abs(st.RotDiffDeg) <= Eps;
    }
}
