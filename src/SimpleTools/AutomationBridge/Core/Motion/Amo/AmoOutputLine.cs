namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal readonly record struct AmoOutputLine(
    int StartTime,
    int EndTime,
    ReadOnlyMemory<byte> TextUtf8
);

