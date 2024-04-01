using Mobsub.SubtitleParse;
using System;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.Test;

[TestClass]
public class SubtitleConvert
{
    [TestMethod]
    public void Ass2Srt()
    {
        var assFile = ".\\test_files\\ass2srt.ass";
        var srtFile = ".\\test_files\\ass2srt.srt";
        var srtConvFile = ".\\test_files\\ass2srt_conv.srt";
        var ass = new AssData() { };
        ass.ReadAssFile(assFile);
        var srt = new SubRipText();
        srt.FromAss(ass);
        srt.WriteSrtFile(srtConvFile, false);

        var refSrt = File.ReadAllText(srtFile).AsSpan();
        var convSrt = File.ReadAllText(srtConvFile).AsSpan();

        Assert.IsTrue(refSrt.SequenceEqual(convSrt));
    }
}
