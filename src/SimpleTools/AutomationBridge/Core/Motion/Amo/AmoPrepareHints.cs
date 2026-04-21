namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal readonly record struct AmoPrepareHints(
    bool EnsurePos,
    bool EnsureMissingScaleTags,
    bool EnsureMissingBorderTag,
    bool EnsureMissingShadowTag,
    bool EnsureMissingRotationTag)
{
    public static AmoPrepareHints Default { get; } = new(
        EnsurePos: true,
        EnsureMissingScaleTags: true,
        EnsureMissingBorderTag: true,
        EnsureMissingShadowTag: true,
        EnsureMissingRotationTag: true);
}

