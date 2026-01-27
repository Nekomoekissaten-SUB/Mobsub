﻿using Mobsub.SubtitleParse.AssTypes;

namespace Test;

public partial class ParseTest
{
    private AssColor32 _colorRed = new(255, 0, 0, 0);
    private string _colorStyleRed = "&H000000FF";
    private string _colorTagRed = "&H0000FF&";

    [TestMethod]
    public void AssColorTagParseSpan()
    {
        var value = AssColor32.Parse(_colorTagRed);
        Assert.IsTrue(value.Equals(_colorRed));
    }

    [TestMethod]
    public void AssColorStyleParseSpan()
    {
        var value = AssColor32.Parse(_colorStyleRed);
        Assert.IsTrue(value.Equals(_colorRed));
    }

    [TestMethod]
    public void AssColorStyleWithSpaceParseSpan()
    {
        var value = AssColor32.Parse($"  {_colorStyleRed} ");
        Assert.IsTrue(value.Equals(_colorRed));
    }

    [TestMethod]
    public void AssColorTagParseUtf8()
    {
        var str = "&H0000FF&"u8;
        var value = AssColor32.Parse(str);
        Assert.IsTrue(value.Equals(_colorRed));
    }

    [TestMethod]
    public void AssColorStyleParseUtf8()
    {
        var str = "&H000000FF"u8;
        var value = AssColor32.Parse(str);
        Assert.IsTrue(value.Equals(_colorRed));
    }

    [TestMethod]
    public void AssColorStyleWithSpaceParseUtf8()
    {
        var str = "  &H000000FF "u8;
        var value = AssColor32.Parse(str);
        Assert.IsTrue(value.Equals(_colorRed));
    }
}
