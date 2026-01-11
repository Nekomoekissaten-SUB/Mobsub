﻿using Mobsub.SubtitleParse.AssTypes;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace Mobsub.SubtitleParse;

public class Utils
{
    public static Encoding GuessEncoding(byte[] buffer)
    {
        return buffer switch
        {
            [0xFF, 0xFE, ..] => Encoding.Unicode,           // UTF-16 (Little-Endian)
            [0xFE, 0xFF, ..] => Encoding.BigEndianUnicode,  // UTF-16 (Big-Endian)
            [0xEF, 0xBB, 0xBF, ..] => Encoding.UTF8,        // UTF-8
            _ => new UTF8Encoding(false)                    // UTF-8 without Bom
        };
    }

    public static Encoding GuessEncoding(ReadOnlySpan<byte> buffer, out int preambleLength)
    {
        if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            preambleLength = 3;
            return Encoding.UTF8;
        }
        if (buffer.Length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
        {
            preambleLength = 2;
            return Encoding.Unicode;
        }
        if (buffer.Length >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
        {
            preambleLength = 2;
            return Encoding.BigEndianUnicode;
        }

        preambleLength = 0;
        return new UTF8Encoding(false);
    }

    public static Encoding EncodingRefOS()
    {
        switch (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            case false:
                return new UTF8Encoding(false);
            default:
                return Encoding.UTF8;
        }
    }

    public static void GuessEncoding(Stream fs, out Encoding charEncoding)
    {
        var buffer = new byte[4];
        _ = fs.Read(buffer, 0, 4);
        charEncoding = GuessEncoding(buffer);
        fs.Seek(0, SeekOrigin.Begin);
    }

    public static void GuessEncoding(Stream fs, out Encoding charEncoding, out bool isCarriageReturn)
    {
        var buffer = new byte[1024];
        var b = fs.Read(buffer, 0, 1024);
        charEncoding = GuessEncoding(buffer);
        isCarriageReturn = false;
        if (b > 0)
        {
            for (var i = 0; i < b - 1; i++)
            {
                if (buffer[i] == 0x0D && buffer[i + 1] == 0x0A)
                {
                    isCarriageReturn = true;
                }
            }
        }
        fs.Seek(0, SeekOrigin.Begin);
    }

    public static void SetProperty(object obj, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type T, string propertyName, ReadOnlySpan<char> value)
    {
        var property = T.GetProperty(propertyName);
        if (property == null)
        {
            return;
        }

        object? typedValue = null;
        if (property.PropertyType == typeof(AssRGB8))
        {
            typedValue = AssRGB8.Parse(value);
        }
        else if (property.PropertyType == typeof(bool))
        {
            if (short.TryParse(value, out var shortValue))
            {
                typedValue = shortValue == -1;
            }
            else
            {
                typedValue = value.SequenceEqual("yes".AsSpan());
            }
        }
        else if (property.PropertyType == typeof(AssTime))
        {
            typedValue = AssTime.ParseFromAss(value);
        }
        else if (property.PropertyType == typeof(int))
        {
            if (int.TryParse(value, out var target))
            {
                typedValue = target;
            }
        }
        else if (property.PropertyType == typeof(string))
        {
            typedValue = value.ToString();
        }
        else
        {
            try
            {
                typedValue = Convert.ChangeType(value.ToString(), property.PropertyType);
            }
            catch (InvalidCastException)
            {
                // Handle exception
            }
        }

        if (typedValue != null)
        {
            property.SetValue(obj, typedValue);
        }
    }
    public static void SetProperty(object obj, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type T, string propertyName, ReadOnlySpan<byte> value)
    {
        var property = T.GetProperty(propertyName);
        if (property == null)
        {
            return;
        }

        object? typedValue = null;
        if (property.PropertyType == typeof(AssRGB8))
        {
            typedValue = AssRGB8.Parse(value);
        }
        else if (property.PropertyType == typeof(bool))
        {
            if (short.TryParse(value, out var shortValue))
            {
                typedValue = shortValue == -1;
            }
            else
            {
                typedValue = value.SequenceEqual("yes"u8);
            }
        }
        else if (property.PropertyType == typeof(AssTime))
        {
            typedValue = AssTime.ParseFromAss(value);
        }
        else if (property.PropertyType == typeof(int))
        {
            if (int.TryParse(value, out var target))
            {
                typedValue = target;
            }
        }
        else if (property.PropertyType == typeof(string))
        {
            typedValue = GetString(value);
        }
        else
        {
            try
            {
                typedValue = Convert.ChangeType(GetString(value), property.PropertyType);
            }
            catch (InvalidCastException)
            {
                // Handle exception
            }
        }

        if (typedValue != null)
        {
            property.SetValue(obj, typedValue);
        }
    }

    public static void SetRangeProperty(object obj, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type T, string propertyName, Range value)
    {
        var property = T.GetProperty(propertyName);
        if (property == null)
        {
            return;
        }
        property.SetValue(obj, value);
    }

    public static bool IsStringInFields(object? obj, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] Type T, string searchString)
    {
        if (obj == null || string.IsNullOrEmpty(searchString))
        {
            return false;
        }
        foreach (var field in T.GetFields())
        {
            if (field.FieldType != typeof(string)) continue;
            try
            {
                var value = (string?)field.GetValue(obj);
                if (value != null && value.Contains(searchString))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // Handle exception
            }
        }
        return false;
    }

    internal static bool TrySplitKeyValue(ReadOnlySpan<char> sp, out string key, out string value)
    {
        var sepIndex = sp.IndexOf(':');
        if (sepIndex < 1)
        {
            key = value = string.Empty;
            return false;
        }

        key = sp[..sepIndex].ToString();
        value = sp[(sepIndex + 1)..].Trim().ToString();
        return true;
    }

    public static bool ReplaceFirst(ref Span<char> span, string oldValue, string newValue)
    {
        var oldSpan = oldValue.AsSpan();
        var idx = span.IndexOf(oldSpan);

        if (idx == -1) return false;
        var newSpan = newValue.AsSpan();
        var replacement = span.Slice(idx, oldSpan.Length);

        if (newSpan.Length != oldSpan.Length)
        {
            var newLength = span.Length - oldSpan.Length + newSpan.Length;
            var newBuffer = new char[newLength].AsSpan();

            span.Slice(0, idx).CopyTo(newBuffer);
            newSpan.CopyTo(newBuffer.Slice(idx));
            span.Slice(idx + oldSpan.Length).CopyTo(newBuffer.Slice(idx + newSpan.Length));

            span = newBuffer;
        }
        else
        {
            newSpan.CopyTo(replacement);
        }
        return true;
    }

    internal static string[] SplitBySeparator(ReadOnlySpan<char> sp)
    {
        var ranges = SplitBySeparatorInternal(sp, out var splitCount);
        var arr = new string [splitCount];
        var i = 0;
        foreach (var range in ranges)
        {
            arr[i] = sp[range].ToString();
            i++;
        }

        return arr;
    }

    internal static Span<Range> SplitBySeparatorInternal(ReadOnlySpan<char> sp, out int splitCount)
    {
        var count = sp.Count(',') + 1;
        Span<Range> ranges = new Range[count];
        splitCount = sp.Split(ranges, ',', StringSplitOptions.TrimEntries);
        return ranges;
    }

    internal static ReadOnlySpan<char> AssParseStyleName(ReadOnlySpan<char> sp)
    {
        // https://sourceforge.net/p/guliverkli2/code/HEAD/tree/src/subtitles/STS.cpp#l1524
        // https://sourceforge.net/p/guliverkli2/code/HEAD/tree/src/subtitles/STS.cpp#l1490
        var spFixed = sp.TrimStart('*');
        if (spFixed.Length == 0 || MemoryExtensions.Equals(spFixed, "default", StringComparison.OrdinalIgnoreCase))
        {
            return "Default";
        }
        return spFixed;
    }

    internal static string GetString(ReadOnlyMemory<byte> bytes) => GetString(bytes.Span);
    internal static string GetString(ReadOnlyMemory<byte> bytes, Range range, bool trimSpaces = false) => GetString(bytes.Span, range, trimSpaces);
    internal static string GetString(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes);
    internal static string GetString(ReadOnlySpan<byte> bytes, Range range, bool trimSpaces = false) =>
        trimSpaces ? Encoding.UTF8.GetString(TrimSpaces(bytes[range])) : Encoding.UTF8.GetString(bytes[range]);

    internal static ReadOnlySpan<byte> TrimSpaces(ReadOnlySpan<byte> span)
    {
        TrimSpaces(span, out int start, out int length);
        return span.Slice(start, length);
    }

    internal static void TrimSpaces(ReadOnlySpan<byte> span, out int start, out int length)
    {
        start = 0;
        int end = span.Length;
        while (start < end && IsSpace(span[start])) start++;
        while (end > start && IsSpace(span[end - 1])) end--;
        length = end - start;
    }

    private static bool IsSpace(byte b)
        => b == (byte)' '; // || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n'

    internal static void SkipSpaces(ref ReadOnlySpan<byte> span)
    {
        int i = 0;
        while (i < span.Length && span[i] == (byte)' ') i++;
        span = span[i..];
    }

    internal static bool TryConsume(ref ReadOnlySpan<byte> span, byte expected)
    {
        SkipSpaces(ref span);
        if (span.IsEmpty || span[0] != expected)
            return false;
        span = span[1..];
        return true;
    }

    internal static bool TryGetParenContent(ReadOnlySpan<byte> payload, out ReadOnlySpan<byte> inner)
    {
        inner = payload;
        // Allow surrounding spaces around the whole "( ... )" payload.
        inner = TrimSpaces(inner);
        if (inner.Length < 2 || inner[0] != (byte)'(' || inner[^1] != (byte)')')
            return false;
        inner = inner[1..^1];
        return true;
    }

    internal static bool TryReadInt(ref ReadOnlySpan<byte> span, out int value)
    {
        value = 0;
        SkipSpaces(ref span);
        if (!Utf8Parser.TryParse(span, out int v, out int consumed))
            return false;
        value = v;
        span = span[consumed..];
        return true;
    }

    internal static bool TryParseIntLoose(ReadOnlySpan<byte> span, out int value, out bool invalid)
    {
        value = 0;
        invalid = false;
        span = TrimSpaces(span);
        if (span.IsEmpty)
            return false;

        int prefix = ScanIntPrefix(span);
        if (prefix == 0)
        {
            invalid = true;
            return true;
        }

        if (!Utf8Parser.TryParse(span[..prefix], out value, out _))
        {
            value = 0;
            invalid = true;
            return true;
        }

        if (prefix < span.Length)
            invalid = true;

        return true;
    }

    internal static bool TryReadDouble(ref ReadOnlySpan<byte> span, out double value)
    {
        value = 0;
        SkipSpaces(ref span);
        if (!Utf8Parser.TryParse(span, out double v, out int consumed))
            return false;
        value = v;
        span = span[consumed..];
        return true;
    }

    internal static bool TryParseDoubleLoose(ReadOnlySpan<byte> span, out double value, out bool invalid)
    {
        value = 0;
        invalid = false;
        span = TrimSpaces(span);
        if (span.IsEmpty)
            return false;

        int prefix = ScanDoublePrefix(span);
        if (prefix == 0)
        {
            invalid = true;
            return true;
        }

        if (!Utf8Parser.TryParse(span[..prefix], out value, out _))
        {
            value = 0;
            invalid = true;
            return true;
        }

        if (prefix < span.Length)
            invalid = true;

        return true;
    }

    internal static int ParseInt(ReadOnlySpan<byte> span)
    {
        span = TrimSpaces(span);
        if (!Utf8Parser.TryParse(span, out int value, out int consumed) || consumed != span.Length)
            throw new FormatException($"Invalid int: {GetString(span)}");
        return value;
    }

    internal static sbyte ParseSByte(ReadOnlySpan<byte> span)
    {
        span = TrimSpaces(span);
        if (!Utf8Parser.TryParse(span, out sbyte value, out int consumed) || consumed != span.Length)
            throw new FormatException($"Invalid sbyte: {GetString(span)}");
        return value;
    }

    internal static byte ParseByte(ReadOnlySpan<byte> span)
    {
        span = TrimSpaces(span);
        if (!Utf8Parser.TryParse(span, out byte value, out int consumed) || consumed != span.Length)
            throw new FormatException($"Invalid byte: {GetString(span)}");
        return value;
    }

    internal static double ParseDouble(ReadOnlySpan<byte> span)
    {
        span = TrimSpaces(span);
        if (!Utf8Parser.TryParse(span, out double value, out int consumed) || consumed != span.Length)
            throw new FormatException($"Invalid double: {GetString(span)}");
        return value;
    }

    private static int ScanIntPrefix(ReadOnlySpan<byte> span)
    {
        int i = 0;
        if (span.Length == 0)
            return 0;

        if (span[0] == (byte)'+' || span[0] == (byte)'-')
            i++;

        int digitStart = i;
        while (i < span.Length && span[i] >= (byte)'0' && span[i] <= (byte)'9')
            i++;

        return i == digitStart ? 0 : i;
    }

    private static int ScanDoublePrefix(ReadOnlySpan<byte> span)
    {
        int i = 0;
        if (span.Length == 0)
            return 0;

        if (span[0] == (byte)'+' || span[0] == (byte)'-')
            i++;

        int digitStart = i;
        while (i < span.Length && span[i] >= (byte)'0' && span[i] <= (byte)'9')
            i++;
        bool hasDigits = i > digitStart;

        if (i < span.Length && span[i] == (byte)'.')
        {
            i++;
            int fracStart = i;
            while (i < span.Length && span[i] >= (byte)'0' && span[i] <= (byte)'9')
                i++;
            hasDigits = hasDigits || i > fracStart;
        }

        if (!hasDigits)
            return 0;

        int expStart = i;
        if (i < span.Length && (span[i] == (byte)'e' || span[i] == (byte)'E'))
        {
            int j = i + 1;
            if (j < span.Length && (span[j] == (byte)'+' || span[j] == (byte)'-'))
                j++;

            int expDigitsStart = j;
            while (j < span.Length && span[j] >= (byte)'0' && span[j] <= (byte)'9')
                j++;

            if (j > expDigitsStart)
            {
                i = j;
            }
            else
            {
                i = expStart;
            }
        }

        return i;
    }
}
