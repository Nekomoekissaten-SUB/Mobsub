using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.RainCurtain.ViewModels.Converter;

public class InvalidValueColorConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 2 && values[0] is AssTime start && values[1] is AssTime end)
        {
            if (start.CompareTo(end) > 0)
            {
                return Brushes.Red;
            }
        }
        return null;
    }
}