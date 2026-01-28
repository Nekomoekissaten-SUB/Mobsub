using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssUtils;

internal static class AssTextUtils
{
    internal static ReadOnlySpan<char> TrimLeadingSpace(ReadOnlySpan<char> span)
        => span.Length > 0 && span[0] == ' ' ? span[1..] : span;

    internal static ReadOnlySpan<char> NormalizeStyleName(ReadOnlySpan<char> span)
    {
        span = span.Trim();
        while (!span.IsEmpty && span[0] == '*')
            span = span[1..];
        return span.Trim();
    }

    internal static bool IsKnownScriptType(ReadOnlySpan<char> v)
        => v.Equals("v4.00", StringComparison.OrdinalIgnoreCase)
        || v.Equals("v4.00+", StringComparison.OrdinalIgnoreCase)
        || v.Equals("v4.00++", StringComparison.OrdinalIgnoreCase);

    internal static bool TryGetKnownScriptInfoKeyId(ReadOnlySpan<char> key, out int id)
    {
        // NOTE: keep ids < 32 for bitmask.
        if (key.Equals(AssConstants.ScriptInfo.ScriptType, StringComparison.OrdinalIgnoreCase)) { id = 0; return true; }
        if (key.Equals(AssConstants.ScriptInfo.PlayResX, StringComparison.OrdinalIgnoreCase)) { id = 1; return true; }
        if (key.Equals(AssConstants.ScriptInfo.PlayResY, StringComparison.OrdinalIgnoreCase)) { id = 2; return true; }
        if (key.Equals(AssConstants.ScriptInfo.LayoutResX, StringComparison.OrdinalIgnoreCase)) { id = 3; return true; }
        if (key.Equals(AssConstants.ScriptInfo.LayoutResY, StringComparison.OrdinalIgnoreCase)) { id = 4; return true; }
        if (key.Equals(AssConstants.ScriptInfo.WrapStyle, StringComparison.OrdinalIgnoreCase)) { id = 5; return true; }
        if (key.Equals(AssConstants.ScriptInfo.Timer, StringComparison.OrdinalIgnoreCase)) { id = 6; return true; }
        if (key.Equals(AssConstants.ScriptInfo.ScaledBorderAndShadow, StringComparison.OrdinalIgnoreCase)) { id = 7; return true; }
        if (key.Equals(AssConstants.ScriptInfo.Kerning, StringComparison.OrdinalIgnoreCase)) { id = 8; return true; }
        if (key.Equals(AssConstants.ScriptInfo.YCbCrMatrix, StringComparison.OrdinalIgnoreCase)) { id = 9; return true; }
        if (key.Equals(AssConstants.ScriptInfo.Title, StringComparison.OrdinalIgnoreCase)) { id = 10; return true; }
        if (key.Equals(AssConstants.ScriptInfo.OriginalScript, StringComparison.OrdinalIgnoreCase)) { id = 11; return true; }
        if (key.Equals(AssConstants.ScriptInfo.OriginalTranslation, StringComparison.OrdinalIgnoreCase)) { id = 12; return true; }
        if (key.Equals(AssConstants.ScriptInfo.OriginalEditing, StringComparison.OrdinalIgnoreCase)) { id = 13; return true; }
        if (key.Equals(AssConstants.ScriptInfo.OriginalTiming, StringComparison.OrdinalIgnoreCase)) { id = 14; return true; }
        if (key.Equals(AssConstants.ScriptInfo.ScriptUpdatedBy, StringComparison.OrdinalIgnoreCase)) { id = 15; return true; }
        if (key.Equals(AssConstants.ScriptInfo.UpdateDetails, StringComparison.OrdinalIgnoreCase)) { id = 16; return true; }
        id = -1;
        return false;
    }

    internal static bool TryParseSectionHeader(ReadOnlySpan<char> header, out AssSection section)
    {
        if (header.SequenceEqual(AssConstants.SectionScriptInfo)) { section = AssSection.ScriptInfo; return true; }
        if (header.SequenceEqual(AssConstants.SectionStyleV4P)) { section = AssSection.StylesV4P; return true; }
        if (header.SequenceEqual(AssConstants.SectionEvent)) { section = AssSection.Events; return true; }
        if (header.SequenceEqual(AssConstants.SectionFonts)) { section = AssSection.Fonts; return true; }
        if (header.SequenceEqual(AssConstants.SectionGraphics)) { section = AssSection.Graphics; return true; }
        if (header.SequenceEqual(AssConstants.SectionAegisubProjectGarbage)) { section = AssSection.AegisubProjectGarbage; return true; }
        if (header.SequenceEqual(AssConstants.SectionAegisubExtradata)) { section = AssSection.AegisubExtradata; return true; }
        section = default;
        return false;
    }

    internal static bool TryValidateEventFieldCount(ReadOnlySpan<char> payload, int fieldCount)
    {
        int commasNeeded = fieldCount - 1;
        int commas = 0;
        for (int i = 0; i < payload.Length; i++)
        {
            if (payload[i] == ',')
            {
                commas++;
                if (commas >= commasNeeded)
                    return true;
            }
        }
        return fieldCount <= 1;
    }

    internal static bool TryGetEventFieldRange(ReadOnlySpan<char> payload, int fieldCount, int fieldIndex, out int start, out int end)
    {
        start = 0;
        end = 0;

        if (fieldIndex < 0 || fieldIndex >= fieldCount)
            return false;

        int currentField = 0;
        int i = 0;
        int fieldStart = 0;

        while (i < payload.Length && currentField < fieldIndex)
        {
            int comma = payload.Slice(i).IndexOf(',');
            if (comma < 0)
                return false;
            i += comma + 1;
            currentField++;
            fieldStart = i;
        }

        if (currentField != fieldIndex)
            return false;

        if (fieldIndex == fieldCount - 1)
        {
            start = fieldStart;
            end = payload.Length;
            return true;
        }

        int nextComma = payload.Slice(fieldStart).IndexOf(',');
        if (nextComma < 0)
            return false;

        start = fieldStart;
        end = fieldStart + nextComma;
        return true;
    }

    internal static bool TryGetCsvField(ReadOnlySpan<char> csv, int index, out ReadOnlySpan<char> field)
    {
        field = default;
        if (index < 0)
            return false;

        int current = 0;
        int start = 0;

        for (int i = 0; i <= csv.Length; i++)
        {
            if (i == csv.Length || csv[i] == ',')
            {
                if (current == index)
                {
                    field = csv.Slice(start, i - start);
                    return true;
                }
                current++;
                start = i + 1;
            }
        }
        return false;
    }

    internal static string[] ParseCsvToArray(ReadOnlySpan<char> csv)
    {
        int count = 1;
        for (int i = 0; i < csv.Length; i++)
        {
            if (csv[i] == ',')
                count++;
        }

        var result = new string[count];
        int start = 0;
        int index = 0;

        for (int i = 0; i <= csv.Length; i++)
        {
            if (i == csv.Length || csv[i] == ',')
            {
                var token = csv.Slice(start, i - start).Trim();
                result[index++] = token.ToString();
                start = i + 1;
            }
        }

        int end = result.Length;
        while (end > 0 && string.IsNullOrEmpty(result[end - 1]))
            end--;
        if (end == result.Length)
            return result;

        var trimmed = new string[end];
        Array.Copy(result, trimmed, end);
        return trimmed;
    }

    internal static int ComputeStyleNameIndex(string[] formats)
    {
        for (int i = 0; i < formats.Length; i++)
        {
            if (formats[i].Equals("Name", StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    internal static void ComputeEventFormatIndices(string[] formats, out int startIndex, out int endIndex, out int styleIndex, out int textIndex)
    {
        startIndex = -1;
        endIndex = -1;
        styleIndex = -1;
        textIndex = -1;

        for (int i = 0; i < formats.Length; i++)
        {
            var f = formats[i];
            if (startIndex < 0 && f.Equals("Start", StringComparison.OrdinalIgnoreCase)) startIndex = i;
            else if (endIndex < 0 && f.Equals("End", StringComparison.OrdinalIgnoreCase)) endIndex = i;
            else if (styleIndex < 0 && f.Equals("Style", StringComparison.OrdinalIgnoreCase)) styleIndex = i;
            else if (textIndex < 0 && f.Equals("Text", StringComparison.OrdinalIgnoreCase)) textIndex = i;
        }
    }
}

