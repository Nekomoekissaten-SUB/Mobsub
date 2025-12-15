using Mobsub.SubtitleParseNT2.AssTypes;
using System.Buffers;
using System.Text;

namespace Mobsub.SubtitleParseNT2.AssUtils;

public sealed class AssFontProcessor(byte wrapStyle, AssStyles styles) : IAssTagProcessor
{
    private readonly byte wrapStyle = wrapStyle;
    private readonly AssStyles styles = styles;
    private AssFontInfo baseInfo;
    private AssFontInfo current;
    private readonly Dictionary<AssFontInfo, Dictionary<Rune, List<int>>> maps = [];
    public int FirstEventLineNumber = -1;
    private int lineNumber = 0;
    private readonly Dictionary<Rune, HashSet<int>> runeLocations = [];
    public bool AnalyzeWithEncoding = false;
    public IReadOnlyDictionary<AssFontInfo, HashSet<Rune>> Results => maps.ToDictionary(keyValuePair => keyValuePair.Key, keyValuePair => new HashSet<Rune>(keyValuePair.Value.Keys));
    public IReadOnlyDictionary<AssFontInfo, Dictionary<Rune, List<int>>> ResultsWithLineNumber => maps;

    public void InitForLine(ReadOnlySpan<byte> styleName)
    {
        baseInfo = new AssFontInfo(styles.GetAssStyleViewByEventStyle(styleName));
        current = baseInfo;
    }
    public void InitForLine(AssFontInfo info)
    {
        baseInfo = info;
        current = info;
    }

    public void OnTag(AssTagSpan tag, AssTagDescriptor desc)
    {
        switch (tag.Tag)
        {
            case AssTag.Bold:
                if (tag.TryGet<int>(out var b)) current.Weight = b; else current.Weight = baseInfo.Weight;
                break;
            case AssTag.Italic:
                if (tag.TryGet<bool>(out var i)) current.Italic = i; else current.Italic = baseInfo.Italic;
                break;
            case AssTag.FontEncoding:
                if (AnalyzeWithEncoding)
                {
                    if (tag.TryGet<int>(out var fe)) current.Encoding = fe; else current.Encoding = baseInfo.Encoding;
                }
                else
                {
                    current.Encoding = 1;
                }
                break;
            case AssTag.FontName:
                if (tag.TryGet<ReadOnlyMemory<byte>>(out var fn) && fn.Length > 0) current.NameBytes = fn.ToArray(); else current.NameBytes = baseInfo.NameBytes;
                break;
            case AssTag.Reset:
                if (tag.TryGet<ReadOnlyMemory<byte>>(out var r) && r.Length > 0)
                {
                    current = new AssFontInfo(styles.GetAssStyleViewByName(r.Span));
                }
                else
                {
                    current = baseInfo;
                }
                break;
        }
    }
    public void OnText(ReadOnlySpan<byte> text)
    {
        if (text.Length == 0) return;

        int charCount = Encoding.UTF8.GetCharCount(text);
        char[]? rented = null;
        Span<char> buffer = charCount <= 512
            ? stackalloc char[charCount]
            : (rented = ArrayPool<char>.Shared.Rent(charCount)).AsSpan(0, charCount);

        try
        {
            Encoding.UTF8.GetChars(text, buffer);
            var enumerator = buffer.EnumerateRunes();
            while (enumerator.MoveNext())
            {
                var rune = enumerator.Current;
                if (!maps.TryGetValue(current, out var set))
                {
                    set = [];
                    maps[current] = set;
                }
                if (!set.TryGetValue(rune, out _))
                {
                    set[rune] = [];
                }
                set[rune].Add(lineNumber);

                if (!runeLocations.ContainsKey(rune)) runeLocations.Add(rune, []);
                runeLocations[rune].Add(lineNumber);
            }
        }
        finally
        {
            if (rented != null)
                ArrayPool<char>.Shared.Return(rented);
        }
    }
    public void OnSpecialChars(AssEventSegmentKind kind, short wrapStyle)
    {
        switch (kind)
        {
            case AssEventSegmentKind.HardLineBreaker:
                break;
            case AssEventSegmentKind.SoftLineBreaker:
                if (wrapStyle != 2) OnText("\u0020"u8);
                break;
            case AssEventSegmentKind.NonBreakingSpace:
                OnText("\u00A0"u8);
                break;
        }
    }

    public void Process(AssEventView ev) => GetUsedFontInfos(ev);
    public object? GetResults() => Results;
    public IReadOnlyDictionary<AssFontInfo, IReadOnlyDictionary<Rune, IReadOnlyList<int>>> GetResultsWithLineNumbers() => maps.ToDictionary(
        outerKvp => outerKvp.Key,
        outerKvp =>
        {
            var innerReadOnlyDict = outerKvp.Value.ToDictionary(
                innerKvp => innerKvp.Key,
                innerKvp => (IReadOnlyList<int>)innerKvp.Value
            );
            return (IReadOnlyDictionary<Rune, IReadOnlyList<int>>)innerReadOnlyDict;
        }
    );
    public int[]? GetExistsLines(Rune rune) => runeLocations.TryGetValue(rune, out var lines) ? lines.ToArray() : null;

    public void ResetResults() => maps.Clear();

    public void GetUsedFontInfos(ReadOnlySpan<byte> line) => GetUsedFontInfosCore(line, default);

    public void GetUsedFontInfos(AssEvents events)
    {
        FirstEventLineNumber = events.Collection.FirstOrDefault()?.GetView().LineNumber ?? -1;
        foreach (var evt in events.Collection)
        {
            GetUsedFontInfos(evt.GetView());
        }
    }

    public void GetUsedFontInfos(AssEventView view)
    {
        if (!view.IsDialogue) return;
        InitForLine(view.StyleSpan);
        lineNumber = view.LineNumber;
        GetUsedFontInfos(view.LineRaw[view.TextReadOnly]);
    }

    public void GetUsedFontInfos(ReadOnlyMemory<byte> line) => GetUsedFontInfosCore(line.Span, line);

    private void GetUsedFontInfosCore(ReadOnlySpan<byte> lineSpan, ReadOnlyMemory<byte> lineMemory)
    {
        // Prefer the ReadOnlyMemory overload when we have backing memory so byte/function tag payloads
        // can be kept as slices (avoids per-tag param allocations).
        var segs = lineMemory.IsEmpty
            ? AssEventParser.ParseLine(lineSpan).Span
            : AssEventParser.ParseLine(lineMemory).Span;

        var wrapStyleCurrent = AssEventParser.GetWrapStyle(segs, wrapStyle);
        if (AssEventParser.HasPolygon(segs)) return;

        foreach (var seg in segs)
        {
            switch (seg.SegmentKind)
            {
                case AssEventSegmentKind.TagBlock:
                    if (seg.Tags == null) break;
                    foreach (var tag in seg.Tags.Value.Span)
                        if (AssTagRegistry.TryGet(tag.Tag, out var desc))
                            OnTag(tag, desc!);
                    break;
                case AssEventSegmentKind.Text:
                    OnText(lineSpan[seg.LineRange]);
                    break;
                default:
                    OnSpecialChars(seg.SegmentKind, wrapStyleCurrent);
                    break;
            }
        }
    }
}

