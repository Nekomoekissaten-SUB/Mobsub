using Mobsub.AutomationBridge.Core.Models;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal static partial class AmoMotionApplier
{
    private static partial AmoOutputLine[] ApplyNonlinear(AmoPreparedLine line, AmoApplyContext ctx, int relStart, int relEnd, List<string> logs)
    {
        var src = line.Source;
        if (src.StartTime is null || src.EndTime is null)
            return Array.Empty<AmoOutputLine>();

        int lineStartTimeAbs = src.StartTime.Value;
        int durationMs = Math.Max(0, src.EndTime.Value - src.StartTime.Value);

        bool doNormal = ctx.MainData.Kind != AmoDataKind.Srs && !ctx.Options.Main.ClipOnly;
        bool doPos = doNormal && (ctx.Options.Main.XPosition || ctx.Options.Main.YPosition || ctx.Options.Main.XScale || ctx.Options.Main.ZRotation);
        bool doOrigin = doNormal && ctx.Options.Main.Origin;
        bool doScale = doNormal && ctx.Options.Main.XScale;
        bool doRot = doNormal && ctx.Options.Main.ZRotation;

        bool doRectClip = ctx.RectClipData is not null && ctx.RectClipData.Kind == AmoDataKind.Tsr;
        bool doVectClip = ctx.VectClipData is not null;

        var outLines = new List<AmoOutputLine>(capacity: (relEnd - relStart + 1));

        int baseStartMsAbs = GetFrameStartMs(ctx, relStart);

        for (int frame = relStart; frame <= relEnd; frame++)
        {
            int newStartMsAbs = GetFrameStartMs(ctx, frame);
            int newEndMsAbs = GetFrameStartMs(ctx, Math.Min(frame + 1, ctx.TotalFrames + 1));

            int timeDeltaMs = newStartMsAbs - baseStartMsAbs;
            int relTimeMs = newStartMsAbs - lineStartTimeAbs;
            if (relTimeMs < 0) relTimeMs = 0;

            byte[] textUtf8;
            if (ctx.Options.Main.KillTrans)
            {
                int timeCenter = (int)Math.Floor(0.5 * (newStartMsAbs + newEndMsAbs) - newStartMsAbs);
                textUtf8 = line.Tokenized.InterpolateAt(timeDeltaMs, timeCenter);
            }
            else
            {
                textUtf8 = line.Tokenized.DetokenizeShifted(timeDeltaMs);
            }

            var mainState = ctx.MainData.GetTsrState(frame, ctx.Options.Main.XPosition, ctx.Options.Main.YPosition, ctx.Options.Main.XScale, ctx.Options.Main.ZRotation);
            bool mainNeutral = false;
            if (ctx.MainData is AmoTsrData tsrNeutral && !ctx.Options.Main.AbsPos)
                mainNeutral = IsNeutralForPosMath(mainState, tsrNeutral);

            MotionTsrMath.TsrState rectState = default;
            MotionTsrMath.TsrState vectState = default;
            if (doRectClip && ctx.RectClipData is not null)
                rectState = ctx.RectClipData.GetTsrState(frame, ctx.Options.Clip.XPosition, ctx.Options.Clip.YPosition, ctx.Options.Clip.XScale, applyRotation: false);
            if (doVectClip && ctx.VectClipData is not null && ctx.VectClipData.Kind == AmoDataKind.Tsr)
                vectState = ctx.VectClipData.GetTsrState(frame, ctx.Options.Clip.XPosition, ctx.Options.Clip.YPosition, ctx.Options.Clip.XScale, ctx.Options.Clip.ZRotation);

            bool needPosRewrite = doPos && ctx.MainData is AmoTsrData && (!mainNeutral || ctx.Options.Main.AbsPos);
            bool needOrgRewrite = doOrigin && ctx.MainData is AmoTsrData && (!mainNeutral || ctx.Options.Main.AbsPos);

            bool doScaleNow = doScale && Math.Abs(mainState.RatioX - 1.0) > Eps;
            bool doRotNow = doRot && Math.Abs(mainState.RotDiffDeg) > Eps;

            var beforeRewrite = textUtf8;
            textUtf8 = RewriteNonlinearFrameOnce(
                textUtf8,
                ctx,
                frame,
                relativeTimeMs: relTimeMs,
                timeDeltaMs: timeDeltaMs,
                mainState,
                rectState,
                vectState,
                needPosRewrite,
                needOrgRewrite,
                doRectClip,
                doVectClip,
                doScaleNow,
                doRotNow,
                logs);

            if (ReferenceEquals(textUtf8, beforeRewrite))
                textUtf8 = AssOverrideTagRewriter.RemoveEmptyOverrideBlocks(textUtf8);

            if (outLines.Count > 0)
            {
                var last = outLines[outLines.Count - 1];
                if (last.EndTime == newStartMsAbs && last.TextUtf8.Span.SequenceEqual(textUtf8))
                {
                    outLines[outLines.Count - 1] = last with { EndTime = newEndMsAbs };
                    continue;
                }
            }

            outLines.Add(new AmoOutputLine(newStartMsAbs, newEndMsAbs, textUtf8));
        }

        return outLines.ToArray();
    }

}
