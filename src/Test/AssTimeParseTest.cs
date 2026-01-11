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
}
