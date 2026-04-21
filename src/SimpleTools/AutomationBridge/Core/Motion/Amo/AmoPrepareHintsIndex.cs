using Mobsub.AutomationBridge.Core.Models;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal sealed class AmoPrepareHintsIndex
{
    private const double Eps = 1e-9;

    private readonly int[] _posPrefix;
    private readonly int[] _scalePrefix;
    private readonly int[] _rotPrefix;

    private readonly bool _hasPosCheck;
    private readonly bool _hasScaleCheck;
    private readonly bool _hasRotCheck;
    private readonly bool _forceEnsurePos;

    private readonly int _selectionStartFrame;
    private readonly int _totalFrames;

    private AmoPrepareHintsIndex(
        int[] posPrefix,
        int[] scalePrefix,
        int[] rotPrefix,
        bool hasPosCheck,
        bool hasScaleCheck,
        bool hasRotCheck,
        bool forceEnsurePos,
        int selectionStartFrame,
        int totalFrames)
    {
        _posPrefix = posPrefix;
        _scalePrefix = scalePrefix;
        _rotPrefix = rotPrefix;
        _hasPosCheck = hasPosCheck;
        _hasScaleCheck = hasScaleCheck;
        _hasRotCheck = hasRotCheck;
        _forceEnsurePos = forceEnsurePos;
        _selectionStartFrame = selectionStartFrame;
        _totalFrames = totalFrames;
    }

    public static AmoPrepareHintsIndex Create(AmoApplyContext ctx)
    {
        // Only TSR main data can drive \pos/\org based motion math.
        bool isTsr = ctx.MainData is AmoTsrData tsr && tsr.Length > 0;
        int n = ctx.TotalFrames;

        // Prefix arrays are 1-based with a 0 at index 0.
        var posPrefix = new int[n + 1];
        var scalePrefix = new int[n + 1];
        var rotPrefix = new int[n + 1];

        bool doNormal = ctx.MainData.Kind != AmoDataKind.Srs && ctx.MainData.Kind != AmoDataKind.None && !ctx.Options.Main.ClipOnly;

        bool wantPosMath = doNormal && ctx.MainData.Kind == AmoDataKind.Tsr &&
            (ctx.Options.Main.XPosition || ctx.Options.Main.YPosition || ctx.Options.Main.XScale || ctx.Options.Main.ZRotation);

        bool wantScale = doNormal && ctx.Options.Main.XScale;
        bool wantRot = doNormal && ctx.Options.Main.ZRotation;

        bool absPosForcesPos = isTsr && wantPosMath && ctx.Options.Main.AbsPos && (ctx.Options.Main.XPosition || ctx.Options.Main.YPosition);
        bool hasPosCheck = isTsr && wantPosMath && !absPosForcesPos;
        bool hasScaleCheck = isTsr && wantScale;
        bool hasRotCheck = isTsr && wantRot;

        if (!isTsr)
        {
            return new AmoPrepareHintsIndex(posPrefix, scalePrefix, rotPrefix, hasPosCheck: false, hasScaleCheck: false, hasRotCheck: false, forceEnsurePos: false, ctx.SelectionStartFrame, ctx.TotalFrames);
        }

        var data = (AmoTsrData)ctx.MainData;
        for (int f = 1; f <= n; f++)
        {
            if (hasPosCheck)
            {
                var st = ctx.MainData.GetTsrState(f, ctx.Options.Main.XPosition, ctx.Options.Main.YPosition, ctx.Options.Main.XScale, ctx.Options.Main.ZRotation);
                posPrefix[f] = posPrefix[f - 1] + (IsNeutralForPosMath(st, data) ? 0 : 1);
            }
            else
            {
                posPrefix[f] = posPrefix[f - 1];
            }

            if (hasScaleCheck)
            {
                var st = ctx.MainData.GetTsrState(f, applyX: false, applyY: false, applyScale: true, applyRotation: false);
                scalePrefix[f] = scalePrefix[f - 1] + (Math.Abs(st.RatioX - 1.0) <= Eps ? 0 : 1);
            }
            else
            {
                scalePrefix[f] = scalePrefix[f - 1];
            }

            if (hasRotCheck)
            {
                var st = ctx.MainData.GetTsrState(f, applyX: false, applyY: false, applyScale: false, applyRotation: true);
                rotPrefix[f] = rotPrefix[f - 1] + (Math.Abs(st.RotDiffDeg) <= Eps ? 0 : 1);
            }
            else
            {
                rotPrefix[f] = rotPrefix[f - 1];
            }
        }

        return new AmoPrepareHintsIndex(posPrefix, scalePrefix, rotPrefix, hasPosCheck, hasScaleCheck, hasRotCheck, forceEnsurePos: absPosForcesPos, ctx.SelectionStartFrame, ctx.TotalFrames);
    }

    public AmoPrepareHints ForLine(AutomationLine line)
    {
        var hints = AmoPrepareHints.Default;

        // If line has no timing info, it won't be changed anyway.
        if (line.StartFrame is null || line.EndFrame is null)
        {
            return hints with
            {
                EnsurePos = false,
                EnsureMissingScaleTags = false,
                EnsureMissingBorderTag = false,
                EnsureMissingShadowTag = false,
                EnsureMissingRotationTag = false,
            };
        }

        GetLineRelFrameRange(line, _selectionStartFrame, _totalFrames, out int relStart, out int relEnd);

        bool ensurePos = _forceEnsurePos
            ? true
            : !_hasPosCheck
            ? false
            : AnyInRange(_posPrefix, relStart, relEnd);

        bool ensureScale = !_hasScaleCheck
            ? false
            : AnyInRange(_scalePrefix, relStart, relEnd);

        bool ensureRot = !_hasRotCheck
            ? false
            : AnyInRange(_rotPrefix, relStart, relEnd);

        return hints with
        {
            EnsurePos = ensurePos,
            EnsureMissingScaleTags = ensureScale,
            EnsureMissingBorderTag = ensureScale,
            EnsureMissingShadowTag = ensureScale,
            EnsureMissingRotationTag = ensureRot,
        };
    }

    private static bool AnyInRange(int[] prefix, int start, int end)
    {
        if (start < 1) start = 1;
        if (end < start) end = start;
        if (end >= prefix.Length) end = prefix.Length - 1;
        int a = prefix[start - 1];
        int b = prefix[end];
        return b - a > 0;
    }

    private static void GetLineRelFrameRange(AutomationLine line, int selectionStartFrame, int totalFrames, out int relStart, out int relEnd)
    {
        int startAbs = line.StartFrame ?? selectionStartFrame;
        int endAbs = line.EndFrame ?? selectionStartFrame;

        relStart = startAbs - selectionStartFrame + 1;
        relEnd = endAbs - selectionStartFrame;

        if (relStart < 1) relStart = 1;
        if (relEnd < 1) relEnd = 1;
        if (relStart > totalFrames) relStart = totalFrames;
        if (relEnd > totalFrames) relEnd = totalFrames;
        if (relEnd < relStart) relEnd = relStart;
    }

    private static bool IsNeutralForPosMath(MotionTsrMath.TsrState st, AmoTsrData tsr)
    {
        return Math.Abs(st.XCur - tsr.XStartPos) <= Eps
            && Math.Abs(st.YCur - tsr.YStartPos) <= Eps
            && Math.Abs(st.RatioX - 1.0) <= Eps
            && Math.Abs(st.RotDiffDeg) <= Eps;
    }
}
