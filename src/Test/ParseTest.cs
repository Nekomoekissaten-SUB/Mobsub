using Microsoft.Extensions.Logging;
using Mobsub.SubtitleParse;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleParse.AssUtils;
using System.Runtime.CompilerServices;
using ZLogger;

namespace Mobsub.Test;

[TestClass]
public partial class ParseTest
{
    [TestMethod]
    public void ReadWrite()
    {
        var assFile = ".\\test_files\\ass2srt.ass";
        var assWriteFile = ".\\test_files\\ass2srt_write.ass";
        var ass = new AssData() { };
        ass.ReadAssFile(assFile);
        ass.WriteAssFile(assWriteFile);
        var refAss = File.ReadAllText(assFile).AsSpan();
        var writeAss = File.ReadAllText(assWriteFile).AsSpan();

        Assert.IsTrue(refAss.SequenceEqual(writeAss));
    }

    // [TestMethod]
    // public void ParseTagBlock()
    // {
    //     var block = @"{\fnFZLanTingHei-B-GBK\bord5\rSign\b1\t(0,100,\rDefault\fnFZLanTingHei-H-GBK\i1\fs100)\s1}".AsSpan();
    //     
    //     var assStyles = ParseAssStyles(styles);
    //     var atp = new AssTagParse(assStyles, GetScriptInfo());
    //     atp.Parse(block, assStyles.Collection.First());
    // }

    [TestMethod]
    public void ParseEventStyleName()
    {
        var c = new Utils();
        string[] sources = [
            "*Default", "", "deFauLt"
            ];
        ReadOnlySpan<char> target;
        foreach (var source in sources)
        {
            target = GetEventStyleName(c, source);
            Assert.IsTrue(target.SequenceEqual("Default"));
        }

        string[] sources2 = ["\\Default", "/Default", "_Default", "* Default"];
        foreach (var source in sources2)
        {
            target = GetEventStyleName(c, source);
            Assert.IsFalse(target.SequenceEqual("Default"));
        }

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "AssParseStyleName")]
        extern static ReadOnlySpan<char> GetEventStyleName(Utils c, ReadOnlySpan<char> sp);
    }

    //[TestMethod]
    //public void ParseEvent()
    //{
    //    var line = @"Dialogue: 1,0:13:10.60,0:13:13.61,Sign,,0,0,0,,{\an7\blur1\fn方正粗雅宋_GBK\fs50\fsp2\bord0\pos(44,754)\b1}值日\N{\fe134\fs60}瞳岛眉美、沃野禁止郎";
    //    //var evt = new SubtitleParse.AssTypes.AssEvent();
    //    var evts = new SubtitleParse.AssTypes.AssEvents();
    //    //evt.Read(line, 0, evts.Formats);

    //    var evt = new SubtitleParse.AssTypes.AssEventView(line, 0, "Dialogue", evts.Formats);
    //}



    [TestMethod]
    public void ParseFile()
    {
        var file = @"F:\code\_test\parser\large_million_lines_simple.ass";
        var ass = new SubtitleParse.AssTypes.AssData();
        ass.ReadAssFileAsync(file).GetAwaiter().GetResult();
    }
}