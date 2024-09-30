using System.Text;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleParse.AssUtils;

namespace Mobsub.Test;

[TestClass]
public partial class AssFontParseTest
{
     private readonly string[] styles =
     [
         @"Style: Sign,Source Han Sans SC Medium,70,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0.1,0,1,3,0,2,30,30,30,1",
     ];
     
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
     
     private AssData GenerateAssData(string[] evts)
     {
         return new AssData()
         {
             ScriptInfo = new AssScriptInfo()
             {
                 ScriptType = "v4.00+",
             },
             Styles = ParseAssStyles(styles),
             Events = ParseAssEvents(evts),
         };
     }
     private static AssStyles ParseAssStyles(string[] stylesStr)
     {
         var assStyles = new AssStyles();
         var lineNumber = 0;
         foreach (var str in stylesStr)
         {
             assStyles.Read(str, lineNumber);
             lineNumber += 1;
         }

         return assStyles;
     }
     private static AssEvents ParseAssEvents(string[] eventsStr)
     {
         var assEvents = new AssEvents();
         var lineNumber = 0;
         foreach (var str in eventsStr)
         {
             assEvents.Read(str, "v4.00++", lineNumber);
             lineNumber += 1;
         }

         return assEvents;
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