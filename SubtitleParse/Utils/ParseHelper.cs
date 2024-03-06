using Mobsub.AssTypes;

namespace Mobsub.Utils;

public class ParseHelper
{
    internal static void SetProperty(object obj, string propertyName, string value)
    {
        var property = obj.GetType().GetProperty(propertyName);
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
            typedValue = AssParseTime(value.AsSpan());
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

    public static bool IsStringInFields(object obj, string searchString)
    {
        if (obj == null || string.IsNullOrEmpty(searchString))
        {
            return false;
        }
        foreach (var field in obj.GetType().GetFields())
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

    internal static AssTime AssParseTime(ReadOnlySpan<char> sp)
    {
        // hours:minutes:seconds:centiseconds
        // 0:00:00.00, number of digits of hours is variable
        var ms = 0;
        var sepPosFirst = 1;

        for (int i = 0; i < sp.Length; i++)
        {
            if (sp[i] == ':')
            {
                sepPosFirst = i;
                break;
            }
        }

        int h = 0;
        for (var i = sepPosFirst - 1; i > -1; i--)
        {
            h += (sp[i] - '0') * (int)Math.Pow(10, i);
        }
        ms += h * 1000 * 60 * 60;

        for (int i = sepPosFirst + 1; i < sp.Length; i++)
        {
            var c = sp[i];
            var n = c - '0';

            if (i == sepPosFirst + 1)
            {
                ms += n * 1000 * 60 * 10;
            }
            else if (i == sepPosFirst + 2)
            {
                ms += n * 1000 * 60;
            }
            else if (i == sepPosFirst + 4)
            {
                ms += n * 1000 * 10;
            }
            else if (i == sepPosFirst + 5)
            {
                ms += n * 1000;
            }
            else if (i == sepPosFirst + 6)
            {
                if (c != '.')
                {
                    throw new Exception($"Wrong timestamp in ass: {sp.ToString()}");
                }
            }
            else if (i == sepPosFirst + 7)
            {
                ms += n * 100;
            }
            else if (i == sepPosFirst + 8)
            {
                ms += n * 10;
            }
            else
            {
                if (c != ':')
                {
                    throw new Exception($"Wrong timestamp in ass: {sp.ToString()}");
                }
            }
        }

        return new AssTime(ms);
    }

}
