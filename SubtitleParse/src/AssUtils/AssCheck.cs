using Mobsub.SubtitleParse.AssTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mobsub.SubtitleParse.AssUtils;

public class AssCheck
{
    public static HashSet<string> GetUsedStyles(List<AssEvent> events)
    {
        var styles = new HashSet<string>();
        var str = new StringBuilder();
        foreach (var et in events)
        {
            if (et.IsDialogue)
            {
                var text = et.Text.ToArray();

                styles.Add(et.Style);

                char[] block = [];
                for (var i = 0; i < text.Length; i++)
                {
                    block = text[i];
                    if (block[0] == '{' && block[^1] == '}' && block.Length > 2 && i != text.Length - 1)
                    {
                        foreach (var ca in AssTagParse.GetTagsFromOvrBlock(block))
                        {
                            if (ca[0] == 'r' && ca.Length > 1 && ca.Length >= 3 && !ca.AsSpan()[..3].SequenceEqual("rnd".AsSpan()))
                            {
                                styles.Add(new string(ca[1..]));
                            }
                        }
                    }
                }
            }
        }
        return styles;
    }
    public static HashSet<string> GetUndefinedStyles(List<AssEvent> events, AssStyles assStyles)
    {
        var usedStyles = GetUsedStyles(events);
        var undefinedStyles = new HashSet<string>(usedStyles);
        undefinedStyles.ExceptWith(assStyles.Names);
        return undefinedStyles;
    }
}
