namespace Mobsub.SubtitleParse.AssTypes;

/// <summary>
/// RGB + ASS alpha (0 opaque .. 255 transparent).
/// </summary>
public readonly struct AssColor32 : IEquatable<AssColor32>
{
    public AssColor32(byte r, byte g, byte b, AssAlpha8 alpha = default)
    {
        R = r;
        G = g;
        B = b;
        Alpha = alpha;
    }

    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public AssAlpha8 Alpha { get; }

    /// <summary>
    /// Alias for <see cref="Alpha"/>.Value to ease migration from RGBA-like structs.
    /// Note: this is ASS alpha semantics (0 opaque .. 255 transparent).
    /// </summary>
    public byte A => Alpha.Value;

    public AssColor32 WithAlpha(AssAlpha8 alpha) => new(R, G, B, alpha);

    public static bool TryParseAssStyleColor(ReadOnlySpan<char> text, out AssColor32 color)
        => AssColorParser.TryParseAssStyleColor(text, out color);

    public static bool TryParseAssOverrideColor(ReadOnlySpan<char> text, out AssColor32 color)
        => AssColorParser.TryParseAssOverrideColor(text, out color);

    public static AssColor32 ParseAssStyleColor(ReadOnlySpan<char> text)
        => AssColorParser.TryParseAssStyleColor(text, out var c) ? c : throw new FormatException("Invalid ASS style color.");

    public static AssColor32 ParseAssOverrideColor(ReadOnlySpan<char> text)
        => AssColorParser.TryParseAssOverrideColor(text, out var c) ? c : throw new FormatException("Invalid ASS override color.");

    // --- SubtitleParse/AssRGB8 compatibility surface (type replaced with AssColor32) ---

    public static AssColor32 Parse(ReadOnlySpan<char> sp)
    {
        if (!AssColorParser.TryParseAssHex(sp, out uint raw, out int digits))
            throw new FormatException("Invalid color format");

        byte r = (byte)(raw & 0xFF);
        byte g = (byte)((raw >> 8) & 0xFF);
        byte b = (byte)((raw >> 16) & 0xFF);
        byte a = (byte)((raw >> 24) & 0xFF);
        return new AssColor32(r, g, b, alpha: a);
    }

    public static AssColor32 Parse(ReadOnlySpan<byte> sp)
    {
        // Keep behavior close to SubtitleParse.AssRGB8.Parse(ReadOnlySpan<byte>).
        if (!AssColorParser.TryParseAssHex(sp, out uint raw, out int digits, out bool invalid) || invalid)
            throw new FormatException("Invalid color format");

        if ((digits & 1) != 0 || digits == 0 || digits > 8)
            throw new FormatException("Invalid color format");

        byte r = (byte)(raw & 0xFF);
        byte g = (byte)((raw >> 8) & 0xFF);
        byte b = (byte)((raw >> 16) & 0xFF);
        byte a = (byte)((raw >> 24) & 0xFF);
        return new AssColor32(r, g, b, alpha: a);
    }

    public static bool TryParseAlphaByte(ReadOnlySpan<byte> sp, out byte value)
        => TryParseAlphaByte(sp, out value, out _);

    public static bool TryParseAlphaByte(ReadOnlySpan<byte> sp, out byte value, out bool invalid)
    {
        value = 0;
        if (!AssColorParser.TryParseAssHex(sp, out uint raw, out _, out invalid))
            return false;
        value = (byte)raw;
        return true;
    }

    public static bool TryParseTagColor(ReadOnlySpan<byte> sp, out AssColor32 color, out bool ignoredHighByte, out bool invalid)
    {
        color = default;
        ignoredHighByte = false;

        if (!AssColorParser.TryParseAssHex(sp, out uint raw, out int digits, out invalid))
            return false;

        ignoredHighByte = digits > 6;
        raw &= 0x00FF_FFFF;

        byte r = (byte)(raw & 0xFF);
        byte g = (byte)((raw >> 8) & 0xFF);
        byte b = (byte)((raw >> 16) & 0xFF);
        color = new AssColor32(r, g, b, alpha: 0);
        return true;
    }

    public string ConvertToString(bool withAlpha = false, bool onlyAlpha = false)
    {
        // Keep output parity with SubtitleParse.AssRGB8.ConvertToString:
        // - onlyAlpha => "AA"
        // - withAlpha => "AABBGGRR"
        // - default   => "BBGGRR"
        if (onlyAlpha)
        {
            Span<char> tmp = stackalloc char[2];
            WriteHexByteUpper(A, tmp);
            return new string(tmp);
        }

        if (withAlpha)
        {
            Span<char> tmp = stackalloc char[8];
            WriteHexByteUpper(A, tmp.Slice(0, 2));
            WriteHexByteUpper(B, tmp.Slice(2, 2));
            WriteHexByteUpper(G, tmp.Slice(4, 2));
            WriteHexByteUpper(R, tmp.Slice(6, 2));
            return new string(tmp);
        }

        Span<char> tmp6 = stackalloc char[6];
        WriteHexByteUpper(B, tmp6.Slice(0, 2));
        WriteHexByteUpper(G, tmp6.Slice(2, 2));
        WriteHexByteUpper(R, tmp6.Slice(4, 2));
        return new string(tmp6);
    }

    private static void WriteHexByteUpper(byte value, Span<char> dest2)
    {
        const string HexChars = "0123456789ABCDEF";
        dest2[0] = HexChars[value >> 4];
        dest2[1] = HexChars[value & 0xF];
    }

    public bool Equals(AssColor32 other)
        => R == other.R && G == other.G && B == other.B && Alpha.Equals(other.Alpha);

    public override bool Equals(object? obj) => obj is AssColor32 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(R, G, B, Alpha.Value);

    public static bool operator ==(AssColor32 left, AssColor32 right) => left.Equals(right);
    public static bool operator !=(AssColor32 left, AssColor32 right) => !left.Equals(right);
}
