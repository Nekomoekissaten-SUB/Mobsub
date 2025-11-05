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
        sp = Utils.TrimSpaces(sp);
        if (sp.Length < 8 || sp[0] != (byte)'&' || sp[1] != (byte)'H')
            throw new FormatException("Invalid color format");

        int sign = (sp[^1] == (byte)'&') ? 3 : 2;
        int hexLen = sp.Length - sign;

        if (hexLen == 6)
        {
            // &HBBGGRR&
            byte r = ParseHexPair(sp[^3], sp[^2]);
            byte g = ParseHexPair(sp[^5], sp[^4]);
            byte b = ParseHexPair(sp[^7], sp[^6]);
            return new AssRGB8(r, g, b, 0x00);
        }
        else if (hexLen == 8)
        {
            // &HAABBGGRR
            byte r = ParseHexPair(sp[^2], sp[^1]);
            byte g = ParseHexPair(sp[^4], sp[^3]);
            byte b = ParseHexPair(sp[^6], sp[^5]);
            byte a = ParseHexPair(sp[^8], sp[^7]);
            return new AssRGB8(r, g, b, a);
        }

        throw new FormatException($"Invalid color length");
    }
    public static AssRGB8 Parse(long value, bool alpha)
    {
        var color = new AssRGB8();
        if (alpha)
        {
            color.A = (byte)((value & 0xff000000) >> 24);
            color.B = (byte)((value & 0x00ff0000) >> 16);
            color.G = (byte)((value & 0x0000ff00) >> 8);
            color.R = (byte)(value & 0x000000ff);
        }
        else
        {
            color.B = (byte)((value & 0xff0000) >> 16);
            color.G = (byte)((value & 0x00ff00) >> 8);
            color.R = (byte)(value & 0x0000ff);
        }

        return color;
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
