using System.Globalization;

namespace Mobsub.Tools.AegisubDialogDslGen;

public static class HydraDialogDsl
{
    public static HydraDialog Hydra(string uiVar = "ui", string gradientVar = "g") => new(uiVar, gradientVar);
}

public sealed class HydraDialog
{
    private readonly string _ui;
    private readonly string _g;

    public HydraDialog(string uiVar, string gradientVar)
    {
        if (string.IsNullOrWhiteSpace(uiVar))
            throw new ArgumentException("uiVar cannot be empty.", nameof(uiVar));
        if (string.IsNullOrWhiteSpace(gradientVar))
            throw new ArgumentException("gradientVar cannot be empty.", nameof(gradientVar));
        _ui = uiVar;
        _g = gradientVar;
    }

    public ControlCell Check(string name, string label, bool defaultValue = false, int w = 1, int h = 1, string? hint = null) =>
        DialogDsl.CheckBox(name, label, UiBool(name, defaultValue), w, h, hint);

    public ControlCell Edit(string name, string defaultValue = "", int w = 1, int h = 1, string? hint = null) =>
        DialogDsl.Edit(name, UiStr(name, defaultValue), w, h, hint);

    public ControlCell Float(string name, double defaultValue = 0, int w = 1, int h = 1, string? hint = null) =>
        DialogDsl.FloatEdit(name, UiNum(name, defaultValue), w, h, hint);

    public ControlCell Int(string name, int defaultValue = 0, int w = 1, int h = 1, string? hint = null) =>
        DialogDsl.IntEdit(name, UiInt(name, defaultValue), w, h, hint);

    public ControlCell Color(string name, int w = 1, int h = 1, string? hint = null) =>
        DialogDsl.ColorAlpha(name, UiColorValue(name), w, h, hint);

    public ControlCell DropDownStr(string name, string itemsLua, string defaultValue, int w = 1, int h = 1, string? hint = null) =>
        DropDownStr(name, new LuaExpr(itemsLua), defaultValue, w, h, hint);

    public ControlCell DropDownStr(string name, LuaExpr items, string defaultValue, int w = 1, int h = 1, string? hint = null) =>
        DialogDsl.DropDown(name, items, value: UiStr(name, defaultValue), w, h, hint);

    public ControlCell DropDownIntToString(string name, IReadOnlyList<string> items, int defaultValue, int w = 1, int h = 1, string? hint = null) =>
        DialogDsl.DropDown(name, items, value: UiIntToString(name, defaultValue), w, h, hint);

    public ControlCell Alpha(string name, string defaultHex = "00", int w = 1, int h = 1, string? hint = null) =>
        DialogDsl.DropDown(name, items: new LuaExpr("ALPHA_HEX"), value: UiAlphaHex(name, defaultHex), w, h, hint);

    public ControlCell GCheck(string name, string label, string? field = null, bool defaultValue = false, int w = 1, int h = 1, string? hint = null) =>
        DialogDsl.CheckBox(name, label, GBool(field ?? DefaultGField(name), defaultValue), w, h, hint);

    public ControlCell GFloat(string name, double defaultValue, string? field = null, int w = 1, int h = 1, string? hint = null) =>
        DialogDsl.FloatEdit(name, GNum(field ?? DefaultGField(name), defaultValue), w, h, hint);

    public ControlCell GInt(string name, int defaultValue, string? field = null, int w = 1, int h = 1, string? hint = null) =>
        DialogDsl.IntEdit(name, GInt(field ?? DefaultGField(name), defaultValue), w, h, hint);

    public ControlCell GInt(string name, int defaultValue, string? field, int min, int w = 1, int h = 1, string? hint = null)
    {
        var props = new List<(string, object?)>(capacity: 4)
        {
            ("name", name),
            ("value", GInt(field ?? DefaultGField(name), defaultValue)),
            ("min", min),
        };
        if (hint is not null) props.Add(("hint", hint));
        return DialogDsl.Control("intedit", w, h, props.ToArray());
    }

    public ControlCell GKind(string name = "g_kind", int w = 2) =>
        DialogDsl.DropDown(name, items: new LuaExpr("GRAD_KINDS"), value: new LuaExpr($"GRAD_KINDS[(to_int({_g}.kind, 0) + 1)] or 'vertical'"), w: w);

    public LuaExpr UiBool(string field, bool defaultValue = false) => new($"to_bool({_ui}.{field}, {LuaBool(defaultValue)})");

    public LuaExpr UiInt(string field, int defaultValue = 0) => new($"to_int({_ui}.{field}, {defaultValue.ToString(CultureInfo.InvariantCulture)})");

    public LuaExpr UiNum(string field, double defaultValue = 0) => new($"to_num({_ui}.{field}, {LuaNumber(defaultValue)})");

    public LuaExpr UiStr(string field, string defaultValue = "") => new($"to_string({_ui}.{field}, {LuaSingleQuoted(defaultValue)})");

    public LuaExpr UiIntToString(string field, int defaultValue = 0) => new($"tostring(to_int({_ui}.{field}, {defaultValue.ToString(CultureInfo.InvariantCulture)}))");

    public LuaExpr UiColorValue(string field) => new($"normalize_coloralpha_picker_value({_ui}.{field})");

    public LuaExpr UiAlphaHex(string field, string defaultHex = "00") => new($"hex2(to_string({_ui}.{field}, {LuaSingleQuoted(defaultHex)}))");

    public LuaExpr GBool(string field, bool defaultValue = false) => new($"to_bool({_g}.{field}, {LuaBool(defaultValue)})");

    public LuaExpr GInt(string field, int defaultValue = 0) => new($"to_int({_g}.{field}, {defaultValue.ToString(CultureInfo.InvariantCulture)})");

    public LuaExpr GNum(string field, double defaultValue = 0) => new($"to_num({_g}.{field}, {LuaNumber(defaultValue)})");

    public LuaExpr GStr(string field, string defaultValue = "") => new($"to_string({_g}.{field}, {LuaSingleQuoted(defaultValue)})");

    private static string DefaultGField(string name)
    {
        if (name.StartsWith("g_", StringComparison.Ordinal) && name.Length > 2)
            return name.Substring(2);
        return name;
    }

    private static string LuaBool(bool value) => value ? "true" : "false";

    private static string LuaNumber(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v))
            throw new ArgumentOutOfRangeException(nameof(v), "Lua numeric literal cannot be NaN/Infinity.");

        // Enough precision for UI defaults; avoids culture issues and verbose "R".
        return v.ToString("0.################", CultureInfo.InvariantCulture);
    }

    private static string LuaSingleQuoted(string s)
    {
        if (s.Length == 0)
            return "''";

        var sb = new System.Text.StringBuilder(capacity: s.Length + 8);
        sb.Append('\'');
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\'': sb.Append("\\'"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(ch); break;
            }
        }
        sb.Append('\'');
        return sb.ToString();
    }
}

