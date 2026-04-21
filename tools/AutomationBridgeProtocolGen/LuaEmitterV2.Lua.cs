using System.Globalization;
using System.Text;

namespace AutomationBridgeProtocolGen;

internal static partial class LuaEmitterV2
{
    private static string LuaPackerName(string typeName)
        => "_pack_" + typeName;

    private static bool IsSafeLuaIdentifier(string s)
    {
        if (string.IsNullOrEmpty(s))
            return false;
        if (!(char.IsLetter(s[0]) || s[0] == '_'))
            return false;
        for (int i = 1; i < s.Length; i++)
        {
            char c = s[i];
            if (!(char.IsLetterOrDigit(c) || c == '_'))
                return false;
        }
        return true;
    }

    private static string LuaAccessor(string tableVar, string key)
        => IsSafeLuaIdentifier(key)
            ? $"{tableVar}.{key}"
            : $"{tableVar}[{LuaStringLiteral(key)}]";

    private static string LuaStringLiteral(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c < ' ')
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static string LuaNumberLiteral(double d)
        => d.ToString("0.################", CultureInfo.InvariantCulture);

    private static void EmitLuaArrayLiteral(StringBuilder sb, IReadOnlyList<string> items)
    {
        sb.Append("{ ");
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(items[i]);
        }
        sb.Append(" }");
    }
}

