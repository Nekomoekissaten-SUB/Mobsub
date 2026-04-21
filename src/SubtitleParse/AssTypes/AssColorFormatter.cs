using System.Buffers;
using System.Buffers.Text;

namespace Mobsub.SubtitleParse.AssTypes;

public static class AssColorFormatter
{
    // Lengths of canonical strings:
    // - "&HAABBGGRR" => 10
    // - "&HBBGGRR&"  => 9
    // - "#RRGGBB"    => 7
    // - "#RRGGBBAA"  => 9

    public static bool TryFormatAssStyleColor(AssColor32 color, Span<char> destination, out int written)
    {
        // &H A A B B G G R R
        if (destination.Length < 10)
        {
            written = 0;
            return false;
        }

        destination[0] = '&';
        destination[1] = 'H';
        WriteHexByteUpper(color.Alpha.Value, destination.Slice(2, 2));
        WriteHexByteUpper(color.B, destination.Slice(4, 2));
        WriteHexByteUpper(color.G, destination.Slice(6, 2));
        WriteHexByteUpper(color.R, destination.Slice(8, 2));
        written = 10;
        return true;
    }

    public static bool TryFormatAssOverrideColor(AssColor32 color, Span<char> destination, out int written)
    {
        // &H B B G G R R &
        if (destination.Length < 9)
        {
            written = 0;
            return false;
        }

        destination[0] = '&';
        destination[1] = 'H';
        WriteHexByteUpper(color.B, destination.Slice(2, 2));
        WriteHexByteUpper(color.G, destination.Slice(4, 2));
        WriteHexByteUpper(color.R, destination.Slice(6, 2));
        destination[8] = '&';
        written = 9;
        return true;
    }

    public static bool TryFormatHtmlHex(AssColor32 color, bool includeAlpha, Span<char> destination, out int written)
    {
        // #RRGGBB[AA]
        int len = includeAlpha ? 9 : 7;
        if (destination.Length < len)
        {
            written = 0;
            return false;
        }

        destination[0] = '#';
        WriteHexByteUpper(color.R, destination.Slice(1, 2));
        WriteHexByteUpper(color.G, destination.Slice(3, 2));
        WriteHexByteUpper(color.B, destination.Slice(5, 2));
        if (includeAlpha)
            WriteHexByteUpper(color.Alpha.Value, destination.Slice(7, 2));

        written = len;
        return true;
    }

    public static string FormatAssStyleColor(AssColor32 color)
    {
        Span<char> tmp = stackalloc char[10];
        _ = TryFormatAssStyleColor(color, tmp, out _);
        return new string(tmp);
    }

    public static string FormatAssOverrideColor(AssColor32 color)
    {
        Span<char> tmp = stackalloc char[9];
        _ = TryFormatAssOverrideColor(color, tmp, out _);
        return new string(tmp);
    }

    public static string FormatHtmlHex(AssColor32 color, bool includeAlpha = false)
    {
        Span<char> tmp = stackalloc char[9];
        _ = TryFormatHtmlHex(color, includeAlpha, tmp, out int written);
        return new string(tmp[..written]);
    }

    private static void WriteHexByteUpper(byte value, Span<char> dest2)
    {
        // Use Utf8Formatter then map bytes to chars to avoid tables; still allocation-free.
        Span<byte> buf = stackalloc byte[2];
        Utf8Formatter.TryFormat(value, buf, out _, new StandardFormat('X', 2));
        dest2[0] = (char)buf[0];
        dest2[1] = (char)buf[1];
    }
}
