using System;
using System.Buffers;
using System.Buffers.Text;
using Mobsub.SubtitleParse;

namespace Mobsub.SubtitleParse.AssText;

public static class AssUtf8Number
{
    public static bool TryFormatCompact3(double value, Span<byte> dest, out int written)
    {
        // Keep the legacy a-mo shape: "0.###" (trim trailing zeros).
        value = Math.Round(value, 3, MidpointRounding.AwayFromZero);

        long scaled = (long)Math.Round(value * 1000.0, 0, MidpointRounding.AwayFromZero);
        if (scaled == 0)
        {
            written = 0;
            if (dest.IsEmpty)
                return false;
            dest[0] = (byte)'0';
            written = 1;
            return true;
        }

        bool neg = scaled < 0;
        // Handle long.MinValue safely.
        ulong abs = neg ? (ulong)(-(scaled + 1)) + 1UL : (ulong)scaled;

        ulong intPart = abs / 1000;
        int frac = (int)(abs % 1000);

        int pos = 0;
        if (neg)
        {
            if ((uint)pos >= (uint)dest.Length)
            {
                written = 0;
                return false;
            }
            dest[pos++] = (byte)'-';
        }

        if (!Utf8Formatter.TryFormat(intPart, dest[pos..], out int intWritten))
        {
            written = 0;
            return false;
        }
        pos += intWritten;

        if (frac == 0)
        {
            written = pos;
            return true;
        }

        if ((uint)pos >= (uint)dest.Length)
        {
            written = 0;
            return false;
        }
        dest[pos++] = (byte)'.';

        int d1 = frac / 100;
        int d2 = (frac / 10) % 10;
        int d3 = frac % 10;

        if (d3 == 0)
        {
            if (d2 == 0)
            {
                if ((uint)pos >= (uint)dest.Length)
                {
                    written = 0;
                    return false;
                }
                dest[pos++] = (byte)('0' + d1);
                written = pos;
                return true;
            }

            if (pos + 2 > dest.Length)
            {
                written = 0;
                return false;
            }
            AsciiDigits.Write2Digits(dest.Slice(pos, 2), (d1 * 10) + d2);
            pos += 2;
            written = pos;
            return true;
        }

        if (pos + 3 > dest.Length)
        {
            written = 0;
            return false;
        }
        AsciiDigits.Write3Digits(dest.Slice(pos, 3), frac);
        pos += 3;
        written = pos;
        return true;
    }

    public static bool TryFormatCompact2(double value, Span<byte> dest, out int written)
    {
        value = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        return TryFormatCompact3(value, dest, out written);
    }

    public static void WriteCompact3(IBufferWriter<byte> writer, double value)
    {
        if (writer == null)
            throw new ArgumentNullException(nameof(writer));

        Span<byte> tmp = stackalloc byte[24];
        if (!TryFormatCompact3(value, tmp, out int written))
            return;
        writer.Write(tmp[..written]);
    }

    public static void WriteCompact2(IBufferWriter<byte> writer, double value)
    {
        if (writer == null)
            throw new ArgumentNullException(nameof(writer));

        Span<byte> tmp = stackalloc byte[24];
        if (!TryFormatCompact2(value, tmp, out int written))
            return;
        writer.Write(tmp[..written]);
    }
}
