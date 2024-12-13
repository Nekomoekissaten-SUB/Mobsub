using System;
using System.Globalization;
using System.Text;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.RainCurtain.ViewModels;

public class AssTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AssTime asstime && parameter is string target
                                     && targetType.IsAssignableTo(typeof(string)))
        {
            var sb = new StringBuilder();
            switch (target)
            {
                case "ctsRounding":
                    AssEvent.WriteTime(sb, asstime, true);
                    break;
                default:
                    AssEvent.WriteTime(sb, asstime, false);
                    break;
            }
            return sb.ToString();
        }
        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}