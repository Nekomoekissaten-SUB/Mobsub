using System.Reflection;
using System.Text;

namespace AutomationBridgeProtocolGen;

internal static partial class LuaEmitterV2
{
    private static string CallConstName(string method)
        => "CALL_" + method.Replace('.', '_').Replace('-', '_').ToUpperInvariant();

    private static void EmitMakeCall(StringBuilder sb, BridgeCallsSpec spec, Assembly asm)
    {
        sb.AppendLine("local function _make_call(method, context, lines, args)");

        foreach (var call in spec.Calls)
        {
            sb.Append("  if method == ").Append(LuaStringLiteral(call.Method)).AppendLine(" then");

            Type callType = FindType(asm, ProtocolNamespace + "." + call.CallType);
            var callFields = GetMessagePackFields(callType);

            string constName = CallConstName(call.Method);
            if (callFields.Count == 0)
            {
                sb.Append("    return { ").Append(constName).AppendLine(", {} }");
                sb.AppendLine("  end");
                continue;
            }

            var payloadExprs = new List<string>(capacity: callFields.Count);
            foreach (var f in callFields.OrderBy(static x => x.Key))
                payloadExprs.Add(BuildCallPayloadExpr(f.Property));

            sb.Append("    return { ").Append(constName).Append(", { ");
            sb.Append(string.Join(", ", payloadExprs));
            sb.AppendLine(" } }");
            sb.AppendLine("  end");
        }

        sb.AppendLine("  error(\"mobsub: unsupported method: \" .. tostring(method))");
        sb.AppendLine("end");
        sb.AppendLine();
    }

    private static string BuildCallPayloadExpr(PropertyInfo prop)
    {
        if (prop.Name == "Context")
            return "_pack_BridgeContext(context)";
        if (prop.Name == "Lines")
            return "_pack_array(lines, _pack_BridgeLine)";
        if (prop.Name == "Args")
            return LuaPackerName(StripNullable(prop.PropertyType).Name) + "(args)";

        string fullName = StripNullable(prop.PropertyType).FullName ?? prop.PropertyType.Name;
        if (fullName == ProtocolNamespace + ".BridgeContext")
            return "_pack_BridgeContext(context)";
        if (IsBridgeLineArray(prop.PropertyType))
            return "_pack_array(lines, _pack_BridgeLine)";

        return LuaPackerName(StripNullable(prop.PropertyType).Name) + "(args)";
    }

    private static bool IsBridgeLineArray(Type t)
    {
        if (!t.IsArray)
            return false;

        Type elem = StripNullable(t.GetElementType() ?? throw new InvalidOperationException("Array elem type is null."));
        return (elem.FullName ?? elem.Name) == ProtocolNamespace + ".BridgeLine";
    }
}

