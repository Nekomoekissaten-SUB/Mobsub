using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using LibGit2Sharp;
using Mobsub.SubtitleParse;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.IkkokuMergeBaseDiff.ViewModels.Converter;

public class DisplayCommitConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }
        
        if (value is Commit commit && targetType.IsAssignableTo(typeof(string)))
        {
            return $"{commit.Id.Sha.AsSpan(0, 7)} ({commit.MessageShort})<{commit.Committer.Name} {commit.Committer.When}>";
        }
        
        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}