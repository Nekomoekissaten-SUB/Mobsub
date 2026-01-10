using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public struct AssRGB8(byte red, byte green, byte blue, byte alpha)
{
    public byte R = red;
    public byte G = green;
    public byte B = blue;
    public byte A = alpha;

    private static readonly sbyte[] HexLut = BuildHexLut();

    public static AssRGB8 Parse(ReadOnlySpan<char> sp)
    {
        sp = sp.Trim();
        byte r = 0, g = 0, b = 0, a = 0;
        var sign = (sp[^1] == '&') ? 3 : 2;

        if ((sp[0] != '&') || (sp[1] != 'H') || ((sp.Length - sign) % 2 != 0))
        {
            throw new Exception($"Invalid color: {sp}");
        }

        var loop = 0;
        for (int i = sp.Length - sign + 1; i > 1; i -= 2)
        {
            var n = Convert.ToByte(HexCharToInt(sp[i - 1]) * 16 + HexCharToInt(sp[i]));

            switch (loop)
            {
                case 0:
                    r = n;
                    break;
                case 1:
                    g = n;
                    break;
                case 2:
                    b = n;
                    break;
                case 3:
                    a = n;
                    break;
                default:
                    throw new Exception($"Invalid color: {sp}");
            }

            loop += 1;
        }

        return new AssRGB8(r, g, b, a);
    }
    public static AssRGB8 Parse(ReadOnlySpan<byte> sp)
    {
        // Optional: trim ASCII spaces if needed
        if (sp.StartsWith((byte)' ') || sp.EndsWith((byte)' ')) sp = Utils.TrimSpaces(sp);

        if (sp.Length < 3) throw new FormatException("Invalid color format");
        if (sp[0] != (byte)'&' || (sp[1] != (byte)'H' && sp[1] != (byte)'h'))
            throw new FormatException("Invalid color prefix");

        // Determine end of hex digits (exclude optional trailing '&')
        int end = sp[^1] == (byte)'&' ? sp.Length - 1 : sp.Length;
        if (end <= 2) throw new FormatException("Missing hex digits");

        // Extract raw hex digits
        ReadOnlySpan<byte> hex = sp.Slice(2, end - 2);
        if (hex.Length == 0) throw new FormatException("Missing hex digits");

        // Validate hex chars and ensure even count
        for (int i = 0; i < hex.Length; i++)
        {
            byte c = hex[i];
            bool isHex =
                (c >= (byte)'0' && c <= (byte)'9') ||
                (c >= (byte)'A' && c <= (byte)'F') ||
                (c >= (byte)'a' && c <= (byte)'f');
            if (!isHex)
            {
                Debug.WriteLine(Utils.GetString(sp));
                throw new FormatException("Invalid hex digit");
            }
        }
        if ((hex.Length & 1) != 0) throw new FormatException("Hex digits must be even");

        // Decide target width: <=6 -> pad to 6 (BBGGRR), 7-8 -> pad to 8 (AABBGGRR)
        int target = hex.Length <= 6 ? 6 : 8;
        if (hex.Length > 8) throw new FormatException("Too many hex digits");

        // Pad-left with '0' to target length
        // We'll parse pairs from the right: RR, GG, BB, (AA if target==8)
        int pad = target - hex.Length;

        byte rr = 0, gg = 0, bb = 0, aa = 0;

        // Helper to read a pair at absolute index in the padded stream
        byte ReadPairAtPaddedIndex(ReadOnlySpan<byte> hex, int paddedIndex)
        {
            // Map paddedIndex into actual hex index: paddedIndex - pad
            int idx = paddedIndex - pad;
            byte hi = idx >= 0 ? hex[idx] : (byte)'0';
            byte lo = (idx + 1) >= 0 ? (idx + 1 < hex.Length ? hex[idx + 1] : (byte)'0') : (byte)'0';
            return ParseHexPair(hi, lo);
        }

        // Rightmost RR at positions target-2, target-1
        rr = ReadPairAtPaddedIndex(hex, target - 2);
        // Next GG at target-4, target-3
        gg = ReadPairAtPaddedIndex(hex, target - 4);
        // Next BB at target-6, target-5
        bb = ReadPairAtPaddedIndex(hex, target - 6);

        if (target == 8)
        {
            // Optional AA at target-8, target-7
            aa = ReadPairAtPaddedIndex(hex, target - 8);
        }

        return new AssRGB8(rr, gg, bb, aa);
    }

    public static bool TryParseAlphaByte(ReadOnlySpan<byte> sp, out byte value)
    {
        return TryParseAlphaByte(sp, out value, out _);
    }

    public static bool TryParseAlphaByte(ReadOnlySpan<byte> sp, out byte value, out bool invalid)
    {
        value = 0;
        if (!TryParseAssHex(sp, out var raw, out _, out invalid))
            return false;
        value = (byte)raw;
        return true;
    }

    public static bool TryParseTagColor(ReadOnlySpan<byte> sp, out AssRGB8 color, out bool ignoredHighByte, out bool invalid)
    {
        color = default;
        ignoredHighByte = false;
        if (!TryParseAssHex(sp, out var raw, out int digits, out invalid))
            return false;

        ignoredHighByte = digits > 6;
        raw &= 0xFFFFFF;
        color = new AssRGB8((byte)(raw & 0xFF), (byte)((raw >> 8) & 0xFF), (byte)((raw >> 16) & 0xFF), 0);
        return true;
    }

    private static bool TryParseAssHex(ReadOnlySpan<byte> sp, out uint value, out int digits, out bool invalid)
    {
        value = 0;
        digits = 0;
        invalid = false;
        sp = Utils.TrimSpaces(sp);
        if (sp.IsEmpty)
            return false;

        if (sp[^1] == (byte)'&')
            sp = sp[..^1];

        if (sp.IsEmpty)
            return false;

        if (sp[0] == (byte)'&')
            sp = sp[1..];
        if (!sp.IsEmpty && (sp[0] == (byte)'H' || sp[0] == (byte)'h'))
            sp = sp[1..];

        if (sp.IsEmpty)
            return false;

        for (int i = 0; i < sp.Length; i++)
        {
            int n = HexLut[sp[i]];
            if (n < 0)
            {
                invalid = true;
                break;
            }
            value = (value << 4) | (uint)n;
            digits++;
        }

        if (digits == 0)
        {
            invalid = true;
            return true;
        }

        if (digits < sp.Length)
            invalid = true;

        return true;
    }

    public readonly string ConvertToString(bool withAlpha = false, bool onlyAlpha = false)
    {
        var count = withAlpha ? 4 : 3;
        var startIndex = withAlpha ? 1 : 0;
        if (onlyAlpha) count = 1;
        var byteArray = new byte[count];

        if (onlyAlpha)
        {
            byteArray[0] = A;
            return Convert.ToHexString(byteArray);
        }

        if (withAlpha)
        {
            byteArray[0] = A;
        }

        byteArray[startIndex] = B;
        byteArray[startIndex + 1] = G;
        byteArray[startIndex + 2] = R;

        var str = Convert.ToHexString(byteArray);

        return str;
    }
    private static int HexCharToInt(char c)
    {
        if (c >= 'A' && c <= 'F')
        {
            return c - 55;
        }
        else if (c >= 'a' && c <= 'f')
        {
            return c - 87;
        }
        else if (c >= '0' && c <= '9')
        {
            return c - 48;
        }
        else
        {
            throw new Exception($"Invalid char: {c}");
        }
    }

    private static sbyte[] BuildHexLut()
    {
        var lut = new sbyte[256];
        for (int i = 0; i < lut.Length; i++) lut[i] = -1;
        for (int i = '0'; i <= '9'; i++) lut[i] = (sbyte)(i - '0');
        for (int i = 'A'; i <= 'F'; i++) lut[i] = (sbyte)(i - 'A' + 10);
        for (int i = 'a'; i <= 'f'; i++) lut[i] = (sbyte)(i - 'a' + 10);
        return lut;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ParseHexPair(byte hi, byte lo)
    {
        int v1 = HexLut[hi];
        int v2 = HexLut[lo];
        if ((v1 | v2) < 0) throw new FormatException($"Invalid hex: {(char)hi}{(char)lo}");
        return (byte)((v1 << 4) | v2);
    }
}
