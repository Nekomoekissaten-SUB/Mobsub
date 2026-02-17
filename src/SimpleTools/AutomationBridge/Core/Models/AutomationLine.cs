namespace Mobsub.AutomationBridge.Core.Models;

internal sealed record AutomationLine(
    int Index,
    string Class,
    ReadOnlyMemory<byte>? TextUtf8,
    string? Raw,
    int? StartTime,
    int? EndTime,
    int? StartFrame,
    int? EndFrame,
    int? Layer,
    bool? Comment,
    string? Style,
    string? Actor,
    string? Effect,
    int? MarginL,
    int? MarginR,
    int? MarginT,
    Dictionary<string, string>? Extra,
    double? Width,
    double? Height,
    int? Align
);

