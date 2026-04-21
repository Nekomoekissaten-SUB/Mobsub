namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal abstract class AmoData
{
    public abstract AmoDataKind Kind { get; }
    public abstract int Length { get; }

    public virtual void SetReferenceFrame(int frame) { }

    public virtual MotionTsrMath.TsrState GetTsrState(
        int frame,
        bool applyX,
        bool applyY,
        bool applyScale,
        bool applyRotation)
        => default;

    public virtual ReadOnlySpan<byte> GetSrsDrawingUtf8(int frame) => default;
}
