using System.Buffers.Text;
using System.Numerics;
using System.Text;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

public static class AssClipQuadExtractor
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    public static bool TryGetQuadFromFirstOverrideBlock(string lineText, Span<Vector2> quad, out string error)
    {
        error = string.Empty;

        if (quad.Length < 4)
            throw new ArgumentException("quad span must have length >= 4.", nameof(quad));

        if (string.IsNullOrEmpty(lineText))
        {
            error = "line_text_empty";
            return false;
        }

        byte[] lineUtf8 = Utf8.GetBytes(lineText);
        if (lineUtf8.Length == 0 || lineUtf8[0] != (byte)'{')
        {
            error = "no_first_override_block";
            return false;
        }

        using var buffer = AssEventTextParser.ParseLinePooled(lineUtf8);
        var segments = buffer.Span;

        if (segments.Length == 0 || segments[0].SegmentKind != AssEventSegmentKind.TagBlock)
        {
            error = "no_first_tag_block";
            return false;
        }

        var seg = segments[0];
        ReadOnlySpan<AssTagSpan> tags = seg.Tags.HasValue ? seg.Tags.Value.Span : default;

        for (int i = 0; i < tags.Length; i++)
        {
            var t = tags[i].Tag;
            if (t != AssTag.Clip && t != AssTag.InverseClip)
                continue;

            if (!tags[i].TryGet<AssTagFunctionValue>(out var func))
            {
                error = "clip_tag_parse_failed";
                return false;
            }

            if (func.Kind == AssTagFunctionKind.ClipRect)
            {
                error = "clip_is_rect";
                return false;
            }

            if (func.Kind != AssTagFunctionKind.ClipDrawing)
            {
                error = "clip_kind_not_supported";
                return false;
            }

            return TryParseClipDrawingQuad(func.Drawing.Span, func.Scale, quad, out error);
        }

        error = "clip_not_found";
        return false;
    }

    private static bool TryParseClipDrawingQuad(ReadOnlySpan<byte> drawingUtf8, int scale, Span<Vector2> quad, out string error)
    {
        error = string.Empty;

        scale = Math.Max(1, scale);
        if (drawingUtf8.IsEmpty)
        {
            error = "clip_drawing_empty";
            return false;
        }

        byte cmd = 0;
        bool hasX = false;
        double x = 0;
        int points = 0;

        int pos = 0;
        while (TryReadToken(drawingUtf8, ref pos, out var token))
        {
            if (token.IsCommand)
            {
                cmd = token.Command;
                hasX = false;
                continue;
            }

            if (cmd == 0)
                continue;

            if (cmd is (byte)'m' or (byte)'n' or (byte)'l')
            {
                if (!hasX)
                {
                    x = token.Number;
                    hasX = true;
                    continue;
                }

                double y = token.Number;
                hasX = false;

                double sx = x / scale;
                double sy = y / scale;
                quad[points++] = new Vector2((float)sx, (float)sy);
                if (points >= 4)
                    return true;

                continue;
            }

            // Ignore numbers for other commands (b, s, c, ...).
        }

        error = "clip_quad_not_found";
        return false;
    }

    private readonly record struct Token(bool IsCommand, byte Command, double Number)
    {
        public static Token Cmd(byte c) => new(IsCommand: true, Command: c, Number: default);
        public static Token Num(double v) => new(IsCommand: false, Command: default, Number: v);
    }

    private static bool TryReadToken(ReadOnlySpan<byte> s, ref int pos, out Token token)
    {
        token = default;

        while ((uint)pos < (uint)s.Length)
        {
            byte ch = s[pos];
            if (ch == (byte)',' || ch == (byte)' ' || ch == (byte)'\t' || ch == (byte)'\r' || ch == (byte)'\n')
            {
                pos++;
                continue;
            }
            break;
        }

        if (pos >= s.Length)
            return false;

        byte c = s[pos];
        if (IsCommandChar(c))
        {
            pos++;
            token = Token.Cmd(ToLowerAscii(c));
            return true;
        }

        int start = pos;
        pos++;
        while ((uint)pos < (uint)s.Length)
        {
            byte ch = s[pos];
            if (ch == (byte)',' || ch == (byte)' ' || ch == (byte)'\t' || ch == (byte)'\r' || ch == (byte)'\n' || IsCommandChar(ch))
                break;
            pos++;
        }

        var span = s.Slice(start, pos - start);
        if (!Utf8Parser.TryParse(span, out double value, out int consumed) || consumed != span.Length)
            return false;

        token = Token.Num(value);
        return true;
    }

    private static bool IsCommandChar(byte c)
    {
        byte lo = ToLowerAscii(c);
        return lo is >= (byte)'a' and <= (byte)'z';
    }

    private static byte ToLowerAscii(byte c)
        => (byte)(c | 0x20);
}
