namespace Mobsub.SubtitleParse.AssText;

public static class AssTextMetrics
{
    private const char TagBlockStart = '{';
    private const char TagBlockEnd = '}';
    private const char Escape = '\\';
    private const char HardLineBreak = 'N';
    private const char SoftLineBreak = 'n';
    private const char NonBreakingSpace = 'h';
    private const char CarriageReturn = '\r';
    private const char LineFeed = '\n';

    /// <summary>
    /// Counts "visible characters" for CPS calculation.
    /// Rules (Aegisub-like):
    /// - Ignores override blocks <c>{...}</c>
    /// - Treats <c>\h</c> as one visible character
    /// - Ignores <c>\N</c>/<c>\n</c>
    /// - Ignores CR/LF
    /// </summary>
    public static int CountVisibleCharsForCps(string? text)
        => string.IsNullOrEmpty(text) ? 0 : CountVisibleCharsForCps(text.AsSpan());

    public static int CountVisibleCharsForCps(ReadOnlySpan<char> text)
    {
        int count = 0;
        bool inOverride = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inOverride)
            {
                if (c == TagBlockEnd)
                    inOverride = false;
                continue;
            }

            if (c == TagBlockStart)
            {
                inOverride = true;
                continue;
            }

            if (c == Escape && i + 1 < text.Length)
            {
                char n = text[i + 1];
                if (n == NonBreakingSpace)
                {
                    count++;
                    i++;
                    continue;
                }
                if (n == HardLineBreak || n == SoftLineBreak)
                {
                    i++;
                    continue;
                }
            }

            if (c == CarriageReturn || c == LineFeed)
                continue;

            count++;
        }

        return count;
    }
}
