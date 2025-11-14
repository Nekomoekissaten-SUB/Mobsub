using Mobsub.SubtitleParseNT2.AssTypes;
using System.Buffers;
using System.Text;

namespace Mobsub.SubtitleParseNT2.AssUtils;

public sealed class AssFontProcessor(byte wrapStyle, AssStyles styles) : IAssTagProcessor
{
    private readonly byte wrapStyle = wrapStyle;
    private readonly AssStyles styles = styles;
    private readonly Dictionary<string, AssStyleView> stylesDict = styles.BuildStyleDictionary();
    private AssFontInfo baseInfo;
    private AssFontInfo current;
    private readonly Dictionary<AssFontInfo, HashSet<Rune>> output = [];
    public bool AnalyzeWithEncoding = false;

    public void InitForLine(ReadOnlySpan<byte> styleName)
    {
        baseInfo = new AssFontInfo(GetAssStyleViewByName(styleName));
        current = baseInfo;
    }
    public void InitForLine(AssFontInfo info)
    {
        baseInfo = info;
        current = info;
    }

    private AssStyleView GetAssStyleViewByName(ReadOnlySpan<byte> styleName)
    {
        var syl = Encoding.UTF8.GetString(styleName);
        if (stylesDict.TryGetValue(syl, out var styleView))
        {
            return styleView;
        }
        throw new KeyNotFoundException($"AssFontProcessor: Style not found: '{syl}'");
    }

    public void OnTag(AssTagSpan tag, AssTagDescriptor desc)
    {
        switch (tag.Tag)
        {
            case AssTag.Bold:
                current.Weight = tag.Value is int b ? b : baseInfo.Weight;
                break;
            case AssTag.Italic:
                current.Italic = tag.Value is int i ? i != 0 : baseInfo.Italic;
                break;
            case AssTag.FontEncoding:
                current.Encoding = AnalyzeWithEncoding ? (tag.Value is int fe ? fe : baseInfo.Encoding) : 1;
                break;
            case AssTag.FontName:
                current.Name = tag.Value is byte[] fn && fn.Length > 0
                    ? Encoding.UTF8.GetString(fn)
                    : baseInfo.Name;
                break;
            case AssTag.Reset:
                var value = tag.Value is byte[] r && r.Length > 0 ? r : null;
                if (value == null)
                {
                    current = baseInfo;
                }
                else
                {
                    current = new AssFontInfo(GetAssStyleViewByName(value.AsSpan()));
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
                if (!output.TryGetValue(current, out var set))
                {
                    set = [];
                    output[current] = set;
                }
                set.Add(rune);
            }
        }
        finally
        {
            if (charCount > 512)
                ArrayPool<char>.Shared.Return(buffer.ToArray());
        }

        if (rented != null)
            ArrayPool<char>.Shared.Return(rented);
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

    public IReadOnlyDictionary<AssFontInfo, HashSet<Rune>> Results => output;
    public void ResetResults() => output.Clear();

    public IReadOnlyDictionary<AssFontInfo, HashSet<Rune>> GetUsedFontInfos(ReadOnlySpan<byte> line)
    {
        var segs = AssEventParser.ParseLine(line);
        var wrapStyleCurrent = AssEventParser.GetWrapStyle(segs, wrapStyle);

        foreach (var seg in segs.Span)
        {
            switch (seg.SegmentKind)
            {
                case AssEventSegmentKind.TagBlock:
                    if (seg.Tags == null) break;
                    foreach (var tag in seg.Tags.Value.Span)
                        if (AssTagRegistry.TryGet(tag.Tag, out var desc))
                            OnTag(tag, desc);
                    break;
                case AssEventSegmentKind.Text:
                    OnText(line[seg.LineRange]);
                    break;
                default:
                    OnSpecialChars(seg.SegmentKind, wrapStyleCurrent);
                    break;
            }
        }
        return output;
    }

    public IReadOnlyDictionary<AssFontInfo, HashSet<Rune>> GetUsedFontInfos(AssEvents events)
    {
        foreach (var evt in events.Collection)
        {
            var view = evt.GetView();
            if (!view.IsDialogue) continue;
            InitForLine(view.StyleSpan);

            _ = GetUsedFontInfos(view.TextSpan);
        }
        return output;
    }
}

