using FluentAssertions;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.Test;

[TestClass]
public class AssTagAlphaParseTest
{
    [TestMethod]
    public void TryParseAlphaByte_ParsesAlphaTag()
    {
        var ok = AssColor32.TryParseAlphaByte("&H80&"u8, out var alpha);
        ok.Should().BeTrue();
        alpha.Should().Be(0x80);
    }

    [TestMethod]
    public void TryParseAlphaByte_ParsesAlphaPrimaryTag()
    {
        var ok = AssColor32.TryParseAlphaByte("&H7F&"u8, out var alpha);
        ok.Should().BeTrue();
        alpha.Should().Be(0x7F);
    }

    [TestMethod]
    public void TryParseAlphaByte_ParsesRawHex()
    {
        var ok = AssColor32.TryParseAlphaByte("7F"u8, out var alpha);
        ok.Should().BeTrue();
        alpha.Should().Be(0x7F);
    }

    [TestMethod]
    public void TryParseAlphaByte_AllowsSingleDigitHex()
    {
        var ok = AssColor32.TryParseAlphaByte("&HF&"u8, out var alpha, out var invalid);
        ok.Should().BeTrue();
        invalid.Should().BeFalse();
        alpha.Should().Be(0x0F);
    }

    [TestMethod]
    public void TryParseAlphaByte_RejectsTooManyDigits()
    {
        var ok = AssColor32.TryParseAlphaByte("255"u8, out var alpha, out var invalid);
        ok.Should().BeTrue();
        invalid.Should().BeTrue();
        alpha.Should().Be(0x55);
    }

    [TestMethod]
    public void TryParseAlphaByte_RejectsNonHex()
    {
        var ok = AssColor32.TryParseAlphaByte("&HZZ&"u8, out var alpha, out var invalid);
        ok.Should().BeTrue();
        invalid.Should().BeTrue();
        alpha.Should().Be(0);
    }
}
