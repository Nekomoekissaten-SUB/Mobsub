using Mobsub.SubtitleParseNT2.AssTypes;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace Mobsub.SubtitleParseNT2;

public class Utils
{
    public static Encoding GuessEncoding(ReadOnlySpan<byte> buffer, out int preambleLength)
    {
        preambleLength = 0;
        if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            preambleLength = 3;
            return Encoding.UTF8;
        }
        if (buffer.Length >= 2)
        {
            if (buffer[0] == 0xFF && buffer[1] == 0xFE)
            {
                preambleLength = 2;
                return Encoding.Unicode;
            }
            if (buffer[0] == 0xFE && buffer[1] == 0xFF)
            {
                preambleLength = 2;
                return Encoding.BigEndianUnicode;
            }
        }
        
        return new UTF8Encoding(false);
    }
    
    public static Encoding GuessEncoding(byte[] buffer) => GuessEncoding(buffer, out _);

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
        trimSpaces ? Encoding.UTF8.GetString(TrimSpaces(bytes[range]).ToArray()) : Encoding.UTF8.GetString(bytes[range].ToArray());

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
}
