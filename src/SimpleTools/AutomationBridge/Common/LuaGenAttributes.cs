using System.Diagnostics;

namespace Mobsub.AutomationBridge.Common;

internal enum LuaPackMode
{
    Default = 0,
    Nilable = 1,
    Strict = 2,
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
internal sealed class LuaPackModeAttribute : Attribute
{
    public LuaPackMode Mode { get; }

    public LuaPackModeAttribute(LuaPackMode mode)
        => Mode = mode;
}

[AttributeUsage(AttributeTargets.Property, Inherited = false)]
internal sealed class LuaKeyAttribute : Attribute
{
    public string Key { get; }

    public LuaKeyAttribute(string key)
        => Key = key;
}

[AttributeUsage(AttributeTargets.Property, Inherited = false)]
internal sealed class LuaAltKeysAttribute : Attribute
{
    public string[] Keys { get; }

    public LuaAltKeysAttribute(params string[] keys)
        => Keys = keys;
}

[AttributeUsage(AttributeTargets.Property, Inherited = false)]
internal sealed class LuaDefaultAttribute : Attribute
{
    public object Value { get; }

    public LuaDefaultAttribute(bool value) => Value = value;
    public LuaDefaultAttribute(int value) => Value = value;
    public LuaDefaultAttribute(double value) => Value = value;
    public LuaDefaultAttribute(string value) => Value = value;
}

[AttributeUsage(AttributeTargets.Property, Inherited = false)]
internal sealed class LuaEmptyStringAsNilAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property, Inherited = false)]
internal sealed class LuaMinAttribute : Attribute
{
    public double Min { get; }

    public LuaMinAttribute(double min)
    {
        if (double.IsNaN(min) || double.IsInfinity(min))
            throw new UnreachableException("LuaMin must be a finite number.");
        Min = min;
    }
}
