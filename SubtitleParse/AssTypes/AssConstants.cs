namespace Mobsub.AssTypes;

public class AssConstants
{
    public const char StartOvrBlock = '{';
    public const char EndOvrBlock = '}';
    public const char BackSlash = '\\';
    public const char LineBreaker = 'N';
    public const char WordBreaker = 'n';
    public const char NBSP = 'h';
    public const char Comment = ';';
    public const char StartValueBlock = '(';
    public const char EndValueBlock = ')';
    public const char FunctionValueSeparator = ',';
    public const int NBSP_Utf16 = 0x00A0;

    public class ScriptInfo
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

    public const string FormatV4 = "Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text";
    public const string FormatV4P = "Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text";
    public const string FormatV4PP = "Layer, Start, End, Style, Name, MarginL, MarginR, MarginT, MarginB, Effect, Text";

    public static bool IsEventLine(ReadOnlySpan<char> sp) => sp.StartsWith("Comment") || sp.StartsWith("Dialogue");
    public static bool IsEventSpecialCharPair(char[] ca) => ca.Length == 2 && ca[0] == '\\' && (ca[1] is LineBreaker or WordBreaker or NBSP);
    public static bool IsEventSpecialCharPair(Span<char> ca) => ca.Length == 2 && ca[0] == '\\' && (ca[1] is LineBreaker or WordBreaker or NBSP);
}