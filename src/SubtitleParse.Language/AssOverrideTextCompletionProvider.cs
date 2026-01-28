using System.Text;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.Language;

public static class AssOverrideTextCompletionProvider
{
    private sealed record TagCompletion(string Name, string Label, AssCompletionKind Kind, string? Detail);

    private static readonly TagCompletion[] Tags = BuildTagCompletions();

    public static AssCompletionResult GetCompletions(string text, AssPosition position, AssOverrideTextAnalysisResult analysis)
    {
        var lineSpan = analysis.LineMap.GetLineSpan(text, position.Line);
        int col = Math.Clamp(position.Character, 0, lineSpan.Length);

        if (!TryGetOverrideTagNamePrefix(lineSpan, col, out int replaceStart, out int replaceEnd, out var prefix))
            return new AssCompletionResult(Array.Empty<AssCompletionItem>(), new AssRange(position, position));

        var items = new List<AssCompletionItem>(capacity: 32);
        for (int i = 0; i < Tags.Length; i++)
        {
            var t = Tags[i];
            if (t.Name.AsSpan().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                items.Add(new AssCompletionItem(t.Label, t.Name, t.Kind, t.Detail));
        }

        return new AssCompletionResult(items, new AssRange(new AssPosition(position.Line, replaceStart), new AssPosition(position.Line, replaceEnd)));
    }

    private static bool TryGetOverrideTagNamePrefix(ReadOnlySpan<char> lineSpan, int col, out int replaceStart, out int replaceEnd, out ReadOnlySpan<char> prefix)
    {
        replaceStart = replaceEnd = col;
        prefix = ReadOnlySpan<char>.Empty;

        int lastOpen = lineSpan.Slice(0, col).LastIndexOf('{');
        if (lastOpen < 0)
            return false;
        int lastClose = lineSpan.Slice(0, col).LastIndexOf('}');
        if (lastClose > lastOpen)
            return false;

        int slash = lineSpan.Slice(lastOpen, col - lastOpen).LastIndexOf('\\');
        if (slash < 0)
            return false;
        slash += lastOpen;

        int nameStart = slash + 1;
        if (nameStart > col)
            return false;

        int nameEnd = nameStart;
        while (nameEnd < lineSpan.Length && IsAsciiLetterOrDigit(lineSpan[nameEnd]))
            nameEnd++;

        int prefixEnd = col;
        int prefixStart = nameStart;
        if (prefixEnd < prefixStart)
            prefixEnd = prefixStart;

        prefix = lineSpan.Slice(prefixStart, prefixEnd - prefixStart);

        replaceStart = prefixStart;
        replaceEnd = prefixEnd;
        return true;
    }

    private static TagCompletion[] BuildTagCompletions()
    {
        var map = new Dictionary<string, TagCompletion>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in Enum.GetValues<AssTag>())
        {
            if (AssTagRegistry.TryGetObsoleteReplacement(tag, out _))
                continue;
            if (!AssTagRegistry.TryGet(tag, out var desc) || desc == null)
                continue;

            var name = Encoding.ASCII.GetString(desc.Name.Span);
            string? detail = tag.ToString();
            if (AssTagRegistry.TryGetFunctionKind(tag, out var fk))
            {
                var sig = AssTagRegistry.GetFunctionSignature(fk);
                if (sig != null)
                    detail = sig;
            }
            else if (tag == AssTag.Alignment)
            {
                detail = "an (replaces legacy \\a)";
            }

            map[name] = new TagCompletion(name, "\\" + name, AssCompletionKind.Keyword, detail);
        }

        var list = new TagCompletion[map.Count];
        int index = 0;
        foreach (var value in map.Values)
            list[index++] = value;
        Array.Sort(list, (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        return list;
    }

    private static bool IsAsciiLetterOrDigit(char c)
        => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
}
