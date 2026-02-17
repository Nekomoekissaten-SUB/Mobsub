using System.Reflection;
using System.Text;

namespace AutomationBridgeProtocolGen;

internal static partial class LuaEmitterV2
{
    private static HashSet<Type> CollectPackerTypes(BridgeCallsSpec spec, Assembly asm)
    {
        var set = new HashSet<Type>();

        foreach (var call in spec.Calls)
        {
            Type callType = FindType(asm, ProtocolNamespace + "." + call.CallType);
            foreach (var f in GetMessagePackFields(callType))
            {
                foreach (var dep in EnumerateRecordDeps(f.Property.PropertyType))
                    set.Add(dep);
            }
        }

        return set;
    }

    private static IEnumerable<Type> EnumerateRecordDeps(Type t)
    {
        Type u = StripNullable(t);

        if (u.IsArray)
        {
            Type elem = StripNullable(u.GetElementType() ?? throw new InvalidOperationException("Array elem type is null."));
            foreach (var x in EnumerateRecordDeps(elem))
                yield return x;
            yield break;
        }

        if (IsDictionaryType(u, out var valueType))
        {
            foreach (var x in EnumerateRecordDeps(valueType))
                yield return x;
            yield break;
        }

        if (!IsMessagePackRecord(u))
            yield break;

        yield return u;

        foreach (var f in GetMessagePackFields(u))
        {
            foreach (var x in EnumerateRecordDeps(f.Property.PropertyType))
                yield return x;
        }
    }

    private static List<Type> TopoSort(HashSet<Type> types)
    {
        var deps = new Dictionary<Type, HashSet<Type>>();
        foreach (var t in types)
            deps[t] = new HashSet<Type>(EnumerateDirectRecordDeps(t, types));

        var ordered = new List<Type>(capacity: types.Count);
        var visiting = new HashSet<Type>();
        var visited = new HashSet<Type>();

        void Visit(Type t)
        {
            if (visited.Contains(t))
                return;
            if (!visiting.Add(t))
                throw new InvalidOperationException("Record dependency cycle detected: " + t.FullName);

            foreach (var d in deps[t])
                Visit(d);

            visiting.Remove(t);
            visited.Add(t);
            ordered.Add(t);
        }

        foreach (var t in types.OrderBy(static x => x.FullName, StringComparer.Ordinal))
            Visit(t);

        return ordered;
    }

    private static IEnumerable<Type> EnumerateDirectRecordDeps(Type recordType, HashSet<Type> universe)
    {
        foreach (var f in GetMessagePackFields(recordType))
        {
            Type ft = StripNullable(f.Property.PropertyType);

            if (ft.IsArray)
            {
                Type elem = StripNullable(ft.GetElementType() ?? throw new InvalidOperationException("Array elem type is null."));
                if (universe.Contains(elem))
                    yield return elem;
                continue;
            }

            if (IsDictionaryType(ft, out var valueType))
            {
                if (universe.Contains(valueType))
                    yield return valueType;
                continue;
            }

            if (universe.Contains(ft))
                yield return ft;
        }
    }

    private static void EmitTypePacker(StringBuilder sb, Type t)
    {
        var fields = GetMessagePackFields(t);
        var mode = GetLuaPackMode(t);

        sb.Append("local function ").Append(LuaPackerName(t.Name)).AppendLine("(t)");

        switch (mode)
        {
            case LuaPackMode.Default:
                EmitDefaultRecordPacker(sb, fields);
                break;
            case LuaPackMode.Nilable:
                EmitNilableRecordPacker(sb, fields);
                break;
            case LuaPackMode.Strict:
                EmitStrictRecordPacker(sb, t, fields);
                break;
            default:
                throw new InvalidOperationException("Unsupported LuaPackMode: " + mode);
        }

        sb.AppendLine("end");
    }

    private static void EmitDefaultRecordPacker(StringBuilder sb, List<MessagePackField> fields)
    {
        if (fields.Count == 0)
        {
            sb.AppendLine("  if type(t) ~= \"table\" then");
            sb.AppendLine("    return {}");
            sb.AppendLine("  end");
            sb.AppendLine("  return {}");
            return;
        }

        int maxKey = fields.Max(static f => f.Key);
        var fieldsByKey = fields.ToDictionary(static f => f.Key, static f => f);
        var defaults = new List<string>(capacity: maxKey + 1);
        for (int k = 0; k <= maxKey; k++)
        {
            if (!fieldsByKey.TryGetValue(k, out var f))
            {
                defaults.Add("nil");
                continue;
            }

            defaults.Add(BuildDefaultFieldExpr(f.Property));
        }

        sb.AppendLine("  if type(t) ~= \"table\" then");
        sb.Append("    return ");
        EmitLuaArrayLiteral(sb, defaults);
        sb.AppendLine();
        sb.AppendLine("  end");
        sb.AppendLine("  local out = {}");

        foreach (var f in fields.OrderBy(static x => x.Key))
        {
            string expr = BuildPackedFieldExpr(f.Property, tableVar: "t");
            sb.Append("  out[").Append(f.Key + 1).Append("] = ").Append(expr).AppendLine();
        }

        sb.AppendLine("  return out");
    }

    private static void EmitNilableRecordPacker(StringBuilder sb, List<MessagePackField> fields)
    {
        sb.AppendLine("  if type(t) ~= \"table\" then");
        sb.AppendLine("    return nil");
        sb.AppendLine("  end");

        if (fields.Count == 0)
        {
            sb.AppendLine("  return {}");
            return;
        }

        sb.AppendLine("  local out = {}");

        foreach (var f in fields.OrderBy(static x => x.Key))
        {
            string expr = BuildPackedFieldExpr(f.Property, tableVar: "t");
            sb.Append("  out[").Append(f.Key + 1).Append("] = ").Append(expr).AppendLine();
        }

        sb.AppendLine("  return out");
    }

    private static void EmitStrictRecordPacker(StringBuilder sb, Type t, List<MessagePackField> fields)
    {
        if (fields.Count == 0)
        {
            sb.AppendLine("  if type(t) ~= \"table\" then");
            sb.AppendLine("    return nil");
            sb.AppendLine("  end");
            sb.AppendLine("  return {}");
            return;
        }

        sb.AppendLine("  if type(t) ~= \"table\" then");
        sb.AppendLine("    return nil");
        sb.AppendLine("  end");

        int maxKey = fields.Max(static f => f.Key);
        var vars = new Dictionary<int, string>();

        foreach (var f in fields.OrderBy(static x => x.Key))
        {
            Type ft = StripNullable(f.Property.PropertyType);
            if (ft != typeof(int) && ft != typeof(double))
                throw new InvalidOperationException($"strict LuaPackMode supports int/double only: {t.Name}.{f.Property.Name} ({ft}).");

            string key = GetLuaKey(f.Property);
            string expr = $"tonumber({LuaAccessor("t", key)})";

            foreach (var alt in GetLuaAltKeys(f.Property))
                expr += $" or tonumber({LuaAccessor("t", alt)})";

            string varName = IsSafeLuaIdentifier(key) ? key : $"v{f.Key}";
            vars[f.Key] = varName;

            sb.Append("  local ").Append(varName).Append(" = ").Append(expr).AppendLine();
            sb.Append("  if ").Append(varName).AppendLine(" == nil then");
            sb.AppendLine("    return nil");
            sb.AppendLine("  end");

            double? min = GetLuaMin(f.Property);
            if (min is not null)
            {
                sb.Append("  if ").Append(varName).Append(" < ").Append(LuaNumberLiteral(min.Value)).AppendLine(" then");
                sb.AppendLine("    return nil");
                sb.AppendLine("  end");
            }
        }

        sb.Append("  return ");
        var items = new List<string>(capacity: maxKey + 1);
        for (int k = 0; k <= maxKey; k++)
            items.Add(vars.TryGetValue(k, out var v) ? v : "nil");

        EmitLuaArrayLiteral(sb, items);
        sb.AppendLine();
    }

    private static string BuildPackedFieldExpr(PropertyInfo prop, string tableVar)
    {
        string key = GetLuaKey(prop);
        string[] altKeys = GetLuaAltKeys(prop).Where(k => k != key).ToArray();

        string acc = LuaAccessor(tableVar, key);
        Type nonNullType = StripNullable(prop.PropertyType);
        bool isNullable = IsNullable(prop);

        if (IsDictionaryType(nonNullType, out var valueType))
        {
            Type k = nonNullType.GetGenericArguments()[0];
            if (k != typeof(string))
                throw new InvalidOperationException($"Unsupported dict key type: {nonNullType} ({prop.DeclaringType?.Name}.{prop.Name}).");

            if (valueType == typeof(string))
                return $"_pack_dict_str_map({acc})";

            if (IsMessagePackRecord(valueType))
                return $"_pack_dict_record_map({acc}, {LuaPackerName(valueType.Name)})";

            throw new InvalidOperationException($"Unsupported dict value type: {nonNullType} ({prop.DeclaringType?.Name}.{prop.Name}).");
        }

        if (nonNullType.IsArray)
        {
            Type elem = StripNullable(nonNullType.GetElementType() ?? throw new InvalidOperationException("Array elem type is null."));
            if (IsMessagePackRecord(elem))
                return $"_pack_array({acc}, {LuaPackerName(elem.Name)})";

            if (!isNullable)
                return $"{acc} or {{}}";
            return acc;
        }

        if (IsBytesType(nonNullType))
            return BuildPackedBytesExpr(prop, tableVar, key, altKeys, isNullable);

        if (IsMessagePackRecord(nonNullType))
        {
            if (isNullable)
                return $"(type({acc}) == \"table\") and {LuaPackerName(nonNullType.Name)}({acc}) or nil";
            return $"{LuaPackerName(nonNullType.Name)}({acc})";
        }

        if (nonNullType.IsEnum)
        {
            if (isNullable)
                return acc;
            return BuildPackedNumberExpr(prop, tableVar, altKeys, acc);
        }

        if (isNullable)
            return acc;

        if (nonNullType == typeof(int) || nonNullType == typeof(double))
            return BuildPackedNumberExpr(prop, tableVar, altKeys, acc);

        if (nonNullType == typeof(bool))
            return BuildPackedBoolExpr(prop, acc);

        if (nonNullType == typeof(string))
            return BuildPackedStringExpr(prop, tableVar, altKeys, acc);

        return acc;
    }

    private static string BuildPackedBoolExpr(PropertyInfo prop, string acc)
    {
        bool? d = TryGetBoolDefault(prop);
        return d == true ? $"{acc} ~= false" : $"{acc} and true or false";
    }

    private static string BuildPackedNumberExpr(PropertyInfo prop, string tableVar, string[] altKeys, string acc)
    {
        string defaultExpr = TryGetLuaDefaultLiteral(prop) ?? (TryGetCtorDefaultLiteral(prop) ?? "0");
        var parts = new List<string> { $"tonumber({acc})" };

        foreach (var alt in altKeys)
            parts.Add($"tonumber({LuaAccessor(tableVar, alt)})");

        return string.Join(" or ", parts) + $" or {defaultExpr}";
    }

    private static string BuildPackedStringExpr(PropertyInfo prop, string tableVar, string[] altKeys, string acc)
    {
        string defaultExpr = TryGetLuaDefaultLiteral(prop) ?? (TryGetCtorDefaultLiteral(prop) ?? "\"\"");
        if (altKeys.Length > 0)
        {
            string chain = acc;
            foreach (var alt in altKeys)
                chain += $" or {LuaAccessor(tableVar, alt)}";
            return $"tostring({chain} or {defaultExpr})";
        }

        return $"tostring({acc} or {defaultExpr})";
    }

    private static string BuildPackedBytesExpr(PropertyInfo prop, string tableVar, string key, string[] altKeys, bool isNullable)
    {
        string defaultExpr = TryGetLuaDefaultLiteral(prop) ?? (isNullable ? "nil" : "\"\"");

        string acc = LuaAccessor(tableVar, key);
        var parts = new List<string> { $"((type({acc}) == \"string\") and {acc})" };
        foreach (var alt in altKeys)
        {
            string altAcc = LuaAccessor(tableVar, alt);
            parts.Add($"((type({altAcc}) == \"string\") and {altAcc})");
        }

        string expr = $"({string.Join(" or ", parts)} or {defaultExpr})";
        if (HasAttribute(prop, LuaEmptyStringAsNilAttributeName))
            expr = $"_nil_if_empty({expr})";

        return expr;
    }

    private static string BuildDefaultFieldExpr(PropertyInfo prop)
    {
        string? lit = TryGetLuaDefaultLiteral(prop) ?? TryGetCtorDefaultLiteral(prop);
        if (lit is not null)
            return lit;

        if (IsNullable(prop))
            return "nil";

        Type t = StripNullable(prop.PropertyType);

        if (t == typeof(int) || t == typeof(double))
            return "0";
        if (t == typeof(bool))
            return "false";
        if (t == typeof(string))
            return "\"\"";
        if (IsBytesType(t))
            return "\"\"";
        if (t.IsEnum)
            return "0";

        if (t.IsArray)
        {
            Type elem = StripNullable(t.GetElementType() ?? throw new InvalidOperationException("Array elem type is null."));
            if (elem == typeof(int) || elem == typeof(double) || elem == typeof(bool) || elem == typeof(string))
                return "{}";
            return "nil";
        }

        if (IsDictionaryType(t, out _))
            return "nil";

        if (IsMessagePackRecord(t))
            return $"{LuaPackerName(t.Name)}(nil)";

        return "nil";
    }
}

