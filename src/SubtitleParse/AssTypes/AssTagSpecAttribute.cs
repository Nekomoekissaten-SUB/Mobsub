using System;

namespace Mobsub.SubtitleParse.AssTypes;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class AssTagSpecAttribute(string name, AssTagValueKind valueKind, AssTagKind tagType, AssTagFunctionKind functionKind = AssTagFunctionKind.None) : Attribute
{
    public string Name { get; } = name;
    public AssTagValueKind ValueKind { get; } = valueKind;
    public AssTagKind TagType { get; } = tagType;
    public AssTagFunctionKind FunctionKind { get; } = functionKind;

    // Optional validation metadata (consumed by the source generator).

    public string? ObsoleteReplacementName { get; set; }

    public ulong IntAllowedMask { get; set; }
    public string? IntAllowedMaskDiagnosticCode { get; set; }
    public string? IntAllowedMaskMessage { get; set; }

    public int IntMin { get; set; } = int.MinValue;
    public int IntMax { get; set; } = int.MaxValue;
    public string? IntRangeDiagnosticCode { get; set; }
    public string? IntRangeMessage { get; set; }

    public double DoubleMin { get; set; } = double.NaN;
    public double DoubleMax { get; set; } = double.NaN;
    public string? DoubleRangeDiagnosticCode { get; set; }
    public string? DoubleRangeMessage { get; set; }
}
