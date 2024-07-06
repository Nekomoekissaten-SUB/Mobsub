using Mobsub.SubtitleParse;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.Test;

[TestClass]
public class ParseTest
{
    [TestMethod]
    public void GetTagsFromOvrBlock_StartFunction()
    {
        var r = GetTagsFromOvrBlock(@"{\t(0,100,\fsvp15\fshp15)\p1\fad(0,100)}");
        Assert.IsTrue(r.Count == 4);
    }
    
    [TestMethod]
    public void GetTagsFromOvrBlock_StartValueBlock()
    {
        var r = GetTagsFromOvrBlock(@"{\pos(960,620)\fs80\fn方正兰亭圆_GBK_中粗\fsp6\c&H349BEF&\3c&H0165FD&}");
        Assert.IsTrue(r.Count == 6);
    }

    private static List<char[]> GetTagsFromOvrBlock(string str)
    {
        var block = str.AsSpan().ToArray();
        var r = AssTagParse.GetTagsFromOvrBlock(block);
        return r;
    }
    
    [TestMethod]
    public void GetTagsFromTransFunction()
    {
        var text = @"t(1570,1840,\fry-90)";
        var block = new char[text.Length].AsSpan();
        text.AsSpan().CopyTo(block);
        var l = AssTagParse.GetTagsFromTransFunction(block);
        Assert.AreEqual(l[0].AsSpan().ToString(), "fry-90");
    }

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
}