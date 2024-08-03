using System.Text;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleParse.AssUtils;

namespace Mobsub.Test;

[TestClass]
public class AssFontParseTest
{
    private AssStyles assStyles;
    private AssEvents assEvents;
    
    private readonly string[] styles =
    [
        @"Style: Sign,Source Han Sans SC Medium,70,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0.1,0,1,3,0,2,30,30,30,1",
    ];
    
    [TestMethod]
    public void ClearOvrFont()
    {
        Prepare([
            @"Dialogue: 1,0:13:10.60,0:13:13.61,Sign,,0,0,0,,{\an7\blur1\fn方正粗雅宋_GBK\fs50\fsp2\bord0\pos(44,754)\b1}值日\N{\b0\fn\fs60}瞳岛眉美、沃野禁止郎",
        ]);

        var assFontParse = new AssFontParse(assEvents.Collection, assStyles.Collection);
        var res = assFontParse.GetUsedFontInfos();

        var target = new Dictionary<AssFontInfo, List<Rune>>
        {
            { new AssFontInfo("方正粗雅宋_GBK,1,0,1"), ConvertToRuneList("值日") },
            { new AssFontInfo("Source Han Sans SC Medium,0,0,1"), ConvertToRuneList("瞳岛眉美、沃野禁止郎") }
        };
        
        Assert.IsTrue(AreDictionariesEqual(res, target));
    }
    
    [TestMethod]
    public void MergeDifferentEncoding()
    {
        Prepare([
            @"Dialogue: 1,0:13:10.60,0:13:13.61,Sign,,0,0,0,,{\an7\blur1\fn方正粗雅宋_GBK\fs50\fsp2\bord0\pos(44,754)\b1}值日\N{\fe134\fs60}瞳岛眉美、沃野禁止郎",
        ]);

        var assFontParse = new AssFontParse(assEvents.Collection, assStyles.Collection);
        var res = assFontParse.GetUsedFontInfos();

        var target = new Dictionary<AssFontInfo, List<Rune>>
        {
            { new AssFontInfo("方正粗雅宋_GBK,1,0,1"), ConvertToRuneList("值日瞳岛眉美、沃野禁止郎") },
        };
        
        Assert.IsTrue(AreDictionariesEqual(res, target));
        
        var res2 = assFontParse.GetUsedFontInfosWithEncoding();
        var target2 = new Dictionary<AssFontInfo, List<Rune>>
        {
            { new AssFontInfo("方正粗雅宋_GBK,1,0,1"), ConvertToRuneList("值日") },
            { new AssFontInfo("方正粗雅宋_GBK,1,0,134"), ConvertToRuneList("瞳岛眉美、沃野禁止郎") }
        };
        Assert.IsTrue(AreDictionariesEqual(res2, target2));
    }

    private void Prepare(string[] evts)
    {
        ParseAssStyles(styles);
        ParseAssEvents(evts);
    }
    private void ParseAssStyles(string[] stylesStr)
    {
        assStyles = new AssStyles();
        var lineNumber = 0;
        foreach (var str in stylesStr)
        {
            assStyles.Read(str, lineNumber);
            lineNumber += 1;
        }
    }
    private void ParseAssEvents(string[] eventsStr)
    {
        assEvents = new AssEvents();
        var lineNumber = 0;
        foreach (var str in eventsStr)
        {
            assEvents.Read(str, "v4.00++", lineNumber);
            lineNumber += 1;
        }
    }
    private static List<Rune> ConvertToRuneList(ReadOnlySpan<char> span)
    {
        List<Rune> runes = [];
        int charsConsumed;
        for (var i = 0; i < span.Length; i += charsConsumed)
        {
            Rune.DecodeFromUtf16(span[i..], out var rune, out charsConsumed);
            runes.Add(rune);
        }

        return runes;
    }
    private static bool AreDictionariesEqual(Dictionary<AssFontInfo, List<Rune>> dict1, Dictionary<AssFontInfo, List<Rune>> dict2)
    {
        if (dict1.Count != dict2.Count)
        {
            return false;
        }

        foreach (var (key, value) in dict1)
        {
            if (!dict2.TryGetValue(key, out var valueInDict2) || !value.SequenceEqual(valueInDict2))
            {
                return false;
            }
        }

        return true;
    }
}