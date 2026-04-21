using System.Runtime.CompilerServices;

namespace Mobsub.SubtitleParse.AssTypes;

internal static class Hex
{
    // ASCII -> nibble lookup. -1 for invalid.
    private static readonly sbyte[] Lut = BuildLut();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetNibble(char c, out int nibble)
    {
        if (c <= 0xFF)
        {
            int n = Lut[(byte)c];
            if (n >= 0)
            {
                nibble = n;
                return true;
            }
        }
        nibble = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TryGetNibbleAscii(byte c)
    {
        int n = Lut[c];
        return n;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseByte(ReadOnlySpan<char> twoHexDigits, out byte value)
    {
        if (twoHexDigits.Length != 2)
        {
            value = 0;
            return false;
        }

        if (!TryGetNibble(twoHexDigits[0], out int hi) || !TryGetNibble(twoHexDigits[1], out int lo))
        {
            value = 0;
            return false;
        }

        value = (byte)((hi << 4) | lo);
        return true;
    }

    private static sbyte[] BuildLut()
    {
        var lut = new sbyte[256];
        for (int i = 0; i < lut.Length; i++) lut[i] = -1;
        for (int i = '0'; i <= '9'; i++) lut[i] = (sbyte)(i - '0');
        for (int i = 'A'; i <= 'F'; i++) lut[i] = (sbyte)(i - 'A' + 10);
        for (int i = 'a'; i <= 'f'; i++) lut[i] = (sbyte)(i - 'a' + 10);
        return lut;
    }
}
