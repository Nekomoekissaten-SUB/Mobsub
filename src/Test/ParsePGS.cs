﻿using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;
using NT2AssTypes = Mobsub.SubtitleParse.AssTypes;
using NT2AssUtils = Mobsub.SubtitleParse.AssUtils;
using NT2PGS = Mobsub.SubtitleParse.PGS;
using Mobsub.SubtitleParse.PGS;

namespace Test;

public partial class ParseTest
{
    [TestMethod]
    public void ParsePgsOld()
    {
        var file = @"F:\code\_test\pgs\buta.sup";
        PGSData.DecodeImages(file, @"F:\code\_test\pgs\buta_Old", 0);
    }

    [TestMethod]
    public void ParsePgsNT2()
    {
        var file = @"F:\code\_test\pgs\buta.sup";
        NT2PGS.PGSData.DecodeImages(file, @"F:\code\_test\pgs\buta_NT2", 0);
    }
}
