using Mobsub.SubtitleParse;
using System.Text;
using Mobsub.ZhConvert;
using OpenCCSharp.Presets;

namespace Mobsub.Test;

public class Program
{
    public static void Main()
    {
        // GetAssUsedFonts(@"D:\code\csharp\Mobsub\Test\files\sections.ass");
        // GetEventTags(@"F:\GitHub\Mobsub\Test\files\[Airota][To_Aru_Kagaku_no_Railgun_S][OVA][1280x720][x264_AAC][GB].ass");
        // GetTagsFromTransFunction(@"\t(1570,1840,\fry-90");
        // GetTagsFromOvrBlock(@"\t(1570,1840,\fry-90");
        // \t(
        //     0,100,
        //     \fsvp15
        //     \fshp15
        //     \t(100,150,
        //     \fsvp0
        //     \fshp95
        //     \t(250,300,
        //     \fsvp0
        //     \fshp5
        //     )
        // \p1
        // \fad(0,100)
        // valueStartIndex = 78

        ZhConvertTest("为了结束这场闹剧", new FileInfo(@"F:\GitHub\OpenCCSharp\OpenCCSharp.Presets\ConversionDictionaries\Hans-TW.json"));
        // ZhConvertTest("为了结束这场闹剧");

        // 爲了結束這場鬧劇
        // 為了結束這場鬧劇

    }

    private static void GetAssUsedFonts(string assPath)
    {
        var assdata = AssParse.ReadAssFile(assPath);
        var fc = AssFontParse.GetUsedFonts(assdata.Events.Collection, assdata.Styles.Collection);
        Console.WriteLine("fine");
    }

    private static void GetEventTags(string assPath)
    {
        var assdata = AssParse.ReadAssFile(assPath);

        // var lines = assdata.Events.Collection.Where(x => x.lineNumber == 242).First();
        // AssTagParse.ClassifyTagsFromLine(lines.Text, out StringBuilder modTags, out StringBuilder weirdTags, out StringBuilder normalTags);

        foreach (var et in assdata.Events.Collection)
        {
            if (et.IsDialogue)
            {
                AssTagParse.ClassifyTagsFromLine(et.Text, out StringBuilder modTags, out StringBuilder weirdTags, out StringBuilder normalTags);
                
            }
        }

        Console.WriteLine("fine");
    }

    private static void GetTagsFromTransFunction(string text)
    {
        // Span<char> block = default;
        var block = new char[text.Length].AsSpan();
        text.AsSpan().CopyTo(block);
        var l = AssTagParse.GetTagsFromTransFunction(block);
    }

    private static void GetTagsFromOvrBlock(string text)
    {
        var block = new char[text.Length].AsSpan();
        text.AsSpan().CopyTo(block);
        AssTagParse.GetTagsFromOvrBlock(block);
    }

    public static void ZhConvertTest(string text, FileInfo config)
    {
        var dicts = OpenCCSharpUtils.LoadJson(config);
        var converter = OpenCCSharpUtils.GetConverter(dicts);

        var sconvp = converter.Convert(text).AsSpan();

        Console.WriteLine("fine");
    }
}
