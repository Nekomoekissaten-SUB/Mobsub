using FluentAssertions;
using Mobsub.SubtitleParseNT2.AssTypes;

namespace Mobsub.Test;

[TestClass]
public class AssTagAlphaParseTest
{
    [TestMethod]
    public void TryParseAlphaByte_ParsesAlphaTag()
    {
        var ok = AssRGB8.TryParseAlphaByte("&H80&"u8, out var alpha);
        ok.Should().BeTrue();
        alpha.Should().Be(0x80);
    }

    [TestMethod]
    public void TryParseAlphaByte_ParsesAlphaPrimaryTag()
    {
        var ok = AssRGB8.TryParseAlphaByte("&H7F&"u8, out var alpha);
        ok.Should().BeTrue();
        alpha.Should().Be(0x7F);
    }

    [TestMethod]
    public void TryParseAlphaByte_ParsesRawHex()
    {
        var ok = AssRGB8.TryParseAlphaByte("7F"u8, out var alpha);
        ok.Should().BeTrue();
        alpha.Should().Be(0x7F);
    }
}
