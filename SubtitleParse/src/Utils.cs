using Mobsub.SubtitleParse.AssTypes;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace Mobsub.SubtitleParse;

public class Utils
{
    public static Encoding GuessEncoding(byte[] buffer)
    {
        if (buffer.Length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
        {
            // UTF-16 (Little-Endian)
            return Encoding.Unicode;
        }
        else if (buffer.Length >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
        {
            // UTF-16 (Big-Endian)
            return Encoding.BigEndianUnicode;
        }
        else if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            // UTF-8
            return Encoding.UTF8;
        }
        else
        {
            // Default to UTF-8 without Bom
            return new UTF8Encoding(false);
        }
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

    public static void GuessEncoding(FileStream fs, out Encoding charEncoding)
    {
        var buffer = new byte[4];
        fs.Read(buffer, 0, 4);
        charEncoding = GuessEncoding(buffer);
        fs.Seek(0, SeekOrigin.Begin);
    }

    public static void GuessEncoding(FileStream fs, out Encoding charEncoding, out bool isCarriageReturn)
    {
        var buffer = new byte[1024];
        var b = fs.Read(buffer, 0, 1024);
        charEncoding = GuessEncoding(buffer);
        isCarriageReturn = false;
        if (b > 0)
        {
            for (int i = 0; i < b - 1; i++)
            {
                if (buffer[i] == 0x0D && buffer[i + 1] == 0x0A)
                {
                    isCarriageReturn = true;
                }
            }
        }
        fs.Seek(0, SeekOrigin.Begin);
    }

    public static void SetProperty(object obj, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type T, string propertyName, string value)
    {
        var property = T.GetProperty(propertyName);
        if (property == null)
        {
            return;
        }

        object? typedValue = null;
        if (property.PropertyType == typeof(AssRGB8))
        {
            var abgr = new AssRGB8();
            abgr.Parse(value);
            typedValue = abgr;
        }
        else if (property.PropertyType == typeof(bool))
        {
            if (short.TryParse(value, out short shortValue))
            {
                typedValue = shortValue == -1;
            }
            else
            {
                typedValue = value.AsSpan().SequenceEqual("yes".AsSpan());
            }
        }
        else if (property.PropertyType == typeof(AssTime))
        {
            typedValue = AssTime.ParseFromAss(value.AsSpan());
        }
        else
        {
            try
            {
                typedValue = Convert.ChangeType(value, property.PropertyType);
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

    public static bool IsStringInFields(object obj, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] Type T, string searchString)
    {
        if (obj == null || string.IsNullOrEmpty(searchString))
        {
            return false;
        }
        foreach (var field in T.GetFields())
        {
            if (field.FieldType == typeof(string))
            {
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

        if (idx != -1)
        {
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
        return false;
    }
}
