using System.Buffers;
using System.Buffers.Text;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal sealed class AmoSrsData : AmoData
{
    public override AmoDataKind Kind => AmoDataKind.Srs;
    public override int Length => _drawingsUtf8.Length - 1; // 1-based

    private readonly byte[][] _drawingsUtf8;

    private AmoSrsData(byte[][] drawingsUtf8)
    {
        _drawingsUtf8 = drawingsUtf8;
    }

    public static AmoSrsData ParseShakeShape(ReadOnlySpan<byte> textUtf8, int scriptHeight, out string? error)
    {
        error = null;
        if (textUtf8.IsEmpty || !textUtf8.StartsWith("shake_shape_data 4.0"u8))
        {
            error = "shake_shape_data_title_missing";
            return new AmoSrsData([Array.Empty<byte>()]);
        }

        if (!TryParseNumShapes(textUtf8, out int numShapes) || numShapes <= 0)
        {
            error = "num_shapes_missing_or_invalid";
            return new AmoSrsData([Array.Empty<byte>()]);
        }

        var vertexLines = new List<(int Start, int Len)>(capacity: 4096);
        int pos = 0;
        while (pos < textUtf8.Length)
        {
            int lineStart = pos;
            var line = ReadLine(textUtf8, ref pos);
            line = TrimEndAsciiWhitespace(line);
            if (line.IsEmpty)
                continue;
            if (line.IndexOf("vertex_data"u8) < 0)
                continue;
            vertexLines.Add((lineStart, line.Length));
        }

        if (vertexLines.Count == 0 || vertexLines.Count % numShapes != 0)
        {
            error = "vertex_data_count_mismatch";
            return new AmoSrsData([Array.Empty<byte>()]);
        }

        int length = vertexLines.Count / numShapes;
        var drawings = new byte[length + 1][];
        drawings[0] = Array.Empty<byte>();

        for (int baseIndex = 0; baseIndex < length; baseIndex++)
        {
            var writer = new ArrayBufferWriter<byte>(initialCapacity: 256);
            for (int shape = 0; shape < numShapes; shape++)
            {
                int idx = baseIndex + (shape * length);
                var (start, len) = vertexLines[idx];
                var vertexLine = textUtf8.Slice(start, len);

                WriteVertexDrawing(writer, vertexLine, scriptHeight);

                if (shape != numShapes - 1)
                    writer.Write(" "u8);
            }

            drawings[baseIndex + 1] = writer.WrittenSpan.ToArray();
        }

        return new AmoSrsData(drawings);
    }

    public override ReadOnlySpan<byte> GetSrsDrawingUtf8(int frame)
    {
        int n = Length;
        if (n <= 0)
            return default;

        if (frame < 1) frame = 1;
        if (frame > n) frame = n;
        return _drawingsUtf8[frame];
    }

    private static bool TryParseNumShapes(ReadOnlySpan<byte> textUtf8, out int numShapes)
    {
        numShapes = 0;

        int idx = textUtf8.IndexOf("num_shapes "u8);
        if (idx < 0)
            return false;
        idx += "num_shapes ".Length;

        int end = idx;
        while (end < textUtf8.Length && textUtf8[end] >= (byte)'0' && textUtf8[end] <= (byte)'9')
            end++;

        if (end <= idx)
            return false;

        var digits = textUtf8.Slice(idx, end - idx);
        return Utf8Parser.TryParse(digits, out numShapes, out int consumed) && consumed == digits.Length;
    }

    private static void WriteVertexDrawing(IBufferWriter<byte> writer, ReadOnlySpan<byte> vertex, int scriptHeight)
    {
        Span<double> buf = stackalloc double[12];
        int bufCount = 0;
        int vertexCount = 0;

        Curve first = default;
        Curve prev = default;
        bool hasOutput = false;

        int i = 0;
        while (TryReadNextNumberToken(vertex, ref i, out var token))
        {
            if (!Utf8Parser.TryParse(token, out double d, out int consumed) || consumed != token.Length)
                continue;

            buf[bufCount++] = d;
            if (bufCount != 12)
                continue;

            var cur = ReadCurve(buf, scriptHeight);
            if (vertexCount == 0)
            {
                first = cur;
                prev = cur;

                writer.Write("m "u8);
                WriteDouble(writer, cur.Vx);
                writer.Write(" "u8);
                WriteDouble(writer, cur.Vy);
                writer.Write(" b "u8);
                hasOutput = true;
            }
            else
            {
                WriteDouble(writer, prev.Rx);
                writer.Write(" "u8);
                WriteDouble(writer, prev.Ry);
                writer.Write(" "u8);
                WriteDouble(writer, cur.Lx);
                writer.Write(" "u8);
                WriteDouble(writer, cur.Ly);
                writer.Write(" "u8);
                WriteDouble(writer, cur.Vx);
                writer.Write(" "u8);
                WriteDouble(writer, cur.Vy);
                writer.Write(" "u8);
                prev = cur;
            }

            vertexCount++;
            bufCount = 0;
        }

        if (!hasOutput || vertexCount < 2)
        {
            writer.Write("m 0 0"u8);
            return;
        }

        WriteDouble(writer, prev.Rx);
        writer.Write(" "u8);
        WriteDouble(writer, prev.Ry);
        writer.Write(" "u8);
        WriteDouble(writer, first.Lx);
        writer.Write(" "u8);
        WriteDouble(writer, first.Ly);
        writer.Write(" "u8);
        WriteDouble(writer, first.Vx);
        writer.Write(" "u8);
        WriteDouble(writer, first.Vy);
    }

    private readonly record struct Curve(double Vx, double Vy, double Lx, double Ly, double Rx, double Ry);

    private static Curve ReadCurve(ReadOnlySpan<double> nums12, int h)
    {
        double vx = nums12[0];
        double vy = h - nums12[1];
        double lx = nums12[2];
        double ly = h - nums12[3];
        double rx = nums12[4];
        double ry = h - nums12[5];
        return new Curve(vx, vy, lx, ly, rx, ry);
    }

    private static void WriteDouble(IBufferWriter<byte> writer, double value)
    {
        Span<byte> tmp = stackalloc byte[64];
        if (!Utf8Formatter.TryFormat(value, tmp, out int written))
            throw new InvalidOperationException("Failed to format double.");
        writer.Write(tmp[..written]);
    }

    private static bool TryReadNextNumberToken(ReadOnlySpan<byte> s, ref int i, out ReadOnlySpan<byte> token)
    {
        token = default;

        while (i < s.Length && !IsNumberStart(s[i]))
            i++;

        if (i >= s.Length)
            return false;

        int start = i;
        i++;

        while (i < s.Length && IsNumberChar(s[i]))
            i++;

        token = s.Slice(start, i - start);
        return true;
    }

    private static bool IsNumberStart(byte b)
        => b == (byte)'-' || b == (byte)'.' || (b >= (byte)'0' && b <= (byte)'9');

    private static bool IsNumberChar(byte b)
        => b == (byte)'-' || b == (byte)'+' || b == (byte)'.' || b == (byte)'e' || b == (byte)'E'
            || (b >= (byte)'0' && b <= (byte)'9');

    private static ReadOnlySpan<byte> ReadLine(ReadOnlySpan<byte> s, ref int pos)
    {
        if (pos >= s.Length)
            return ReadOnlySpan<byte>.Empty;

        int start = pos;
        while (pos < s.Length && s[pos] != (byte)'\n' && s[pos] != (byte)'\r')
            pos++;

        var line = s.Slice(start, pos - start);
        if (pos < s.Length && s[pos] == (byte)'\r')
            pos++;
        if (pos < s.Length && s[pos] == (byte)'\n')
            pos++;
        return line;
    }

    private static ReadOnlySpan<byte> TrimEndAsciiWhitespace(ReadOnlySpan<byte> s)
    {
        int end = s.Length;
        while (end > 0 && IsAsciiWhitespace(s[end - 1]))
            end--;
        return end == s.Length ? s : s[..end];
    }

    private static bool IsAsciiWhitespace(byte b)
        => b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
}
