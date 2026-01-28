using System;

namespace Mobsub.SubtitleParse.AssTypes;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class AssTagSpecAttribute(string name, Type valueType, AssTagKind tagType, AssTagFunctionKind functionKind = AssTagFunctionKind.None) : Attribute
{
    public string Name { get; } = name;
    public Type ValueType { get; } = valueType;
    public AssTagKind TagType { get; } = tagType;
    public AssTagFunctionKind FunctionKind { get; } = functionKind;
}

