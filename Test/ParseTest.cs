using Mobsub.SubtitleParse;

namespace Mobsub.Test;

[TestClass]
public class ParseTest
{
    [TestMethod]
    public void GetTagsFromOvrBlock()
    {
        var s = @"{\t(0,100,\fsvp15\fshp15)\p1\fad(0,100)}";
        var block = new char[s.Length].AsSpan();
        s.AsSpan().CopyTo(block);
        var r = AssTagParse.GetTagsFromOvrBlock(block);
        Assert.IsTrue(r.Count == 4);
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
        var ass = AssParse.ReadAssFile(assFile);
        AssParse.WriteAssFile(ass, assWriteFile);

        var refAss = File.ReadAllText(assFile).AsSpan();
        var writeAss = File.ReadAllText(assWriteFile).AsSpan();

        Assert.IsTrue(refAss.SequenceEqual(writeAss));
    }
}