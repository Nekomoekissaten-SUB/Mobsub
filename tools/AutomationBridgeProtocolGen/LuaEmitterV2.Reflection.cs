using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace AutomationBridgeProtocolGen;

internal static partial class LuaEmitterV2
{
    private const string ProtocolNamespace = "Mobsub.AutomationBridge.Protocol";
    private const string CommonNamespace = "Mobsub.AutomationBridge.Common";

    private const string MessagePackObjectAttributeName = "MessagePack.MessagePackObjectAttribute";
    private const string MessagePackKeyAttributeName = "MessagePack.KeyAttribute";

    private const string LuaPackModeAttributeName = CommonNamespace + ".LuaPackModeAttribute";
    private const string LuaKeyAttributeName = CommonNamespace + ".LuaKeyAttribute";
    private const string LuaAltKeysAttributeName = CommonNamespace + ".LuaAltKeysAttribute";
    private const string LuaDefaultAttributeName = CommonNamespace + ".LuaDefaultAttribute";
    private const string LuaEmptyStringAsNilAttributeName = CommonNamespace + ".LuaEmptyStringAsNilAttribute";
    private const string LuaMinAttributeName = CommonNamespace + ".LuaMinAttribute";

    private static readonly JsonNamingPolicy LuaNaming = JsonNamingPolicy.SnakeCaseLower;

    private static bool HasAttribute(MemberInfo member, string fullName)
        => member.CustomAttributes.Any(a => a.AttributeType.FullName == fullName);

    private static Type FindType(Assembly asm, string fullName)
        => asm.GetType(fullName, throwOnError: true)
           ?? throw new InvalidOperationException("Type not found: " + fullName);

    private static bool IsMessagePackRecord(Type t)
        => HasAttribute(t, MessagePackObjectAttributeName);

    private static List<MessagePackField> GetMessagePackFields(Type t)
    {
        var list = new List<MessagePackField>();
        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            int? key = GetMessagePackKey(p);
            if (key is not null)
                list.Add(new MessagePackField(p, key.Value));
        }
        return list;
    }

    private static int? GetMessagePackKey(PropertyInfo prop)
    {
        foreach (var a in prop.CustomAttributes)
        {
            if (a.AttributeType.FullName != MessagePackKeyAttributeName)
                continue;

            if (a.ConstructorArguments.Count == 1 && a.ConstructorArguments[0].Value is int i)
                return i;
        }

        return null;
    }

    private static bool IsDictionaryType(Type t, out Type valueType)
    {
        valueType = typeof(object);
        if (!t.IsGenericType)
            return false;
        if (t.GetGenericTypeDefinition() != typeof(Dictionary<,>))
            return false;

        Type[] args = t.GetGenericArguments();
        valueType = StripNullable(args[1]);
        return true;
    }

    private static bool IsBytesType(Type t)
        => t == typeof(ReadOnlyMemory<byte>);

    private static Type StripNullable(Type t)
        => Nullable.GetUnderlyingType(t) ?? t;

    private static bool IsNullable(PropertyInfo prop)
    {
        Type t = prop.PropertyType;
        if (Nullable.GetUnderlyingType(t) is not null)
            return true;
        if (t.IsValueType)
            return false;

        var nic = new NullabilityInfoContext();
        var info = nic.Create(prop);
        return info.ReadState == NullabilityState.Nullable;
    }

    private static LuaPackMode GetLuaPackMode(Type t)
    {
        var attr = t.CustomAttributes.FirstOrDefault(static a => a.AttributeType.FullName == LuaPackModeAttributeName);
        if (attr is null || attr.ConstructorArguments.Count != 1)
            throw new InvalidOperationException($"Missing [LuaPackMode] on record type: {t.FullName}");

        object? v = attr.ConstructorArguments[0].Value;
        if (v is null)
            throw new InvalidOperationException($"Invalid [LuaPackMode] on record type: {t.FullName}");

        if (v.GetType().IsEnum)
            return (LuaPackMode)Convert.ToInt32(v, CultureInfo.InvariantCulture);
        if (v is int i)
            return (LuaPackMode)i;

        throw new InvalidOperationException($"Invalid [LuaPackMode] value on record type: {t.FullName}");
    }

    private static string GetLuaKey(PropertyInfo prop)
    {
        var attr = prop.CustomAttributes.FirstOrDefault(static a => a.AttributeType.FullName == LuaKeyAttributeName);
        if (attr is not null && attr.ConstructorArguments.Count == 1 && attr.ConstructorArguments[0].Value is string s && !string.IsNullOrWhiteSpace(s))
            return s.Trim();

        return LuaNaming.ConvertName(prop.Name);
    }

    private static string[] GetLuaAltKeys(PropertyInfo prop)
    {
        var attr = prop.CustomAttributes.FirstOrDefault(static a => a.AttributeType.FullName == LuaAltKeysAttributeName);
        if (attr is null || attr.ConstructorArguments.Count != 1)
            return Array.Empty<string>();

        var arg = attr.ConstructorArguments[0];
        if (arg.Value is not IReadOnlyCollection<CustomAttributeTypedArgument> items)
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var item in items)
        {
            if (item.Value is string s && !string.IsNullOrWhiteSpace(s))
                list.Add(s.Trim());
        }
        return list.ToArray();
    }

    private static double? GetLuaMin(PropertyInfo prop)
    {
        var attr = prop.CustomAttributes.FirstOrDefault(static a => a.AttributeType.FullName == LuaMinAttributeName);
        if (attr is null || attr.ConstructorArguments.Count != 1)
            return null;

        object? v = attr.ConstructorArguments[0].Value;
        return v switch
        {
            int i => i,
            double d => d,
            _ => null,
        };
    }

    private static string? TryGetLuaDefaultLiteral(PropertyInfo prop)
    {
        var attr = prop.CustomAttributes.FirstOrDefault(static a => a.AttributeType.FullName == LuaDefaultAttributeName);
        if (attr is null || attr.ConstructorArguments.Count != 1)
            return null;

        object? v = attr.ConstructorArguments[0].Value;
        if (v is null)
            return "nil";

        if (v is bool b)
            return b ? "true" : "false";
        if (v is int i)
            return i.ToString(CultureInfo.InvariantCulture);
        if (v is double d)
            return LuaNumberLiteral(d);
        if (v is string s)
            return LuaStringLiteral(s);
        if (v.GetType().IsEnum)
            return Convert.ToInt32(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

        return null;
    }

    private static string? TryGetCtorDefaultLiteral(PropertyInfo prop)
    {
        var ctor = FindPrimaryConstructor(prop.DeclaringType ?? throw new InvalidOperationException("DeclaringType is null."));
        if (ctor is null)
            return null;

        var param = ctor.GetParameters().FirstOrDefault(p => string.Equals(p.Name, prop.Name, StringComparison.OrdinalIgnoreCase));
        if (param is null || !param.HasDefaultValue)
            return null;

        object? v = param.DefaultValue;
        if (v is null)
            return "nil";

        if (v is bool b)
            return b ? "true" : "false";
        if (v is int i)
            return i.ToString(CultureInfo.InvariantCulture);
        if (v is double d)
            return LuaNumberLiteral(d);
        if (v is string s)
            return LuaStringLiteral(s);
        if (v.GetType().IsEnum)
            return Convert.ToInt32(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

        return null;
    }

    private static bool? TryGetBoolDefault(PropertyInfo prop)
    {
        var attr = prop.CustomAttributes.FirstOrDefault(static a => a.AttributeType.FullName == LuaDefaultAttributeName);
        if (attr is not null && attr.ConstructorArguments.Count == 1 && attr.ConstructorArguments[0].Value is bool b0)
            return b0;

        var ctor = FindPrimaryConstructor(prop.DeclaringType ?? throw new InvalidOperationException("DeclaringType is null."));
        if (ctor is null)
            return null;

        var param = ctor.GetParameters().FirstOrDefault(p => string.Equals(p.Name, prop.Name, StringComparison.OrdinalIgnoreCase));
        if (param is null || !param.HasDefaultValue)
            return null;

        return param.DefaultValue as bool?;
    }

    private static ConstructorInfo? FindPrimaryConstructor(Type t)
    {
        var keyProps = GetMessagePackFields(t).Select(static f => f.Property.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (keyProps.Count == 0)
            return null;

        ConstructorInfo? best = null;
        int bestScore = -1;

        foreach (var ctor in t.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var ps = ctor.GetParameters();
            if (ps.Length == 0)
                continue;

            int score = 0;
            foreach (var p in ps)
            {
                if (p.Name is not null && keyProps.Contains(p.Name))
                    score++;
            }

            if (score == ps.Length && score > bestScore)
            {
                best = ctor;
                bestScore = score;
            }
        }

        return best;
    }

    private sealed record MessagePackField(PropertyInfo Property, int Key);

    private enum LuaPackMode
    {
        Default = 0,
        Nilable = 1,
        Strict = 2,
    }
}

