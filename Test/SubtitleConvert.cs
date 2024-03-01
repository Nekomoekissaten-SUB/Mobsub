namespace Mobsub.Test;
using Mobsub.SubtitleParse;
using System;

[TestClass]
public class SubtitleConvert
{
    [TestMethod]
    public void Ass2Srt()
    {
        var assFile = ".\\test_files\\ass2srt.ass";
        var srtFile = ".\\test_files\\ass2srt.srt";
        var srtConvFile = ".\\test_files\\ass2srt_conv.srt";
        var ass = AssParse.ReadAssFile(assFile);
        var srt = new SubRipText();
        srt.FromAss(ass);
        srt.WriteSrtFile(srtConvFile, false);

        var refSrt = File.ReadAllText(srtFile).AsSpan();
        var convSrt = File.ReadAllText(srtConvFile).AsSpan();

        Assert.IsTrue(refSrt.SequenceEqual(convSrt));
    }
}
