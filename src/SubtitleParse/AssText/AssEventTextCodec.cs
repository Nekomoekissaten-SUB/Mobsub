using System.Text;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

public static class AssEventTextCodec
{
    /// <summary>
    /// Converts ASS in-text hard line breaks (<c>\N</c>) to real newlines for editing display.
    /// Does not touch <c>\n</c> or <c>\h</c>, and does not interpret sequences inside tag blocks <c>{...}</c>.
    /// </summary>
    public static string ToEditingText(string? assEventText)
    {
        if (string.IsNullOrEmpty(assEventText))
            return string.Empty;

        var sb = new StringBuilder(assEventText.Length);

        bool insideTagBlock = false;
        for (int i = 0; i < assEventText.Length; i++)
        {
            char c = assEventText[i];
            if (c == AssConstants.Text.OverrideBlockStart)
            {
                insideTagBlock = true;
                sb.Append(c);
                continue;
            }
            if (c == AssConstants.Text.OverrideBlockEnd)
            {
                insideTagBlock = false;
                sb.Append(c);
                continue;
            }

            if (!insideTagBlock
                && c == AssConstants.Text.Escape
                && i + 1 < assEventText.Length
                && assEventText[i + 1] == AssConstants.Text.HardLineBreak)
            {
                sb.Append(AssConstants.Text.LineFeed);
                i++;
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts real newlines from an editor back to ASS single-line text by replacing them with <c>\N</c>.
    /// Leaves existing <c>\N</c>/<c>\n</c>/<c>\h</c> sequences untouched.
    /// </summary>
    public static string ToAssEventText(string? editingText)
    {
        if (string.IsNullOrEmpty(editingText))
            return string.Empty;

        // Normalize CRLF/CR to LF first, then replace LF -> \N.
        // We must never leave real newlines in AssEvent.Text because it breaks ASS script formatting.
        if (!editingText.Contains(AssConstants.Text.LineFeed) && !editingText.Contains(AssConstants.Text.CarriageReturn))
            return editingText;

        var sb = new StringBuilder(editingText.Length + 8);
        for (int i = 0; i < editingText.Length; i++)
        {
            char c = editingText[i];
            if (c == AssConstants.Text.CarriageReturn)
            {
                if (i + 1 < editingText.Length && editingText[i + 1] == AssConstants.Text.LineFeed)
                    i++;
                sb.Append(AssConstants.Text.AssHardLineBreak);
                continue;
            }
            if (c == AssConstants.Text.LineFeed)
            {
                sb.Append(AssConstants.Text.AssHardLineBreak);
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }
}
