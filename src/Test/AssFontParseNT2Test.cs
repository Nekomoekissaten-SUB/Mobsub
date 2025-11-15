using FluentAssertions;
using Microsoft.Extensions.Logging;
using Mobsub.SubtitleParseNT2.AssTypes;
using Mobsub.SubtitleParseNT2.AssUtils;
using System;
using System.Text;
using NT2AssTypes = Mobsub.SubtitleParseNT2.AssTypes;
using NT2AssUtils = Mobsub.SubtitleParseNT2.AssUtils;

namespace Mobsub.Test;

public partial class ParseTest
{
    private readonly AssFontInfo baseState = new("Source Han Sans SC Medium,0,0,1");

    [TestMethod]
    public void ClearOvrFontNT2()
    {
        var result = GetUsedFontInfos(@"{\an7\blur1\fn方正粗雅宋_GBK\fs50\fsp2\bord0\pos(44,754)\b1}值日\N{\b0\fn\fs60}瞳岛眉美、沃野禁止郎"u8);

        var target = new Dictionary<AssFontInfo, HashSet<Rune>>
         {
             { new AssFontInfo("方正粗雅宋_GBK,1,0,1"), ConvertToRuneList("值日") },
             { new AssFontInfo("Source Han Sans SC Medium,0,0,1"), ConvertToRuneList("瞳岛眉美、沃野禁止郎") }
         };

        target.Should().BeEquivalentTo(result);
    }

    [TestMethod]
    public void MergeDifferentEncodingNT2()
    {
        var line = @"{\an7\blur1\fn方正粗雅宋_GBK\fs50\fsp2\bord0\pos(44,754)\b1}值日\N{\fe134\fs60}瞳岛眉美、沃野禁止郎"u8;
        var result = GetUsedFontInfos(line);

        var target = new Dictionary<AssFontInfo, HashSet<Rune>>
         {
             { new AssFontInfo("方正粗雅宋_GBK,1,0,1"), ConvertToRuneList("值日瞳岛眉美、沃野禁止郎") },
         };
        target.Should().BeEquivalentTo(result);

        var result2 = GetUsedFontInfos(line, true);
        var target2 = new Dictionary<AssFontInfo, HashSet<Rune>>
         {
             { new AssFontInfo("方正粗雅宋_GBK,1,0,1"), ConvertToRuneList("值日") },
             { new AssFontInfo("方正粗雅宋_GBK,1,0,134"), ConvertToRuneList("瞳岛眉美、沃野禁止郎") }
         };
        target2.Should().BeEquivalentTo(result2);
    }

    [TestMethod]
    public async Task ReadFileAndGetFontInfosNT2()
    {
        var logger = GetLogger(LogLevel.Debug);

        var assFile = @".\test_files\rStyle.ass";
        var ass = new AssData(logger);
        await ass.ReadAssFileAsync(assFile);
        var processor = new AssFontProcessor(ass.ScriptInfo.WrapStyle, ass.Styles) { AnalyzeWithEncoding = true };
        processor.GetUsedFontInfos(ass.Events!);
        var infos = processor.Results;

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

    private Dictionary<AssFontInfo, HashSet<Rune>> GetUsedFontInfos(ReadOnlySpan<byte> line, bool withEncoding = false)
    {
        var processor = new AssFontProcessor(0, new AssStyles()) { AnalyzeWithEncoding = withEncoding };
        processor.InitForLine(baseState);
        processor.GetUsedFontInfos(line);
        return processor.Results.ToDictionary();
    }

    [TestMethod]
    public async Task test()
    {
        var ass = new NT2AssTypes.AssData();
        ass.ReadAssFileAsync(@"F:\code\_test\parser\large_million_lines_simple.ass").GetAwaiter().GetResult();

        var processor = new NT2AssUtils.AssFontProcessor(ass.ScriptInfo.WrapStyle, ass.Styles) { AnalyzeWithEncoding = true };
        processor.GetUsedFontInfos(ass.Events!);
        var infos = processor.Results;
    }

    [TestMethod]
    public async Task test22()
    {
        var ass = new NT2AssTypes.AssData(target: AssParseTarget.ParseAssFontsInfoWithEncoding);
        ass.ReadAssFileAsync(@"F:\code\_test\parser\large_million_lines_simple.ass").GetAwaiter().GetResult();
        var infos = ass.Processor!.GetResults();
    }

    [TestMethod]
    public void test2()
    {
        var line = @"本字幕由喵萌Production制作 仅供交流试看之用 请勿用于商业用途\N{\fscx90\fscy90}翻译：槐安  ノルチ   微凉　校对：Ronny　时轴：黑金莲的腿环番茄鸡　后期：{MIR　特效：Nemeton"u8;
        var result = GetUsedFontInfos(line);
    }
}