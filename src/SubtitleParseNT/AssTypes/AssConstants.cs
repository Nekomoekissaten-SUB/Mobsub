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

    internal const string StyleFormatV4 = "Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, TertiaryColour, BackColour, Bold, Italic, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, AlphaLevel, Encoding";
    internal const string StyleFormatV4P = "Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding";
    internal const string StyleFormatV4PP = "Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginT, MarginB, Encoding, RelativeTo";

    public const string EventFormatV4 = "Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text";
    public const string EventFormatV4P = "Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text";
    public const string EventFormatV4PP = "Layer, Start, End, Style, Name, MarginL, MarginR, MarginT, MarginB, Effect, Text";

    internal class ScriptInfo
    {
        // Functional Headers
        public const string ScriptType = "ScriptType";
        public const string PlayResX = "PlayResX";
        public const string PlayResY = "PlayResY";
        public const string LayoutResX = "LayoutResX";
        public const string LayoutResY = "LayoutResY";
        public const string WrapStyle = "WrapStyle";
        public const string Timer = "Timer";
        public const string ScaledBorderAndShadow = "ScaledBorderAndShadow";
        public const string Kerning = "Kerning";    // unused?
        public const string YCbCrMatrix = "YCbCr Matrix";

        // Informational Headers
        public const string Title = "Title";
        public const string OriginalScript = "Original Script";
        public const string OriginalTranslation = "Original Translation";
        public const string OriginalEditing = "Original Editing";
        public const string OriginalTiming = "Original Timing";
        public const string ScriptUpdatedBy = "Script Updated By";
        public const string UpdateDetails = "Update Details";

    }
}
