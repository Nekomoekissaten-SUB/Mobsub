using System.Text;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleParse.AssUtils;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Mobsub.Test;

public partial class ParseTest
{
     [TestMethod]
     public void ClearOvrFont()
     {
         var ass = GenerateAssData([
             @"Dialogue: 1,0:13:10.60,0:13:13.61,Sign,,0,0,0,,{\an7\blur1\fn方正粗雅宋_GBK\fs50\fsp2\bord0\pos(44,754)\b1}值日\N{\b0\fn\fs60}瞳岛眉美、沃野禁止郎",
         ]);

         var assAnlz = new AssAnalyze(ass);
         var res = assAnlz.GetUsedFontInfos();

         var target = new Dictionary<AssFontInfo, HashSet<Rune>>
         {
             { new AssFontInfo("方正粗雅宋_GBK,1,0,1"), ConvertToRuneList("值日") },
             { new AssFontInfo("Source Han Sans SC Medium,0,0,1"), ConvertToRuneList("瞳岛眉美、沃野禁止郎") }
         };
         
         Assert.IsTrue(AreDictionariesEqual(res, target));
     }
     
     [TestMethod]
     public void MergeDifferentEncoding()
     {
         var ass = GenerateAssData([
             @"Dialogue: 1,0:13:10.60,0:13:13.61,Sign,,0,0,0,,{\an7\blur1\fn方正粗雅宋_GBK\fs50\fsp2\bord0\pos(44,754)\b1}值日\N{\fe134\fs60}瞳岛眉美、沃野禁止郎",
         ]);

         var assAnlz = new AssAnalyze(ass);
         var res = assAnlz.GetUsedFontInfos();
         
         var target = new Dictionary<AssFontInfo, HashSet<Rune>>
         {
             { new AssFontInfo("方正粗雅宋_GBK,1,0,1"), ConvertToRuneList("值日瞳岛眉美、沃野禁止郎") },
         };
         
         Assert.IsTrue(AreDictionariesEqual(res, target));
         
         var res2 = assAnlz.GetUsedFontInfosWithEncoding();
         var target2 = new Dictionary<AssFontInfo, HashSet<Rune>>
         {
             { new AssFontInfo("方正粗雅宋_GBK,1,0,1"), ConvertToRuneList("值日") },
             { new AssFontInfo("方正粗雅宋_GBK,1,0,134"), ConvertToRuneList("瞳岛眉美、沃野禁止郎") }
         };
         Assert.IsTrue(AreDictionariesEqual(res2, target2));
     }
     
     [TestMethod]
     public void ReadFileAndGetFontInfos()
     {
         var logger = GetLogger(LogLevel.Debug);
        
         var assFile = @".\test_files\rStyle.ass";
         var ass = new AssData(logger);
         ass.ReadAssFile(assFile);

         var anlz = new AssAnalyze(ass, logger);
         var infos = anlz.GetUsedFontInfosWithEncoding();

         Dictionary<AssFontInfo, int> correctMap = [];
         correctMap.Add(new AssFontInfo("FOT-UDMarugo_Large Pr6N E,0,0,1"), 20);
         correctMap.Add(new AssFontInfo("FZCuYuan-M03,0,0,1"), 39);
         correctMap.Add(new AssFontInfo("FOT-UDMarugo_Large Pr6N B,0,0,1"), 19);
        
         Assert.IsTrue(infos.Keys.SequenceEqual(correctMap.Keys));
         foreach (var (k, v) in infos)
         {
             Assert.IsTrue(v.Count == correctMap[k]);
         }
     }
     

     private static HashSet<Rune> ConvertToRuneList(ReadOnlySpan<char> span)
     {
         HashSet<Rune> runes = [];
         int charsConsumed;
         for (var i = 0; i < span.Length; i += charsConsumed)
         {
             Rune.DecodeFromUtf16(span[i..], out var rune, out charsConsumed);
             runes.Add(rune);
         }

         return runes;
     }
     private static bool AreDictionariesEqual(Dictionary<AssFontInfo, HashSet<Rune>> dict1, Dictionary<AssFontInfo, HashSet<Rune>> dict2)
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