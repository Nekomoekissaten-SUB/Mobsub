using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Mobsub.SubtitleProcess;

namespace Mobsub.IkkokuMergeBaseDiff.ViewModels.Converter;

public class DisplayCommitConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }
        
        if (value is MergeSimplifiedChineseGitDiff.GitCommitInfo commit && targetType.IsAssignableTo(typeof(string)))
        {
            var sha = commit.Sha;
            var shortSha = sha.Length > 7 ? sha.AsSpan(0, 7) : sha.AsSpan();
            return $"{shortSha} ({commit.Summary})<{commit.CommitterName} {commit.When}>";
        }
        
        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}
