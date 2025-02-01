using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.RainCurtain.Helper;

public class Calculator
{
    // libaegisub/common/character_count.cpp
    public static int CharacterCount(ReadOnlySpan<char> s, CalculateCharacterCountIgnore mask)
    {
        var count = 0;
        var ranges = AssEvent.SplitEventText(s);
        foreach (var range in ranges)
        {
            var block = s[range];
            if (!AssEvent.IsTextBlock(block)){ continue; }

            foreach (var c in block)
            {
                if ((mask & CalculateCharacterCountIgnore.Punctuation) != 0)
                {
                    if (char.IsPunctuation(c))
                    {
                        continue;
                    }
                }

                if ((mask & CalculateCharacterCountIgnore.WhiteSpace) != 0)
                {
                    if (char.IsWhiteSpace(c))
                    {
                        continue;
                    }
                }
                
                count++;
            }
        }
        
        return count;
    }
}