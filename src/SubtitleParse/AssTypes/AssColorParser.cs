using System.Runtime.CompilerServices;

namespace Mobsub.SubtitleParse.AssTypes;

public static class AssColorParser
{
    /// <summary>
    /// Parse ASS style color: canonical output is <c>&amp;HAABBGGRR</c> (no trailing '&amp;').
    /// Accepts 6 or 8 hex digits and optional leading <c>&amp;H</c>/<c>H</c>.
    /// </summary>
    public static bool TryParseAssStyleColor(ReadOnlySpan<char> text, out AssColor32 color)
    {
        color = default;

        text = NormalizeAssToken(text);
        if (text.IsEmpty)
            return false;

        // Only accept 6 or 8 digits (Aegisub does).
        if (text.Length is not (6 or 8))
            return false;

        if (!TryParseHexUInt(text, out uint raw))
            return false;

        byte r = (byte)(raw & 0xFF);
        byte g = (byte)((raw >> 8) & 0xFF);
        byte b = (byte)((raw >> 16) & 0xFF);
        byte a = (byte)((raw >> 24) & 0xFF);

        // 6 digits => Aegisub treats alpha as 0 (opaque).
        if (text.Length == 6)
            a = 0;

        color = new AssColor32(r, g, b, alpha: a);
        return true;
    }

    /// <summary>
    /// Parse ASS override/event color: canonical output is <c>&amp;HBBGGRR&amp;</c>.
    /// Accepts 6 or 8 hex digits and optional leading <c>&amp;H</c>/<c>H</c> and optional trailing '&amp;'.
    /// </summary>
    public static bool TryParseAssOverrideColor(ReadOnlySpan<char> text, out AssColor32 color)
    {
        // Same permissive surface syntax as style parsing, but semantics match Aegisub picker:
        // treat the value as BBGGRR (ignore alpha even if present).
        color = default;

        text = NormalizeAssToken(text);
        if (text.IsEmpty)
            return false;

        if (text.Length is not (6 or 8))
            return false;

        if (!TryParseHexUInt(text, out uint raw))
            return false;

        raw &= 0x00FF_FFFF;
        byte r = (byte)(raw & 0xFF);
        byte g = (byte)((raw >> 8) & 0xFF);
        byte b = (byte)((raw >> 16) & 0xFF);

        color = new AssColor32(r, g, b, alpha: 0);
        return true;
    }

    // --- Low-level helpers (used for SubtitleParse parity/migration) ---

    internal static bool TryParseAssHex(ReadOnlySpan<char> text, out uint raw, out int digits)
    {
        raw = 0;
        digits = 0;

        ReadOnlySpan<char> token = NormalizeAssToken(text);
        if (token.IsEmpty)
            return false;

        // Keep parity with SubtitleParse AssRGB8.Parse: accept any even count up to 8.
        if ((token.Length & 1) != 0 || token.Length > 8)
            return false;

        if (!TryParseHexUInt(token, out raw))
            return false;

        digits = token.Length;
        return true;
    }

    internal static bool TryParseAssHex(ReadOnlySpan<byte> text, out uint raw, out int digits, out bool invalid)
    {
        raw = 0;
        digits = 0;
        invalid = false;

        text = TrimSpacesAscii(text);
        if (text.IsEmpty)
            return false;

        if (text[^1] == (byte)'&')
            text = text[..^1];

        if (text.IsEmpty)
            return false;

        if (text[0] == (byte)'&')
            text = text[1..];
        if (!text.IsEmpty && (text[0] == (byte)'H' || text[0] == (byte)'h'))
            text = text[1..];

        if (text.IsEmpty)
            return false;

        for (int i = 0; i < text.Length; i++)
        {
            int n = Hex.TryGetNibbleAscii(text[i]);
            if (n < 0)
            {
                invalid = true;
                break;
            }
            raw = (raw << 4) | (uint)n;
            digits++;
        }

        if (digits == 0)
        {
            invalid = true;
            return true;
        }

        if (digits < text.Length)
            invalid = true;

        return true;
    }

    internal static ReadOnlySpan<byte> TrimSpacesAscii(ReadOnlySpan<byte> sp)
    {
        int start = 0;
        int end = sp.Length;
        while (start < end && sp[start] == (byte)' ') start++;
        while (end > start && sp[end - 1] == (byte)' ') end--;
        return sp.Slice(start, end - start);
    }

    internal static ReadOnlySpan<char> NormalizeAssToken(ReadOnlySpan<char> text)
    {
        text = text.Trim();
        if (text.IsEmpty)
            return default;

        if (text[^1] == '&')
            text = text[..^1];

        text = text.Trim();
        if (text.IsEmpty)
            return default;

        if (text[0] == '&')
            text = text[1..];

        if (!text.IsEmpty && (text[0] == 'H' || text[0] == 'h'))
            text = text[1..];

        return text.Trim();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryParseHexUInt(ReadOnlySpan<char> hexDigits, out uint value)
    {
        value = 0;
        for (int i = 0; i < hexDigits.Length; i++)
        {
            if (!Hex.TryGetNibble(hexDigits[i], out int n))
            {
                value = 0;
                return false;
            }
            value = (value << 4) | (uint)n;
        }
        return true;
    }

    public static bool TryParseHtmlHex(ReadOnlySpan<char> text, out AssColor32 color, bool allowAlpha = true)
    {
        // Accepts #RGB, #RRGGBB, and optionally #RRGGBBAA.
        color = default;
        text = text.Trim();
        if (text.IsEmpty)
            return false;

        if (text[0] == '#')
            text = text[1..];

        if (text.Length == 3)
        {
            if (!Hex.TryGetNibble(text[0], out int r1) ||
                !Hex.TryGetNibble(text[1], out int g1) ||
                !Hex.TryGetNibble(text[2], out int b1))
                return false;

            byte r = (byte)((r1 << 4) | r1);
            byte g = (byte)((g1 << 4) | g1);
            byte b = (byte)((b1 << 4) | b1);
            color = new AssColor32(r, g, b, alpha: 0);
            return true;
        }

        if (text.Length == 6)
        {
            if (!Hex.TryParseByte(text.Slice(0, 2), out byte r) ||
                !Hex.TryParseByte(text.Slice(2, 2), out byte g) ||
                !Hex.TryParseByte(text.Slice(4, 2), out byte b))
                return false;

            color = new AssColor32(r, g, b, alpha: 0);
            return true;
        }

        if (allowAlpha && text.Length == 8)
        {
            if (!Hex.TryParseByte(text.Slice(0, 2), out byte r) ||
                !Hex.TryParseByte(text.Slice(2, 2), out byte g) ||
                !Hex.TryParseByte(text.Slice(4, 2), out byte b) ||
                !Hex.TryParseByte(text.Slice(6, 2), out byte a))
                return false;

            // Interpret AA as ASS alpha for consistency with Aegisub's internal alpha.
            color = new AssColor32(r, g, b, alpha: a);
            return true;
        }

        return false;
    }
}
