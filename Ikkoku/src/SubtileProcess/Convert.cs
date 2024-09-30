using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.Ikkoku.SubtileProcess;

public class ConvertSub
{
    public static void ConvertAssToTxt(StreamWriter sw, AssData ass)
    {
        foreach (var e in ass.Events.Collection.ToArray())
        {
            foreach (var range in e.TextRanges)
            {
                var text = e.Text.AsSpan()[range];
                if (text[0] != AssConstants.StartOvrBlock)
                {
                    sw.Write(text);
                }
            }
            
            sw.Write(Environment.NewLine);
        }
    }
}