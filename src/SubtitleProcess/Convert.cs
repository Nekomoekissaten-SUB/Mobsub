using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleProcess;

public class ConvertSub
{
    public static void ConvertAssToTxt(StreamWriter sw, AssData ass)
    {
        if (ass.Events == null)
        {
            return;
        }

        foreach (var e in ass.Events.Collection.ToArray())
        {
            if (e.TextRanges.Length == 0)
            {
                e.UpdateTextRanges();
            }
            foreach (var range in e.TextRanges)
            {
                var text = e.Text.AsSpan()[range];
                if (!AssEvent.IsOverrideBlock(text))
                {
                    sw.Write(text);
                }
            }
            
            sw.Write(Environment.NewLine);
        }
    }
}
