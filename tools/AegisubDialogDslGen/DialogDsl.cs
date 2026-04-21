using System.Collections.Frozen;

namespace Mobsub.Tools.AegisubDialogDslGen;

public sealed record DialogDef(int? Columns, IReadOnlyList<DialogRow> Rows);

public abstract record DialogRow;

public sealed record SequentialRow(IReadOnlyList<DialogCell> Cells) : DialogRow;

public sealed record LeftRightRow(IReadOnlyList<DialogCell> Left, IReadOnlyList<DialogCell> Right, int Gap = 0) : DialogRow;

public abstract record DialogCell;

public sealed record SpacerCell(int Spacer) : DialogCell;

public sealed record ControlCell(string Class, int W, int H, FrozenDictionary<string, object?> Props) : DialogCell;

public readonly record struct LuaExpr(string Code);

public static class DialogDsl
{
    public static DialogDef Dialog(params DialogRow[] rows) => new(Columns: null, Rows: rows);

    public static DialogDef Dialog(int columns, params DialogRow[] rows) => new(columns, rows);

    public static DialogDef Dialog(int? columns = null, params DialogRow[] rows) => new(columns, rows);

    public static SequentialRow Row(params DialogCell[] cells) => new(cells);

    public static SequentialRow Row(IReadOnlyList<DialogCell> cells) => new(cells);

    public static LeftRightRow LeftRight(IReadOnlyList<DialogCell> left, IReadOnlyList<DialogCell> right, int gap = 0) =>
        new(left, right, gap);

    public static SpacerCell Spacer(int w) => new(w);

    public static ControlCell Control(string @class, int w = 1, int h = 1, params (string Key, object? Value)[] props)
    {
        if (string.IsNullOrWhiteSpace(@class))
            throw new ArgumentException("class is required.", nameof(@class));
        if (w < 1) throw new ArgumentOutOfRangeException(nameof(w), "w must be >= 1.");
        if (h < 1) throw new ArgumentOutOfRangeException(nameof(h), "h must be >= 1.");

        if (props.Length == 0)
            return new ControlCell(@class, w, h, FrozenDictionary<string, object?>.Empty);

        var dict = new Dictionary<string, object?>(capacity: props.Length);
        foreach (var (key, value) in props)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Property name cannot be empty.", nameof(props));
            dict[key] = value;
        }

        return new ControlCell(@class, w, h, dict.ToFrozenDictionary(StringComparer.Ordinal));
    }

    public static LuaExpr Lua(string code) => new(code);

    public static ControlCell Label(string text, int w = 1, int h = 1, string? hint = null)
    {
        return hint is null
            ? Control("label", w, h, ("label", text))
            : Control("label", w, h, ("label", text), ("hint", hint));
    }

    public static ControlCell Label(LuaExpr text, int w = 1, int h = 1, string? hint = null)
    {
        return hint is null
            ? Control("label", w, h, ("label", text))
            : Control("label", w, h, ("label", text), ("hint", hint));
    }

    public static ControlCell CheckBox(string name, string label, bool? value = null, int w = 1, int h = 1, string? hint = null)
    {
        var props = new List<(string, object?)>(capacity: 3)
        {
            ("name", name),
            ("label", label),
        };
        if (value is not null) props.Add(("value", value.Value));
        if (hint is not null) props.Add(("hint", hint));
        return Control("checkbox", w, h, props.ToArray());
    }

    public static ControlCell CheckBox(string name, string label, LuaExpr value, int w = 1, int h = 1, string? hint = null)
    {
        var props = new List<(string, object?)>(capacity: 4)
        {
            ("name", name),
            ("label", label),
            ("value", value),
        };
        if (hint is not null) props.Add(("hint", hint));
        return Control("checkbox", w, h, props.ToArray());
    }

    public static ControlCell Button(string name, string label, int w = 1, int h = 1, string? hint = null)
    {
        return hint is null
            ? Control("button", w, h, ("name", name), ("label", label))
            : Control("button", w, h, ("name", name), ("label", label), ("hint", hint));
    }

    public static ControlCell Edit(string name, object? value = null, int w = 1, int h = 1, string? hint = null)
    {
        var props = new List<(string, object?)>(capacity: 3) { ("name", name) };
        if (value is not null) props.Add(("value", value));
        if (hint is not null) props.Add(("hint", hint));
        return Control("edit", w, h, props.ToArray());
    }

    public static ControlCell IntEdit(string name, int? value = null, int w = 1, int h = 1, string? hint = null)
    {
        var props = new List<(string, object?)>(capacity: 3) { ("name", name) };
        if (value is not null) props.Add(("value", value.Value));
        if (hint is not null) props.Add(("hint", hint));
        return Control("intedit", w, h, props.ToArray());
    }

    public static ControlCell IntEdit(string name, LuaExpr value, int w = 1, int h = 1, string? hint = null)
    {
        var props = new List<(string, object?)>(capacity: 3) { ("name", name), ("value", value) };
        if (hint is not null) props.Add(("hint", hint));
        return Control("intedit", w, h, props.ToArray());
    }

    public static ControlCell FloatEdit(string name, double? value = null, int w = 1, int h = 1, string? hint = null)
    {
        var props = new List<(string, object?)>(capacity: 3) { ("name", name) };
        if (value is not null) props.Add(("value", value.Value));
        if (hint is not null) props.Add(("hint", hint));
        return Control("floatedit", w, h, props.ToArray());
    }

    public static ControlCell FloatEdit(string name, LuaExpr value, int w = 1, int h = 1, string? hint = null)
    {
        var props = new List<(string, object?)>(capacity: 3) { ("name", name), ("value", value) };
        if (hint is not null) props.Add(("hint", hint));
        return Control("floatedit", w, h, props.ToArray());
    }

    public static ControlCell ColorAlpha(string name, object? value = null, int w = 1, int h = 1, string? hint = null)
    {
        var props = new List<(string, object?)>(capacity: 3) { ("name", name) };
        if (value is not null) props.Add(("value", value));
        if (hint is not null) props.Add(("hint", hint));
        return Control("coloralpha", w, h, props.ToArray());
    }

    public static ControlCell DropDown(string name, IReadOnlyList<string> items, object? value = null, int w = 1, int h = 1, string? hint = null)
    {
        var props = new List<(string, object?)>(capacity: 4)
        {
            ("name", name),
            ("items", items),
        };
        if (value is not null) props.Add(("value", value));
        if (hint is not null) props.Add(("hint", hint));
        return Control("dropdown", w, h, props.ToArray());
    }

    public static ControlCell DropDown(string name, LuaExpr items, object? value = null, int w = 1, int h = 1, string? hint = null)
    {
        var props = new List<(string, object?)>(capacity: 4)
        {
            ("name", name),
            ("items", items),
        };
        if (value is not null) props.Add(("value", value));
        if (hint is not null) props.Add(("hint", hint));
        return Control("dropdown", w, h, props.ToArray());
    }

    public static ControlCell DropDownList(string name, IReadOnlyList<string> items, object? value = null, int w = 1, int h = 1, string? hint = null) =>
        DropDown(name, items, value, w, h, hint);
}
