using System.Text;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

public static class AssSubtitleParseTagStripper
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    public static string RemoveTagsInAllOverrideBlocks(string lineText, Func<AssTag, bool> shouldRemove)
    {
        if (string.IsNullOrEmpty(lineText))
            return lineText;

        using var edit = AssEventTextEdit.Parse(lineText, Utf8);
        int matches = edit.DeleteTagsInAllOverrideBlocks(shouldRemove);
        if (matches == 0)
            return lineText;

        return edit.ApplyToString(Utf8);
    }
}
