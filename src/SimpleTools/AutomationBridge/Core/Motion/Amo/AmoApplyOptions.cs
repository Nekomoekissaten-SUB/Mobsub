namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal enum AmoLinearMode : int
{
    ForceNonlinear = 0,
    ForceLinear = 1,
    AutoLinearPos = 2,
    AutoSegmentPos = 3,
}

internal enum AmoPosErrorMode : int
{
    Full = 0,
    IgnoreScaleRot = 1,
}

internal readonly record struct AmoMainOptions(
    bool XPosition,
    bool YPosition,
    bool Origin,
    bool AbsPos,
    bool XScale,
    bool Border,
    bool Shadow,
    bool Blur,
    double BlurScale,
    bool ZRotation,
    bool ClipOnly,
    bool RectClip,
    bool VectClip,
    bool RcToVc,
    bool KillTrans,
    bool Relative,
    int StartFrame,
    AmoLinearMode LinearMode,
    double SegmentPosEps,
    AmoPosErrorMode PosErrorMode
);

internal readonly record struct AmoClipOptions(
    bool XPosition,
    bool YPosition,
    bool XScale,
    bool ZRotation,
    bool RectClip,
    bool VectClip,
    bool RcToVc,
    int StartFrame
);

internal readonly record struct AmoApplyOptions(
    AmoMainOptions Main,
    AmoClipOptions Clip
);
