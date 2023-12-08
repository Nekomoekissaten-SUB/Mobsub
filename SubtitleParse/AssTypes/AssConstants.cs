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


    public static bool IsEventSpecialCharPair(char[] ca) => ca.Length == 2 && ca[0] == '\\' && (ca[1] is LineBreaker or WordBreaker or NBSP);
    public static bool IsEventSpecialCharPair(Span<char> ca) => ca.Length == 2 && ca[0] == '\\' && (ca[1] is LineBreaker or WordBreaker or NBSP);
}