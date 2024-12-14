using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Mobsub.RainCurtain.ViewModels.Converter;

public class HideZeroConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i)
        {
            return i == 0 ? string.Empty : i;
        }
        
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}