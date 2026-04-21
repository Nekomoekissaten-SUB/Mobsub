using System.Globalization;
using System.Buffers;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Mobsub.Tools.AegisubDialogDslGen;

internal static class Program
{
    private enum InputFormat : byte
    {
        Auto = 0,
        Json = 1,
        Dsl = 2,
        Cs = 3,
    }

    private enum RowKind : byte
    {
        Sequential = 0,
        LeftRight = 1,
    }

    private readonly record struct RowSpec(
        RowKind Kind,
        JsonElement Element,
        int Width,
        int LeftWidth,
        int RightWidth,
        int Gap);

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            Console.Error.WriteLine("Usage: AegisubDialogDslGen <input.(json|jsonc|dsl|cs|csx)> [--out <output.lua>] [--format auto|json|dsl|cs]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("JSON/JSONC format (minimal):");
            Console.Error.WriteLine("  {");
            Console.Error.WriteLine("    \"columns\": 10,   // optional; defaults to max row width");
            Console.Error.WriteLine("    \"rows\": [");
            Console.Error.WriteLine("      [ {\"class\":\"label\",\"label\":\"Hello\",\"w\":3}, {\"spacer\":4}, {\"class\":\"checkbox\",\"name\":\"x\",\"label\":\"X\"} ]");
            Console.Error.WriteLine("      // or: {\"left\":[...], \"right\":[...], \"gap\":1} for right-aligned groups");
            Console.Error.WriteLine("    ]");
            Console.Error.WriteLine("  }");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Indent DSL format (minimal):");
            Console.Error.WriteLine("  # columns is optional; it defaults to the widest row (in grid units).");
            Console.Error.WriteLine("  columns=10");
            Console.Error.WriteLine("  row:");
            Console.Error.WriteLine("    label label=\"Hello\" w=3");
            Console.Error.WriteLine("    spacer 4");
            Console.Error.WriteLine("    checkbox name=x label=X");
            Console.Error.WriteLine("  row gap=1:");
            Console.Error.WriteLine("    left:");
            Console.Error.WriteLine("      label label=Left w=3");
            Console.Error.WriteLine("    right:");
            Console.Error.WriteLine("      label label=Right:");
            Console.Error.WriteLine("      edit name=value value=abc w=3");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Notes:");
            Console.Error.WriteLine("  - Each row is laid out left-to-right; x/y are generated.");
            Console.Error.WriteLine("  - Use \"w\"/\"h\" (or \"width\"/\"height\") for spans; defaults to 1.");
            Console.Error.WriteLine("  - Use {\"spacer\":N} to advance x by N without emitting a control.");
            Console.Error.WriteLine("  - Any string value starting with '@' is emitted as raw Lua (no quotes). Use '@@' to emit a string starting with '@'.");
            Console.Error.WriteLine("  - In indent DSL, you can write raw Lua as '@expr', or '@( ... )' / '@{ ... }' (allows whitespace).");
            Console.Error.WriteLine("  - In indent DSL, label supports shorthand: `label \"Text\"` == `label label=\"Text\"`.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("C# format:");
            Console.Error.WriteLine("  - Input is evaluated as a C# script and must return a DialogDef (see DialogDsl).");
            Console.Error.WriteLine("  - '#load \"...\"' is supported (relative to the script file), which helps reuse subsets.");
            Console.Error.WriteLine("  - HydraDialogDsl.Hydra() provides common Hydra GUI bindings (to_bool/to_num/to_string patterns).");
            return 2;
        }

        string inputPath = args[0];
        string? outputPath = null;
        InputFormat inputFormat = InputFormat.Auto;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--out" && i + 1 < args.Length)
            {
                outputPath = args[i + 1];
                i++;
            }
            else if (args[i] == "--format" && i + 1 < args.Length)
            {
                inputFormat = args[i + 1] switch
                {
                    "auto" => InputFormat.Auto,
                    "json" => InputFormat.Json,
                    "dsl" => InputFormat.Dsl,
                    "cs" => InputFormat.Cs,
                    _ => throw new InvalidDataException($"Unknown --format: {args[i + 1]} (expected auto|json|dsl|cs)."),
                };
                i++;
            }
            else
            {
                Console.Error.WriteLine($"Unknown arg: {args[i]}");
                return 2;
            }
        }

        string inputText = File.ReadAllText(inputPath, Encoding.UTF8);
        InputFormat effectiveFormat = inputFormat == InputFormat.Auto ? DetectFormat(inputPath, inputText) : inputFormat;

        var jsonOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        using JsonDocument doc = effectiveFormat switch
        {
            InputFormat.Json => JsonDocument.Parse(inputText, jsonOptions),
            InputFormat.Dsl => JsonDocument.Parse(DslToJson(inputText, inputPath), jsonOptions),
            InputFormat.Cs => CsToJsonDocument(inputText, inputPath, jsonOptions),
            _ => throw new InvalidDataException($"Unknown input format: {effectiveFormat}"),
        };

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Root must be a JSON object.");

        if (!doc.RootElement.TryGetProperty("rows", out var rowsEl) || rowsEl.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Missing or invalid 'rows' (array).");

        var rows = new List<RowSpec>(capacity: rowsEl.GetArrayLength());
        int maxRowWidth = 0;
        int rowIndexForWidth = 0;
        foreach (var rowEl in rowsEl.EnumerateArray())
        {
            RowSpec spec = ParseRowSpec(rowEl, rowIndexForWidth);
            rows.Add(spec);
            if (spec.Width > maxRowWidth)
                maxRowWidth = spec.Width;
            rowIndexForWidth++;
        }

        int columns = maxRowWidth;
        if (doc.RootElement.TryGetProperty("columns", out var colEl))
        {
            if (colEl.ValueKind != JsonValueKind.Number || !colEl.TryGetInt32(out columns))
                throw new InvalidDataException("Invalid 'columns' (int).");
        }

        if (columns < maxRowWidth)
            throw new InvalidDataException($"columns is too small: columns={columns}, required>={maxRowWidth}.");

        var sb = new StringBuilder(capacity: 4096);
        sb.AppendLine($"-- Generated by AegisubDialogDslGen from {Path.GetFileName(inputPath)}");
        sb.AppendLine($"-- Columns: {columns}");
        sb.AppendLine("return {");

        int y = 0;
        foreach (RowSpec row in rows)
        {
            switch (row.Kind)
            {
                case RowKind.Sequential:
                {
                    int x = 0;
                    foreach (var cellEl in row.Element.EnumerateArray())
                    {
                        x = EmitCell(sb, y, x, columns, cellEl, $"rows[{y}]");
                    }
                    break;
                }

                case RowKind.LeftRight:
                {
                    JsonElement left = row.Element.GetProperty("left");
                    JsonElement right = row.Element.GetProperty("right");

                    int x = 0;
                    foreach (var cellEl in left.EnumerateArray())
                    {
                        x = EmitCell(sb, y, x, columns, cellEl, $"rows[{y}].left");
                    }

                    int xr = columns - row.RightWidth;
                    foreach (var cellEl in right.EnumerateArray())
                    {
                        xr = EmitCell(sb, y, xr, columns, cellEl, $"rows[{y}].right");
                    }

                    break;
                }

                default:
                    throw new InvalidDataException($"rows[{y}] has unknown kind.");
            }

            y++;
        }

        sb.AppendLine("}");

        string lua = sb.ToString();
        if (outputPath is not null)
            File.WriteAllText(outputPath, lua, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        else
            Console.Write(lua);

        return 0;
    }

    private static InputFormat DetectFormat(string path, string text)
    {
        string ext = Path.GetExtension(path);
        if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jsonc", StringComparison.OrdinalIgnoreCase))
            return InputFormat.Json;
        if (ext.Equals(".dsl", StringComparison.OrdinalIgnoreCase))
            return InputFormat.Dsl;
        if (ext.Equals(".cs", StringComparison.OrdinalIgnoreCase) || ext.Equals(".csx", StringComparison.OrdinalIgnoreCase))
            return InputFormat.Cs;

        // Fallback: inspect first non-empty, non-comment line.
        foreach (string rawLine in text.Split('\n'))
        {
            ReadOnlySpan<char> line = rawLine.AsSpan();
            if (line.Length > 0 && line[^1] == '\r')
                line = line[..^1];
            line = line.TrimStart();
            if (line.Length == 0)
                continue;
            if (line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith('#'))
                continue;
            return line[0] == '{' ? InputFormat.Json : InputFormat.Dsl;
        }

        return InputFormat.Json;
    }

    private static string DslToJson(string text, string path)
    {
        var root = IndentDslParser.Parse(text, path);
        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = false });
    }

    private static JsonDocument CsToJsonDocument(string scriptText, string scriptPath, JsonDocumentOptions jsonOptions)
    {
        object? result;
        try
        {
            result = CSharpScript.EvaluateAsync<object?>(scriptText, CreateCsScriptOptions(scriptPath)).GetAwaiter().GetResult();
        }
        catch (CompilationErrorException ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("C# DSL compilation failed:");
            foreach (var diag in ex.Diagnostics)
                sb.AppendLine(diag.ToString());
            throw new InvalidDataException(sb.ToString());
        }

        if (result is null)
            throw new InvalidDataException("C# DSL returned null; expected DialogDef (or JSON). If your script ends with `Dialog(...);`, remove the trailing semicolon so the last expression is returned.");

        return result switch
        {
            DialogDef dialog => DialogDefToJsonDocument(dialog, jsonOptions),
            JsonDocument doc => doc,
            JsonElement el => JsonDocument.Parse(el.GetRawText(), jsonOptions),
            string json => JsonDocument.Parse(json, jsonOptions),
            _ => throw new InvalidDataException($"C# DSL returned unsupported type: {result.GetType().FullName} (expected DialogDef/JsonDocument/JsonElement/string)."),
        };
    }

    private static ScriptOptions CreateCsScriptOptions(string scriptPath)
    {
        string baseDir = Path.GetDirectoryName(Path.GetFullPath(scriptPath)) ?? Environment.CurrentDirectory;

        var options = ScriptOptions.Default
            .WithFilePath(scriptPath)
            .WithSourceResolver(ScriptSourceResolver.Default.WithBaseDirectory(baseDir))
            .WithMetadataResolver(ScriptMetadataResolver.Default.WithBaseDirectory(baseDir))
            .WithImports(
                "System",
                "System.Collections.Generic",
                "Mobsub.Tools.AegisubDialogDslGen")
            .AddReferences(
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(JsonSerializer).Assembly,
                typeof(DialogDsl).Assembly);

        return options;
    }

    private static JsonDocument DialogDefToJsonDocument(DialogDef dialog, JsonDocumentOptions jsonOptions)
    {
        var buffer = new ArrayBufferWriter<byte>(initialCapacity: 4096);
        using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            w.WriteStartObject();

            if (dialog.Columns is int cols)
                w.WriteNumber("columns", cols);

            w.WritePropertyName("rows");
            w.WriteStartArray();
            foreach (DialogRow row in dialog.Rows)
            {
                switch (row)
                {
                    case SequentialRow seq:
                        WriteCellArray(w, seq.Cells);
                        break;

                    case LeftRightRow lr:
                        w.WriteStartObject();
                        w.WritePropertyName("left");
                        WriteCellArray(w, lr.Left);
                        w.WritePropertyName("right");
                        WriteCellArray(w, lr.Right);
                        if (lr.Gap != 0)
                            w.WriteNumber("gap", lr.Gap);
                        w.WriteEndObject();
                        break;

                    default:
                        throw new InvalidDataException($"Unsupported row type: {row.GetType().FullName}");
                }
            }
            w.WriteEndArray();

            w.WriteEndObject();
            w.Flush();
        }

        return JsonDocument.Parse(buffer.WrittenMemory, jsonOptions);
    }

    private static void WriteCellArray(Utf8JsonWriter w, IReadOnlyList<DialogCell> cells)
    {
        w.WriteStartArray();
        foreach (DialogCell cell in cells)
        {
            switch (cell)
            {
                case SpacerCell spacer:
                    w.WriteStartObject();
                    w.WriteNumber("spacer", spacer.Spacer);
                    w.WriteEndObject();
                    break;

                case ControlCell control:
                    w.WriteStartObject();
                    w.WriteString("class", control.Class);
                    if (control.W != 1) w.WriteNumber("w", control.W);
                    if (control.H != 1) w.WriteNumber("h", control.H);
                    foreach (var kv in control.Props)
                        WriteProp(w, kv.Key, kv.Value);
                    w.WriteEndObject();
                    break;

                default:
                    throw new InvalidDataException($"Unsupported cell type: {cell.GetType().FullName}");
            }
        }
        w.WriteEndArray();
    }

    private static void WriteProp(Utf8JsonWriter w, string key, object? value)
    {
        if (key is "class" or "w" or "h" or "width" or "height" or "spacer")
            throw new InvalidDataException($"Invalid property name '{key}' (reserved).");

        switch (value)
        {
            case null:
                w.WriteNull(key);
                return;
            case string s:
                w.WriteString(key, s);
                return;
            case bool b:
                w.WriteBoolean(key, b);
                return;
            case int i:
                w.WriteNumber(key, i);
                return;
            case long l:
                w.WriteNumber(key, l);
                return;
            case double d:
                w.WriteNumber(key, d);
                return;
            case float f:
                w.WriteNumber(key, f);
                return;
            case LuaExpr lua:
                w.WriteString(key, "@" + (lua.Code ?? ""));
                return;
            case IReadOnlyList<string> strs:
                w.WritePropertyName(key);
                w.WriteStartArray();
                foreach (string it in strs)
                    w.WriteStringValue(it);
                w.WriteEndArray();
                return;
            case IEnumerable<string> e:
                w.WritePropertyName(key);
                w.WriteStartArray();
                foreach (string it in e)
                    w.WriteStringValue(it);
                w.WriteEndArray();
                return;
            default:
                throw new InvalidDataException($"Unsupported property value type for '{key}': {value.GetType().FullName}");
        }
    }

    private static RowSpec ParseRowSpec(JsonElement rowEl, int rowIndex)
    {
        if (rowEl.ValueKind == JsonValueKind.Array)
        {
            int width = ComputeRowWidth(rowEl, $"rows[{rowIndex}]");
            return new RowSpec(RowKind.Sequential, rowEl, width, LeftWidth: 0, RightWidth: 0, Gap: 0);
        }

        if (rowEl.ValueKind == JsonValueKind.Object)
        {
            if (!rowEl.TryGetProperty("left", out var leftEl) || leftEl.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"rows[{rowIndex}] object must have 'left' (array).");
            if (!rowEl.TryGetProperty("right", out var rightEl) || rightEl.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"rows[{rowIndex}] object must have 'right' (array).");

            int gap = 0;
            if (TryGetInt(rowEl, "gap", out int g))
            {
                if (g < 0)
                    throw new InvalidDataException($"rows[{rowIndex}].gap must be >= 0.");
                gap = g;
            }

            int leftWidth = ComputeRowWidth(leftEl, $"rows[{rowIndex}].left");
            int rightWidth = ComputeRowWidth(rightEl, $"rows[{rowIndex}].right");
            int width = leftWidth + rightWidth + gap;
            return new RowSpec(RowKind.LeftRight, rowEl, width, leftWidth, rightWidth, gap);
        }

        throw new InvalidDataException($"rows[{rowIndex}] must be an array or an object.");
    }

    private static int ComputeRowWidth(JsonElement rowEl, string path)
    {
        if (rowEl.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"{path} must be an array.");

        int width = 0;
        foreach (var cellEl in rowEl.EnumerateArray())
        {
            if (cellEl.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"{path} contains a non-object cell.");

            if (TryGetInt(cellEl, "spacer", out int spacer))
            {
                if (spacer < 0)
                    throw new InvalidDataException($"{path} has spacer < 0.");
                width += spacer;
                continue;
            }

            int w = GetSpan(cellEl, "w", "width", defaultValue: 1, path);
            width += w;
        }

        return width;
    }

    private static int EmitCell(StringBuilder sb, int y, int x, int columns, JsonElement cellEl, string path)
    {
        if (cellEl.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"{path} contains a non-object cell.");

        if (TryGetInt(cellEl, "spacer", out int spacer))
        {
            if (spacer < 0)
                throw new InvalidDataException($"{path} has spacer < 0.");
            return x + spacer;
        }

        if (!TryGetString(cellEl, "class", out string? className) || string.IsNullOrWhiteSpace(className))
            throw new InvalidDataException($"{path} cell is missing required 'class'.");

        int w = GetSpan(cellEl, "w", "width", defaultValue: 1, path);
        int h = GetSpan(cellEl, "h", "height", defaultValue: 1, path);

        if (x + w > columns)
            throw new InvalidDataException($"{path} cell would overflow columns: x={x}, w={w}, columns={columns}.");

        sb.Append("  { ");
        sb.Append("x = ").Append(x).Append(", ");
        sb.Append("y = ").Append(y).Append(", ");
        if (w != 1)
            sb.Append("width = ").Append(w).Append(", ");
        if (h != 1)
            sb.Append("height = ").Append(h).Append(", ");
        sb.Append("class = ").Append(LuaString(className)).Append(", ");

        // Emit remaining properties verbatim (except layout keys).
        foreach (var prop in cellEl.EnumerateObject())
        {
            string name = prop.Name;
            if (name is "spacer" or "w" or "h" or "width" or "height" or "class")
                continue;

            sb.Append(name).Append(" = ").Append(LuaValue(prop.Value)).Append(", ");
        }

        // Trim trailing ", "
        if (sb.Length >= 2 && sb[sb.Length - 2] == ',' && sb[sb.Length - 1] == ' ')
            sb.Length -= 2;

        sb.AppendLine(" },");

        return x + w;
    }

    private static int GetSpan(JsonElement obj, string key1, string key2, int defaultValue, string path)
    {
        int v = defaultValue;
        if (TryGetInt(obj, key1, out int v1)) v = v1;
        if (TryGetInt(obj, key2, out int v2)) v = v2;
        if (v < 1)
            throw new InvalidDataException($"{path} has invalid {key1}/{key2} (must be >= 1).");
        return v;
    }

    private static bool TryGetInt(JsonElement obj, string name, out int value)
    {
        value = 0;
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Number)
            return false;
        return el.TryGetInt32(out value);
    }

    private static bool TryGetString(JsonElement obj, string name, out string? value)
    {
        value = null;
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
            return false;
        value = el.GetString();
        return true;
    }

    private static string LuaValue(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
                {
                    string s = el.GetString() ?? "";
                    if (s.Length > 0 && s[0] == '@')
                    {
                        if (s.Length > 1 && s[1] == '@')
                            return LuaString(s.Substring(1));
                        return s.Substring(1);
                    }
                    return LuaString(s);
                }
            case JsonValueKind.Number:
                return el.GetRawText();
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Null:
                return "nil";
            case JsonValueKind.Array:
                {
                    var sb = new StringBuilder();
                    sb.Append("{ ");
                    bool first = true;
                    foreach (var it in el.EnumerateArray())
                    {
                        if (!first) sb.Append(", ");
                        first = false;
                        sb.Append(LuaValue(it));
                    }
                    sb.Append(" }");
                    return sb.ToString();
                }
            case JsonValueKind.Object:
                {
                    if (el.TryGetProperty("lua", out var luaEl) && luaEl.ValueKind == JsonValueKind.String)
                    {
                        string s = luaEl.GetString() ?? "";
                        return s;
                    }
                    throw new InvalidDataException("Object values must be {\"lua\":\"...\"}.");
                }
            default:
                throw new InvalidDataException($"Unsupported JSON value kind: {el.ValueKind}");
        }
    }

    private static string LuaString(string s)
    {
        var sb = new StringBuilder(capacity: s.Length + 8);
        sb.Append('"');
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < 0x20)
                    {
                        sb.Append("\\x");
                        sb.Append(((int)ch).ToString("X2", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private sealed class IndentDslParser
    {
        private sealed class RowBuilder
        {
            public RowKind Kind { get; private set; } = RowKind.Sequential;
            public int Gap { get; set; }
            public List<object?>? Cells { get; private set; }
            public List<object?>? Left { get; private set; }
            public List<object?>? Right { get; private set; }

            public void AddSequentialCell(object? cell, int lineNo)
            {
                if (Kind != RowKind.Sequential)
                    throw new InvalidDataException($"Line {lineNo}: cannot add sequential cells to a left/right row.");
                (Cells ??= []).Add(cell);
            }

            public void EnsureLeftRight(int lineNo)
            {
                if (Kind == RowKind.LeftRight)
                    return;
                if (Cells is { Count: > 0 })
                    throw new InvalidDataException($"Line {lineNo}: cannot start left/right blocks after sequential cells.");
                Kind = RowKind.LeftRight;
                Left ??= [];
                Right ??= [];
            }

            public void AddLeftCell(object? cell, int lineNo)
            {
                EnsureLeftRight(lineNo);
                (Left ??= []).Add(cell);
            }

            public void AddRightCell(object? cell, int lineNo)
            {
                EnsureLeftRight(lineNo);
                (Right ??= []).Add(cell);
            }

            public object ToJsonRow()
            {
                if (Kind == RowKind.LeftRight)
                {
                    var obj = new Dictionary<string, object?>(capacity: 3)
                    {
                        ["left"] = Left ?? [],
                        ["right"] = Right ?? [],
                    };
                    if (Gap != 0)
                        obj["gap"] = Gap;
                    return obj;
                }

                return Cells ?? [];
            }
        }

        private enum BlockKind : byte
        {
            Root = 0,
            Row = 1,
            Left = 2,
            Right = 3,
        }

        private sealed class Block
        {
            public Block(BlockKind kind, int indent, RowBuilder? row)
            {
                Kind = kind;
                Indent = indent;
                Row = row;
            }

            public BlockKind Kind { get; }
            public int Indent { get; set; }
            public RowBuilder? Row { get; }
        }

        public static Dictionary<string, object?> Parse(string text, string path)
        {
            var parser = new IndentDslParser(text, path);
            return parser.ParseCore();
        }

        private readonly string _text;
        private readonly string _path;
        private int? _columns;
        private readonly List<RowBuilder> _rows = [];
        private readonly Stack<Block> _stack = new();
        private Block? _pendingBlock;

        private IndentDslParser(string text, string path)
        {
            _text = text;
            _path = path;
        }

        private Dictionary<string, object?> ParseCore()
        {
            _stack.Push(new Block(BlockKind.Root, indent: 0, row: null));

            int lineNo = 0;
            foreach (string raw in _text.Split('\n'))
            {
                lineNo++;
                ReadOnlySpan<char> line = raw.AsSpan();
                if (line.Length > 0 && line[^1] == '\r')
                    line = line[..^1];

                int indent = CountIndent(line, out ReadOnlySpan<char> content);
                content = StripInlineComment(content).Trim();
                if (content.Length == 0)
                    continue;

                // Close blocks on dedent.
                while (indent < _stack.Peek().Indent && _stack.Count > 1)
                    _stack.Pop();
                if (indent < _stack.Peek().Indent)
                    throw new InvalidDataException($"Line {lineNo}: indentation does not match any parent block.");

                // Open pending block if we see an indent increase; otherwise treat it as an empty block.
                if (indent > _stack.Peek().Indent)
                {
                    if (_pendingBlock is null)
                        throw new InvalidDataException($"Line {lineNo}: unexpected indentation.");
                    _pendingBlock.Indent = indent;
                    _stack.Push(_pendingBlock);
                    _pendingBlock = null;
                }
                else if (_pendingBlock is not null)
                {
                    _pendingBlock = null;
                }

                Block cur = _stack.Peek();
                bool isHeader = content.EndsWith(':');
                if (isHeader)
                {
                    ReadOnlySpan<char> header = content[..^1].Trim();
                    ParseHeader(cur, header, lineNo);
                }
                else
                {
                    ParseStatement(cur, content, lineNo);
                }
            }

            // If the file ends with a header, treat it as an empty block.
            _pendingBlock = null;

            var root = new Dictionary<string, object?>(capacity: 2);
            if (_columns is not null)
                root["columns"] = _columns.Value;
            var jsonRows = new List<object>(_rows.Count);
            foreach (RowBuilder row in _rows)
                jsonRows.Add(row.ToJsonRow());
            root["rows"] = jsonRows;
            return root;
        }

        private void ParseHeader(Block cur, ReadOnlySpan<char> header, int lineNo)
        {
            if (_pendingBlock is not null)
                throw new InvalidDataException($"Line {lineNo}: nested block header without body for previous header.");

            string keyword = ReadFirstWord(header, out ReadOnlySpan<char> rest);
            switch (cur.Kind)
            {
                case BlockKind.Root:
                    if (keyword != "row")
                        throw new InvalidDataException($"Line {lineNo}: unknown block '{keyword}:' at root.");
                    var row = new RowBuilder();
                    ParseRowHeaderAttrs(row, rest, lineNo);
                    _rows.Add(row);
                    _pendingBlock = new Block(BlockKind.Row, indent: 0, row);
                    return;

                case BlockKind.Row:
                    if (cur.Row is null)
                        throw new InvalidDataException($"Line {lineNo}: internal error: row block has no row.");
                    if (keyword == "left")
                    {
                        cur.Row.EnsureLeftRight(lineNo);
                        _pendingBlock = new Block(BlockKind.Left, indent: 0, cur.Row);
                        return;
                    }
                    if (keyword == "right")
                    {
                        cur.Row.EnsureLeftRight(lineNo);
                        _pendingBlock = new Block(BlockKind.Right, indent: 0, cur.Row);
                        return;
                    }
                    throw new InvalidDataException($"Line {lineNo}: unknown block '{keyword}:' inside row.");

                default:
                    throw new InvalidDataException($"Line {lineNo}: blocks are not allowed here.");
            }
        }

        private void ParseStatement(Block cur, ReadOnlySpan<char> content, int lineNo)
        {
            string keyword = ReadKeyword(content, out ReadOnlySpan<char> rest);
            switch (cur.Kind)
            {
                case BlockKind.Root:
                    if (keyword != "columns")
                        throw new InvalidDataException($"Line {lineNo}: expected 'row:' or 'columns=...'; got '{keyword}'.");
                    _columns = ParseIntDirectiveValue(rest, lineNo, "columns");
                    return;

                case BlockKind.Row:
                    if (cur.Row is null)
                        throw new InvalidDataException($"Line {lineNo}: internal error: row block has no row.");
                    if (keyword == "gap")
                    {
                        cur.Row.Gap = ParseIntDirectiveValue(rest, lineNo, "gap");
                        return;
                    }
                    cur.Row.AddSequentialCell(ParseCellLine(content, lineNo), lineNo);
                    return;

                case BlockKind.Left:
                    cur.Row!.AddLeftCell(ParseCellLine(content, lineNo), lineNo);
                    return;

                case BlockKind.Right:
                    cur.Row!.AddRightCell(ParseCellLine(content, lineNo), lineNo);
                    return;

                default:
                    throw new InvalidDataException($"Line {lineNo}: unknown parser state.");
            }
        }

        private static int ParseIntDirectiveValue(ReadOnlySpan<char> rest, int lineNo, string name)
        {
            int idx = 0;
            SkipWs(rest, ref idx);
            if (idx < rest.Length && (rest[idx] == '=' || rest[idx] == ':'))
            {
                idx++;
                SkipWs(rest, ref idx);
            }
            object? v = ReadValue(rest, ref idx, lineNo);
            if (v is int i)
                return i;
            if (v is double d && Math.Abs(d - Math.Round(d)) < 1e-9)
                return (int)Math.Round(d);
            throw new InvalidDataException($"Line {lineNo}: {name} must be an integer.");
        }

        private static void ParseRowHeaderAttrs(RowBuilder row, ReadOnlySpan<char> rest, int lineNo)
        {
            int idx = 0;
            while (true)
            {
                SkipWs(rest, ref idx);
                if (idx >= rest.Length)
                    return;

                ReadOnlySpan<char> key = ReadKey(rest, ref idx, lineNo);
                if (!IsLuaIdentifier(key))
                    throw new InvalidDataException($"Line {lineNo}: invalid attribute name '{key.ToString()}'.");

                if (idx >= rest.Length || rest[idx] != '=')
                    throw new InvalidDataException($"Line {lineNo}: expected '=' after '{key.ToString()}'.");
                idx++;

                object? value = ReadValue(rest, ref idx, lineNo);
                if (key.SequenceEqual("gap"))
                {
                    if (value is int gi)
                        row.Gap = gi;
                    else
                        throw new InvalidDataException($"Line {lineNo}: row gap must be an integer.");
                }
                else
                {
                    throw new InvalidDataException($"Line {lineNo}: unknown row attribute '{key.ToString()}'.");
                }
            }
        }

        private static Dictionary<string, object?> ParseCellLine(ReadOnlySpan<char> content, int lineNo)
        {
            string head = ReadKeyword(content, out ReadOnlySpan<char> rest);

            if (head == "spacer")
            {
                int ridx = 0;
                object? v = ReadDirectiveLikeValue(rest, ref ridx, lineNo);
                if (v is not int spacer || spacer < 0)
                    throw new InvalidDataException($"Line {lineNo}: spacer must be an integer >= 0.");
                return new Dictionary<string, object?>(capacity: 1) { ["spacer"] = spacer };
            }

            var obj = new Dictionary<string, object?>(capacity: 8) { ["class"] = head };

            int idx = 0;

            // Positional shorthand (indent DSL only): `label "Text"` => `{class="label", label="Text"}`
            // (still supports the explicit `label=...` form).
            if (head == "label")
            {
                SkipWs(rest, ref idx);
                if (idx < rest.Length)
                {
                    bool looksLikeKey = false;

                    char ch = rest[idx];
                    if (ch == '"' || ch == '\'' || ch == '@' || ch == '[' || char.IsDigit(ch) || ch == '-' || ch == '+')
                    {
                        looksLikeKey = false;
                    }
                    else
                    {
                        int j = idx;
                        while (j < rest.Length && !char.IsWhiteSpace(rest[j]) && rest[j] != '=')
                            j++;
                        looksLikeKey = j < rest.Length && rest[j] == '=';
                    }

                    if (!looksLikeKey)
                    {
                        obj["label"] = ReadValue(rest, ref idx, lineNo);
                    }
                }
            }

            while (true)
            {
                SkipWs(rest, ref idx);
                if (idx >= rest.Length)
                    break;

                ReadOnlySpan<char> key = ReadKey(rest, ref idx, lineNo);
                if (!IsLuaIdentifier(key))
                    throw new InvalidDataException($"Line {lineNo}: invalid property name '{key.ToString()}'.");

                if (idx >= rest.Length || rest[idx] != '=')
                    throw new InvalidDataException($"Line {lineNo}: expected '=' after '{key.ToString()}'.");
                idx++;

                object? value = ReadValue(rest, ref idx, lineNo);
                obj[key.ToString()] = value;
            }

            return obj;
        }

        private static object? ReadDirectiveLikeValue(ReadOnlySpan<char> rest, ref int idx, int lineNo)
        {
            SkipWs(rest, ref idx);
            if (idx < rest.Length && (rest[idx] == '=' || rest[idx] == ':'))
            {
                idx++;
                SkipWs(rest, ref idx);
            }
            return ReadValue(rest, ref idx, lineNo);
        }

        private static int CountIndent(ReadOnlySpan<char> line, out ReadOnlySpan<char> content)
        {
            int indent = 0;
            int i = 0;
            for (; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == ' ')
                {
                    indent++;
                    continue;
                }
                if (ch == '\t')
                {
                    indent += 4;
                    continue;
                }
                break;
            }

            content = line[i..];
            return indent;
        }

        private static ReadOnlySpan<char> StripInlineComment(ReadOnlySpan<char> content)
        {
            char quote = '\0';
            int bracketDepth = 0;

            for (int i = 0; i < content.Length; i++)
            {
                char ch = content[i];

                if (quote != '\0')
                {
                    if (ch == '\\')
                    {
                        if (i + 1 < content.Length)
                            i++;
                        continue;
                    }
                    if (ch == quote)
                        quote = '\0';
                    continue;
                }

                if (ch is '"' or '\'')
                {
                    quote = ch;
                    continue;
                }

                if (ch == '[')
                {
                    bracketDepth++;
                    continue;
                }
                if (ch == ']')
                {
                    if (bracketDepth > 0)
                        bracketDepth--;
                    continue;
                }

                if (bracketDepth != 0)
                    continue;

                if (ch == '#')
                    return content[..i];

                if (ch == '/' && i + 1 < content.Length && content[i + 1] == '/')
                    return content[..i];
            }

            return content;
        }

        private static string ReadFirstWord(ReadOnlySpan<char> span, out ReadOnlySpan<char> rest)
        {
            int idx = 0;
            SkipWs(span, ref idx);
            int start = idx;
            while (idx < span.Length && !char.IsWhiteSpace(span[idx]))
                idx++;
            if (idx == start)
                throw new InvalidDataException("Expected a keyword.");
            rest = span[idx..];
            return span[start..idx].ToString();
        }

        private static string ReadKeyword(ReadOnlySpan<char> span, out ReadOnlySpan<char> rest)
        {
            int idx = 0;
            SkipWs(span, ref idx);
            int start = idx;
            while (idx < span.Length)
            {
                char ch = span[idx];
                if (char.IsWhiteSpace(ch) || ch is '=' or ':')
                    break;
                idx++;
            }
            if (idx == start)
                throw new InvalidDataException("Expected a keyword.");
            rest = span[idx..];
            return span[start..idx].ToString();
        }

        private static void SkipWs(ReadOnlySpan<char> span, ref int idx)
        {
            while (idx < span.Length && char.IsWhiteSpace(span[idx]))
                idx++;
        }

        private static ReadOnlySpan<char> ReadKey(ReadOnlySpan<char> span, ref int idx, int lineNo)
        {
            int start = idx;
            while (idx < span.Length)
            {
                char ch = span[idx];
                if (ch == '=' || char.IsWhiteSpace(ch))
                    break;
                idx++;
            }

            if (idx == start)
                throw new InvalidDataException($"Line {lineNo}: expected an identifier.");

            return span[start..idx];
        }

        private static bool IsLuaIdentifier(ReadOnlySpan<char> s)
        {
            if (s.Length == 0)
                return false;
            char c0 = s[0];
            if (!(c0 == '_' || char.IsLetter(c0)))
                return false;
            for (int i = 1; i < s.Length; i++)
            {
                char ch = s[i];
                if (!(ch == '_' || char.IsLetterOrDigit(ch)))
                    return false;
            }
            return true;
        }

        private static object? ReadValue(ReadOnlySpan<char> span, ref int idx, int lineNo)
        {
            SkipWs(span, ref idx);
            if (idx >= span.Length)
                throw new InvalidDataException($"Line {lineNo}: expected a value.");

            char ch = span[idx];
            if (ch == '@')
                return ReadRawLua(span, ref idx, lineNo);
            if (ch == '"' || ch == '\'')
                return ReadQuotedString(span, ref idx, ch, lineNo);

            if (ch == '[')
                return ReadArray(span, ref idx, lineNo);

            int start = idx;
            while (idx < span.Length && !char.IsWhiteSpace(span[idx]))
                idx++;
            ReadOnlySpan<char> token = span[start..idx];
            return ParseBareValue(token);
        }

        private static string ReadRawLua(ReadOnlySpan<char> span, ref int idx, int lineNo)
        {
            // Raw Lua:
            // - @expr (no whitespace)
            // - @( ... ) / @{ ... } where ... may contain whitespace; nested delimiters supported; strings honored.
            int at = idx;
            idx++; // '@'
            if (idx >= span.Length)
                return "@";

            char open = span[idx];
            if (open is not '(' and not '{')
            {
                int start = at;
                while (idx < span.Length && !char.IsWhiteSpace(span[idx]))
                    idx++;
                return span[start..idx].ToString();
            }

            char close = open == '(' ? ')' : '}';
            idx++; // skip open

            int innerStart = idx;
            int depth = 1;
            char quote = '\0';

            while (idx < span.Length)
            {
                char ch = span[idx];

                if (quote != '\0')
                {
                    if (ch == '\\')
                    {
                        idx += idx + 1 < span.Length ? 2 : 1;
                        continue;
                    }
                    if (ch == quote)
                        quote = '\0';
                    idx++;
                    continue;
                }

                if (ch is '"' or '\'')
                {
                    quote = ch;
                    idx++;
                    continue;
                }

                if (ch == open)
                {
                    depth++;
                    idx++;
                    continue;
                }

                if (ch == close)
                {
                    depth--;
                    if (depth == 0)
                    {
                        ReadOnlySpan<char> inner = span[innerStart..idx].Trim();
                        idx++; // consume close
                        return "@" + inner.ToString();
                    }
                    idx++;
                    continue;
                }

                idx++;
            }

            throw new InvalidDataException($"Line {lineNo}: unterminated raw Lua @{open}...{close}.");
        }

        private static object? ReadArray(ReadOnlySpan<char> span, ref int idx, int lineNo)
        {
            // '['
            idx++;
            var list = new List<object?>();
            while (true)
            {
                SkipWs(span, ref idx);
                if (idx >= span.Length)
                    throw new InvalidDataException($"Line {lineNo}: unterminated array (missing ']').");
                if (span[idx] == ']')
                {
                    idx++;
                    return list;
                }

                object? element = ReadArrayElement(span, ref idx, lineNo);
                list.Add(element);

                SkipWs(span, ref idx);
                if (idx >= span.Length)
                    throw new InvalidDataException($"Line {lineNo}: unterminated array (missing ']').");

                if (span[idx] == ',')
                {
                    idx++;
                    continue;
                }

                if (span[idx] == ']')
                {
                    idx++;
                    return list;
                }

                throw new InvalidDataException($"Line {lineNo}: expected ',' or ']' in array.");
            }
        }

        private static object? ReadArrayElement(ReadOnlySpan<char> span, ref int idx, int lineNo)
        {
            SkipWs(span, ref idx);
            if (idx >= span.Length)
                throw new InvalidDataException($"Line {lineNo}: expected array element.");

            char ch = span[idx];
            if (ch == '@')
                return ReadRawLua(span, ref idx, lineNo);
            if (ch == '"' || ch == '\'')
                return ReadQuotedString(span, ref idx, ch, lineNo);

            if (ch == '[')
                return ReadArray(span, ref idx, lineNo);

            int start = idx;
            char quote = '\0';
            int bracketDepth = 0;
            for (; idx < span.Length; idx++)
            {
                char c = span[idx];
                if (quote != '\0')
                {
                    if (c == '\\' && idx + 1 < span.Length)
                    {
                        idx++;
                        continue;
                    }
                    if (c == quote)
                        quote = '\0';
                    continue;
                }

                if (c is '"' or '\'')
                {
                    quote = c;
                    continue;
                }

                if (c == '[') { bracketDepth++; continue; }
                if (c == ']' && bracketDepth > 0) { bracketDepth--; continue; }

                if (bracketDepth == 0 && (c == ',' || c == ']'))
                    break;
            }

            ReadOnlySpan<char> token = span[start..idx].Trim();
            if (token.Length == 0)
                throw new InvalidDataException($"Line {lineNo}: empty array element.");
            return ParseBareValue(token);
        }

        private static string ReadQuotedString(ReadOnlySpan<char> span, ref int idx, char quote, int lineNo)
        {
            idx++; // skip quote
            int segmentStart = idx;
            StringBuilder? sb = null;
            while (idx < span.Length)
            {
                char ch = span[idx];
                if (ch == quote)
                {
                    if (sb is null)
                    {
                        string s = span[segmentStart..idx].ToString();
                        idx++; // closing quote
                        return s;
                    }

                    sb.Append(span[segmentStart..idx]);
                    idx++; // closing quote
                    return sb.ToString();
                }

                if (ch != '\\')
                {
                    idx++;
                    continue;
                }

                sb ??= new StringBuilder();
                sb.Append(span[segmentStart..idx]);

                idx++; // backslash
                if (idx >= span.Length)
                    throw new InvalidDataException($"Line {lineNo}: unterminated escape sequence.");
                char esc = span[idx++];
                switch (esc)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case '\\': sb.Append('\\'); break;
                    case '"': sb.Append('"'); break;
                    case '\'': sb.Append('\''); break;
                    default:
                        // Preserve unknown escapes (useful for ASS: "\\t", "\\c", etc.).
                        sb.Append('\\');
                        sb.Append(esc);
                        break;
                }

                segmentStart = idx;
            }

            throw new InvalidDataException($"Line {lineNo}: unterminated string (missing closing {quote}).");
        }

        private static object? ParseBareValue(ReadOnlySpan<char> token)
        {
            if (token.Length == 0)
                return "";

            // Raw Lua: @expr (no spaces). Escaping the leading @ is handled by LuaValue: '@@' => string '@...'
            if (token[0] == '@')
                return token.ToString();

            if (token.Equals("true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (token.Equals("false", StringComparison.OrdinalIgnoreCase))
                return false;
            if (token.Equals("null", StringComparison.OrdinalIgnoreCase) || token.Equals("nil", StringComparison.OrdinalIgnoreCase))
                return null;

            bool maybeNumber = true;
            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                if (!(char.IsDigit(c) || c is '+' or '-' or '.' or 'e' or 'E'))
                {
                    maybeNumber = false;
                    break;
                }
            }

            if (maybeNumber)
            {
                bool hasDotOrExp = token.IndexOf('.') >= 0 || token.IndexOf('e') >= 0 || token.IndexOf('E') >= 0;
                bool hasLeadingZeroInt = !hasDotOrExp && token.Length > 1 && token[0] == '0' && char.IsDigit(token[1]);
                bool hasLeadingZeroNegInt = !hasDotOrExp && token.Length > 2 && token[0] == '-' && token[1] == '0' && char.IsDigit(token[2]);
                if (!hasLeadingZeroInt && !hasLeadingZeroNegInt)
                {
                    if (!hasDotOrExp && int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                        return i;
                    if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                        return d;
                }
            }

            return token.ToString();
        }
    }
}
