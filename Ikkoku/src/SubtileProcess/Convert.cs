using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.Ikkoku.SubtileProcess;

public class ConvertSub
{
    public static void ConvertAssToTxt(StreamWriter sw, AssData ass)
    {
        foreach (var e in ass.Events.Collection.ToArray())
        {
            foreach (var text in e.Text.ToArray())
            {
                if (text[0] != '{')
                {
                    sw.Write(text);
                }
            }
            
            sw.Write(Environment.NewLine);
        }
    }
}