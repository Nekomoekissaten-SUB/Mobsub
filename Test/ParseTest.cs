using Mobsub.SubtitleParse;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.Test;

[TestClass]
public class ParseTest
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
}