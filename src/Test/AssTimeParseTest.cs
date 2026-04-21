﻿using Mobsub.SubtitleParse.AssTypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Test;

[TestClass]
public partial class ParseTest
{
    private AssTime _time = new(223748000000);
    private Mobsub.SubtitleParse.AssTypes.AssTime _time2 = new(223748000000);
    private AssTime _time_multi_hour = new(452967800000);
    private Mobsub.SubtitleParse.AssTypes.AssTime _time2_multi_hour = new(452967800000);

    [TestMethod]
    public void AssTimeParseSpan()
    {
        var str = "6:12:54.80";
        var time = AssTime.ParseFromAss(str);
        Assert.IsTrue(time.Equals(_time));
    }

    [TestMethod]
    public void AssTimeParseSpanUtf8()
    {
        var str = "6:12:54.80"u8;
        var time = Mobsub.SubtitleParse.AssTypes.AssTime.ParseFromAss(str);
        Assert.IsTrue(time.Equals(_time2));
    }

    [TestMethod]
    public void AssTimeParseSpanMultiHour()
    {
        var str = "12:34:56.78";
        var time = AssTime.ParseFromAss(str);
        Assert.IsTrue(time.Equals(_time_multi_hour));
    }

    [TestMethod]
    public void AssTimeParseSpanUtf8MultiHour()
    {
        var str = "12:34:56.78"u8;
        var time = Mobsub.SubtitleParse.AssTypes.AssTime.ParseFromAss(str);
        Assert.IsTrue(time.Equals(_time2_multi_hour));
    }

    [TestMethod]
    public void AssTimeWriteAssTimeTruncatesToCentiseconds()
    {
        var time = new AssTime(155);
        var sb = new StringBuilder();
        AssTime.WriteAssTime(sb, time, ctsRounding: false);
        Assert.AreEqual("0:00:00.15", sb.ToString());
    }

    [TestMethod]
    public void AssTimeWriteAssTimeCtsRounding()
    {
        var time = new AssTime(155);
        var sb = new StringBuilder();
        AssTime.WriteAssTime(sb, time, ctsRounding: true);
        Assert.AreEqual("0:00:00.16", sb.ToString());
    }

    [TestMethod]
    public void AssTimeWriteAssTimeCtsRoundingClampTo99()
    {
        var time = new AssTime(995);
        var sb = new StringBuilder();
        AssTime.WriteAssTime(sb, time, ctsRounding: true);
        Assert.AreEqual("0:00:00.99", sb.ToString());
    }
}
