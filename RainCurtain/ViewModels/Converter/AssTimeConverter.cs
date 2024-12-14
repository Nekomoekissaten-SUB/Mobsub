using System;
using System.Globalization;
using System.Text;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Mobsub.SubtitleParse;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.RainCurtain.ViewModels.Converter;

public class AssTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AssTime asstime && parameter is string target
                                     && targetType.IsAssignableTo(typeof(string)))
        {
            return asstime.ToString(SubtitleType.Ass, target == "ctsRounding");
        }
        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}