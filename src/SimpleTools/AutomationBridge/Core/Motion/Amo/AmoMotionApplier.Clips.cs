using System.Buffers;
using System.Buffers.Text;
using Mobsub.SubtitleParse.AssText;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal static partial class AmoMotionApplier
{
    private static byte[] TransformDrawingUtf8(ReadOnlySpan<byte> drawingUtf8, AmoTsrData data, MotionTsrMath.TsrState state, double xDelta, double yDelta)
    {
        if (drawingUtf8.IsEmpty)
            return Array.Empty<byte>();

        var writer = new ArrayBufferWriter<byte>(drawingUtf8.Length + 32);

        int i = 0;
        while (i < drawingUtf8.Length)
        {
            // Copy until a number token.
            byte c = drawingUtf8[i];
            if (!IsNumberStart(c))
            {
                WriteByte(writer, c);
                i++;
                continue;
            }

            int xStart = i;
            i++;
            while ((uint)i < (uint)drawingUtf8.Length && IsNumberChar(drawingUtf8[i]))
                i++;

            ReadOnlySpan<byte> xToken = drawingUtf8.Slice(xStart, i - xStart);
            if (!Utf8Parser.TryParse(xToken, out double x, out int consumed) || consumed != xToken.Length)
            {
                writer.Write(xToken);
                continue;
            }

            int sp = i;
            while ((uint)sp < (uint)drawingUtf8.Length && drawingUtf8[sp] == (byte)' ')
                sp++;
            if (sp == i)
            {
                writer.Write(xToken);
                continue;
            }

            int yStart = sp;
            int yEnd = yStart;
            while ((uint)yEnd < (uint)drawingUtf8.Length && IsNumberChar(drawingUtf8[yEnd]))
                yEnd++;

            ReadOnlySpan<byte> yToken = drawingUtf8.Slice(yStart, yEnd - yStart);
            if (!Utf8Parser.TryParse(yToken, out double y, out consumed) || consumed != yToken.Length)
            {
                writer.Write(drawingUtf8.Slice(xStart, yEnd - xStart));
                i = yEnd;
                continue;
            }

            x += xDelta;
            y += yDelta;
            var (nx, ny) = MotionTsrMath.PositionMath(x, y, data.XStartPos, data.YStartPos, state);
            WriteCompact2(writer, nx);
            WriteByte(writer, (byte)' ');
            WriteCompact2(writer, ny);
            i = yEnd;
        }

        return writer.WrittenSpan.ToArray();
    }

    private static bool IsNumberStart(byte b)
        => b is (byte)'-' or (byte)'.' || (uint)(b - (byte)'0') <= 9;

    private static bool IsNumberChar(byte b)
        => b is (byte)'-' or (byte)'+' or (byte)'.' or (byte)'e' or (byte)'E' || (uint)(b - (byte)'0') <= 9;

    private static void WriteByte(IBufferWriter<byte> writer, byte b)
    {
        Span<byte> span = writer.GetSpan(1);
        span[0] = b;
        writer.Advance(1);
    }

    private static void WriteCompact2(IBufferWriter<byte> writer, double value)
    {
        Span<byte> tmp = stackalloc byte[64];
        if (!AssUtf8Number.TryFormatCompact2(value, tmp, out int written))
            return;
        writer.Write(tmp[..written]);
    }
}
