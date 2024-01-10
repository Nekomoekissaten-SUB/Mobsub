using System.Diagnostics;
using System.Text;
using Mobsub.AssTypes;
using Mobsub.SubtitleParse;

namespace Mobsub.Ikkoku;

public partial class SubtileProcess
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