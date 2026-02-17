namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal readonly record struct AmoLinePatch(
    int Index,
    bool CanUseSetText,
    AmoOutputLine[] OutputLines
);

