﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Mobsub.SubtitleParse.AssTypes;

public enum AssParseTarget : byte
{
    Default = 0,
    ParseAssFontsInfo = 1,
    ParseAssFontsInfoWithEncoding = 2,
}
