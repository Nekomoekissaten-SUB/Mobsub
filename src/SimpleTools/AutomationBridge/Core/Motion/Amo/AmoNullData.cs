namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal sealed class AmoNullData : AmoData
{
    public override AmoDataKind Kind => AmoDataKind.None;
    public override int Length => 0;

    public override MotionTsrMath.TsrState GetTsrState(int frame, bool applyX, bool applyY, bool applyScale, bool applyRotation)
        => new MotionTsrMath.TsrState(XCur: 0, YCur: 0, RatioX: 1, RatioY: 1, RotDiffDeg: 0);
}
