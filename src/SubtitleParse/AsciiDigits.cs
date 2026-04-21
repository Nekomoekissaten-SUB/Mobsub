using System;
using System.Runtime.CompilerServices;

namespace Mobsub.SubtitleParse;

internal static class AsciiDigits
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseDigit(byte c, out int digit)
    {
        int d = c - (byte)'0';
        if ((uint)d <= 9)
        {
            digit = d;
            return true;
        }

        digit = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseDigit(char c, out int digit)
    {
        int d = c - '0';
        if ((uint)d <= 9)
        {
            digit = d;
            return true;
        }

        digit = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseDigitPair(ReadOnlySpan<byte> span, int start, out int value)
    {
        value = 0;
        if ((uint)(start + 1) >= (uint)span.Length)
            return false;

        int high = span[start] - (byte)'0';
        int low = span[start + 1] - (byte)'0';
        if ((uint)high > 9 || (uint)low > 9)
            return false;

        value = (high * 10) + low;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseDigitPair(ReadOnlySpan<char> span, int start, out int value)
    {
        value = 0;
        if ((uint)(start + 1) >= (uint)span.Length)
            return false;

        int high = span[start] - '0';
        int low = span[start + 1] - '0';
        if ((uint)high > 9 || (uint)low > 9)
            return false;

        value = (high * 10) + low;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write2Digits(Span<char> dest2, int value)
    {
        if (dest2.Length < 2)
            throw new ArgumentException("Destination span is too small.", nameof(dest2));

        dest2[0] = (char)('0' + (value / 10));
        dest2[1] = (char)('0' + (value % 10));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write2Digits(Span<byte> dest2, int value)
    {
        if (dest2.Length < 2)
            throw new ArgumentException("Destination span is too small.", nameof(dest2));

        dest2[0] = (byte)('0' + (value / 10));
        dest2[1] = (byte)('0' + (value % 10));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write3Digits(Span<char> dest3, int value)
    {
        if (dest3.Length < 3)
            throw new ArgumentException("Destination span is too small.", nameof(dest3));

        dest3[0] = (char)('0' + (value / 100));
        dest3[1] = (char)('0' + (value / 10 % 10));
        dest3[2] = (char)('0' + (value % 10));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write3Digits(Span<byte> dest3, int value)
    {
        if (dest3.Length < 3)
            throw new ArgumentException("Destination span is too small.", nameof(dest3));

        dest3[0] = (byte)('0' + (value / 100));
        dest3[1] = (byte)('0' + (value / 10 % 10));
        dest3[2] = (byte)('0' + (value % 10));
    }
}

