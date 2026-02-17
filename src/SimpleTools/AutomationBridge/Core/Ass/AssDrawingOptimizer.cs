using System.Globalization;

namespace Mobsub.AutomationBridge.Core.Ass;

public static class AssDrawingOptimizer
{
    public static string OptimizeDrawing(
        string drawing,
        double curveTolerance,
        double simplifyTolerance = 0,
        int precisionDecimals = 0,
        bool closeContours = false)
    {
        if (string.IsNullOrWhiteSpace(drawing))
            return string.Empty;

        var commands = ParseToOutlineCommands(drawing.AsSpan());
        if (commands.Count == 0)
            return string.Empty;

        return AssOutlineDrawing.ToAssDrawing(
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(commands),
            curveTolerance: curveTolerance,
            simplifyTolerance: simplifyTolerance,
            precisionDecimals: precisionDecimals,
            closeContours: closeContours);
    }

    private static List<OutlineCommand> ParseToOutlineCommands(ReadOnlySpan<char> s)
    {
        var list = new List<OutlineCommand>(capacity: Math.Min(4096, Math.Max(16, s.Length / 4)));

        char cmd = '\0';
        Span<double> buf = stackalloc double[6];
        int bufCount = 0;

        int pos = 0;
        while (TryReadToken(s, ref pos, out var token))
        {
            if (token.IsCommand)
            {
                cmd = token.Command;
                bufCount = 0;

                if (cmd == 'c')
                {
                    list.Add(new OutlineCommand(OutlineCommandType.Close, default, default, default));
                    cmd = '\0';
                }

                if (cmd == 's')
                {
                    // Not implemented (spline). Keeping it explicit avoids silently wrong output.
                    throw new NotSupportedException("ASS drawing command 's' is not supported by this optimizer yet.");
                }

                continue;
            }

            if (cmd == '\0')
                continue;

            switch (cmd)
            {
                case 'm':
                case 'n':
                    buf[bufCount++] = token.Number;
                    if (bufCount == 2)
                    {
                        list.Add(new OutlineCommand(
                            OutlineCommandType.MoveTo,
                            new System.Numerics.Vector2((float)buf[0], (float)buf[1]),
                            default,
                            default));
                        bufCount = 0;
                    }
                    break;

                case 'l':
                    buf[bufCount++] = token.Number;
                    if (bufCount == 2)
                    {
                        list.Add(new OutlineCommand(
                            OutlineCommandType.LineTo,
                            new System.Numerics.Vector2((float)buf[0], (float)buf[1]),
                            default,
                            default));
                        bufCount = 0;
                    }
                    break;

                case 'b':
                    buf[bufCount++] = token.Number;
                    if (bufCount == 6)
                    {
                        list.Add(new OutlineCommand(
                            OutlineCommandType.CubicTo,
                            new System.Numerics.Vector2((float)buf[0], (float)buf[1]),
                            new System.Numerics.Vector2((float)buf[2], (float)buf[3]),
                            new System.Numerics.Vector2((float)buf[4], (float)buf[5])));
                        bufCount = 0;
                    }
                    break;

                default:
                    // Unknown command; ignore numbers until next command.
                    break;
            }
        }

        return list;
    }

    private readonly record struct Token(bool IsCommand, char Command, double Number)
    {
        public static Token Cmd(char c) => new(IsCommand: true, Command: c, Number: default);
        public static Token Num(double v) => new(IsCommand: false, Command: default, Number: v);
    }

    private static bool TryReadToken(ReadOnlySpan<char> s, ref int pos, out Token token)
    {
        token = default;

        while (pos < s.Length)
        {
            char ch = s[pos];
            if (char.IsWhiteSpace(ch) || ch == ',')
            {
                pos++;
                continue;
            }
            break;
        }

        if (pos >= s.Length)
            return false;

        char c = s[pos];
        if (IsCommandChar(c))
        {
            pos++;
            token = Token.Cmd(char.ToLowerInvariant(c));
            return true;
        }

        // Number: read until separator/command.
        int start = pos;
        pos++;
        while (pos < s.Length)
        {
            char ch = s[pos];
            if (char.IsWhiteSpace(ch) || ch == ',' || IsCommandChar(ch))
                break;
            pos++;
        }

        var span = s.Slice(start, pos - start);
        if (!double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            // Skip invalid token.
            return TryReadToken(s, ref pos, out token);
        }

        token = Token.Num(value);
        return true;
    }

    private static bool IsCommandChar(char c)
        => (uint)(c | 0x20) - 'a' <= (uint)('z' - 'a'); // ASCII letter
}
