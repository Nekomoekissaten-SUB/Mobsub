using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Mobsub.RainCurtain.Helper;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.RainCurtain.ViewModels.Converter;

public class CharactersPerSecondConverter : IMultiValueConverter
{
    // https://github.com/wangqr/Aegisub/blob/33ff9b4083320c9ace3b1ee80a9f0b93bd07ec05/src/grid_column.cpp#L303
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is [bool isDialogue, AssTime start, AssTime end, string text])
        {
            if (!isDialogue)
            {
                return string.Empty;
            }
            
            if (string.IsNullOrEmpty(text))
            {
                return "0";
            }
            var milliseconds = (int)end.SubtractionMilliseconds(start);
            if (milliseconds <= 100)
            {
                return "0";
            }
            const CalculateCharacterCountIgnore ignore = CalculateCharacterCountIgnore.Punctuation | CalculateCharacterCountIgnore.WhiteSpace;
            var chars = Calculator.CharacterCount(text, ignore);
            // floor
            return $"{chars * 1000 / milliseconds}";
        }

        return "0";
    }
}