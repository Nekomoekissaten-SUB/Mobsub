using Mobsub.SubtitleParse;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleParse.AssUtils;
using Microsoft.Extensions.Logging;
using ZLogger;
using System.Runtime.CompilerServices;

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
        var evt = new AssEvent();
        string[] sources = [
            "*Default", "\tDefault", "  Default", " Default", "", "   ", "      ", "deFauLt"
            ];
        ReadOnlySpan<char> target;
        foreach (var source in sources)
        {
            target = GetEventStyleName(evt, source);
            Assert.IsTrue(target.SequenceEqual("Default"));
        }

        string[] sources2 = ["\\Default", "/Default", "_Default", "   \\t1Default"];
        foreach (var source in sources2)
        {
            target = GetEventStyleName(evt, source);
            Assert.IsFalse(target.SequenceEqual("Default"));
        }

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "GetEventStyleName")]
        extern static ReadOnlySpan<char> GetEventStyleName(AssEvent c, ReadOnlySpan<char> sp);
    }
}