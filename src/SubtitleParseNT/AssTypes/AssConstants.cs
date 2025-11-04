using System;
using System.Collections.Generic;
using System.Text;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public class AssConstants
{
    public const string SectionScriptInfo = "[Script Info]";
    public const string SectionStyleV4P = "[V4+ Styles]";
    public const string SectionEvent = "[Events]";

    public const string ScriptTypeV4P = "v4.00+";
    public const string ScriptTypeV4PP = "v4.00++";

    public const string EventFormatV4 = "Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text";
    public const string EventFormatV4P = "Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text";
    public const string EventFormatV4PP = "Layer, Start, End, Style, Name, MarginL, MarginR, MarginT, MarginB, Effect, Text";
}
